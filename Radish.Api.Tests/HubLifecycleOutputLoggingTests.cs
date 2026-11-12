using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radish.Api.Hubs;
using Radish.Api.Tests.ErrorHandling;
using Radish.Common.OptionTool;
using Radish.Extension.Log;
using Serilog;
using Serilog.Context;
using Serilog.Core.Enrichers;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class HubLifecycleOutputLoggingTests
{
    private const string PrivatePayload = "HUB_SCOPE_TEST_SENTINEL";
    private const string Unknown = "runtime.unclassified";
    private const string FrameworkHandler = "Microsoft.AspNetCore.SignalR.HubConnectionHandler";
    private static readonly string[] IdentityFields = ["SourceContext", "EventCode", "SourceCategory", "Diagnostic"];
    private static readonly DateTimeOffset OriginalTime = new(2026, 11, 12, 0, 0, 0, TimeSpan.Zero);

    internal sealed record Expected(Type Source, string Code, string FailureKind);
    private sealed record RegisteredEvent(Type Source, string Name, string Code);

    private static readonly RegisteredEvent[] Catalog =
    [
        new(typeof(ChatHub), "ChatConnectionClosed", "chat.connection_closed"),
        new(typeof(NotificationHub), "NotificationConnectionClosed", "notification.connection_closed")
    ];

    public static IEnumerable<object[]> OutputScopes()
    {
        foreach (var route in HangfireOutputLoggingTests.OutputRoutes())
        foreach (var mel in new[] { false, true })
        foreach (var activity in new[] { false, true })
            yield return route.Concat(new object[] { mel, activity }).ToArray();
    }

    public static IEnumerable<object[]> BrokenOutputCases()
    {
        foreach (var route in HangfireOutputLoggingTests.OutputRoutes())
        foreach (var chat in new[] { false, true })
            yield return route.Concat(new object[] { chat }).ToArray();
    }

    [Theory]
    [MemberData(nameof(OutputScopes))]
    public async Task DisconnectEvents_ShouldKeepCleanupAndRebuildFinalOutput(
        bool candidate, string environment, bool audit, bool mel, bool activity)
    {
        using var output = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var scope = Pollute(output, mel);
        using var trace = activity ? new Activity("hub-lifecycle-output").SetIdFormat(ActivityIdFormat.W3C).Start() : null;
        await ChatHubLoggingTests.VerifyLifecycleAsync(output, Check);
        await NotificationHubLoggingTests.VerifyLifecycleAsync(output, Check);

        void Check(Expected item)
        {
            var summary = Assert.Single(output.Events);
            Assert.Equal(item.Code, summary.Code);
            Assert.Equal("Warning", summary.Level);
            Assert.Equal(item.FailureKind, summary.FailureKind);
            Assert.False(summary.Diagnostic);
            if (candidate) CheckJson(output.Output.ToString(), item.FailureKind, trace);
            else CheckLegacy(Assert.Single(output.Sink.Events), item.Source, item.FailureKind, trace);
            Assert.Equal("", output.Emergency.ToString());
            output.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(BrokenOutputCases))]
    public async Task OutputFault_ShouldKeepExistingBehaviorAfterCleanup(bool candidate, string environment, bool audit, bool chat)
    {
        using var broken = new HangfireBrokenWriter();
        using var output = new HttpBoundaryCapture(candidate, environment, audit: audit, output: broken);
        var failure = new IOException(PrivatePayload);
        output.Sink.EmitFailure = failure;

        if (chat) await ChatHubLoggingTests.VerifyOutputFailureAsync(output, Invoke);
        else await NotificationHubLoggingTests.VerifyOutputFailureAsync(output, Invoke);
        Assert.Empty(output.Events);
        if (candidate)
        {
            using var emergency = JsonDocument.Parse(output.Emergency.ToString());
            Assert.Equal("pipeline.output_failed", emergency.RootElement.GetProperty("eventCode").GetString());
            Assert.DoesNotContain(PrivatePayload, output.Emergency.ToString());
        }
        else Assert.Equal("", output.Emergency.ToString());

        async Task Invoke(Func<Task> call)
        {
            if (!audit) await call();
            else
            {
                var thrown = await Assert.ThrowsAsync<AggregateException>(call);
                Assert.Same(failure, Assert.Single(thrown.Flatten().InnerExceptions));
            }
        }
    }

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public void WrongIdentityOrLevel_ShouldUseSafeUnknown(bool candidate, string environment, bool audit)
    {
        using var output = new HttpBoundaryCapture(candidate, environment, audit: audit);
        foreach (var item in Catalog)
        {
            var otherHub = Catalog.Single(e => e.Source != item.Source);
            WriteAndCheck(Raw(item with { Source = otherHub.Source }), otherHub.Source);
            var missing = Raw(item);
            missing.RemovePropertyIfPresent("EventId");
            WriteAndCheck(missing, item.Source);
            foreach (var invalid in new LogEventPropertyValue[]
            {
                new ScalarValue(1), Identity(1, "ErrorDispatchingHubEvent"), Identity(2, item.Name),
                Identity("1", item.Name), new StructureValue([new("Id", new ScalarValue(1))]),
                new StructureValue([new("Id", new ScalarValue(1)), new("Id", new ScalarValue(1)), new("Name", new ScalarValue(item.Name))])
            })
            {
                var raw = Raw(item);
                raw.AddOrUpdateProperty(new("EventId", invalid));
                WriteAndCheck(raw, item.Source);
            }
            WriteAndCheck(Raw(item, LogEventLevel.Error), item.Source);
            WriteAndCheck(Raw(item, LogEventLevel.Debug), item.Source);
        }

        void WriteAndCheck(LogEvent raw, Type source)
        {
            output.Logger.Write(raw);
            var diagnostic = raw.Level < LogEventLevel.Warning;
            if (candidate && environment == "Production" && diagnostic) Assert.Empty(output.Events);
            else
            {
                var entry = Assert.Single(output.Events);
                Assert.Equal(Unknown, entry.Code);
                Assert.Equal(candidate && diagnostic ? "Info" : raw.Level.ToString(), entry.Level);
                Assert.Equal(diagnostic, entry.Diagnostic);
                Assert.Null(entry.FailureKind);
                if (candidate) CheckJson(output.Output.ToString(), null, null);
                else CheckLegacy(Assert.Single(output.Sink.Events), source, null, null);
            }
            Assert.Equal("", output.Emergency.ToString());
            output.Clear();
        }
    }

    [Fact]
    public void DisabledDiagnostics_ShouldKeepRegisteredWarningsAndDropUnknownDebug()
    {
        using var output = new HttpBoundaryCapture(true, "Development", diagnostics: false);
        foreach (var item in Catalog)
        {
            output.Logger.Write(Raw(item, LogEventLevel.Debug));
            Assert.Empty(output.Events);
            output.Logger.Write(Raw(item));
            var entry = Assert.Single(output.Events);
            Assert.Equal(item.Code, entry.Code);
            Assert.False(entry.Diagnostic);
            CheckJson(output.Output.ToString(), "io", null);
            output.Clear();
        }
    }

    [Fact]
    public void Adapter_ShouldRetainTypedCorrelationAndTimestampAndRejectInvalidFailureKind()
    {
        using var output = new HttpBoundaryTestSink();
        var adapter = new NotificationRealtimeLogSink(output);
        using var activity = new Activity("hub-current").SetIdFormat(ActivityIdFormat.W3C).Start();
        var trace = ActivityTraceId.CreateRandom();
        var span = ActivitySpanId.CreateRandom();
        foreach (var item in Catalog)
        foreach (var invalid in new object?[] { PrivatePayload, 1, null })
        {
            var raw = Raw(item, trace: trace, span: span);
            raw.AddOrUpdateProperty(new("failureKind", new ScalarValue(invalid)));
            adapter.Emit(raw);
            var entry = Assert.Single(output.Events);
            Assert.Equal(item.Code, Scalar(entry, "EventCode"));
            Assert.Equal(LogEventLevel.Warning, entry.Level);
            Assert.Equal(OriginalTime, entry.Timestamp);
            Assert.Equal(trace, entry.TraceId);
            Assert.Equal(span, entry.SpanId);
            Assert.Equal(IdentityFields.Order(), entry.Properties.Keys.Order());
            Assert.Null(entry.Exception);
            Assert.DoesNotContain(PrivatePayload, entry.RenderMessage());
            output.Events.Clear();
        }

        foreach (var source in new[] { FrameworkHandler, typeof(CommentHub).FullName, typeof(ChatHub).FullName + ".Adjacent" })
        {
            var raw = Raw(Catalog[0]);
            raw.AddOrUpdateProperty(new("SourceContext", new ScalarValue(source)));
            adapter.Emit(raw);
            Assert.Same(raw, Assert.Single(output.Events));
            output.Events.Clear();
        }
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void CodeConsole_ShouldRenderFixedDisconnectSummaries(string environment)
    {
        var previous = Console.Out;
        using var text = new StringWriter();
        using var services = new ServiceCollection().BuildServiceProvider();
        try
        {
            Console.SetOut(text);
            var config = new LoggerConfiguration();
            SerilogSetup.ConfigureLegacy(config, new ConfigurationBuilder().Build(), services, new SerilogOptions
            { Console = new() { Enable = true }, File = new() { Enable = false }, Database = new() { Enable = false } }, environment);
            using var logger = config.CreateLogger();
            foreach (var item in Catalog) logger.Write(Raw(item));
            var rendered = text.ToString();
            Assert.Equal(Catalog.Length, rendered.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
            Assert.DoesNotContain(PrivatePayload, rendered);
            Assert.Contains("Chat connection closed with a failure.", rendered);
            Assert.Contains("Notification connection closed with a failure.", rendered);
        }
        finally { Console.SetOut(previous); }
    }

    private static IDisposable Pollute(HttpBoundaryCapture output, bool mel)
    {
        var fields = new Dictionary<string, object>
        {
            ["SourceContext"] = FrameworkHandler, ["EventId"] = new EventId(1, "ErrorDispatchingHubEvent"),
            ["HubMethod"] = "OnDisconnectedAsync", ["EventCode"] = "signalr.disconnect_failed",
            ["SourceCategory"] = "infrastructure", ["failureKind"] = "database", ["Diagnostic"] = true,
            ["statusCode"] = 599, ["attempt"] = 12,
            ["traceId"] = "11111111111111111111111111111111", ["spanId"] = "2222222222222222",
            ["operationId"] = "01234567-89ab-4cde-8f01-23456789abcd",
            ["privatePayload"] = PrivatePayload, ["ConnectionId"] = PrivatePayload
        };
        return mel ? output.Factory.CreateLogger("hub.output.scope").BeginScope(fields)!
            : LogContext.Push(fields.Select(p => new PropertyEnricher(p.Key, p.Value)).ToArray());
    }

    private static void CheckJson(string line, string? failureKind, Activity? activity)
    {
        using var document = JsonDocument.Parse(line);
        var entry = document.RootElement;
        var properties = entry.GetProperty("properties");
        Assert.Equal("application", entry.GetProperty("sourceCategory").GetString());
        Assert.Equal(activity?.TraceId.ToHexString(), OptionalString(entry, "traceId"));
        Assert.Equal(activity?.SpanId.ToHexString(), OptionalString(entry, "spanId"));
        Assert.False(entry.TryGetProperty("operationId", out _));
        if (failureKind is null) Assert.Empty(properties.EnumerateObject());
        else
        {
            Assert.Equal("failureKind", Assert.Single(properties.EnumerateObject()).Name);
            Assert.Equal(failureKind, properties.GetProperty("failureKind").GetString());
        }
        Assert.DoesNotContain(PrivatePayload, line);
    }

    private static void CheckLegacy(LogEvent entry, Type source, string? failureKind, Activity? activity)
    {
        Assert.Equal(source.FullName, Scalar(entry, "SourceContext"));
        Assert.Equal("application", Scalar(entry, "SourceCategory"));
        Assert.Equal(activity?.TraceId, entry.TraceId);
        Assert.Equal(activity?.SpanId, entry.SpanId);
        var fields = failureKind is null ? IdentityFields : [.. IdentityFields, "failureKind"];
        Assert.Equal(fields.Order(), entry.Properties.Keys.Order());
        if (failureKind is not null) Assert.Equal(failureKind, Scalar(entry, "failureKind"));
        Assert.Null(entry.Exception);
        Assert.DoesNotContain(PrivatePayload, entry.RenderMessage() + string.Join(" ", entry.Properties.Values));
    }

    private static string? OptionalString(JsonElement item, string key) =>
        item.TryGetProperty(key, out var value) ? value.GetString() : null;

    private static object? Scalar(LogEvent entry, string key) => Assert.IsType<ScalarValue>(entry.Properties[key]).Value;

    private static LogEvent Raw(RegisteredEvent item, LogEventLevel level = LogEventLevel.Warning,
        ActivityTraceId trace = default, ActivitySpanId span = default) =>
        new(OriginalTime, level, new IOException(PrivatePayload), new MessageTemplateParser().Parse(PrivatePayload),
        [
            new("SourceContext", new ScalarValue(item.Source.FullName)), new("EventCode", new ScalarValue(item.Code)),
            new("EventId", Identity(1, item.Name)), new("SourceCategory", new ScalarValue("infrastructure")),
            new("Diagnostic", new ScalarValue(true)), new("ConnectionId", new ScalarValue(PrivatePayload)),
            new("failureKind", new ScalarValue("io")), new("statusCode", new ScalarValue(503)),
            new("operationId", new ScalarValue("01234567-89ab-4cde-8f01-23456789abcd"))
        ], trace, span);

    private static StructureValue Identity(object id, string name) =>
        new([new("Id", new ScalarValue(id)), new("Name", new ScalarValue(name))]);
}
