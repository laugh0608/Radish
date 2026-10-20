using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radish.Common.LogTool;
using Radish.Common.OptionTool;
using Radish.Extension.Log;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class SignalRFrameworkSinkTests
{
    private const string Handler = "Microsoft.AspNetCore.SignalR.HubConnectionHandler";
    private const string Dispatcher = "Microsoft.AspNetCore.SignalR.Internal.DefaultHubDispatcher";
    private const string Connection = "Microsoft.AspNetCore.SignalR.HubConnectionContext";

    [Theory]
    [InlineData(Handler, 4, "ErrorSendingClose", "signalr.close_failed")]
    [InlineData(Dispatcher, 25, "FailedStreaming", "signalr.stream_failed")]
    [InlineData(Connection, 6, "FailedWritingMessage", "signalr.write_failed")]
    [InlineData(Handler, 999, "ErrorDispatchingHubEvent", "signalr.unclassified")]
    [InlineData(Handler, 1, "NewFrameworkEvent", "signalr.unclassified")]
    [InlineData(Connection, 999, "NewFrameworkEvent", "signalr.unclassified")]
    public void Adapter_ShouldNeverReadArbitraryValues_AndPreserveTimeLevelAndTrace(string source, int id, string name, string code)
    {
        var sink = new SignalRTestSink();
        var adapter = new SignalRFrameworkLogSink(sink);
        var original = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Fatal, new UnrenderableException(),
            new MessageTemplateParser().Parse(SignalRLogCapture.Secret + " {payload}"), new[]
            {
                new LogEventProperty("SourceContext", new ScalarValue(source)),
                new LogEventProperty("EventCode", new ScalarValue("http.failed")),
                new LogEventProperty("EventId", new StructureValue(new[]
                {
                    new LogEventProperty("Id", new ScalarValue(id)), new LogEventProperty("Name", new ScalarValue(name))
                })),
                new LogEventProperty("payload", new ScalarValue(new UnrenderableValue())),
                new LogEventProperty("access_token", new ScalarValue(SignalRLogCapture.Secret))
            }, ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom());
        adapter.Emit(original);
        var result = Assert.Single(sink.Events);
        Assert.NotSame(original, result);
        Assert.Equal(original.Timestamp, result.Timestamp);
        Assert.Equal(original.Level, result.Level);
        Assert.Equal(original.TraceId, result.TraceId);
        Assert.Equal(original.SpanId, result.SpanId);
        Assert.Equal(code, ((ScalarValue)result.Properties["EventCode"]).Value);
        Assert.Equal("other", ((ScalarValue)result.Properties["failureKind"]).Value);
        Assert.Null(result.Exception);
        Assert.DoesNotContain(SignalRLogCapture.Secret, result.RenderMessage() + string.Join(" ", result.Properties));
        Assert.IsType<UnrenderableException>(original.Exception);
        Assert.True(original.Properties.ContainsKey("payload"));
    }

    [Theory]
    [InlineData("Microsoft.AspNetCore.SignalR.HubConnectionHandler.Custom")]
    [InlineData("Microsoft.AspNetCore.Http.Connections.Internal.HttpConnectionManager.Custom")]
    [InlineData("Radish.Api.Hubs.ChatHub")]
    public void OtherSources_ShouldPassThroughSameEvent(string source)
    {
        var sink = new SignalRTestSink();
        var original = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, new IOException("unchanged"),
            new MessageTemplateParser().Parse("Unchanged"), new[] { new LogEventProperty("SourceContext", new ScalarValue(source)) });
        new SignalRFrameworkLogSink(sink).Emit(original);
        Assert.Same(original, Assert.Single(sink.Events));
    }

    [Theory]
    [MemberData(nameof(SignalRFrameworkLoggingTests.OutputModes), MemberType = typeof(SignalRFrameworkLoggingTests))]
    public void UnknownWarningsAndErrors_ShouldRemainVisible_AndDiagnosticsRequireOptIn(bool candidate, string environment)
    {
        using var capture = new SignalRLogCapture(candidate, environment, diagnostics: false);
        var logger = capture.Factory.CreateLogger(Connection);
        logger.LogWarning(new EventId(990, "FutureWarning"), new UnrenderableException(), "{secret}", SignalRLogCapture.Secret);
        logger.LogError(new EventId(991, "FutureError"), new UnrenderableException(), "{secret}", SignalRLogCapture.Secret);
        logger.LogInformation(new EventId(992, "FutureInfo"), "{secret}", SignalRLogCapture.Secret);
        logger.LogDebug(new EventId(993, "FutureDebug"), "{secret}", SignalRLogCapture.Secret);
        Assert.Equal(candidate ? 2 : 4, capture.Codes.Length);
        Assert.All(capture.Codes, code => Assert.Equal("signalr.unclassified", code));
        capture.AssertSafe();
    }

    [Fact]
    public void LegacyConfiguration_ShouldKeepRootEnrichmentOverridesReloadFanoutAndDisposal()
    {
        var first = new SignalRTestSink();
        var second = new SignalRTestSink();
        var key1 = Guid.NewGuid().ToString("N");
        var key2 = Guid.NewGuid().ToString("N");
        SignalRTestSinkExtensions.Sinks[key1] = first;
        SignalRTestSinkExtensions.Sinks[key2] = second;
        try
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Serilog:Using:0"] = "Radish.Api.Tests",
                ["Serilog:WriteTo:0:Name"] = "SignalRTestCapture",
                ["Serilog:WriteTo:0:Args:key"] = key1,
                ["Serilog:WriteTo:1:Name"] = "SignalRTestCapture",
                ["Serilog:WriteTo:1:Args:key"] = key2,
                ["Serilog:MinimumLevel:Override:" + Dispatcher] = "Error",
                ["Serilog:Properties:secret"] = SignalRLogCapture.Secret
            }).Build();
            using var services = new ServiceCollection().BuildServiceProvider();
            var configuration = new LoggerConfiguration();
            SerilogSetup.ConfigureLegacy(configuration, config, services, DisabledOutputs("Information"));
            using (var logger = configuration.CreateLogger())
            {
                var framework = logger.ForContext("SourceContext", Dispatcher);
                framework.Warning("Filtered");
                framework.Error(new UnrenderableException(), SignalRLogCapture.Secret);
                logger.Information("Unchanged application event");
                logger.Debug("Filtered by root minimum");
                var sql = logger.ForContext(LogContextTool.LogSource, LogContextTool.AopSql);
                sql.Information("Unchanged SQL event");
                Assert.Equal(3, first.Events.Count);
                Assert.Equal(3, second.Events.Count);
                var events = first.Events.ToArray();
                Assert.Null(events[0].Exception);
                Assert.False(events[0].Properties.ContainsKey("secret"));
                Assert.Equal(SignalRLogCapture.Secret, ((ScalarValue)events[1].Properties["secret"]).Value);
                Assert.Equal(LogContextTool.AopSql, ((ScalarValue)events[2].Properties[LogContextTool.LogSource]).Value);
                config["Serilog:MinimumLevel:Override:" + Dispatcher] = "Warning";
                config.Reload();
                framework.Warning("Visible after reload");
                Assert.Equal(4, first.Events.Count);
                Assert.Equal(4, second.Events.Count);
            }
            Assert.True(first.Disposed);
            Assert.True(second.Disposed);
        }
        finally
        {
            SignalRTestSinkExtensions.Sinks.TryRemove(key1, out _);
            SignalRTestSinkExtensions.Sinks.TryRemove(key2, out _);
        }
    }

    [Fact]
    public void LegacyAuditConfiguration_ShouldKeepThrowingSinkSemantics()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Serilog:Using:0"] = "Radish.Api.Tests",
            ["Serilog:AuditTo:0:Name"] = "SignalRThrowingAudit"
        }).Build();
        using var services = new ServiceCollection().BuildServiceProvider();
        var configuration = new LoggerConfiguration();
        SerilogSetup.ConfigureLegacy(configuration, config, services, DisabledOutputs("Information"));
        using var logger = configuration.CreateLogger();
        var error = Assert.Throws<AggregateException>(() => logger.ForContext("SourceContext", Handler)
            .Error(new UnrenderableException(), SignalRLogCapture.Secret));
        Assert.Equal("Audit sink failure", Assert.IsType<IOException>(Assert.Single(error.Flatten().InnerExceptions)).Message);
    }

    private static SerilogOptions DisabledOutputs(string minimum) => new()
    {
        MinimumLevel = minimum, Console = new() { Enable = false }, File = new() { Enable = false }, Database = new() { Enable = false }
    };
    private sealed class UnrenderableValue
    {
        public override string ToString() => throw new InvalidOperationException("Must not render payload");
    }
    private sealed class UnrenderableException : Exception
    {
        public override string Message => throw new InvalidOperationException("Must not read Message");
        public override string ToString() => throw new InvalidOperationException("Must not render exception");
    }
}

public static class SignalRThrowingAuditExtensions
{
    public static LoggerConfiguration SignalRThrowingAudit(this LoggerAuditSinkConfiguration configuration) => configuration.Sink(new AuditSink());
    private sealed class AuditSink : ILogEventSink
    {
        public void Emit(LogEvent logEvent)
        {
            Assert.Null(logEvent.Exception);
            Assert.DoesNotContain(SignalRLogCapture.Secret, logEvent.RenderMessage());
            throw new IOException("Audit sink failure");
        }
    }
}
