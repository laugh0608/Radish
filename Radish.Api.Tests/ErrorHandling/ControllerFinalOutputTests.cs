using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Radish.Common.OptionTool;
using Radish.Api.Controllers;
using Radish.Extension.Log;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class ControllerFinalOutputTests
{
    internal static readonly (Type Source, string[] Codes)[] Catalog =
    [
        (typeof(CoinController), ["http.failed", "coin.balance_query_rejected", "coin.transaction_query_rejected", "coin.adjustment_rejected"]),
        (typeof(PaymentPasswordController), ["http.failed"]),
        (typeof(AttachmentController), ["http.failed", "upload.cleanup.failed"]),
        (typeof(ShopController), ["http.failed", "order.cancellation_rejected", "order.fulfillment_retry_rejected", "order.remark_rejected",
            "benefit.activation_rejected", "benefit.deactivation_rejected", "benefit.revocation_rejected", "product.management_rejected", "inventory.use_failed", "inventory.rename_failed"]),
        (typeof(ReactionController), ["http.failed"]),
        (typeof(PostQuickReplyController), ["http.failed", "quick_reply.query_rejected", "quick_reply.notification_enqueue_failed"]),
        (typeof(PollController), ["http.failed", "poll.request_failed"]),
        (typeof(LotteryController), ["http.failed", "lottery.request_failed"]),
        (typeof(QuestionController), ["http.failed", "question.request_failed"]),
        (typeof(PostController), ["post.edit_failed", "post.top_failed"]),
        (typeof(CommentController), ["comment.create_failed", "comment.like_failed", "comment.edit_failed"]),
        (typeof(TagController), ["tag.request_failed"]),
        (typeof(CategoryController), ["category.request_failed"]),
        (typeof(StickerController), ["sticker.request_failed"])
    ];

    [Theory]
    [MemberData(nameof(ApplicationOperationLoggingTests.Routes), MemberType = typeof(ApplicationOperationLoggingTests))]
    public void EveryRegisteredEvent_ShouldRebuildOnlyItsFieldsAndTypedCorrelation(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var scope = Pollution(capture);
        using var activity = new Activity("controller-output").SetIdFormat(ActivityIdFormat.W3C).Start();
        foreach (var (source, codes) in Catalog)
        foreach (var code in codes)
        {
            Emit(capture, source, code, "timeout", 503, "quota-complete");
            Assert.Equal(code, Assert.Single(capture.Codes));
            Assert.Equal("timeout", Assert.Single(capture.Events).FailureKind);
            Assert.False(Assert.Single(capture.Events).Diagnostic);
            var expected = new List<string> { "failureKind" };
            if (code == "http.failed") expected.Add("statusCode");
            if (code == "upload.cleanup.failed") expected.Add("cleanupOperation");
            AssertFields(capture, expected);
            var category = code is "http.failed" or "coin.balance_query_rejected" or "coin.transaction_query_rejected" or
                "coin.adjustment_rejected" or "order.cancellation_rejected" or "order.fulfillment_retry_rejected" ? "http" : "application";
            if (candidate)
            {
                using var json = JsonDocument.Parse(capture.Output.ToString());
                Assert.Equal(activity.TraceId.ToHexString(), json.RootElement.GetProperty("traceId").GetString());
                Assert.Equal(category, json.RootElement.GetProperty("sourceCategory").GetString());
                Assert.False(json.RootElement.TryGetProperty("requestId", out _));
            }
            else
            {
                var result = Assert.Single(capture.Sink.Events);
                Assert.Equal(activity.TraceId, result.TraceId);
                Assert.Equal(activity.SpanId, result.SpanId);
                Assert.Equal(source.FullName, ((ScalarValue)result.Properties["SourceContext"]).Value);
                Assert.Equal(category, ((ScalarValue)result.Properties["SourceCategory"]).Value);
            }
            capture.AssertSafe(); capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(ApplicationOperationLoggingTests.Routes), MemberType = typeof(ApplicationOperationLoggingTests))]
    public void KnownEvents_ShouldRejectUnsafeAndCrossEventValues(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        foreach (var (source, codes) in Catalog)
        foreach (var code in codes)
        foreach (var status in new object[] { 499, 600, 503L, "503", new { value = 503 } })
        {
            Emit(capture, source, code, HttpBoundaryCapture.Secret, status, HttpBoundaryCapture.Secret);
            Assert.Equal(code, Assert.Single(capture.Codes));
            AssertFields(capture, []);
            capture.AssertSafe(); capture.Clear();
        }
        foreach (var operation in new[] { "quota-complete", "quota-release" })
        {
            Emit(capture, typeof(AttachmentController), "upload.cleanup.failed", "io", 503, operation);
            AssertFields(capture, ["failureKind", "cleanupOperation"]);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(ApplicationOperationLoggingTests.Routes), MemberType = typeof(ApplicationOperationLoggingTests))]
    public void UnknownAndCrossSourceIdentities_ShouldUseFixedUnknownAndDiagnosticGate(bool candidate, string environment, bool audit)
    {
        foreach (var diagnostics in new[] { false, true })
        {
            using var capture = new HttpBoundaryCapture(candidate, environment, diagnostics: diagnostics, audit: audit);
            using var scope = Pollution(capture);
            foreach (var (source, codes) in Catalog)
            foreach (var code in new string?[] { null, HttpBoundaryCapture.Secret, "user.current_avatar_degraded", Catalog.SelectMany(c => c.Codes).First(c => !codes.Contains(c)) })
            foreach (var level in Enum.GetValues<LogEventLevel>())
            {
                var logger = capture.Logger.ForContext("SourceContext", source.FullName);
                if (code != null) logger = logger.ForContext("EventCode", code);
                logger.Write(level, new HangfireUnreadableException(), "Private {failureKind}", "io");
                if (level < LogEventLevel.Warning && !(environment == "Development" && diagnostics)) Assert.Empty(capture.Events);
                else
                {
                    Assert.EndsWith(".unknown", Assert.Single(capture.Codes));
                    Assert.Equal(level < LogEventLevel.Warning, Assert.Single(capture.Events).Diagnostic);
                    AssertFields(capture, []);
                    capture.AssertSafe();
                }
                capture.Clear();
            }
        }
    }

    [Theory]
    [MemberData(nameof(ApplicationOperationLoggingTests.Routes), MemberType = typeof(ApplicationOperationLoggingTests))]
    public void Attachment_ShouldRequireExactNamedIdentity(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        var logger = capture.Factory.CreateLogger<AttachmentController>();
        foreach (var identity in new[] { new EventId(1), new EventId(2, "AttachmentUploadFailed"), new EventId(1, "ApiRequestFailed"), new EventId(1, "UploadAccountingFailed") })
        {
            logger.LogError(identity, new HangfireUnreadableException(), "Private {EventCode} {failureKind}", "http.failed", "io");
            Assert.Equal("controller.attachment.unknown", Assert.Single(capture.Codes));
            AssertFields(capture, []);
            capture.AssertSafe(); capture.Clear();
        }
    }

    [Fact]
    public void AdjacentAndServiceSources_ShouldPassThroughAndTimestampShouldSurviveRebuild()
    {
        using var sink = new HttpBoundaryTestSink();
        var wrapper = new ApplicationOperationLogSink(sink);
        var timestamp = DateTimeOffset.UtcNow.AddDays(-1);
        var trace = ActivityTraceId.CreateRandom();
        var span = ActivitySpanId.CreateRandom();
        foreach (var (source, _) in Catalog)
        foreach (var category in new[] { source.FullName + ".Other", source.FullName!.Replace(".Controllers.", ".Services.") })
        {
            var value = new LogEvent(timestamp, LogEventLevel.Warning, null, new MessageTemplateParser().Parse("Adjacent"),
                [new("SourceContext", new ScalarValue(category)), new("EventCode", new ScalarValue("http.failed"))]);
            wrapper.Emit(value);
            Assert.Same(value, Assert.Single(sink.Events)); sink.Events.Clear();
        }
        wrapper.Emit(new LogEvent(timestamp, LogEventLevel.Error, new HangfireUnreadableException(), new MessageTemplateParser().Parse("Private"),
            [new("SourceContext", new ScalarValue(typeof(ReactionController).FullName)), new("EventCode", new ScalarValue("http.failed"))], trace, span));
        var rebuilt = Assert.Single(sink.Events);
        Assert.Equal(timestamp, rebuilt.Timestamp);
        Assert.Equal(trace, rebuilt.TraceId);
        Assert.Equal(span, rebuilt.SpanId);
        Assert.Null(rebuilt.Exception);
    }

    [Fact]
    public void CodeConsole_ShouldStripControllerPayloadAndRejectUnknownInfoInProduction()
    {
        var previous = Console.Out;
        using var output = new StringWriter();
        using var services = new ServiceCollection().BuildServiceProvider();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RadishLogging:Mode"] = "Development", ["RadishLogging:Diagnostics"] = "true"
        }).Build();
        try
        {
            Console.SetOut(output);
            var setup = new LoggerConfiguration();
            SerilogSetup.ConfigureLegacy(setup, configuration, services, new SerilogOptions
            {
                MinimumLevel = "Verbose", Console = new() { Enable = true, EnableSqlLog = false },
                File = new() { Enable = false }, Database = new() { Enable = false }
            }, "Production");
            using var logger = setup.CreateLogger();
            using var scope = LogContext.PushProperty("payload", HttpBoundaryCapture.Secret);
            logger.ForContext<ReactionController>().ForContext("EventCode", HttpBoundaryCapture.Secret).Information("Private");
            Assert.Equal("", output.ToString());
            logger.ForContext<ReactionController>().ForContext("EventCode", "http.failed")
                .Error(new HangfireUnreadableException(), "Private {failureKind} {statusCode} {payload}", "io", 503, HttpBoundaryCapture.Secret);
            Assert.Contains("http.failed", output.ToString());
            Assert.Contains("503", output.ToString());
            Assert.DoesNotContain(HttpBoundaryCapture.Secret, output.ToString());
        }
        finally { Console.SetOut(previous); }
    }

    internal static IDisposable? Pollution(HttpBoundaryCapture capture) => capture.Factory.CreateLogger("test.scope").BeginScope(new Dictionary<string, object>
    {
        ["Authorization"] = HttpBoundaryCapture.Secret, ["payload"] = new { Nested = new { Secret = HttpBoundaryCapture.Secret } },
        ["SourceContext"] = HttpBoundaryCapture.Secret, ["EventCode"] = "runtime.failed", ["Diagnostic"] = true,
        ["SourceCategory"] = "database", ["rateLimitState"] = "enabled", ["operationId"] = "01234567-89ab-4cde-8f01-23456789abcd",
        ["requestId"] = "controller-scope-request"
    });

    private static void Emit(HttpBoundaryCapture capture, Type source, string code, object kind, object status, object operation)
    {
        if (source == typeof(AttachmentController))
            capture.Factory.CreateLogger<AttachmentController>().LogError(code == "http.failed" ? new EventId(1, "AttachmentUploadFailed") : new EventId(2, "UploadAccountingFailed"),
                new HangfireUnreadableException(), "Private {failureKind} {statusCode} {cleanupOperation} {payload}", kind, status, operation, HttpBoundaryCapture.Secret);
        else
            capture.Logger.ForContext("SourceContext", source.FullName).ForContext("EventCode", code)
                .Error(new HangfireUnreadableException(), "Private {failureKind} {statusCode} {cleanupOperation} {payload}", kind, status, operation, HttpBoundaryCapture.Secret);
    }

    internal static void AssertFields(HttpBoundaryCapture capture, IEnumerable<string> expected)
    {
        if (capture.Candidate)
        {
            using var json = JsonDocument.Parse(capture.Output.ToString());
            Assert.Equal(expected.Order(), json.RootElement.GetProperty("properties").EnumerateObject().Select(p => p.Name).Order());
        }
        else Assert.Equal(expected.Concat(["SourceContext", "EventCode", "SourceCategory", "Diagnostic"]).Order(), Assert.Single(capture.Sink.Events).Properties.Keys.Order());
    }
}
