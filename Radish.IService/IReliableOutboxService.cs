using Radish.Model;

namespace Radish.IService;

public interface IReliableOutboxService
{
    Task<long> AddAsync<TPayload>(
        string sourceDatabase,
        long tenantId,
        string taskType,
        string idempotencyKey,
        string aggregateType,
        string aggregateId,
        TPayload payload,
        DateTime occurredAtUtc,
        int schemaVersion = 1);

    Task<IReadOnlyList<ReliableOutboxSnapshot>> ClaimDueAsync(
        string sourceDatabase,
        int batchSize,
        string workerId,
        DateTime nowUtc,
        TimeSpan leaseDuration);

    Task<ReliableOutboxSnapshot?> QueryByIdAsync(string sourceDatabase, long outboxId);
    Task<IReadOnlyList<ReliableOutboxSnapshot>> QueryDeadLettersAsync(string sourceDatabase, int take);
    Task MarkSucceededAsync(string sourceDatabase, long outboxId, DateTime completedAtUtc);
    Task MarkFailedAsync(string sourceDatabase, long outboxId, Exception exception, DateTime failedAtUtc);
    Task<bool> ReplayAsync(string sourceDatabase, long outboxId, string operatorName, string reason, DateTime nowUtc);
}

public interface IReliableTaskProcessor
{
    Task ProcessAsync(ReliableOutboxSnapshot message, CancellationToken cancellationToken = default);
}

public sealed class PermanentReliableTaskException : Exception
{
    public PermanentReliableTaskException(string message) : base(message)
    {
    }
}
