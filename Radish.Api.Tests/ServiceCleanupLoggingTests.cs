using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Common.CacheTool;
using Radish.Common.OptionTool;
using Radish.Common.TimeTool;
using Radish.Extension.Log;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.Models;
using Radish.Model.ViewModels;
using Radish.Service;
using Serilog;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class ServiceCleanupLoggingTests
{
    private const string Secret = "SERVICE_CLEANUP_PRIVATE_SENTINEL";
    private static readonly DateTime Now = new(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Tokens_ShouldCountOnlyActualRevocations(bool candidate)
    {
        using var capture = new Capture(candidate);
        var repository = Tokens();
        Expression<Func<FileAccessToken, bool>>? predicate = null;
        repository.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<FileAccessToken, bool>>>()))
            .Callback<Expression<Func<FileAccessToken, bool>>>(value => predicate = value)
            .ReturnsAsync([new() { Id = 1, TokenHash = Secret }, new() { Id = 2, TokenHash = Secret }]);
        repository.Setup(r => r.TryRevokeByIdAsync(1, Now)).ReturnsAsync(true);
        repository.Setup(r => r.TryRevokeByIdAsync(2, Now)).ReturnsAsync(false);
        await TokenService(repository).CleanupExpiredTokensAsync();
        Assert.True(predicate!.Compile()(new() { ExpiresAt = Now }));
        Assert.False(predicate.Compile()(new() { ExpiresAt = Now.AddTicks(1) }));
        Assert.False(predicate.Compile()(new() { ExpiresAt = Now, IsRevoked = true }));
        AssertSafeSingle(capture, "job.cleanup.completed");
        if (candidate) AssertCounts(capture, updated: 1, processed: 2, skipped: 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Tokens_ShouldKeepConcurrentSkipsAndEmptyRunsQuiet(bool empty)
    {
        using var capture = new Capture(true);
        var repository = Tokens();
        repository.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<FileAccessToken, bool>>>()))
            .ReturnsAsync(empty ? [] : [new() { Id = 1 }]);
        await TokenService(repository).CleanupExpiredTokensAsync();
        Assert.Equal("", capture.Output.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Tokens_ShouldReportEarlierProgressWithoutConsumingOrDuplicatingFailure(bool candidate)
    {
        using var capture = new Capture(candidate);
        var repository = Tokens();
        repository.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<FileAccessToken, bool>>>()))
            .ReturnsAsync([new() { Id = 1 }, new() { Id = 2 }, new() { Id = 3 }]);
        repository.Setup(r => r.TryRevokeByIdAsync(1, Now)).ReturnsAsync(true);
        var failure = new IOException(Secret);
        repository.Setup(r => r.TryRevokeByIdAsync(2, Now)).ThrowsAsync(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => TokenService(repository).CleanupExpiredTokensAsync()));
        repository.Verify(r => r.TryRevokeByIdAsync(3, It.IsAny<DateTime>()), Times.Never);
        AssertSafeSingle(capture, "job.cleanup.interrupted");
        Assert.DoesNotContain("Error", capture.Output.ToString());
        if (candidate) AssertCounts(capture, updated: 1, processed: 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Uploads_ShouldAggregateConsumedFailuresAndPreserveSettlementDeduplication(bool candidate)
    {
        using var capture = new Capture(candidate);
        using var files = new Uploads();
        var good = Session();
        var invalid = Session(); invalid.SessionId = Secret;
        var skipped = Session();
        var failed = Session();
        var completed = Session("Completed"); completed.AttachmentId = 123;
        files.CreateDirectory(good);
        files.Repository.Setup(r => r.QueryExpiredAcrossTenantsAsync(Now)).ReturnsAsync([good, invalid, skipped, failed]);
        files.Repository.Setup(r => r.TryMarkExpiredAcrossTenantsAsync(It.IsAny<string>(), 3, 7, Now, Now)).ReturnsAsync(true);
        files.Repository.Setup(r => r.TryMarkExpiredAcrossTenantsAsync(skipped.SessionId, 3, 7, Now, Now)).ReturnsAsync(false);
        files.Repository.Setup(r => r.TryMarkExpiredAcrossTenantsAsync(failed.SessionId, 3, 7, Now, Now)).ThrowsAsync(new IOException(Secret));
        files.Quota.Setup(q => q.FailUploadAsync(7, invalid.SessionId)).ThrowsAsync(new IOException(Secret));
        files.Repository.Setup(r => r.QueryTerminalForSettlementAcrossTenantsAsync(Now.AddDays(-8), 2000))
            .ReturnsAsync([good, invalid, completed]);
        await files.Service.CleanupExpiredSessionsAsync();
        files.Quota.Verify(q => q.FailUploadAsync(7, good.SessionId), Times.Once);
        files.Quota.Verify(q => q.FailUploadAsync(7, invalid.SessionId), Times.Once);
        files.Quota.Verify(q => q.CompleteUploadAsync(7, completed.SessionId), Times.Once);
        Assert.False(Directory.Exists(Path.Combine(files.Root, good.SessionId)));
        AssertSafeSingle(capture, "job.cleanup.failed");
        if (candidate) AssertCounts(capture, updated: 2, processed: 3, skipped: 1, removed: 1, settlements: 2, failed: 3);
    }

    [Fact]
    public async Task Uploads_ShouldPreserveOrphanGracePeriodAndTerminalDirectoryRules()
    {
        using var capture = new Capture(true);
        using var files = new Uploads();
        var old = Session(); var recent = Session(); var active = Session(); var terminal = Session("Failed");
        foreach (var session in new[] { old, recent, active, terminal }) files.CreateDirectory(session);
        Directory.SetLastWriteTimeUtc(Path.Combine(files.Root, old.SessionId), Now.AddMinutes(-30));
        Directory.SetLastWriteTimeUtc(Path.Combine(files.Root, recent.SessionId), Now.AddMinutes(-29));
        Directory.CreateDirectory(Path.Combine(files.Root, Secret));
        files.Repository.Setup(r => r.QueryBySessionIdsAcrossTenantsAsync(It.IsAny<List<string>>())).ReturnsAsync([active, terminal]);
        await files.Service.CleanupExpiredSessionsAsync();
        Assert.False(Directory.Exists(Path.Combine(files.Root, old.SessionId)));
        Assert.False(Directory.Exists(Path.Combine(files.Root, terminal.SessionId)));
        Assert.True(Directory.Exists(Path.Combine(files.Root, recent.SessionId)));
        Assert.True(Directory.Exists(Path.Combine(files.Root, active.SessionId)));
        Assert.True(Directory.Exists(Path.Combine(files.Root, Secret)));
        AssertSafeSingle(capture, "job.cleanup.completed");
        AssertCounts(capture, removed: 2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Uploads_ShouldKeepEmptyAndSettlementOnlyReplaysQuiet(bool replay)
    {
        using var capture = new Capture(true);
        using var files = new Uploads();
        var completed = Session("Completed"); completed.AttachmentId = 123;
        if (replay) files.Repository.Setup(r => r.QueryTerminalForSettlementAcrossTenantsAsync(Now.AddDays(-8), 2000)).ReturnsAsync([completed]);
        await files.Service.CleanupExpiredSessionsAsync();
        files.Quota.Verify(q => q.CompleteUploadAsync(7, completed.SessionId), replay ? Times.Once() : Times.Never());
        Assert.Equal("", capture.Output.ToString());
    }

    [Fact]
    public async Task Uploads_ShouldPropagateLateQueryFailureWithoutClaimingCompletion()
    {
        using var capture = new Capture(true);
        using var files = new Uploads();
        var expired = Session(); files.CreateDirectory(expired); files.CreateDirectory(Session());
        files.Repository.Setup(r => r.QueryExpiredAcrossTenantsAsync(Now)).ReturnsAsync([expired]);
        files.Repository.Setup(r => r.TryMarkExpiredAcrossTenantsAsync(expired.SessionId, 3, 7, Now, Now)).ReturnsAsync(true);
        var failure = new IOException(Secret);
        files.Repository.Setup(r => r.QueryBySessionIdsAcrossTenantsAsync(It.IsAny<List<string>>())).ThrowsAsync(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => files.Service.CleanupExpiredSessionsAsync()));
        files.Repository.Verify(r => r.QueryTerminalForSettlementAcrossTenantsAsync(It.IsAny<DateTime>(), It.IsAny<int>()), Times.Never);
        AssertSafeSingle(capture, "job.cleanup.interrupted");
        Assert.DoesNotContain("Error", capture.Output.ToString());
        AssertCounts(capture, updated: 1, processed: 1, removed: 1, settlements: 1);
    }

    [Fact]
    public async Task Cleanup_ShouldPropagateInitialQueryFailuresWithoutLoggingThemAgain()
    {
        using var capture = new Capture(true);
        using var files = new Uploads();
        var failure = new IOException(Secret);
        files.Repository.Setup(r => r.QueryExpiredAcrossTenantsAsync(Now)).ThrowsAsync(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => files.Service.CleanupExpiredSessionsAsync()));
        var tokens = Tokens();
        tokens.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<FileAccessToken, bool>>>())).ThrowsAsync(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => TokenService(tokens).CleanupExpiredTokensAsync()));
        Assert.Equal("", capture.Output.ToString());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ForegroundReplay_ShouldConsumeQuotaFailuresWithSafeEvents(bool candidate, bool completed)
    {
        using var capture = new Capture(candidate);
        using var files = new Uploads();
        var session = Session(completed ? "Completed" : "Cancelled"); session.AttachmentId = 123;
        files.CreateDirectory(session);
        files.Repository.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UploadSession, bool>>>())).ReturnsAsync(session);
        var attachment = new AttachmentVo { VoId = 123 };
        files.Attachments.Setup(a => a.QueryByIdAsync(123)).ReturnsAsync(attachment);
        files.Quota.Setup(q => q.CompleteUploadAsync(7, session.SessionId)).ThrowsAsync(new IOException(Secret));
        files.Quota.Setup(q => q.FailUploadAsync(7, session.SessionId)).ThrowsAsync(new IOException(Secret));
        if (completed) Assert.Same(attachment, await files.Service.MergeChunksAsync(new MergeChunksDto { SessionId = session.SessionId }, 7, Secret));
        else await files.Service.CancelSessionAsync(session.SessionId, 7);
        Assert.False(Directory.Exists(Path.Combine(files.Root, session.SessionId)));
        AssertSafeSingle(capture, "upload.cleanup.failed");
        Assert.DoesNotContain(session.SessionId, capture.Output.ToString());
        Assert.Contains(completed ? "quota-complete" : "quota-release", capture.Output.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealQuotaSettlement_ShouldRemainQuietAndIdempotent(bool candidate)
    {
        using var capture = new Capture(candidate);
        var clock = new FixedTimeProvider();
        var cache = new Caching(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
        var quota = new UploadRateLimitService(cache,
            Options.Create(new UploadRateLimitOptions { Enable = true, MaxConcurrentUploads = 3, MaxUploadsPerMinute = 5, MaxDailyUploadSize = 10000 }),
            Options.Create(new RedisOptions { Enable = false }), clock,
            new BusinessCalendar(clock, Options.Create(new TimeOptions { DefaultTimeZoneId = "Asia/Shanghai" })), []);
        Assert.True((await quota.AcquireUploadAsync(7, Secret + "-complete", 100)).IsAllowed);
        Assert.True((await quota.AcquireUploadAsync(7, Secret + "-release", 200)).IsAllowed);
        await quota.CompleteUploadAsync(7, Secret + "-complete");
        await quota.CompleteUploadAsync(7, Secret + "-complete");
        await quota.FailUploadAsync(7, Secret + "-release");
        await quota.FailUploadAsync(7, Secret + "-release");
        var stats = await quota.GetUploadStatisticsAsync(7);
        Assert.Equal(100, stats.UploadedSizeToday);
        Assert.Equal(0, stats.ReservedUploadSizeToday);
        Assert.Equal(0, stats.CurrentConcurrentUploads);
        Assert.Equal(2, stats.UploadsThisMinute);
        Assert.Equal("", capture.Output.ToString());
    }

    private static Mock<IFileAccessTokenRepository> Tokens() => new();
    private static FileAccessTokenService TokenService(Mock<IFileAccessTokenRepository> repository) =>
        new(repository.Object, Mock.Of<IBaseRepository<Attachment>>(), Mock.Of<IWikiAttachmentAccessService>(), new FixedTimeProvider());
    private static UploadSession Session(string status = "Uploading") => new()
    {
        SessionId = Guid.NewGuid().ToString("N"), TenantId = 3, UserId = 7, Status = status,
        FileName = Secret, ExpiresAt = Now.AddDays(-1)
    };
    private static void AssertSafeSingle(Capture capture, string eventCode)
    {
        var output = capture.Output.ToString();
        Assert.Single(output.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains(eventCode, output);
        Assert.DoesNotContain(Secret, output);
        Assert.DoesNotContain("SessionId", output);
        Assert.DoesNotContain("TokenHash", output);
    }
    private static void AssertCounts(Capture capture, int updated = 0, int processed = 0, int skipped = 0, int removed = 0, int settlements = 0, int failed = 0)
    {
        using var json = JsonDocument.Parse(capture.Output.ToString());
        var properties = json.RootElement.GetProperty("properties");
        Assert.Equal(updated, properties.GetProperty("updatedCount").GetInt32());
        Assert.Equal(processed, properties.GetProperty("processedCount").GetInt32());
        Assert.Equal(skipped, properties.GetProperty("skippedCount").GetInt32());
        Assert.Equal(removed, properties.GetProperty("removedDirectoryCount").GetInt32());
        Assert.Equal(settlements, properties.GetProperty("settlementCount").GetInt32());
        Assert.Equal(failed, properties.GetProperty("failedCount").GetInt32());
    }
    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
    private sealed class Uploads : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"radish-service-cleanup-{Guid.NewGuid():N}");
        public Mock<IUploadSessionRepository> Repository { get; } = new();
        public Mock<IUploadRateLimitService> Quota { get; } = new();
        public Mock<IAttachmentService> Attachments { get; } = new();
        public ChunkedUploadService Service { get; }
        public Uploads()
        {
            Repository.Setup(r => r.QueryExpiredAcrossTenantsAsync(Now)).ReturnsAsync([]);
            Repository.Setup(r => r.QueryBySessionIdsAcrossTenantsAsync(It.IsAny<List<string>>())).ReturnsAsync([]);
            Repository.Setup(r => r.QueryTerminalForSettlementAcrossTenantsAsync(Now.AddDays(-8), 2000)).ReturnsAsync([]);
            Service = new ChunkedUploadService(Repository.Object, Attachments.Object, Quota.Object,
                Options.Create(new ChunkedUploadOptions { TempChunkPath = Root }), Options.Create(new FileStorageOptions()), new FixedTimeProvider());
        }
        public void CreateDirectory(UploadSession session) => Directory.CreateDirectory(Path.Combine(Root, session.SessionId));
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); }
    }
    private sealed class Capture : IDisposable
    {
        private readonly Serilog.ILogger _previous = Log.Logger;
        public StringWriter Output { get; } = new();
        private readonly Serilog.Core.Logger _logger;
        public Capture(bool candidate)
        {
            var config = new LoggerConfiguration();
            if (candidate) RuntimeLoggingConfiguration.Configure(config, new ConfigurationBuilder().Build(), "Production", "api", Output, Output);
            else config.Enrich.FromLogContext().WriteTo.Sink(new LegacySink(Output));
            _logger = config.CreateLogger(); Log.Logger = _logger;
        }
        public void Dispose() { Log.Logger = _previous; _logger.Dispose(); Output.Dispose(); }
    }
    private sealed class LegacySink(TextWriter output) : Serilog.Core.ILogEventSink
    {
        private readonly Serilog.Formatting.Display.MessageTemplateTextFormatter _formatter = new("{Level} {Message:lj} {Properties:j} {Exception}{NewLine}");
        public void Emit(Serilog.Events.LogEvent value) => _formatter.Format(value, output);
    }
}
