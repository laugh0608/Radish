using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radish.Common.OptionTool;
using Radish.Extension.Log;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace Radish.Api.Tests;

public sealed class SignalRTestSink : ILogEventSink, IDisposable
{
    public ConcurrentQueue<LogEvent> Events { get; } = new();
    public bool Disposed { get; private set; }
    public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
    public void Dispose() => Disposed = true;
}

public static class SignalRTestSinkExtensions
{
    public static readonly ConcurrentDictionary<string, SignalRTestSink> Sinks = new();
    public static LoggerConfiguration SignalRTestCapture(this LoggerSinkConfiguration configuration, string key) =>
        configuration.Sink(Sinks[key]);
}

internal sealed class SignalRLogCapture : IDisposable
{
    public const string Secret = "SIGNALR_PRIVATE_SENTINEL";
    public readonly StringWriter Output = new();
    public readonly SignalRTestSink Sink = new();
    public readonly Logger Logger;
    public readonly ILoggerFactory Factory;
    private readonly string _key = Guid.NewGuid().ToString("N");
    private readonly bool _candidate;

    public SignalRLogCapture(bool candidate, string environment, bool diagnostics = true)
    {
        _candidate = candidate;
        var values = new Dictionary<string, string?>
        {
            ["RadishLogging:Mode"] = environment,
            ["RadishLogging:Diagnostics"] = (diagnostics && environment == "Development").ToString(),
            ["Serilog:Using:0"] = "Radish.Api.Tests",
            ["Serilog:WriteTo:0:Name"] = "SignalRTestCapture",
            ["Serilog:WriteTo:0:Args:key"] = _key
        };
        SignalRTestSinkExtensions.Sinks[_key] = Sink;
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var logger = new LoggerConfiguration();
        if (candidate)
            RuntimeLoggingConfiguration.Configure(logger, config, environment, "api", Output, TextWriter.Null);
        else
        {
            using var services = new ServiceCollection().BuildServiceProvider();
            SerilogSetup.ConfigureLegacy(logger, config, services, new SerilogOptions
            {
                MinimumLevel = "Verbose",
                Console = new() { Enable = false }, File = new() { Enable = false }, Database = new() { Enable = false }
            });
        }
        Logger = logger.CreateLogger();
        Factory = new Serilog.Extensions.Logging.SerilogLoggerFactory(Logger, dispose: false);
    }

    public string[] Codes => _candidate
        ? Output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => { using var json = JsonDocument.Parse(line); return json.RootElement.GetProperty("eventCode").GetString()!; }).ToArray()
        : Sink.Events.Where(e => e.Properties.ContainsKey("EventCode"))
            .Select(e => ((ScalarValue)e.Properties["EventCode"]).Value!.ToString()!).ToArray();
    public void AssertSafe()
    {
        Assert.DoesNotContain(Secret, Output.ToString());
        foreach (var e in Sink.Events)
        {
            // 协议注册 / resolver 来源不在本批三个框架类别内，不把它计入已治理证据。
            if (e.Properties.TryGetValue("SourceContext", out var source) && source is ScalarValue
                { Value: "Microsoft.AspNetCore.SignalR.Internal.DefaultHubProtocolResolver" }) continue;
            Assert.True(e.Properties.ContainsKey("EventCode"));
            Assert.Null(e.Exception);
            Assert.DoesNotContain(Secret, e.RenderMessage() + string.Join(" ", e.Properties.Select(p => p.Value.ToString())));
            Assert.All(e.Properties.Keys, key => Assert.Contains(key,
                new[] { "EventCode", "SourceCategory", "Diagnostic", "failureKind", "SourceContext" }));
        }
    }
    public void Dispose()
    {
        Factory.Dispose();
        Logger.Dispose();
        SignalRTestSinkExtensions.Sinks.TryRemove(_key, out _);
        Output.Dispose();
    }
}

internal sealed class SignalRTestState
{
    public Exception? ConnectFailure;
    public Exception? DisconnectFailure;
    public Exception? DisconnectReason;
    public Exception Failure = new IOException(SignalRLogCapture.Secret);
    public readonly List<string> Steps = new();
}

