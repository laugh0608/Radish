using System;
using System.Collections.Generic;
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
using Radish.Api.Services;
using Radish.Api.Tests.ErrorHandling;
using Radish.Common.OptionTool;
using Radish.Extension.Log;
using Radish.IRepository;
using Radish.Model;
using Radish.Service.Internal;
using Radish.Service.Jobs;
using Serilog;
using Serilog.Context;
using Serilog.Core.Enrichers;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Radish.Api.Tests;

public sealed partial class CleanupLoggingTests
{
    private const string Completed = "job.cleanup.completed";
    private const string Warning = "job.cleanup.warning";
    private const string Failed = "job.cleanup.failed";
    private const string CapacityWarning = "job.cleanup.capacity_warning";
    private static readonly string[] FileFields = ["jobKind", "outcome", "durationMs", "processedCount", "movedCount", "missingCount",
        "skippedCount", "removedDirectoryCount", "failedCount", "directoryFailureCount"];
    private static readonly string[] IdentityFields = ["SourceContext", "SourceCategory", "EventCode", "Diagnostic"];

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public async Task FileCleanup_ShouldRebuildAllFourSummariesWithoutAmbientFields(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var logger = new ApplicationLogScope(capture.Logger);
        using var scope = PolluteCleanupScope();
        using var activity = new Activity("file-cleanup").SetIdFormat(ActivityIdFormat.W3C).Start();
        using var files = new Files(DateTimeOffset.UtcNow.AddDays(120));
        var file = WriteFixtureFile(files, "source");
        files.Storage.Setup(s => s.GetFullPath("existing")).Returns(file);
        files.Storage.Setup(s => s.GetFullPath("missing")).Returns(Path.Combine(files.Root, "absent"));
        files.Storage.Setup(s => s.GetFullPath("throws")).Throws(new IOException(HttpBoundaryCapture.Secret));
        SetAttachments(files, new Attachment { StoragePath = "existing" }, new Attachment { StoragePath = "missing" },
            new Attachment { StoragePath = "throws" });

        Assert.Equal(2, await files.Job.CleanupDeletedFilesAsync());
        Assert.False(File.Exists(file));
        AssertFileSummary(capture, activity, Failed, "Error", "file-deleted", "partial", "io",
            ("processedCount", 2), ("movedCount", 1), ("missingCount", 1), ("failedCount", 1));

        SetAttachments(files, new Attachment { StoragePath = "missing" });
        Assert.Equal(1, await files.Job.CleanupDeletedFilesAsync());
        AssertFileSummary(capture, activity, Warning, "Warning", "file-deleted", "partial", null,
            ("processedCount", 1), ("movedCount", 0), ("missingCount", 1), ("directoryFailureCount", 0));

        var temp = WriteFixtureFile(files, "Temp/ordinary/old");
        var chunk = WriteFixtureFile(files, "Temp/Chunks/session/old");
        Assert.Equal(1, await files.Job.CleanupTempFilesAsync());
        Assert.False(File.Exists(temp));
        Assert.True(File.Exists(chunk));
        AssertFileSummary(capture, activity, Completed, "Information", "file-temp", "succeeded", null,
            ("processedCount", 1), ("movedCount", 1));

        var recycled = WriteFixtureFile(files, "Recycle/expired/old");
        var removed = await files.Job.CleanupRecycleBinAsync();
        Assert.Equal(3, removed);
        Assert.False(File.Exists(recycled));
        AssertFileSummary(capture, activity, Completed, "Information", "file-recycle", "succeeded", null,
            ("processedCount", removed), ("movedCount", 0));

        var orphan = new Attachment { Id = 7, StoragePath = "missing" };
        var protectedAttachment = new Attachment { Id = 8, StoragePath = "protected" };
        SetAttachments(files, orphan, protectedAttachment);
        files.References.Setup(r => r.GetReferencedAttachmentIdsAsync(It.IsAny<IReadOnlyCollection<long>>())).ReturnsAsync([8]);
        Assert.Equal(1, await files.Job.CleanupOrphanAttachmentsAsync());
        Assert.True(orphan.IsDeleted);
        Assert.False(protectedAttachment.IsDeleted);
        files.Repository.Verify(r => r.UpdateAsync(orphan), Times.Once);
        AssertFileSummary(capture, activity, Warning, "Warning", "file-orphan", "partial", null,
            ("processedCount", 1), ("missingCount", 1), ("skippedCount", 1));
    }

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public async Task RecordCleanup_ShouldSeparateCompletionAndCapacityFields(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var scope = PolluteCleanupScope();
        using var activity = new Activity("record-cleanup").SetIdFormat(ActivityIdFormat.W3C).Start();
        var (inbox, wiki, chat, result) = RecordJobs(capture.Factory);

        Assert.Same(result, await inbox.ExecuteAsync());
        Assert.Equal(4, await wiki.ExecuteAsync());
        Assert.Equal(5, await chat.ExecuteAsync());
        Assert.Equal(new[] { Completed, CapacityWarning, Completed, Completed }, capture.Codes);
        AssertSummary(capture, 0, activity, typeof(NotificationInboxCleanupJob), Completed, "Information", "notification-inbox", null,
            ["jobKind", "relationCount", "groupCount", "notificationCount", "durationMs"],
            ("relationCount", 3), ("groupCount", 0), ("notificationCount", 0));
        AssertSummary(capture, 1, activity, typeof(NotificationInboxCleanupJob), CapacityWarning, "Warning", "notification-inbox", null,
            ["jobKind", "count"], ("count", 2));
        AssertSummary(capture, 2, activity, typeof(WikiDraftPayloadCleanupJob), Completed, "Information", "wiki-drafts", null,
            ["jobKind", "count", "durationMs"], ("count", 4));
        AssertSummary(capture, 3, activity, typeof(ChatMessageReactionOperationCleanupJob), Completed, "Information", "chat-reactions", null,
            ["jobKind", "count", "durationMs"], ("count", 5));
        capture.AssertSafe();
    }

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public async Task Cleanup_ShouldPreserveRepositoryAndOutputFailureOwnership(bool candidate, string environment, bool audit)
    {
        using var fileWriter = new HangfireBrokenWriter();
        using var fileCapture = new HttpBoundaryCapture(candidate, environment, audit: audit, output: fileWriter);
        using var global = new ApplicationLogScope(fileCapture.Logger);
        using var scope = PolluteCleanupScope();
        using var files = new Files();
        using var recordWriter = new HangfireBrokenWriter();
        using var recordCapture = new HttpBoundaryCapture(candidate, environment, audit: audit, output: recordWriter);
        var repositoryFailure = new IOException(HttpBoundaryCapture.Secret);
        var sinkFailure = new IOException(HttpBoundaryCapture.Secret);
        fileCapture.Sink.EmitFailure = recordCapture.Sink.EmitFailure = sinkFailure;

        SetAttachments(files);
        Assert.Equal(0, await files.Job.CleanupDeletedFilesAsync());
        Assert.Equal(0, await files.Job.CleanupTempFilesAsync());
        Assert.Equal(0, await files.Job.CleanupRecycleBinAsync());
        Assert.Equal(0, await files.Job.CleanupOrphanAttachmentsAsync());
        var (failedInbox, failedWiki, failedChat, _) = RecordJobs(recordCapture.Factory, repositoryFailure);
        Assert.Same(repositoryFailure, await Assert.ThrowsAsync<IOException>(() => failedInbox.ExecuteAsync()));
        Assert.Same(repositoryFailure, await Assert.ThrowsAsync<IOException>(() => failedWiki.ExecuteAsync()));
        Assert.Same(repositoryFailure, await Assert.ThrowsAsync<IOException>(() => failedChat.ExecuteAsync()));
        Assert.Empty(fileCapture.Events);
        Assert.Empty(recordCapture.Events);
        Assert.Equal("", fileCapture.Emergency.ToString());
        Assert.Equal("", recordCapture.Emergency.ToString());

        files.Repository.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<Attachment, bool>>>())).ThrowsAsync(repositoryFailure);
        var (inbox, _, _, result) = RecordJobs(recordCapture.Factory);
        if (audit)
        {
            var fileError = await Assert.ThrowsAsync<AggregateException>(() => files.Job.CleanupDeletedFilesAsync());
            var inboxError = await Assert.ThrowsAsync<AggregateException>(() => inbox.ExecuteAsync());
            Assert.Same(sinkFailure, Assert.Single(fileError.Flatten().InnerExceptions));
            Assert.Same(sinkFailure, Assert.Single(inboxError.Flatten().InnerExceptions));
        }
        else
        {
            Assert.Equal(0, await files.Job.CleanupDeletedFilesAsync());
            Assert.Same(result, await inbox.ExecuteAsync());
        }

        Assert.Empty(fileCapture.Events);
        Assert.Empty(recordCapture.Events);
        if (candidate)
        {
            foreach (var capture in new[] { fileCapture, recordCapture })
            {
                using var json = JsonDocument.Parse(capture.Emergency.ToString());
                Assert.Equal("pipeline.output_failed", json.RootElement.GetProperty("eventCode").GetString());
                Assert.DoesNotContain(HttpBoundaryCapture.Secret, capture.Emergency.ToString());
            }
        }
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Cleanup_LegacyCodeConsoleShouldUseFixedSummaries(string environment)
    {
        var previous = Console.Out;
        using var output = new StringWriter();
        using var services = new ServiceCollection().BuildServiceProvider();
        try
        {
            Console.SetOut(output);
            var config = new LoggerConfiguration();
            SerilogSetup.ConfigureLegacy(config, new ConfigurationBuilder().Build(), services, new SerilogOptions
            {
                Console = new() { Enable = true, EnableSqlLog = false },
                File = new() { Enable = false }, Database = new() { Enable = false }
            }, environment);
            using var logger = config.CreateLogger();
            using var global = new ApplicationLogScope(logger);
            using var factory = LoggerFactory.Create(builder => builder.AddSerilog(logger));
            using var scope = PolluteCleanupScope();
            using var files = new Files();
            SetAttachments(files, new Attachment { StoragePath = "missing" });
            files.Storage.Setup(s => s.GetFullPath("missing")).Returns(Path.Combine(files.Root, "absent"));
            var (inbox, _, _, _) = RecordJobs(factory);

            Assert.Equal(1, await files.Job.CleanupDeletedFilesAsync());
            await inbox.ExecuteAsync();
            Assert.Collection(Lines(output),
                line => Assert.EndsWith("Cleanup batch completed with warnings.", line.TrimEnd()),
                line => Assert.EndsWith("Cleanup batch completed.", line.TrimEnd()),
                line => Assert.EndsWith("Notification inbox capacity requires attention.", line.TrimEnd()));
            Assert.DoesNotContain(HttpBoundaryCapture.Secret, output.ToString());
        }
        finally { Console.SetOut(previous); }
    }

    [Fact]
    public void CleanupSink_ShouldKeepDirectoryFailureKindAndPassThroughOtherSources()
    {
        using var output = new HttpBoundaryTestSink();
        var sink = new CleanupJobLogSink(output);
        var timestamp = DateTimeOffset.UtcNow.AddDays(-1);
        var trace = ActivityTraceId.CreateRandom();
        var span = ActivitySpanId.CreateRandom();
        var template = new MessageTemplateParser().Parse(HttpBoundaryCapture.Secret);
        LogEvent Create(Type source, string code, LogEventLevel level = LogEventLevel.Warning) =>
            new(timestamp, level, new IOException(HttpBoundaryCapture.Secret), template,
            [
                new("SourceContext", new ScalarValue(source.FullName)), new("EventCode", new ScalarValue(code)),
                new("jobKind", new ScalarValue("file-temp")), new("outcome", new ScalarValue("partial")),
                new("directoryFailureCount", new ScalarValue(1)), new("failureKind", new ScalarValue("access-denied")),
                new("processedCount", new ScalarValue(-1)), new("durationMs", new ScalarValue(double.PositiveInfinity))
            ], trace, span);

        var serviceEvent = Create(typeof(ServiceCleanupSummary), Completed);
        sink.Emit(serviceEvent);
        sink.Emit(Create(typeof(FileCleanupJob), Warning));
        sink.Emit(Create(typeof(ChatMessageReactionOperationCleanupJob), CapacityWarning));
        sink.Emit(Create(typeof(FileCleanupJob), "job.cleanup.interrupted", LogEventLevel.Information));

        Assert.Collection(output.Events,
            value => Assert.Same(serviceEvent, value),
            value =>
            {
                Assert.Equal(timestamp, value.Timestamp);
                Assert.Equal(trace, value.TraceId);
                Assert.Equal(span, value.SpanId);
                Assert.Equal(LogEventLevel.Warning, value.Level);
                Assert.Equal("access-denied", ScalarValueOf(value, "failureKind"));
                Assert.Equal(1, ScalarValueOf(value, "directoryFailureCount"));
                Assert.Equal(IdentityFields.Concat(new[] { "jobKind", "outcome", "directoryFailureCount", "failureKind" }).Order(),
                    value.Properties.Keys.Order());
                Assert.Null(value.Exception);
            },
            value => AssertUnknown(value, false),
            value => AssertUnknown(value, true));
    }

    private static void AssertUnknown(LogEvent value, bool diagnostic)
    {
        Assert.Equal("runtime.unclassified", ScalarValueOf(value, "EventCode"));
        Assert.Equal(diagnostic, ScalarValueOf(value, "Diagnostic"));
        Assert.Equal(IdentityFields.Order(), value.Properties.Keys.Order());
        Assert.Null(value.Exception);
        Assert.DoesNotContain(HttpBoundaryCapture.Secret, value.RenderMessage());
    }

    private static void AssertFileSummary(HttpBoundaryCapture capture, Activity activity, string code, string level, string kind,
        string outcome, string? failure, params (string Name, object Value)[] counts)
    {
        Assert.Single(capture.Events);
        AssertSummary(capture, 0, activity, typeof(FileCleanupJob), code, level, kind, failure,
            failure == null ? FileFields : [.. FileFields, "failureKind"], [("outcome", outcome), .. counts]);
        capture.AssertSafe();
        capture.Clear();
    }

    private static void AssertSummary(HttpBoundaryCapture capture, int index, Activity activity, Type source, string code,
        string level, string kind, string? failure, string[] fields, params (string Name, object Value)[] expected)
    {
        var summary = capture.Events[index];
        Assert.Equal(code, summary.Code);
        Assert.Equal(capture.Candidate && level == "Information" ? "Info" : level, summary.Level);
        Assert.Equal(failure, summary.FailureKind);
        Assert.False(summary.Diagnostic);
        if (capture.Candidate)
        {
            using var json = JsonDocument.Parse(Lines(capture.Output)[index]);
            var root = json.RootElement;
            Assert.Equal("job", root.GetProperty("sourceCategory").GetString());
            Assert.Equal(activity.TraceId.ToHexString(), root.GetProperty("traceId").GetString());
            Assert.Equal(activity.SpanId.ToHexString(), root.GetProperty("spanId").GetString());
            Assert.False(root.TryGetProperty("operationId", out _));
            var properties = root.GetProperty("properties");
            Assert.Equal(fields.Order(), properties.EnumerateObject().Select(p => p.Name).Order());
            Assert.Equal(kind, properties.GetProperty("jobKind").GetString());
            foreach (var (name, value) in expected)
            {
                var field = properties.GetProperty(name);
                Assert.Equal(value, value is int ? (object)field.GetInt32() : field.GetString());
            }
        }
        else
        {
            var value = capture.Sink.Events.ElementAt(index);
            Assert.Equal(fields.Concat(IdentityFields).Order(), value.Properties.Keys.Order());
            Assert.Equal(source.FullName, ScalarValueOf(value, "SourceContext"));
            Assert.Equal("job", ScalarValueOf(value, "SourceCategory"));
            Assert.Equal(activity.TraceId, value.TraceId);
            Assert.Equal(activity.SpanId, value.SpanId);
            Assert.Equal(kind, ScalarValueOf(value, "jobKind"));
            foreach (var (name, expectedValue) in expected) Assert.Equal(expectedValue, ScalarValueOf(value, name));
        }
    }

    private static object? ScalarValueOf(LogEvent value, string key) => Assert.IsType<ScalarValue>(value.Properties[key]).Value;

    private static IDisposable PolluteCleanupScope() => LogContext.Push(
        new PropertyEnricher("SourceContext", typeof(BusinessJobSummary).FullName!),
        new PropertyEnricher("EventCode", "job.batch.failed"), new PropertyEnricher("SourceCategory", "http"),
        new PropertyEnricher("jobKind", "post-lottery"), new PropertyEnricher("outcome", "failed"),
        new PropertyEnricher("privatePayload", HttpBoundaryCapture.Secret), new PropertyEnricher("statusCode", 503),
        new PropertyEnricher("operationId", "12345678-1234-4234-8234-123456789abc"), new PropertyEnricher("failureKind", "database"),
        new PropertyEnricher("movedCount", 8), new PropertyEnricher("relationCount", 9), new PropertyEnricher("count", 99),
        new PropertyEnricher("durationMs", 777d), new PropertyEnricher("Diagnostic", true),
        new PropertyEnricher("traceId", "11111111111111111111111111111111"), new PropertyEnricher("spanId", "1111111111111111"));

    private static void SetAttachments(Files files, params Attachment[] values) =>
        files.Repository.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<Attachment, bool>>>())).ReturnsAsync(new List<Attachment>(values));

    private static string WriteFixtureFile(Files files, string relativePath)
    {
        var path = Path.Combine(files.Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, HttpBoundaryCapture.Secret);
        return path;
    }

    private static (NotificationInboxCleanupJob Inbox, WikiDraftPayloadCleanupJob Wiki, ChatMessageReactionOperationCleanupJob Chat,
        NotificationInboxCleanupResult Result) RecordJobs(ILoggerFactory factory, Exception? failure = null)
    {
        var clock = new FixedTimeProvider();
        var now = clock.GetUtcNow().UtcDateTime;
        var inbox = new Mock<INotificationInboxRepository>(MockBehavior.Strict);
        var wiki = new Mock<IWikiDocumentRepository>(MockBehavior.Strict);
        var chat = new Mock<IChatMessageReactionRepository>(MockBehavior.Strict);
        var result = new NotificationInboxCleanupResult(3, 0, 0,
            [new NotificationInboxCapacityWarning(1, 2, 9000), new NotificationInboxCapacityWarning(1, 3, 8000)]);
        var inboxCall = inbox.Setup(r => r.CleanupAsync(now, 200, 5000));
        var wikiCall = wiki.Setup(r => r.PurgeTerminalDraftPayloadsAsync(now.AddDays(-90), 200, now));
        var chatCall = chat.Setup(r => r.DeleteExpiredOperationsAsync(now, 500));
        if (failure == null)
        {
            inboxCall.ReturnsAsync(result);
            wikiCall.ReturnsAsync(4);
            chatCall.ReturnsAsync(5);
        }
        else
        {
            inboxCall.ThrowsAsync(failure);
            wikiCall.ThrowsAsync(failure);
            chatCall.ThrowsAsync(failure);
        }

        return (new(inbox.Object, clock, factory.CreateLogger<NotificationInboxCleanupJob>()),
            new(wiki.Object, clock, factory.CreateLogger<WikiDraftPayloadCleanupJob>()),
            new(chat.Object, clock, factory.CreateLogger<ChatMessageReactionOperationCleanupJob>()), result);
    }
}
