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
using Radish.Api.Controllers;
using Radish.Api.Tests.ErrorHandling;
using Radish.Common.OptionTool;
using Radish.Extension.Log;
using Radish.Service;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Core.Enrichers;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class ContentStickerOutputLoggingTests
{
    private const string PrivatePayload = "CONTENT_SCOPE_TEST_SENTINEL";
    private const string Unknown = "runtime.unclassified";
    private static readonly string[] IdentityFields = ["SourceContext", "EventCode", "SourceCategory", "Diagnostic"];
    private static readonly DateTimeOffset OriginalTime = new(2026, 11, 12, 0, 0, 0, TimeSpan.Zero);

    internal sealed record Expected(Type Source, string Code, string Level, string Failure);
    internal delegate void AssertEvents(params Expected[] expected);
    private sealed record RegisteredEvent(Type Source, string Code, LogEventLevel Level, int Id = 0, string? Name = null);

    private static readonly RegisteredEvent[] Catalog =
    [
        new(typeof(CommentService), "comment.edit_failed", LogEventLevel.Error),
        new(typeof(CommentService), "comment.god_recheck_failed", LogEventLevel.Error),
        new(typeof(CommentService), "comment.sofa_recheck_failed", LogEventLevel.Error),
        new(typeof(CommentService), "comment.highlight_recheck_failed", LogEventLevel.Error),
        new(typeof(CommentService), "comment.highlight_fill_failed", LogEventLevel.Error),
        new(typeof(ContentSubmissionService), "content_submission.conflict_resolved", LogEventLevel.Warning),
        new(typeof(ForumContentRevisionService), "post.restore_failed", LogEventLevel.Error),
        new(typeof(ReactionService), "reaction.retrying", LogEventLevel.Warning, 1, "ReactionRetrying"),
        new(typeof(StickerService), "sticker.cache_read_failed", LogEventLevel.Warning, 1, "StickerCacheReadFailed"),
        new(typeof(StickerService), "sticker.cache_write_failed", LogEventLevel.Warning, 2, "StickerCacheWriteFailed"),
        new(typeof(StickerService), "sticker.cache_invalidation_failed", LogEventLevel.Warning, 3, "StickerCacheInvalidationFailed"),
        new(typeof(StickerService), "sticker.batch_item_failed", LogEventLevel.Error, 4, "StickerBatchItemFailed"),
        new(typeof(StickerService), "sticker.thumbnail_failed", LogEventLevel.Warning, 5, "StickerThumbnailFailed")
    ];

    public static IEnumerable<object[]> OutputScopes()
    {
        foreach (var route in HangfireOutputLoggingTests.OutputRoutes())
        foreach (var mel in new[] { false, true })
        foreach (var activity in new[] { false, true })
            yield return route.Concat(new object[] { mel, activity }).ToArray();
    }

    public static IEnumerable<object[]> SharedCodeCases()
    {
        foreach (var route in HangfireOutputLoggingTests.OutputRoutes())
        foreach (var mel in new[] { false, true })
            yield return route.Concat(new object[] { mel }).ToArray();
    }

    public static IEnumerable<object[]> FaultCases()
    {
        foreach (var route in HangfireOutputLoggingTests.OutputRoutes())
        foreach (var source in new[] { "comment", "submission", "restore", "reaction", "sticker" })
            yield return route.Concat(new object[] { source }).ToArray();
    }

    [Theory]
    [MemberData(nameof(OutputScopes))]
    public async Task RegisteredEvents_ShouldRebuildFinalOutputAndKeepBusinessResults(
        bool candidate, string environment, bool audit, bool mel, bool activity)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var global = new ApplicationLogScope(capture.Logger);
        using var scope = Pollute(capture, mel, typeof(ProductService));
        using var trace = activity ? new Activity("content-output").SetIdFormat(ActivityIdFormat.W3C).Start() : null;
        await CommentHighlightLoggingTests.VerifyHighlightsAsync(Check);
        await CommentControllerLoggingTests.VerifyEditAsync(Check);
        await ContentSubmissionLoggingTests.VerifySubmissionAsync(Check);
        await PostRestoreLoggingTests.VerifyRestoreAsync(Check);
        await ReactionControllerLoggingTests.VerifyReactionAsync(Check);
        await StickerControllerLoggingTests.VerifyStickersAsync(Check);

        // 只在内层重算日志处注入故障，让真实外层 catch 经过同一正式目标路径。
        using var outer = new HttpBoundaryCapture(candidate, environment, audit: audit, additionalAudit: new InnerHighlightAuditFault());
        using var outerGlobal = new ApplicationLogScope(outer.Logger);
        await CommentHighlightLoggingTests.VerifyOuterAsync(expected => CheckEvents(outer, trace, expected));

        void Check(params Expected[] expected) => CheckEvents(capture, trace, expected);
    }

    [Theory]
    [MemberData(nameof(SharedCodeCases))]
    public async Task CommentEdit_ShouldKeepServiceOwnershipDespiteControllerScope(bool candidate, string environment, bool audit, bool mel)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var global = new ApplicationLogScope(capture.Logger);
        using var scope = Pollute(capture, mel, typeof(CommentController));
        await CommentControllerLoggingTests.VerifyEditAsync(expected => CheckEvents(capture, null, expected));
    }

    [Theory]
    [MemberData(nameof(FaultCases))]
    public async Task OutputFault_ShouldPreserveExistingFailureBehavior(bool candidate, string environment, bool audit, string source)
    {
        using var broken = new HangfireBrokenWriter();
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit, output: broken);
        using var global = new ApplicationLogScope(capture.Logger);
        var failure = new IOException(PrivatePayload);
        capture.Sink.EmitFailure = failure;
        Func<Func<Task>, Task>? whenAuditFails = audit ? CheckAuditFailure : null;
        switch (source)
        {
            case "comment": await CommentHighlightLoggingTests.VerifyHighlightFaultAsync(whenAuditFails); break;
            case "submission": await ContentSubmissionLoggingTests.VerifySubmissionFaultAsync(whenAuditFails); break;
            case "restore": await PostRestoreLoggingTests.VerifyRestoreFaultAsync(whenAuditFails); break;
            case "reaction": await ReactionControllerLoggingTests.VerifyReactionFaultAsync(whenAuditFails); break;
            case "sticker": await StickerControllerLoggingTests.VerifyStickerFaultAsync(whenAuditFails); break;
            default: throw new ArgumentOutOfRangeException(nameof(source));
        }
        Assert.Empty(capture.Events);
        if (candidate)
        {
            using var emergency = JsonDocument.Parse(capture.Emergency.ToString());
            Assert.Equal("pipeline.output_failed", emergency.RootElement.GetProperty("eventCode").GetString());
            Assert.DoesNotContain(PrivatePayload, capture.Emergency.ToString());
        }
        else Assert.Equal("", capture.Emergency.ToString());

        async Task CheckAuditFailure(Func<Task> call)
        {
            var thrown = await Record.ExceptionAsync(call);
            Assert.Same(failure, thrown is AggregateException aggregate ? Assert.Single(aggregate.Flatten().InnerExceptions) : thrown);
        }
    }

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public void InvalidIdentityOrLevel_ShouldUseSafeUnknown(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        foreach (var item in Catalog)
        {
            var otherSource = item.Source == typeof(CommentService) ? typeof(ContentSubmissionService) : typeof(CommentService);
            WriteAndCheck(Raw(item with { Source = otherSource }), otherSource);
            var missing = Raw(item);
            missing.RemovePropertyIfPresent(item.Id == 0 ? "EventCode" : "EventId");
            WriteAndCheck(missing, item.Source);
            WriteAndCheck(Raw(item, item.Level == LogEventLevel.Warning ? LogEventLevel.Error : LogEventLevel.Warning), item.Source);
            WriteAndCheck(Raw(item, LogEventLevel.Debug), item.Source);
            if (item.Id == 0) continue;
            foreach (var invalid in new LogEventPropertyValue[]
            {
                new ScalarValue(item.Id), Identity(item.Id, PrivatePayload), Identity(item.Id + 10, item.Name!),
                Identity(item.Id.ToString(), item.Name!), new StructureValue([new("Id", new ScalarValue(item.Id))]),
                new StructureValue([new("Id", new ScalarValue(item.Id)), new("Name", new ScalarValue(item.Name)), new("Name", new ScalarValue(item.Name))])
            })
            {
                var raw = Raw(item);
                raw.AddOrUpdateProperty(new("EventId", invalid));
                WriteAndCheck(raw, item.Source);
            }
        }

        void WriteAndCheck(LogEvent raw, Type source)
        {
            capture.Logger.Write(raw);
            var diagnostic = raw.Level < LogEventLevel.Warning;
            if (candidate && environment == "Production" && diagnostic) Assert.Empty(capture.Events);
            else
            {
                var summary = Assert.Single(capture.Events);
                Assert.Equal(Unknown, summary.Code);
                Assert.Equal(candidate && diagnostic ? "Info" : raw.Level.ToString(), summary.Level);
                Assert.Equal(diagnostic, summary.Diagnostic);
                Assert.Null(summary.FailureKind);
                if (candidate) CheckJson(capture.Output.ToString(), null, null);
                else CheckLegacy(Assert.Single(capture.Sink.Events), source, null, null);
            }
            Assert.Equal("", capture.Emergency.ToString());
            capture.Clear();
        }
    }

    [Fact]
    public void DisabledDiagnostics_ShouldKeepRegisteredWarningsAndDropUnknownDebug()
    {
        using var capture = new HttpBoundaryCapture(true, "Development", diagnostics: false);
        foreach (var item in Catalog)
        {
            capture.Logger.Write(Raw(item, LogEventLevel.Debug));
            Assert.Empty(capture.Events);
            capture.Logger.Write(Raw(item));
            CheckEvents(capture, null, [new(item.Source, item.Code, item.Level.ToString(), "io")]);
        }
    }

    [Fact]
    public void Adapter_ShouldKeepTypedCorrelationAndTimestampAndRejectInvalidFailureKind()
    {
        using var output = new HttpBoundaryTestSink();
        var adapter = new ContentStickerLogSink(output);
        using var activity = new Activity("content-current").SetIdFormat(ActivityIdFormat.W3C).Start();
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

        var controller = Raw(Catalog[0]);
        controller.AddOrUpdateProperty(new("SourceContext", new ScalarValue(typeof(CommentController).FullName)));
        adapter.Emit(controller);
        Assert.Same(controller, Assert.Single(output.Events));
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
            Assert.DoesNotContain("Unrecognized", rendered);
            Assert.Contains("Comment edit consumed a failure.", rendered);
            Assert.Contains("Post restore consumed a failure.", rendered);
            Assert.Contains("Reaction toggle selected its single conflict retry.", rendered);
            Assert.Contains("Sticker thumbnail generation degraded.", rendered);
        }
        finally { Console.SetOut(previous); }
    }

    private static void CheckEvents(HttpBoundaryCapture capture, Activity? activity, Expected[] expected)
    {
        var summaries = capture.Events;
        Assert.Equal(expected.Length, summaries.Length);
        var legacy = capture.Sink.Events.ToArray();
        var lines = capture.Output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < expected.Length; i++)
        {
            var item = expected[i];
            Assert.Equal(item.Code, summaries[i].Code);
            Assert.Equal(item.Level, summaries[i].Level);
            Assert.Equal(item.Failure, summaries[i].FailureKind);
            Assert.False(summaries[i].Diagnostic);
            if (capture.Candidate) CheckJson(lines[i], item.Failure, activity);
            else CheckLegacy(legacy[i], item.Source, item.Failure, activity);
        }
        Assert.Equal("", capture.Emergency.ToString());
        capture.Clear();
    }

    private static IDisposable Pollute(HttpBoundaryCapture capture, bool mel, Type source)
    {
        var fields = new Dictionary<string, object>
        {
            ["SourceContext"] = source.FullName!, ["SourceCategory"] = "job", ["EventCode"] = "product.configuration_rejected",
            ["EventId"] = new EventId(99, "Neighbor"), ["failureKind"] = "database", ["statusCode"] = 599, ["attempt"] = 12,
            ["traceId"] = "11111111111111111111111111111111", ["spanId"] = "2222222222222222",
            ["operationId"] = "01234567-89ab-4cde-8f01-23456789abcd", ["privatePayload"] = PrivatePayload,
            ["Diagnostic"] = true
        };
        return mel ? capture.Factory.CreateLogger("content.output.scope").BeginScope(fields)!
            : LogContext.Push(fields.Select(pair => new PropertyEnricher(pair.Key, pair.Value)).ToArray());
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

    private static LogEvent Raw(RegisteredEvent item, LogEventLevel? level = null,
        ActivityTraceId trace = default, ActivitySpanId span = default)
    {
        var properties = new List<LogEventProperty>
        {
            new("SourceContext", new ScalarValue(item.Source.FullName)), new("EventCode", new ScalarValue(item.Code)),
            new("SourceCategory", new ScalarValue("job")), new("Diagnostic", new ScalarValue(true)),
            new("privatePayload", new ScalarValue(PrivatePayload)), new("failureKind", new ScalarValue("io")),
            new("statusCode", new ScalarValue(503)), new("operationId", new ScalarValue("01234567-89ab-4cde-8f01-23456789abcd"))
        };
        if (item.Id != 0) properties.Add(new("EventId", Identity(item.Id, item.Name!)));
        return new LogEvent(OriginalTime, level ?? item.Level, new IOException(PrivatePayload),
            new MessageTemplateParser().Parse(PrivatePayload), properties, trace, span);
    }

    private static StructureValue Identity(object id, string name) =>
        new([new("Id", new ScalarValue(id)), new("Name", new ScalarValue(name))]);

    private sealed class InnerHighlightAuditFault : ILogEventSink
    {
        public void Emit(LogEvent value)
        {
            if (value.Properties.TryGetValue("EventCode", out var code) &&
                code is ScalarValue { Value: "comment.god_recheck_failed" })
                throw new IOException(PrivatePayload);
        }
    }
}
