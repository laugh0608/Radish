using Hangfire;
using System.Diagnostics;
using Radish.IRepository;

namespace Radish.Api.Services;

/// <summary>按固定保留期小批次清理 Chat 消息回应幂等事实。</summary>
public sealed class ChatMessageReactionOperationCleanupJob
{
    private readonly IChatMessageReactionRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ChatMessageReactionOperationCleanupJob> _logger;

    public ChatMessageReactionOperationCleanupJob(
        IChatMessageReactionRepository repository,
        TimeProvider timeProvider,
        ILogger<ChatMessageReactionOperationCleanupJob> logger)
    {
        _repository = repository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [60, 300], LogEvents = false)]
    public async Task<int> ExecuteAsync(int batchSize = 500)
    {
        var started = Stopwatch.GetTimestamp();
        var deletedCount = await _repository.DeleteExpiredOperationsAsync(
            _timeProvider.GetUtcNow().UtcDateTime,
            batchSize);
        if (deletedCount > 0)
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["EventCode"] = "job.cleanup.completed", ["SourceCategory"] = "job", ["jobKind"] = "chat-reactions"
            });
            _logger.LogInformation("Cleanup completed; count={count}; duration={durationMs} ms",
                deletedCount, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }

        return deletedCount;
    }
}
