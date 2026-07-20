using Hangfire;
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

    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [60, 300])]
    public async Task<int> ExecuteAsync(int retentionDays = 90, int batchSize = 200)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var effectiveRetentionDays = Math.Max(1, retentionDays);
        var purgedCount = await _repository.PurgeTerminalDraftPayloadsAsync(
            nowUtc.AddDays(-effectiveRetentionDays),
            batchSize,
            nowUtc);
        if (purgedCount > 0)
        {
            _logger.LogInformation(
                "[WikiDraftPayloadCleanup] 已清理 {PurgedCount} 份终态草稿正文，保留 {RetentionDays} 天",
                purgedCount,
                effectiveRetentionDays);
        }

        return purgedCount;
    }
}
