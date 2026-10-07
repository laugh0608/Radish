using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radish.Api.ErrorHandling;
using Radish.Common.OptionTool;
using Radish.Extension.Log;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

public sealed class HttpBoundaryTestSink : ILogEventSink
{
    public readonly ConcurrentQueue<LogEvent> Events = new();
    public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
}

public static class HttpBoundaryTestSinkExtensions
{
    public static readonly ConcurrentDictionary<string, HttpBoundaryTestSink> Sinks = new();
    public static LoggerConfiguration HttpBoundaryCapture(this LoggerSinkConfiguration configuration, string key) => configuration.Sink(Sinks[key]);
    public static LoggerConfiguration HttpBoundaryCapture(this LoggerAuditSinkConfiguration configuration, string key) => configuration.Sink(Sinks[key]);
}

internal sealed class HttpBoundaryCapture : IDisposable
{
    public const string Secret = "HTTP_BOUNDARY_PRIVATE_SENTINEL";
    public const string Source = "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware";
    public readonly StringWriter Output = new();
    public readonly StringWriter Emergency = new();
    public readonly HttpBoundaryTestSink Sink = new();
    public readonly Logger Logger;
    public readonly ILoggerFactory Factory;
    public readonly bool Candidate;
    public readonly string Environment;
    private readonly string _key = Guid.NewGuid().ToString("N");

    public HttpBoundaryCapture(bool candidate, string environment, string service = "api", bool diagnostics = true, bool audit = false)
    {
        Candidate = candidate;
        Environment = environment;
        HttpBoundaryTestSinkExtensions.Sinks[_key] = Sink;
        var values = new Dictionary<string, string?>
        {
            ["RadishLogging:Mode"] = environment,
            ["RadishLogging:Diagnostics"] = (diagnostics && environment == "Development").ToString(),
            ["Serilog:Using:0"] = "Radish.Api.Tests",
            [$"Serilog:{(audit ? "AuditTo" : "WriteTo")}:0:Name"] = "HttpBoundaryCapture",
            [$"Serilog:{(audit ? "AuditTo" : "WriteTo")}:0:Args:key"] = _key
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var logger = new LoggerConfiguration();
        if (candidate)
            RuntimeLoggingConfiguration.Configure(logger, config, environment, service, Output, Emergency);
        else
        {
            using var services = new ServiceCollection().BuildServiceProvider();
            SerilogSetup.ConfigureLegacy(logger, config, services, new SerilogOptions
            {
                MinimumLevel = "Verbose", Console = new() { Enable = false }, File = new() { Enable = false }, Database = new() { Enable = false }
            });
        }
        Logger = logger.CreateLogger();
        Factory = new Serilog.Extensions.Logging.SerilogLoggerFactory(Logger, dispose: false);
    }

    public CapturedHttpEvent[] Events => Candidate
        ? Output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line =>
        {
            using var json = JsonDocument.Parse(line);
            var root = json.RootElement;
            Assert.Equal(Environment, root.GetProperty("mode").GetString());
            var properties = root.GetProperty("properties");
            return new CapturedHttpEvent(root.GetProperty("eventCode").GetString()!, root.GetProperty("level").GetString()!,
                properties.TryGetProperty("failureKind", out var kind) ? kind.GetString() : null,
                root.GetProperty("diagnostic").GetBoolean());
        }).ToArray()
        : Sink.Events.Select(e => new CapturedHttpEvent(
            e.Properties.TryGetValue("EventCode", out var code) && code is ScalarValue { Value: string text } ? text : "unadapted",
            e.Level.ToString(), e.Properties.TryGetValue("failureKind", out var value) && value is ScalarValue { Value: string kind } ? kind : null,
            e.Properties.TryGetValue("Diagnostic", out var diagnostic) && diagnostic is ScalarValue { Value: true })).ToArray();
    public string[] Codes => Events.Select(e => e.Code).ToArray();
    public IDisposable? SensitiveScope() => Factory.CreateLogger("test.scope").BeginScope(new Dictionary<string, object>
    {
        ["RequestPath"] = "/" + Secret, ["Authorization"] = "Bearer " + Secret, ["payload"] = new { Secret }
    });
    public void AssertSafe()
    {
        Assert.Equal("", Emergency.ToString());
        Assert.DoesNotContain(Secret, Output.ToString());
        foreach (var e in Sink.Events)
        {
            Assert.Null(e.Exception);
            Assert.DoesNotContain(Secret, e.RenderMessage() + string.Join(" ", e.Properties.Select(p => p.Value.ToString())));
            if (e.Properties.TryGetValue("SourceContext", out var source) && source is ScalarValue { Value: Source })
                Assert.All(e.Properties.Keys, key => Assert.Contains(key,
                    new[] { "EventCode", "SourceCategory", "SourceContext", "Diagnostic", "failureKind" }));
        }
    }
    public void Clear() { Output.GetStringBuilder().Clear(); Sink.Events.Clear(); }
    public void Dispose()
    {
        Factory.Dispose();
        Logger.Dispose();
        HttpBoundaryTestSinkExtensions.Sinks.TryRemove(_key, out _);
        Output.Dispose();
        Emergency.Dispose();
    }
}

internal sealed record CapturedHttpEvent(string Code, string Level, string? FailureKind, bool Diagnostic);

internal sealed class HttpBoundaryPipeline : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly RequestDelegate _pipeline;
    public readonly DefaultHttpContext Context = new();
    public readonly MemoryStream Body = new();
    public readonly MutableResponseFeature ResponseFeature = new();

    public HttpBoundaryPipeline(HttpBoundaryCapture capture, RequestDelegate action, bool gateway = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = capture.Environment });
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(capture.Logger, dispose: false);
        builder.Services.AddSingleton<ApiExceptionHandler>();
        _app = builder.Build();
        if (gateway) _app.UseExceptionHandler("/Error");
        else _app.UseApiExceptionHandler();
        _app.Run(action);
        _pipeline = ((IApplicationBuilder)_app).Build();
        Context.Features.Set<IHttpResponseFeature>(ResponseFeature);
        Context.RequestServices = _app.Services;
        Context.Request.Path = gateway ? "/original" : "/api/test";
        Context.Request.QueryString = new QueryString("?credential=" + HttpBoundaryCapture.Secret);
        Context.TraceIdentifier = "boundary-correlation";
        Context.Response.Body = Body;
    }
    public Task Invoke() => _pipeline(Context);
    public async ValueTask DisposeAsync() { await _app.DisposeAsync(); await Body.DisposeAsync(); }
}

internal sealed class MutableResponseFeature : HttpResponseFeature
{
    public bool Started;
    public override bool HasStarted => Started;
}

/// <summary>在真实 WriteAsJsonAsync 输出位置设置写入屏障或故障，不替换 JSON 序列化。</summary>
internal sealed class ControlledResponseStream(Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> write) : MemoryStream
{
    public int Attempts;
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Attempts++;
        return write(buffer, cancellationToken);
    }
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Unexpected synchronous serializer write");
    public override void Write(ReadOnlySpan<byte> buffer) => throw new InvalidOperationException("Unexpected synchronous serializer write");
}
