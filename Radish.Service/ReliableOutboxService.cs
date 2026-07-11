using System.Text.Json;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;

namespace Radish.Service;

public sealed class ReliableOutboxService : IReliableOutboxService
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(12)
    ];

    private readonly IReliableOutboxRepository _repository;

    public ReliableOutboxService(IReliableOutboxRepository repository)
    {
        _repository = repository;
    }

    public Task<long> AddAsync<TPayload>(
        string sourceDatabase,
        long tenantId,
        string taskType,
        string idempotencyKey,
        string aggregateType,
        string aggregateId,
        TPayload payload,
        DateTime occurredAtUtc,
        int schemaVersion = 1)
    {
        var draft = new ReliableOutboxDraft(
            sourceDatabase,
            tenantId,
            taskType,
            schemaVersion,
            idempotencyKey,
            aggregateType,
            aggregateId,
            JsonSerializer.Serialize(payload),
            occurredAtUtc);
        return _repository.AddAsync(draft);
    }

    public Task<IReadOnlyList<ReliableOutboxSnapshot>> ClaimDueAsync(
        string sourceDatabase,
        int batchSize,
        string workerId,
        DateTime nowUtc,
        TimeSpan leaseDuration)
    {
        return _repository.ClaimDueAsync(sourceDatabase, batchSize, workerId, nowUtc, leaseDuration);
    }

    public Task<ReliableOutboxSnapshot?> QueryByIdAsync(string sourceDatabase, long outboxId)
    {
        return _repository.QueryByIdAsync(sourceDatabase, outboxId);
    }

    public Task<IReadOnlyList<ReliableOutboxSnapshot>> QueryDeadLettersAsync(string sourceDatabase, int take)
    {
        return _repository.QueryDeadLettersAsync(sourceDatabase, take);
    }

    public Task MarkSucceededAsync(string sourceDatabase, long outboxId, DateTime completedAtUtc)
    {
        return _repository.MarkSucceededAsync(sourceDatabase, outboxId, completedAtUtc);
    }

    public async Task MarkFailedAsync(string sourceDatabase, long outboxId, Exception exception, DateTime failedAtUtc)
    {
        var message = await _repository.QueryByIdAsync(sourceDatabase, outboxId);
        if (message == null)
        {
            return;
        }

        var permanent = exception is PermanentReliableTaskException;
        DateTime? retryAtUtc = null;
        if (!permanent && message.AttemptCount < RetryDelays.Length)
        {
            var jitter = TimeSpan.FromSeconds(Math.Abs(outboxId % 31));
            retryAtUtc = failedAtUtc.Add(RetryDelays[message.AttemptCount]).Add(jitter);
        }

        await _repository.MarkFailedAsync(
            sourceDatabase,
            outboxId,
            permanent ? "PermanentFailure" : exception.GetType().Name,
            permanent ? exception.Message : "任务执行失败，完整异常请查看服务端日志",
            failedAtUtc,
            retryAtUtc);
    }

    public Task<bool> ReplayAsync(string sourceDatabase, long outboxId, string operatorName, string reason, DateTime nowUtc)
    {
        return _repository.ReplayAsync(sourceDatabase, outboxId, operatorName, reason, nowUtc);
    }
}
