using Radish.Model;

namespace Radish.IRepository;

public interface IReliableOutboxRepository
{
    Task<long> AddAsync(ReliableOutboxDraft draft);
    Task<IReadOnlyList<ReliableOutboxSnapshot>> ClaimDueAsync(
        string sourceDatabase,
        int batchSize,
        string workerId,
        DateTime nowUtc,
        TimeSpan leaseDuration);
    Task<ReliableOutboxSnapshot?> QueryByIdAsync(string sourceDatabase, long outboxId);
    Task<IReadOnlyList<ReliableOutboxSnapshot>> QueryDeadLettersAsync(string sourceDatabase, int take);
    Task MarkSucceededAsync(string sourceDatabase, long outboxId, DateTime completedAtUtc);
    Task MarkFailedAsync(
        string sourceDatabase,
        long outboxId,
        string errorCode,
        string errorSummary,
        DateTime failedAtUtc,
        DateTime? retryAtUtc);
    Task<bool> ReplayAsync(
        string sourceDatabase,
        long outboxId,
        string operatorName,
        string reason,
        DateTime nowUtc);
}
