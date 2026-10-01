using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Api.Services;
using Radish.Common.OptionTool;
using Radish.Common.TimeTool;
using Radish.Extension.Log;
using Radish.Infrastructure.FileStorage;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Service.Jobs;
using Serilog;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class CleanupLoggingTests
{
    private const string Secret = "CLEANUP_PRIVATE_SENTINEL";

    [Theory]
    [InlineData(0, 2, false, 60, "job.retrying")]
    [InlineData(1, 2, false, 300, "job.retrying")]
    [InlineData(2, 2, false, 0, "job.failed")]
    [InlineData(2, 2, true, 0, "job.failed")]
    [InlineData(0, 0, false, 0, "job.failed")]
    [InlineData(0, 2, false, 0, "job.retrying")]
    public void Hangfire_ShouldObserveActualRetryDecision_WithoutChangingPolicy(
        int attempt, int limit, bool delete, int delay, string eventCode)
    {
        using var capture = new Capture(true);
        var values = new Dictionary<string, string> { ["RetryCount"] = attempt.ToString() };
        var connection = new Mock<IStorageConnection>();
        connection.Setup(c => c.GetJobParameter(Secret, "RetryCount")).Returns(() => values["RetryCount"]);
        connection.Setup(c => c.SetJobParameter(Secret, "RetryCount", It.IsAny<string>()))
            .Callback<string, string, string>((_, _, value) => values["RetryCount"] = value);
        var transaction = new Mock<IWriteOnlyTransaction>();
        var failure = new InvalidOperationException(Secret);
        var failed = new FailedState(failure);
        var context = new ElectStateContext(new ApplyStateContext(Mock.Of<JobStorage>(), connection.Object,
            transaction.Object, new BackgroundJob(Secret, Job.FromExpression(() => JobTarget.NoOp()), DateTime.UtcNow),
            failed, ProcessingState.StateName));
        var retry = new AutomaticRetryAttribute
        {
            Attempts = limit, DelaysInSeconds = delay == 0 ? [0] : [60, 300], LogEvents = false,
            OnAttemptsExceeded = delete ? AttemptsExceededAction.Delete : AttemptsExceededAction.Fail
        };
        var observer = new HangfireRuntimeStateFilter();
        Assert.True(observer.Order > retry.Order);
        var before = DateTime.UtcNow;
        retry.OnStateElection(context);
        var elected = context.CandidateState;
        observer.OnStateElection(context);
        Assert.Same(elected, context.CandidateState);
        Assert.Same(failure, failed.Exception);
        if (eventCode == "job.retrying")
        {
            Assert.Equal((attempt + 1).ToString(), values["RetryCount"]);
            if (delay == 0) Assert.IsType<EnqueuedState>(elected);
            else Assert.InRange(Assert.IsType<ScheduledState>(elected).EnqueueAt, before.AddSeconds(delay), DateTime.UtcNow.AddSeconds(delay));
        }
        else if (delete) Assert.IsType<DeletedState>(elected);
        else Assert.Same(failed, elected);
        transaction.Verify(t => t.Commit(), Times.Never);
        using var value = JsonDocument.Parse(capture.Output.ToString());
        Assert.Equal(eventCode, value.RootElement.GetProperty("eventCode").GetString());
        Assert.Equal(eventCode == "job.retrying" ? "Warning" : "Error", value.RootElement.GetProperty("level").GetString());
        Assert.Equal("invalid-operation", value.RootElement.GetProperty("properties").GetProperty("failureKind").GetString());
        Assert.DoesNotContain(Secret, capture.Output.ToString());
    }

    [Fact]
    public void Hangfire_ShouldKeepNormalSchedulingAndShutdownRequeueQuiet()
    {
        using var capture = new Capture(true);
        foreach (var state in new IState[] { new ScheduledState(TimeSpan.FromMinutes(1)), new EnqueuedState(), new SucceededState(null, 0, 0) })
        {
            var context = new ElectStateContext(new ApplyStateContext(Mock.Of<JobStorage>(), Mock.Of<IStorageConnection>(),
                Mock.Of<IWriteOnlyTransaction>(), new BackgroundJob(Secret, Job.FromExpression(() => JobTarget.NoOp()), DateTime.UtcNow),
                state, ProcessingState.StateName));
            new HangfireRuntimeStateFilter().OnStateElection(context);
            Assert.Same(state, context.CandidateState);
        }
        Assert.Equal("", capture.Output.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HangfireProvider_ShouldNeverEvaluateMessages_OrDuplicateAutomaticRetry(bool candidate)
    {
        using var capture = new Capture(candidate);
        var provider = new HangfireRuntimeLogProvider();
        Func<string> message = () => throw new InvalidOperationException(Secret);
        var retry = provider.GetLogger(typeof(AutomaticRetryAttribute).FullName!);
        Assert.False(retry.Log(Hangfire.Logging.LogLevel.Error, message, new InvalidOperationException(Secret)));
        var runtime = provider.GetLogger(Secret);
        Assert.True(runtime.Log(Hangfire.Logging.LogLevel.Error, null));
        Assert.Equal("", capture.Output.ToString());
        Assert.True(runtime.Log(Hangfire.Logging.LogLevel.Error, message, new IOException(Secret)));
        Assert.Contains("hangfire.runtime_failed", capture.Output.ToString());
        Assert.DoesNotContain(Secret, capture.Output.ToString());
        Assert.Single(Lines(capture.Output));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FileCleanup_ShouldAggregatePartialWork_AndPreserveReturnContract(bool candidate)
    {
        using var capture = new Capture(candidate);
        using var files = new Files();
        var source = Path.Combine(files.Root, Secret);
        await File.WriteAllTextAsync(source, Secret, TestContext.Current.CancellationToken);
        files.Repository.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<Attachment, bool>>>()))
            .ReturnsAsync([new Attachment { StoragePath = "good" }, new Attachment { StoragePath = "bad" }, new Attachment { StoragePath = "missing" }]);
        files.Storage.Setup(s => s.GetFullPath("good")).Returns(source);
        files.Storage.Setup(s => s.GetFullPath("bad")).Throws(new InvalidOperationException(Secret));
        files.Storage.Setup(s => s.GetFullPath("missing")).Returns(source + "-missing");
        Assert.Equal(2, await files.Job.CleanupDeletedFilesAsync()); // 原计数包含缺失文件对应的已处理记录。
        Assert.False(File.Exists(source));
        Assert.Single(Lines(capture.Output));
        Assert.Contains("job.cleanup.failed", capture.Output.ToString());
        Assert.DoesNotContain(Secret, capture.Output.ToString());
        if (candidate)
        {
            using var value = JsonDocument.Parse(capture.Output.ToString());
            var properties = value.RootElement.GetProperty("properties");
            Assert.Equal("partial", properties.GetProperty("outcome").GetString());
            Assert.Equal(1, properties.GetProperty("movedCount").GetInt32());
            Assert.Equal(1, properties.GetProperty("missingCount").GetInt32());
            Assert.Equal(1, properties.GetProperty("failedCount").GetInt32());
            Assert.Equal(2, properties.GetProperty("processedCount").GetInt32());
        }
    }

    [Fact]
    public async Task FileCleanup_ShouldKeepEmptyRunsQuiet_AndRecordConsumedFailureOnce()
    {
        using var capture = new Capture(true);
        using var files = new Files();
        files.Repository.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<Attachment, bool>>>())).ReturnsAsync([]);
        Assert.Equal(0, await files.Job.CleanupDeletedFilesAsync());
        Assert.Equal(0, await files.Job.CleanupOrphanAttachmentsAsync());
        Assert.Equal(0, await files.Job.CleanupTempFilesAsync());
        Assert.Equal(0, await files.Job.CleanupRecycleBinAsync());
        Assert.Equal("", capture.Output.ToString());
        files.Repository.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<Attachment, bool>>>())).ThrowsAsync(new IOException(Secret));
        Assert.Equal(0, await files.Job.CleanupDeletedFilesAsync());
        Assert.Single(Lines(capture.Output));
        Assert.Contains("job.cleanup.failed", capture.Output.ToString());
        Assert.DoesNotContain(Secret, capture.Output.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InboxCleanup_ShouldSummarizeRelationOnlyChangesAndCapacity(bool candidate)
    {
        using var capture = new Capture(candidate);
        using var factory = LoggerFactory.Create(builder => builder.AddSerilog(capture.Logger));
        var repository = new Mock<INotificationInboxRepository>();
        var result = new NotificationInboxCleanupResult(3, 0, 0,
            [new NotificationInboxCapacityWarning(1, 2, 9000), new NotificationInboxCapacityWarning(1, 3, 8000)]);
        repository.Setup(r => r.CleanupAsync(It.IsAny<DateTime>(), 200, 5000)).ReturnsAsync(result);
        var job = new NotificationInboxCleanupJob(repository.Object, TimeProvider.System, factory.CreateLogger<NotificationInboxCleanupJob>());
        Assert.Same(result, await job.ExecuteAsync());
        Assert.Equal(2, Lines(capture.Output).Length);
        Assert.Contains("job.cleanup.completed", capture.Output.ToString());
        Assert.Contains("job.cleanup.capacity_warning", capture.Output.ToString());
        Assert.DoesNotContain("TenantId", capture.Output.ToString());
        Assert.DoesNotContain("UserId", capture.Output.ToString());
    }

    [Fact]
    public async Task RepositoryCleanup_ShouldKeepRetentionAndFailurePropagation()
    {
        using var capture = new Capture(true);
        using var factory = LoggerFactory.Create(builder => builder.AddSerilog(capture.Logger));
        var clock = new FixedTimeProvider();
        var wiki = new Mock<IWikiDocumentRepository>();
        wiki.Setup(r => r.PurgeTerminalDraftPayloadsAsync(clock.GetUtcNow().UtcDateTime.AddDays(-1), 200, clock.GetUtcNow().UtcDateTime)).ReturnsAsync(4);
        var job = new WikiDraftPayloadCleanupJob(wiki.Object, clock, factory.CreateLogger<WikiDraftPayloadCleanupJob>());
        Assert.Equal(4, await job.ExecuteAsync(0));
        wiki.VerifyAll();
        Assert.Single(Lines(capture.Output));
        capture.Output.GetStringBuilder().Clear();
        var failure = new IOException(Secret);
        var reactions = new Mock<IChatMessageReactionRepository>();
        reactions.Setup(r => r.DeleteExpiredOperationsAsync(clock.GetUtcNow().UtcDateTime, 500)).ThrowsAsync(failure);
        var reactionJob = new ChatMessageReactionOperationCleanupJob(reactions.Object, clock, factory.CreateLogger<ChatMessageReactionOperationCleanupJob>());
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => reactionJob.ExecuteAsync()));
        Assert.Equal("", capture.Output.ToString()); // 仍由 Hangfire 最终重试边界处理。
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FileCleanup_ShouldDistinguishMissingFilesFromActualMoves(bool missing)
    {
        using var capture = new Capture(true);
        using var files = new Files();
        var source = Path.Combine(files.Root, Secret);
        if (!missing) await File.WriteAllTextAsync(source, Secret, TestContext.Current.CancellationToken);
        files.Repository.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<Attachment, bool>>>()))
            .ReturnsAsync([new Attachment { StoragePath = Secret }]);
        files.Storage.Setup(s => s.GetFullPath(Secret)).Returns(source);
        Assert.Equal(1, await files.Job.CleanupDeletedFilesAsync());
        using var value = JsonDocument.Parse(capture.Output.ToString());
        Assert.Equal(missing ? "job.cleanup.warning" : "job.cleanup.completed", value.RootElement.GetProperty("eventCode").GetString());
        var properties = value.RootElement.GetProperty("properties");
        Assert.Equal(missing ? 0 : 1, properties.GetProperty("movedCount").GetInt32());
        Assert.Equal(missing ? 1 : 0, properties.GetProperty("missingCount").GetInt32());
        Assert.Equal(1, properties.GetProperty("processedCount").GetInt32());
        Assert.DoesNotContain(Secret, capture.Output.ToString());
    }

    [Fact]
    public async Task RepositoryCleanup_ShouldKeepEmptyRunsQuiet()
    {
        using var capture = new Capture(true);
        using var factory = LoggerFactory.Create(builder => builder.AddSerilog(capture.Logger));
        var wiki = new WikiDraftPayloadCleanupJob(Mock.Of<IWikiDocumentRepository>(), TimeProvider.System,
            factory.CreateLogger<WikiDraftPayloadCleanupJob>());
        var reactions = new ChatMessageReactionOperationCleanupJob(Mock.Of<IChatMessageReactionRepository>(), TimeProvider.System,
            factory.CreateLogger<ChatMessageReactionOperationCleanupJob>());
        var repository = new Mock<INotificationInboxRepository>();
        repository.Setup(r => r.CleanupAsync(It.IsAny<DateTime>(), 200, 5000))
            .ReturnsAsync(new NotificationInboxCleanupResult(0, 0, 0, []));
        var inbox = new NotificationInboxCleanupJob(repository.Object, TimeProvider.System,
            factory.CreateLogger<NotificationInboxCleanupJob>());
        Assert.Equal(0, await wiki.ExecuteAsync());
        Assert.Equal(0, await reactions.ExecuteAsync());
        await inbox.ExecuteAsync();
        Assert.Equal("", capture.Output.ToString());
    }

    private static class JobTarget
    {
        public static void NoOp() { }
    }
    private static string[] Lines(StringWriter output) => output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 23, 0, 0, 0, TimeSpan.Zero);
    }

    private sealed class Files : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"radish-cleanup-log-{Guid.NewGuid():N}");
        public Mock<IBaseRepository<Attachment>> Repository { get; } = new();
        public Mock<IFileStorage> Storage { get; } = new();
        public FileCleanupJob Job { get; }
        public Files()
        {
            var clock = new FixedTimeProvider();
            Job = new FileCleanupJob(Repository.Object, Mock.Of<IAttachmentReferenceInspector>(), Storage.Object,
                clock, new BusinessCalendar(clock, Options.Create(new TimeOptions { DefaultTimeZoneId = "Asia/Shanghai" })),
                Options.Create(new ChunkedUploadOptions { TempChunkPath = Path.Combine(Root, "Temp", "Chunks") }), Root);
        }
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private sealed class Capture : IDisposable
    {
        private readonly Serilog.ILogger _previous = Log.Logger;
        public StringWriter Output { get; } = new();
        public Serilog.Core.Logger Logger { get; }
        public Capture(bool candidate)
        {
            var configuration = new LoggerConfiguration();
            if (candidate) RuntimeLoggingConfiguration.Configure(configuration, new ConfigurationBuilder().Build(), "Production", "api", Output, Output);
            else configuration.Enrich.FromLogContext().WriteTo.Sink(new LegacySink(Output));
            Logger = configuration.CreateLogger();
            Log.Logger = Logger;
        }
        public void Dispose() { Log.Logger = _previous; Logger.Dispose(); Output.Dispose(); }
    }

    private sealed class LegacySink(TextWriter output) : Serilog.Core.ILogEventSink
    {
        private readonly Serilog.Formatting.Display.MessageTemplateTextFormatter _formatter = new("{Level} {Message:lj} {Properties:j} {Exception}{NewLine}");
        public void Emit(Serilog.Events.LogEvent value) => _formatter.Format(value, output);
    }
}