public sealed class LoggingProbeHub : Hub
{
    private readonly SignalRTestState _state;
    public LoggingProbeHub(IServiceProvider services) => _state = services.GetRequiredService<SignalRTestState>();
    public override Task OnConnectedAsync()
    {
        _state.Steps.Add("connect");
        if (_state.ConnectFailure != null) throw _state.ConnectFailure;
        return Task.CompletedTask;
    }
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _state.Steps.Add("disconnect");
        _state.DisconnectReason = exception;
        if (_state.DisconnectFailure != null) throw _state.DisconnectFailure;
        return Task.CompletedTask;
    }
    public string Echo(string text) => text;
    public void Fail() => throw _state.Failure;
    public int Number(int number) => number;
}

internal sealed class SignalRConnectionFixture : IAsyncDisposable
{
    public readonly SignalRTestState State = new();
    public readonly DefaultConnectionContext Connection;
    public readonly FailingPipeWriter Writer;
    private readonly Pipe _input = new();
    private readonly Pipe _output = new();
    private readonly ServiceProvider _services;
    private readonly HubConnectionHandler<LoggingProbeHub> _handler;
    public Task Running { get; private set; } = Task.CompletedTask;

    public SignalRConnectionFixture(ILoggerFactory factory, string environment)
    {
        var services = new ServiceCollection();
        services.AddSingleton(factory);
        services.AddLogging();
        services.AddSingleton(State);
        services.AddSignalR(o => { o.EnableDetailedErrors = environment == "Development"; o.MaximumReceiveMessageSize = 32768; });
        _services = services.BuildServiceProvider();
        _handler = _services.GetRequiredService<HubConnectionHandler<LoggingProbeHub>>();
        Writer = new FailingPipeWriter(_output.Writer);
        Connection = new DefaultConnectionContext(SignalRLogCapture.Secret)
        {
            Transport = new Duplex(_input.Reader, Writer)
        };
        Connection.Features.Set<IConnectionHeartbeatFeature>(new Heartbeat());
    }
    public void Start() => Running = _handler.OnConnectedAsync(Connection);
    public async Task Handshake()
    {
        Start();
        await Send("{\"protocol\":\"json\",\"version\":1}");
        Assert.Equal("{}", await Read());
    }
    public async Task Send(string json) => await _input.Writer.WriteAsync(Encoding.UTF8.GetBytes(json + '\u001e'));
    public async Task<string> Read()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            var read = await _output.Reader.ReadAsync(timeout.Token);
            var position = read.Buffer.PositionOf((byte)0x1e);
            if (position != null)
            {
                var text = Encoding.UTF8.GetString(read.Buffer.Slice(0, position.Value).ToArray());
                _output.Reader.AdvanceTo(read.Buffer.GetPosition(1, position.Value));
                return text;
            }
            _output.Reader.AdvanceTo(read.Buffer.Start, read.Buffer.End);
            if (read.IsCompleted) throw new InvalidOperationException("Expected protocol frame.");
        }
    }
    public async Task Close(string? error = null)
    {
        await Send(error == null ? "{\"type\":7}" : JsonSerializer.Serialize(new { type = 7, error }));
        await Running.WaitAsync(TimeSpan.FromSeconds(10));
    }
    public async ValueTask DisposeAsync()
    {
        await _input.Writer.CompleteAsync();
        try { await Running.WaitAsync(TimeSpan.FromSeconds(10)); }
        catch (Exception) when (Running.IsCompleted) { /* 测试已断言故障；清理不替换该故障。 */ }
        await Connection.DisposeAsync();
        await _input.Reader.CompleteAsync();
        await _output.Writer.CompleteAsync();
        await _output.Reader.CompleteAsync();
        await _services.DisposeAsync();
    }
    private sealed record Duplex(PipeReader Input, PipeWriter Output) : IDuplexPipe;
    private sealed class Heartbeat : IConnectionHeartbeatFeature
    {
        public void OnHeartbeat(Action<object> action, object state) { }
    }
}

internal sealed class FailingPipeWriter(PipeWriter inner) : PipeWriter
{
    public bool Fail;
    public readonly List<IOException> Failures = new();
    public override void Advance(int bytes) => inner.Advance(bytes);
    public override Memory<byte> GetMemory(int sizeHint = 0) => inner.GetMemory(sizeHint);
    public override Span<byte> GetSpan(int sizeHint = 0) => inner.GetSpan(sizeHint);
    public override void CancelPendingFlush() => inner.CancelPendingFlush();
    public override void Complete(Exception? exception = null) => inner.Complete(exception);
    public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
    {
        if (!Fail) return inner.FlushAsync(cancellationToken);
        var failure = new IOException(SignalRLogCapture.Secret);
        Failures.Add(failure);
        return ValueTask.FromException<FlushResult>(failure);
    }
}
