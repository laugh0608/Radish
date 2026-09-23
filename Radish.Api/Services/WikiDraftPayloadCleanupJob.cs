using Hangfire;
using System.Diagnostics;
using Radish.IRepository;

namespace Radish.Api.Services;

/// <summary>按保留期清空终态 Wiki 草稿正文，保留审核元数据与正式 Revision。</summary>
public sealed class WikiDraftPayloadCleanupJob
{
    private readonly IWikiDocumentRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WikiDraftPayloadCleanupJob> _logger;

    public WikiDraftPayloadCleanupJob(
        IWikiDocumentRepository repository,
        TimeProvider timeProvider,
        ILogger<WikiDraftPayloadCleanupJob> logger)
    {
        _repository = repository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [60, 300], LogEvents = false)]
    public async Task<int> ExecuteAsync(int retentionDays = 90, int batchSize = 200)
    {
        var started = Stopwatch.GetTimestamp();
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var effectiveRetentionDays = Math.Max(1, retentionDays);
        var purgedCount = await _repository.PurgeTerminalDraftPayloadsAsync(
            nowUtc.AddDays(-effectiveRetentionDays),
            batchSize,
            nowUtc);
        if (purgedCount > 0)
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["EventCode"] = "job.cleanup.completed", ["SourceCategory"] = "job", ["jobKind"] = "wiki-drafts"
            });
            _logger.LogInformation("Cleanup completed; count={count}; duration={durationMs} ms",
                purgedCount, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }

        return purgedCount;
    }
}
