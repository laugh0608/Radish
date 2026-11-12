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
using Radish.Api.Services;
using Radish.Api.Tests.ErrorHandling;
using Radish.Common.OptionTool;
using Radish.Extension.Log;
using Radish.Service;
using Serilog;
using Serilog.Context;
using Serilog.Core.Enrichers;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class NotificationRealtimeOutputLoggingTests
{
    private const string PrivatePayload = "NOTIFICATION_SCOPE_TEST_SENTINEL";
    private const string Unknown = "runtime.unclassified";
    private static readonly string[] IdentityFields = ["SourceContext", "EventCode", "SourceCategory", "Diagnostic"];
    private static readonly DateTimeOffset OriginalTime = new(2026, 11, 12, 0, 0, 0, TimeSpan.Zero);

    internal sealed record Expected(Type Source, string Code, string FailureKind, string Level = "Warning");
    internal delegate void AssertEvents(params Expected[] expected);
    private sealed record EventIdentity(Type Source, int Id, string Name, string Code, LogEventLevel Level = LogEventLevel.Warning);

    private static readonly EventIdentity[] Catalog =
    [
        new(typeof(UserFollowService), 1, "FollowNotificationEnqueueFailed", "user_follow.notification_enqueue_failed", LogEventLevel.Error),
        new(typeof(NotificationPushService), 1, "NotificationPushFailed", "notification.push_failed"),
        new(typeof(UserInteractionRealtimeNotifier), 1, "ChatRelationshipPushFailed", "user_interaction.chat_push_failed"),
        new(typeof(UserInteractionRealtimeNotifier), 2, "NotificationRelationshipPushFailed", "user_interaction.notification_push_failed"),
        new(typeof(CommentRealtimePushService), 1, "CommentPushFailed", "comment.push_failed")
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
        foreach (var source in new[] { "follow", "notification", "interaction", "comment" })
            yield return route.Concat(new object[] { source }).ToArray();
    }

    [Theory]
    [MemberData(nameof(OutputScopes))]
    public async Task RegisteredEvents_ShouldRebuildFinalOutputAndPreserveBusinessResults(
        bool candidate, string environment, bool audit, bool mel, bool activity)
    {
        using var output = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var global = new ApplicationLogScope(output.Logger);
        using var scope = Pollute(output, mel);
        using var trace = activity ? new Activity("notification-output").SetIdFormat(ActivityIdFormat.W3C).Start() : null;

        await UserFollowLoggingTests.VerifyFollowAsync(output, Check);
        await NotificationCreationLoggingTests.VerifyNotificationAsync(output, Check);
        await UserInteractionLoggingTests.VerifyInteractionAsync(output, Check);
        await CommentRealtimeLoggingTests.VerifyCommentAsync(output, Check);

        void Check(params Expected[] expected)
        {
            var summaries = output.Events;
            Assert.Equal(expected.Length, summaries.Length);
            var legacy = output.Sink.Events.ToArray();
            var lines = output.Output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < expected.Length; i++)
            {
                var item = expected[i];
                Assert.Equal(item.Code, summaries[i].Code);
                Assert.Equal(item.Level, summaries[i].Level);
                Assert.Equal(item.FailureKind, summaries[i].FailureKind);
                Assert.False(summaries[i].Diagnostic);
                if (output.Candidate)
                    CheckJson(lines[i], item.FailureKind, trace);
                else
                    CheckLegacy(legacy[i], item.Source, item.FailureKind, trace);
            }
            Assert.Equal("", output.Emergency.ToString());
            output.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(BrokenOutputCases))]
    public async Task OutputFault_ShouldKeepLegacyAndCandidateFailureSemantics(bool candidate, string environment, bool audit, string source)
    {
        using var broken = new HangfireBrokenWriter();
        using var output = new HttpBoundaryCapture(candidate, environment, audit: audit, output: broken);
        using var global = new ApplicationLogScope(output.Logger);
        var failure = new IOException(PrivatePayload);
        output.Sink.EmitFailure = failure;
        Func<Func<Task>, Task>? assertAuditFailure = audit ? CheckAuditFailure : null;
        switch (source)
        {
            case "follow": await UserFollowLoggingTests.VerifyFollowOutputFaultAsync(output, assertAuditFailure); break;
            case "notification": await NotificationCreationLoggingTests.VerifyNotificationOutputFaultAsync(output, assertAuditFailure); break;
            case "interaction": await UserInteractionLoggingTests.VerifyInteractionOutputFaultAsync(output, assertAuditFailure); break;
            case "comment": await CommentRealtimeLoggingTests.VerifyCommentOutputFaultAsync(output, assertAuditFailure); break;
            default: throw new ArgumentOutOfRangeException(nameof(source));
        }

        Assert.Empty(output.Events);
        if (candidate)
        {
            using var emergency = JsonDocument.Parse(output.Emergency.ToString());
            Assert.Equal("pipeline.output_failed", emergency.RootElement.GetProperty("eventCode").GetString());
            Assert.DoesNotContain(PrivatePayload, output.Emergency.ToString());
        }
        else Assert.Equal("", output.Emergency.ToString());

        async Task CheckAuditFailure(Func<Task> call)
        {
            var thrown = await Assert.ThrowsAsync<AggregateException>(call);
            Assert.Same(failure, Assert.Single(thrown.Flatten().InnerExceptions));
        }
    }

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public void InvalidIdentityOrLevel_ShouldUseSafeUnknown(bool candidate, string environment, bool audit)
    {
        using var output = new HttpBoundaryCapture(candidate, environment, audit: audit);
        foreach (var item in Catalog)
        {
            var wrongSource = item.Source == typeof(UserFollowService) ? typeof(NotificationPushService) : typeof(UserFollowService);
            WriteAndCheck(Raw(item with { Source = wrongSource }), wrongSource);
            var missing = Raw(item);
            missing.RemovePropertyIfPresent("EventId");
            WriteAndCheck(missing, item.Source);
            foreach (var invalid in new LogEventPropertyValue[]
            {
                new ScalarValue(item.Id), Identity(item.Id, PrivatePayload), Identity(item.Id + 10, item.Name),
                Identity(item.Id.ToString(), item.Name), new StructureValue([new("Id", new ScalarValue(item.Id))]),
                new StructureValue([new("Id", new ScalarValue(item.Id)), new("Name", new ScalarValue(item.Name)), new("Name", new ScalarValue(item.Name))])
            })
            {
                var raw = Raw(item);
                // Raw 包含合法 EventCode，未知身份仍不能靠该属性补齐。
                raw.AddOrUpdateProperty(new("EventId", invalid));
                WriteAndCheck(raw, item.Source);
            }
            WriteAndCheck(Raw(item, item.Level == LogEventLevel.Warning ? LogEventLevel.Error : LogEventLevel.Warning), item.Source);
            WriteAndCheck(Raw(item, LogEventLevel.Debug), item.Source);
        }

        void WriteAndCheck(LogEvent raw, Type source)
        {
            output.Logger.Write(raw);
            var diagnostic = raw.Level < LogEventLevel.Warning;
            if (candidate && environment == "Production" && diagnostic) Assert.Empty(output.Events);
            else
            {
                var value = Assert.Single(output.Events);
                Assert.Equal(Unknown, value.Code);
                Assert.Equal(candidate && diagnostic ? "Info" : raw.Level.ToString(), value.Level);
                Assert.Equal(diagnostic, value.Diagnostic);
                Assert.Null(value.FailureKind);
                if (candidate) CheckJson(output.Output.ToString(), null, null);
                else CheckLegacy(Assert.Single(output.Sink.Events), source, null, null);
            }
            Assert.Equal("", output.Emergency.ToString());
            output.Clear();
        }
    }

    [Fact]
    public void DisabledDiagnostics_ShouldKeepRegisteredEventsAndDropUnknownDebug()
    {
        using var output = new HttpBoundaryCapture(true, "Development", diagnostics: false);
        var item = Catalog[0];
        output.Logger.Write(Raw(item, LogEventLevel.Debug));
        Assert.Empty(output.Events);
        output.Logger.Write(Raw(item));
        var entry = Assert.Single(output.Events);
        Assert.Equal(item.Code, entry.Code);
        Assert.False(entry.Diagnostic);
        CheckJson(output.Output.ToString(), "io", null);
    }

    [Fact]
    public void Adapter_ShouldPreserveTimestampAndTypedCorrelationAndRejectInvalidFailureKinds()
    {
        using var output = new HttpBoundaryTestSink();
        var adapter = new NotificationRealtimeLogSink(output);
        using var activity = new Activity("notification-current").SetIdFormat(ActivityIdFormat.W3C).Start();
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
            Assert.Equal(item.Level, entry.Level);
            Assert.Equal(OriginalTime, entry.Timestamp);
            Assert.Equal(trace, entry.TraceId);
            Assert.Equal(span, entry.SpanId);
            Assert.Equal(IdentityFields.Order(), entry.Properties.Keys.Order());
            Assert.Null(entry.Exception);
            Assert.DoesNotContain(PrivatePayload, entry.RenderMessage());
            output.Events.Clear();
        }

        var adjacent = Raw(Catalog[0]);
        adjacent.AddOrUpdateProperty(new("SourceContext", new ScalarValue(typeof(UserFollowService).FullName + ".Adjacent")));
        adapter.Emit(adjacent);
        Assert.Same(adjacent, Assert.Single(output.Events));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void CodeConsole_ShouldRenderOnlyFixedSummaries(string environment)
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
            Assert.Contains("Follow notification enqueue failed before a handled response.", rendered);
            Assert.Contains("Notification realtime push is unavailable.", rendered);
            Assert.Contains("Relationship invalidation chat push is unavailable.", rendered);
            Assert.Contains("Relationship invalidation notification push is unavailable.", rendered);
            Assert.Contains("Comment realtime push is unavailable.", rendered);
        }
        finally { Console.SetOut(previous); }
    }

    private static IDisposable Pollute(HttpBoundaryCapture output, bool mel)
    {
        var values = new Dictionary<string, object>
        {
            ["SourceContext"] = typeof(ProductService).FullName!, ["SourceCategory"] = "job",
            ["EventCode"] = "product.configuration_rejected", ["EventId"] = new EventId(99, "Neighbor"),
            ["failureKind"] = "database", ["statusCode"] = 599, ["attempt"] = 12,
            ["traceId"] = "11111111111111111111111111111111", ["spanId"] = "2222222222222222",
            ["operationId"] = "01234567-89ab-4cde-8f01-23456789abcd", ["privatePayload"] = PrivatePayload,
            ["Diagnostic"] = true
        };
        return mel ? output.Factory.CreateLogger("notification.output.scope").BeginScope(values)!
            : LogContext.Push(values.Select(p => new PropertyEnricher(p.Key, p.Value)).ToArray());
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

    private static LogEvent Raw(EventIdentity item, LogEventLevel? level = null, ActivityTraceId trace = default, ActivitySpanId span = default) =>
        new(OriginalTime, level ?? item.Level, new IOException(PrivatePayload), new MessageTemplateParser().Parse(PrivatePayload),
        [
            new("SourceContext", new ScalarValue(item.Source.FullName)), new("EventCode", new ScalarValue(item.Code)),
            new("EventId", Identity(item.Id, item.Name)), new("SourceCategory", new ScalarValue("job")),
            new("Diagnostic", new ScalarValue(true)), new("privatePayload", new ScalarValue(PrivatePayload)),
            new("failureKind", new ScalarValue("io")), new("statusCode", new ScalarValue(503)),
            new("operationId", new ScalarValue("01234567-89ab-4cde-8f01-23456789abcd"))
        ], trace, span);

    private static StructureValue Identity(object id, string name) =>
        new([new("Id", new ScalarValue(id)), new("Name", new ScalarValue(name))]);
}
