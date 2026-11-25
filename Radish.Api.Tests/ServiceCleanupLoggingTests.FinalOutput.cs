using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.Tests.ErrorHandling;
using Radish.Common.OptionTool;
using Radish.Extension.Log;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.Models;
using Radish.Model.ViewModels;
using Radish.Service.Internal;
using Radish.Service.Jobs;
using Serilog;
using Serilog.Context;
using Serilog.Core.Enrichers;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Radish.Api.Tests;

public sealed partial class ServiceCleanupLoggingTests
{
    private const string Done = "job.cleanup.completed";
    private const string Interrupted = "job.cleanup.interrupted";
    private const string Failed = "job.cleanup.failed";
    private const string Foreground = "upload.cleanup.failed";
    private static readonly string[] IdentityFields = ["SourceContext", "SourceCategory", "EventCode", "Diagnostic"];
    private static readonly string[] BatchFields = ["jobKind", "outcome", "durationMs", "processedCount", "updatedCount",
        "removedDirectoryCount", "settlementCount", "skippedCount", "failedCount"];
    private static readonly string[] ForegroundFields = ["cleanupOperation", "failureKind"];

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public async Task Background_ShouldPreserveOnlyItsBatchProgressAcrossRoutes(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var global = new ApplicationLogScope(capture.Logger);
        using var scope = PollutedCleanupScope();
        using var activity = new Activity("service-cleanup").SetIdFormat(ActivityIdFormat.W3C).Start();
        var failure = new IOException(Secret);

        await RunTokenBatch();
        AssertBatch(capture, activity, Done, "file-tokens", "succeeded", processed: 2, updated: 1, skipped: 1);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => RunTokenBatch(failure)));
        AssertBatch(capture, activity, Interrupted, "file-tokens", "partial", processed: 1, updated: 1);

        await RunUploadBatch();
        AssertBatch(capture, activity, Done, "upload-sessions", "succeeded", processed: 1, updated: 1, removed: 1, settlements: 1);
        await RunUploadBatch(quotaFailure: new TimeoutException(Secret));
        AssertBatch(capture, activity, Failed, "upload-sessions", "partial", processed: 1, updated: 1, removed: 1,
            failed: 1, failureKind: "timeout");
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => RunUploadBatch(queryFailure: failure)));
        AssertBatch(capture, activity, Interrupted, "upload-sessions", "partial", processed: 1, updated: 1, removed: 1, settlements: 1);
    }

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public async Task Foreground_ShouldKeepOnlyItsOperationAndFailureKindAcrossRoutes(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var global = new ApplicationLogScope(capture.Logger);
        using var scope = PollutedCleanupScope();
        using var activity = new Activity("foreground-cleanup").SetIdFormat(ActivityIdFormat.W3C).Start();
        foreach (var operation in new[] { "quota-release", "quota-complete", "directory" })
        {
            await RunForegroundCleanup(operation);
            var failure = operation == "directory" ? "other" : "io";
            AssertOutput(capture, activity, typeof(ServiceCleanupSummary), Foreground, "application", "Error", failure,
                ForegroundFields, ("cleanupOperation", operation));
            capture.Clear();
        }

        capture.Factory.CreateLogger<AttachmentController>().LogError(new EventId(2, "UploadAccountingFailed"),
            "Attachment accounting {cleanupOperation} {failureKind}", "quota-release", "io");
        AssertOutput(capture, activity, typeof(AttachmentController), Foreground, "application", "Error", "io",
            ForegroundFields, ("cleanupOperation", "quota-release"));
    }

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public async Task ServiceCleanup_ShouldPreserveEmptyRunsAndOutputFailureOwnership(bool candidate, string environment, bool audit)
    {
        using var writer = new HangfireBrokenWriter();
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit, output: writer);
        using var global = new ApplicationLogScope(capture.Logger);
        using var scope = PollutedCleanupScope();
        var repositoryFailure = new IOException(Secret);
        var sinkFailure = new IOException(Secret);
        capture.Sink.EmitFailure = sinkFailure;

        using var uploads = new Uploads();
        var tokens = Tokens();
        tokens.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<FileAccessToken, bool>>>())).ReturnsAsync([]);
        await uploads.Service.CleanupExpiredSessionsAsync();
        await TokenService(tokens).CleanupExpiredTokensAsync();
        uploads.Repository.Setup(r => r.QueryExpiredAcrossTenantsAsync(Now)).ThrowsAsync(repositoryFailure);
        tokens.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<FileAccessToken, bool>>>())).ThrowsAsync(repositoryFailure);
        Assert.Same(repositoryFailure, await Assert.ThrowsAsync<IOException>(() => uploads.Service.CleanupExpiredSessionsAsync()));
        Assert.Same(repositoryFailure, await Assert.ThrowsAsync<IOException>(() => TokenService(tokens).CleanupExpiredTokensAsync()));
        Assert.Empty(capture.Events);
        Assert.Equal("", capture.Emergency.ToString());

        if (audit)
        {
            var interrupted = await Assert.ThrowsAsync<AggregateException>(() => RunTokenBatch(repositoryFailure));
            var foreground = await Assert.ThrowsAsync<AggregateException>(() => RunForegroundCleanup("quota-release"));
            Assert.Same(sinkFailure, Assert.Single(interrupted.Flatten().InnerExceptions));
            Assert.Same(sinkFailure, Assert.Single(foreground.Flatten().InnerExceptions));
        }
        else
        {
            Assert.Same(repositoryFailure, await Assert.ThrowsAsync<IOException>(() => RunTokenBatch(repositoryFailure)));
            await RunForegroundCleanup("quota-release");
            if (candidate)
            {
                using var json = JsonDocument.Parse(capture.Emergency.ToString());
                Assert.Equal("pipeline.output_failed", json.RootElement.GetProperty("eventCode").GetString());
                Assert.DoesNotContain(Secret, capture.Emergency.ToString());
            }
        }
        Assert.Empty(capture.Events);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task ServiceCleanup_LegacyCodeConsoleShouldKeepFixedSummaries(string environment)
    {
        var previous = Console.Out;
        using var output = new StringWriter();
        using var services = new ServiceCollection().BuildServiceProvider();
        try
        {
            Console.SetOut(output);
            var configuration = new LoggerConfiguration();
            SerilogSetup.ConfigureLegacy(configuration, new ConfigurationBuilder().Build(), services, new SerilogOptions
            {
                Console = new() { Enable = true, EnableSqlLog = false },
                File = new() { Enable = false }, Database = new() { Enable = false }
            }, environment);
            using var logger = configuration.CreateLogger();
            using var global = new ApplicationLogScope(logger);
            using var scope = PollutedCleanupScope();
            await RunTokenBatch();
            await RunForegroundCleanup("quota-release");
            Assert.Collection(output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries),
                line => Assert.EndsWith("Service cleanup completed.", line.TrimEnd()),
                line => Assert.EndsWith("Upload cleanup operation consumed a failure.", line.TrimEnd()));
            Assert.DoesNotContain(Secret, output.ToString());
        }
        finally { Console.SetOut(previous); }
    }

    [Fact]
    public void ServiceCleanupSink_ShouldRejectInvalidCombinationsAndFields()
    {
        using var output = new HttpBoundaryTestSink();
        var sink = new ServiceCleanupLogSink(output);
        var timestamp = DateTimeOffset.UtcNow.AddDays(-1);
        var trace = ActivityTraceId.CreateRandom();
        var span = ActivitySpanId.CreateRandom();
        var template = new MessageTemplateParser().Parse(Secret);
        LogEvent Create(Type source, string code, string kind, LogEventLevel level) => new(timestamp, level,
            new IOException(Secret), template,
            [new("SourceContext", new ScalarValue(source.FullName)), new("EventCode", new ScalarValue(code)),
                new("jobKind", new ScalarValue(kind)), new("processedCount", new ScalarValue(-1)),
                new("updatedCount", new ScalarValue(1L)), new("durationMs", new ScalarValue(double.PositiveInfinity)),
                new("outcome", new ScalarValue("succeeded")), new("failureKind", new ScalarValue(Secret)),
                new("cleanupOperation", new ScalarValue(Secret))], trace, span);

        var adjacent = Create(typeof(FileCleanupJob), Done, "file-temp", LogEventLevel.Information);
        sink.Emit(adjacent);
        sink.Emit(Create(typeof(ServiceCleanupSummary), Failed, "file-tokens", LogEventLevel.Error));
        sink.Emit(Create(typeof(ServiceCleanupSummary), Foreground, "file-tokens", LogEventLevel.Information));
        sink.Emit(Create(typeof(ServiceCleanupSummary), Interrupted, "file-tokens", LogEventLevel.Information));
        sink.Emit(Create(typeof(ServiceCleanupSummary), Foreground, "file-tokens", LogEventLevel.Error));
        Assert.Collection(output.Events,
            value => Assert.Same(adjacent, value),
            value => AssertFields(value, "runtime.unclassified", false, IdentityFields),
            value => AssertFields(value, "runtime.unclassified", true, IdentityFields),
            value =>
            {
                AssertFields(value, Interrupted, false, [.. IdentityFields, "jobKind"]);
                Assert.Equal(timestamp, value.Timestamp);
                Assert.Equal(LogEventLevel.Information, value.Level);
                Assert.Equal(trace, value.TraceId);
                Assert.Equal(span, value.SpanId);
            },
            value => AssertFields(value, Foreground, false, IdentityFields));
    }

    private static void AssertFields(LogEvent value, string code, bool diagnostic, string[] fields)
    {
        Assert.Equal(code, ReadScalar(value, "EventCode"));
        Assert.Equal(diagnostic, ReadScalar(value, "Diagnostic"));
        Assert.Equal(fields.Order(StringComparer.Ordinal), value.Properties.Keys.Order(StringComparer.Ordinal));
        Assert.Null(value.Exception);
        Assert.DoesNotContain(Secret, value.RenderMessage());
    }

    private static void AssertBatch(HttpBoundaryCapture capture, Activity activity, string code, string kind, string outcome,
        int processed, int updated, int skipped = 0, int removed = 0, int settlements = 0, int failed = 0, string? failureKind = null)
    {
        AssertOutput(capture, activity, typeof(ServiceCleanupSummary), code, "job", code == Failed ? "Error" : "Information", failureKind,
            failureKind == null ? BatchFields : [.. BatchFields, "failureKind"],
            ("jobKind", kind), ("outcome", outcome), ("processedCount", processed), ("updatedCount", updated),
            ("skippedCount", skipped), ("removedDirectoryCount", removed), ("settlementCount", settlements), ("failedCount", failed));
        capture.Clear();
    }

    private static void AssertOutput(HttpBoundaryCapture capture, Activity activity, Type source, string code, string category,
        string level, string? failure, string[] fields, params (string Key, object Value)[] expected)
    {
        var summary = Assert.Single(capture.Events);
        Assert.Equal(code, summary.Code);
        Assert.Equal(capture.Candidate && level == "Information" ? "Info" : level, summary.Level);
        Assert.Equal(failure, summary.FailureKind);
        Assert.False(summary.Diagnostic);
        capture.AssertSafe();
        if (capture.Candidate)
        {
            using var json = JsonDocument.Parse(capture.Output.ToString());
            var root = json.RootElement;
            var properties = root.GetProperty("properties");
            Assert.Equal(category, root.GetProperty("sourceCategory").GetString());
            Assert.Equal(activity.TraceId.ToHexString(), root.GetProperty("traceId").GetString());
            Assert.Equal(activity.SpanId.ToHexString(), root.GetProperty("spanId").GetString());
            Assert.False(root.TryGetProperty("operationId", out _));
            Assert.Equal(fields.Order(StringComparer.Ordinal), properties.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
            foreach (var (key, expectedValue) in expected)
            {
                var property = properties.GetProperty(key);
                Assert.Equal(expectedValue, expectedValue is int ? (object)property.GetInt32() : property.GetString());
            }
            Assert.DoesNotContain(Secret, capture.Output.ToString());
        }
        else
        {
            var value = Assert.Single(capture.Sink.Events);
            AssertFields(value, code, false, [.. IdentityFields, .. fields]);
            Assert.Equal(source.FullName, ReadScalar(value, "SourceContext"));
            Assert.Equal(category, ReadScalar(value, "SourceCategory"));
            Assert.Equal(activity.TraceId, value.TraceId);
            Assert.Equal(activity.SpanId, value.SpanId);
            foreach (var (key, expectedValue) in expected) Assert.Equal(expectedValue, ReadScalar(value, key));
        }
    }

    private static object? ReadScalar(LogEvent value, string key) => Assert.IsType<ScalarValue>(value.Properties[key]).Value;

    private static IDisposable PollutedCleanupScope() => LogContext.Push(
        new PropertyEnricher("SourceContext", typeof(FileCleanupJob).FullName!),
        new PropertyEnricher("EventCode", "job.cleanup.warning"), new PropertyEnricher("SourceCategory", "http"),
        new PropertyEnricher("jobKind", "file-temp"), new PropertyEnricher("outcome", "failed"), new PropertyEnricher("Diagnostic", true),
        new PropertyEnricher("operationId", "12345678-1234-4234-8234-123456789abc"),
        new PropertyEnricher("traceId", "11111111111111111111111111111111"), new PropertyEnricher("spanId", "1111111111111111"),
        new PropertyEnricher("privatePayload", Secret), new PropertyEnricher("statusCode", 503), new PropertyEnricher("movedCount", 7),
        new PropertyEnricher("failureKind", "database"), new PropertyEnricher("processedCount", 99), new PropertyEnricher("settlementCount", 66),
        new PropertyEnricher("cleanupOperation", "directory"));

    private static async Task RunTokenBatch(Exception? failure = null)
    {
        var tokens = Tokens();
        tokens.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<FileAccessToken, bool>>>()))
            .ReturnsAsync([new() { Id = 1, TokenHash = Secret }, new() { Id = 2 }]);
        tokens.Setup(r => r.TryRevokeByIdAsync(1, Now)).ReturnsAsync(true);
        var second = tokens.Setup(r => r.TryRevokeByIdAsync(2, Now));
        if (failure == null) second.ReturnsAsync(false);
        else second.ThrowsAsync(failure);
        await TokenService(tokens).CleanupExpiredTokensAsync();
    }

    private static async Task RunUploadBatch(Exception? quotaFailure = null, Exception? queryFailure = null)
    {
        using var files = new Uploads();
        var session = Session();
        files.CreateDirectory(session);
        files.Repository.Setup(r => r.QueryExpiredAcrossTenantsAsync(Now)).ReturnsAsync([session]);
        files.Repository.Setup(r => r.TryMarkExpiredAcrossTenantsAsync(session.SessionId, 3, 7, Now, Now)).ReturnsAsync(true);
        if (quotaFailure != null) files.Quota.Setup(q => q.FailUploadAsync(7, session.SessionId)).ThrowsAsync(quotaFailure);
        if (queryFailure != null)
            files.Repository.Setup(r => r.QueryTerminalForSettlementAcrossTenantsAsync(Now.AddDays(-8), 2000)).ThrowsAsync(queryFailure);
        await files.Service.CleanupExpiredSessionsAsync();
        Assert.False(Directory.Exists(Path.Combine(files.Root, session.SessionId)));
    }

    private static async Task RunForegroundCleanup(string operation)
    {
        using var files = new Uploads();
        var session = Session(operation == "quota-release" ? "Cancelled" : "Completed");
        session.AttachmentId = 123;
        var requestId = session.SessionId;
        if (operation == "directory") session.SessionId = Secret;
        else files.CreateDirectory(session);
        files.Repository.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UploadSession, bool>>>())).ReturnsAsync(session);
        var attachment = new AttachmentVo { VoId = 123 };
        files.Attachments.Setup(a => a.QueryByIdAsync(123)).ReturnsAsync(attachment);
        if (operation == "quota-release")
        {
            files.Quota.Setup(q => q.FailUploadAsync(7, requestId)).ThrowsAsync(new IOException(Secret));
            await files.Service.CancelSessionAsync(requestId, 7);
        }
        else
        {
            if (operation == "quota-complete")
                files.Quota.Setup(q => q.CompleteUploadAsync(7, requestId)).ThrowsAsync(new IOException(Secret));
            Assert.Same(attachment, await files.Service.MergeChunksAsync(new MergeChunksDto { SessionId = requestId }, 7, Secret));
        }
        Assert.False(Directory.Exists(Path.Combine(files.Root, requestId)));
    }
}
