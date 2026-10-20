using System;
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
using Radish.Common.Exceptions;
using Radish.Extension.Log;
using Serilog;
using Serilog.Events;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class ApiNotFoundPipelineLoggingTests
{
    private const string Secret = "NOT_FOUND_PRIVATE_SENTINEL";
    private const string WriteSecret = "RESPONSE_WRITE_PRIVATE_SENTINEL";
    private const string PublicMessage = "频道、消息不存在或无权访问";
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task HandledBusinessErrors_ShouldCompleteFullContractWithoutFrameworkRethrow(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var status in new[] { 400, 403, 404, 409, 429, 500, 503 })
        {
            var failure = new BusinessException(PublicMessage, new IOException(Secret), status,
                "Chat.ChannelUnavailable", "error.chat.channel_unavailable", 7, "safe\0value", new { Password = Secret });
            failure.Data["payload"] = Secret;
            await using var pipeline = new Pipeline(capture, environment, _ => throw failure);

            await pipeline.InvokeAsync();

            var context = pipeline.Context;
            Assert.False(context.Response.HasStarted);
            Assert.Equal(status, context.Response.StatusCode);
            Assert.StartsWith("application/json", context.Response.ContentType);
            Assert.Equal("not-found-correlation", context.Response.Headers["X-Correlation-ID"]);
            using var json = JsonDocument.Parse(pipeline.Body.ToArray());
            var root = json.RootElement;
            Assert.Equal(status, root.GetProperty("statusCode").GetInt32());
            Assert.False(root.GetProperty("isSuccess").GetBoolean());
            Assert.Equal(PublicMessage, root.GetProperty("messageInfo").GetString());
            Assert.Equal("Chat.ChannelUnavailable", root.GetProperty("code").GetString());
            Assert.Equal("error.chat.channel_unavailable", root.GetProperty("messageKey").GetString());
            Assert.Equal("not-found-correlation", root.GetProperty("traceId").GetString());
            var arguments = root.GetProperty("messageArguments");
            Assert.Equal(3, arguments.GetArrayLength());
            Assert.Equal(7, arguments[0].GetInt32());
            Assert.Equal("safe value", arguments[1].GetString());
            Assert.Equal("[unsupported]", arguments[2].GetString());
            Assert.DoesNotContain(Secret, root.GetRawText());
            if (status >= 500) capture.AssertSingleApiFailure(status);
            else capture.AssertQuiet();
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task NormalNotFoundResponses_ShouldRemainUntouched(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var path in new[] { "/api/v1/missing", "/hub/missing", "/missing" })
        {
            await using var pipeline = new Pipeline(capture, environment, async context =>
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("original not-found body");
            });
            pipeline.Context.Request.Path = path;
            await pipeline.InvokeAsync();
            Assert.Equal(404, pipeline.Context.Response.StatusCode);
            Assert.Equal("original not-found body", System.Text.Encoding.UTF8.GetString(pipeline.Body.ToArray()));
            Assert.False(pipeline.Context.Response.Headers.ContainsKey("X-Correlation-ID"));
            capture.AssertQuiet();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task StartedResponse_ShouldStillRethrowOriginalWithoutRewritingBody(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var failure = new BusinessException(Secret, 404, "Private.Code");
        await using var pipeline = new Pipeline(capture, environment, _ => throw failure);
        pipeline.Context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature { StatusCode = 206 });
        pipeline.Context.Response.Body = pipeline.Body;
        pipeline.Body.Write("already-started"u8);

        var propagated = await Assert.ThrowsAsync<BusinessException>(pipeline.InvokeAsync);

        Assert.Same(failure, propagated);
        Assert.Equal(206, pipeline.Context.Response.StatusCode);
        Assert.Equal("already-started", System.Text.Encoding.UTF8.GetString(pipeline.Body.ToArray()));
        Assert.False(pipeline.Context.Response.Headers.ContainsKey("X-Correlation-ID"));
        capture.AssertFrameworkBoundary("http.response_started");
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ResponseWriteFailure_ShouldStillRethrowOriginalException(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var status in new[] { 404, 503 })
        {
            var failure = new BusinessException(PublicMessage, new IOException(Secret), status, "Private.Code");
            await using var pipeline = new Pipeline(capture, environment, _ => throw failure);
            using var brokenBody = new FailingWriteStream();
            pipeline.Context.Response.Body = brokenBody;

            var propagated = await Assert.ThrowsAsync<BusinessException>(pipeline.InvokeAsync);

            Assert.Same(failure, propagated);
            Assert.True(brokenBody.WriteAttempts > 0);
            Assert.Equal(status, pipeline.Context.Response.StatusCode);
            Assert.False(pipeline.Context.Response.HasStarted);
            // 框架仍保留原异常传播；响应写入故障不是一个已成功处理的 404。
            capture.AssertFrameworkBoundary("http.exception_handler_failed");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task NonApiAndHealthRequests_ShouldStillPropagateWithoutMessageModel(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var path in new[] { "/hub/chat", "/hub/comment", "/connect/authorize", "/api/health", "/api/health/live", "/healthz", "/scalar" })
        {
            var failure = new BusinessException(Secret, 404, "Private.Code");
            await using var pipeline = new Pipeline(capture, environment, _ => throw failure);
            pipeline.Context.Request.Path = path;

            Assert.Same(failure, await Assert.ThrowsAsync<BusinessException>(pipeline.InvokeAsync));

            Assert.Empty(pipeline.Body.ToArray());
            Assert.False(pipeline.Context.Response.Headers.ContainsKey("X-Correlation-ID"));
            capture.AssertFrameworkBoundary("http.exception_handler_failed");
            capture.Clear();
        }
    }

    private sealed class Pipeline : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly RequestDelegate _pipeline;
        public DefaultHttpContext Context { get; } = new();
        public MemoryStream Body { get; } = new();

        public Pipeline(Capture capture, string environment, RequestDelegate action)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
            builder.Logging.ClearProviders();
            builder.Services.AddSerilog(capture.Logger, dispose: false);
            builder.Services.AddSingleton<ApiExceptionHandler>();
            _app = builder.Build();
            _app.UseApiExceptionHandler();
            _app.Run(action);
            _pipeline = ((IApplicationBuilder)_app).Build();
            Context.Request.Path = "/api/v1/Chat/Channel";
            Context.Request.QueryString = new QueryString("?private=" + Secret);
            Context.TraceIdentifier = "not-found-correlation";
            Context.Response.Body = Body;
            Context.RequestServices = _app.Services;
        }
        public Task InvokeAsync() => _pipeline(Context);
        public async ValueTask DisposeAsync() { await _app.DisposeAsync(); await Body.DisposeAsync(); }
    }

    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }

    private sealed class FailingWriteStream : MemoryStream
    {
        public int WriteAttempts { get; private set; }
        private IOException Failure() { WriteAttempts++; return new IOException(WriteSecret); }
        public override void Write(byte[] buffer, int offset, int count) => throw Failure();
        public override void Write(ReadOnlySpan<byte> buffer) => throw Failure();
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => Task.FromException(Failure());
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromException(Failure());
    }

    private sealed class Capture : IDisposable
    {
        private readonly StringWriter _output = new();
        private readonly List<LogEvent> _events = [];
        private readonly string? _mode;
        public Serilog.Core.Logger Logger { get; }

        public Capture(bool candidate, string environment)
        {
            var config = new LoggerConfiguration().MinimumLevel.Verbose();
            if (candidate)
            {
                _mode = environment;
                var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RadishLogging:Mode"] = environment,
                    ["RadishLogging:Diagnostics"] = (environment == "Development").ToString()
                }).Build();
                RuntimeLoggingConfiguration.Configure(config, settings, environment, "api", _output, _output);
            }
            else config.Enrich.FromLogContext().WriteTo.Sink(new HttpExceptionFrameworkLogSink(new LegacySink(_output, _events)));
            Logger = config.CreateLogger();
        }
        public void AssertQuiet() => Assert.Equal("", _output.ToString());
        public void AssertSingleApiFailure(int status)
        {
            var text = _output.ToString();
            Assert.Single(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            foreach (var forbidden in new[] { Secret, PublicMessage, "Private.Code", "Chat.ChannelUnavailable", "runtime.unclassified", "IOException" })
                Assert.DoesNotContain(forbidden, text);
            if (_mode != null)
            {
                using var json = JsonDocument.Parse(text);
                var root = json.RootElement;
                Assert.Equal("http.failed", root.GetProperty("eventCode").GetString());
                Assert.Equal("Error", root.GetProperty("level").GetString());
                Assert.Equal(_mode, root.GetProperty("mode").GetString());
                var properties = root.GetProperty("properties");
                Assert.Equal(2, properties.EnumerateObject().Count());
                Assert.Equal(status, properties.GetProperty("statusCode").GetInt32());
                Assert.Equal("other", properties.GetProperty("failureKind").GetString());
            }
            else
            {
                var value = Assert.Single(_events);
                Assert.Null(value.Exception);
                Assert.Equal(LogEventLevel.Error, value.Level);
                Assert.Equal("http.failed", Assert.IsType<ScalarValue>(value.Properties["EventCode"]).Value);
                Assert.Equal(status, Assert.IsType<ScalarValue>(value.Properties["statusCode"]).Value);
                Assert.Equal("other", Assert.IsType<ScalarValue>(value.Properties["failureKind"]).Value);
            }
        }
        public void AssertFrameworkBoundary(string phase)
        {
            // 四模式保留框架阶段及传播，不输出原始异常或提前生成 API 自有事件。
            if (_mode != null)
            {
                var codes = new List<string?>();
                foreach (var line in _output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    using var json = JsonDocument.Parse(line);
                    Assert.Equal(_mode, json.RootElement.GetProperty("mode").GetString());
                    codes.Add(json.RootElement.GetProperty("eventCode").GetString());
                }
                Assert.Equal(new[] { phase, "http.framework_exception" }, codes);
                Assert.DoesNotContain(Secret, _output.ToString());
                Assert.DoesNotContain(WriteSecret, _output.ToString());
            }
            else
            {
                Assert.Equal(new[] { phase, "http.framework_exception" },
                    _events.Select(e => Assert.IsType<ScalarValue>(e.Properties["EventCode"]).Value));
                Assert.All(_events, e => Assert.Null(e.Exception));
                Assert.DoesNotContain(Secret, _output.ToString());
                Assert.DoesNotContain(WriteSecret, _output.ToString());
            }
        }
        public void Clear() { _output.GetStringBuilder().Clear(); _events.Clear(); }
        public void Dispose() { Logger.Dispose(); _output.Dispose(); }
    }
    private sealed class LegacySink(TextWriter output, List<LogEvent> events) : Serilog.Core.ILogEventSink
    {
        private readonly Serilog.Formatting.Display.MessageTemplateTextFormatter _formatter = new("{Level} {Message:lj} {Properties:j} {Exception}{NewLine}");
        public void Emit(LogEvent value) { events.Add(value); _formatter.Format(value, output); }
    }
}
