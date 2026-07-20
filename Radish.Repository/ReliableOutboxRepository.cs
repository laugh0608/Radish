using Radish.Common;
using Radish.IRepository;
using Radish.Model;
using SqlSugar;

namespace Radish.Repository;

public sealed class ReliableOutboxRepository : IReliableOutboxRepository
{
    private readonly SqlSugarScope _dbScope;

    public ReliableOutboxRepository(ISqlSugarClient sqlSugarClient)
    {
        _dbScope = (SqlSugarScope)sqlSugarClient;
    }

    public async Task<long> AddAsync(ReliableOutboxDraft draft)
    {
        ValidateDraft(draft);
        var nowUtc = draft.OccurredAtUtc.Kind == DateTimeKind.Utc
            ? draft.OccurredAtUtc
            : draft.OccurredAtUtc.ToUniversalTime();

        if (IsChat(draft.SourceDatabase))
        {
            var entity = CreateEntity<ChatReliableOutboxMessage>(draft, nowUtc);
            return await GetConnection(ReliableOutboxSources.Chat)
                .Insertable(entity)
                .ExecuteReturnSnowflakeIdAsync();
        }

        var mainEntity = CreateEntity<ReliableOutboxMessage>(draft, nowUtc);
        return await GetConnection(ReliableOutboxSources.Main)
            .Insertable(mainEntity)
            .ExecuteReturnSnowflakeIdAsync();
    }

    public async Task<IReadOnlyList<ReliableOutboxSnapshot>> ClaimDueAsync(
        string sourceDatabase,
        int batchSize,
        string workerId,
        DateTime nowUtc,
        TimeSpan leaseDuration)
    {
        var safeBatchSize = Math.Clamp(batchSize, 1, 200);
        var leaseExpiredAtUtc = nowUtc.Subtract(leaseDuration);

        return IsChat(sourceDatabase)
            ? await ClaimDueCoreAsync<ChatReliableOutboxMessage>(ReliableOutboxSources.Chat, safeBatchSize, workerId, nowUtc, leaseExpiredAtUtc)
            : await ClaimDueCoreAsync<ReliableOutboxMessage>(ReliableOutboxSources.Main, safeBatchSize, workerId, nowUtc, leaseExpiredAtUtc);
    }

    public async Task<ReliableOutboxSnapshot?> QueryByIdAsync(string sourceDatabase, long outboxId)
    {
        return IsChat(sourceDatabase)
            ? ToSnapshot(ReliableOutboxSources.Chat, await GetConnection(ReliableOutboxSources.Chat).Queryable<ChatReliableOutboxMessage>().InSingleAsync(outboxId))
            : ToSnapshot(ReliableOutboxSources.Main, await GetConnection(ReliableOutboxSources.Main).Queryable<ReliableOutboxMessage>().InSingleAsync(outboxId));
    }

    public async Task<IReadOnlyList<ReliableOutboxSnapshot>> QueryDeadLettersAsync(string sourceDatabase, int take)
    {
        var safeTake = Math.Clamp(take, 1, 200);
        return IsChat(sourceDatabase)
            ? (await GetConnection(ReliableOutboxSources.Chat).Queryable<ChatReliableOutboxMessage>()
                .Where(message => message.Status == ReliableOutboxStatuses.DeadLetter)
                .OrderByDescending(message => message.ModifyTime)
                .Take(safeTake)
                .ToListAsync())
                .Select(message => ToSnapshot(ReliableOutboxSources.Chat, message)!)
                .ToList()
            : (await GetConnection(ReliableOutboxSources.Main).Queryable<ReliableOutboxMessage>()
                .Where(message => message.Status == ReliableOutboxStatuses.DeadLetter)
                .OrderByDescending(message => message.ModifyTime)
                .Take(safeTake)
                .ToListAsync())
                .Select(message => ToSnapshot(ReliableOutboxSources.Main, message)!)
                .ToList();
    }

    public async Task MarkSucceededAsync(string sourceDatabase, long outboxId, DateTime completedAtUtc)
    {
        if (IsChat(sourceDatabase))
        {
            await MarkSucceededCoreAsync<ChatReliableOutboxMessage>(ReliableOutboxSources.Chat, outboxId, completedAtUtc);
            return;
        }

        await MarkSucceededCoreAsync<ReliableOutboxMessage>(ReliableOutboxSources.Main, outboxId, completedAtUtc);
    }

    public async Task MarkFailedAsync(
        string sourceDatabase,
        long outboxId,
        string errorCode,
        string errorSummary,
        DateTime failedAtUtc,
        DateTime? retryAtUtc)
    {
        if (IsChat(sourceDatabase))
        {
            await MarkFailedCoreAsync<ChatReliableOutboxMessage>(ReliableOutboxSources.Chat, outboxId, errorCode, errorSummary, failedAtUtc, retryAtUtc);
            return;
        }

        await MarkFailedCoreAsync<ReliableOutboxMessage>(ReliableOutboxSources.Main, outboxId, errorCode, errorSummary, failedAtUtc, retryAtUtc);
    }

    public async Task<bool> ReplayAsync(
        string sourceDatabase,
        long outboxId,
        string operatorName,
        string reason,
        DateTime nowUtc)
    {
        return IsChat(sourceDatabase)
            ? await ReplayCoreAsync<ChatReliableOutboxMessage>(ReliableOutboxSources.Chat, outboxId, operatorName, reason, nowUtc)
            : await ReplayCoreAsync<ReliableOutboxMessage>(ReliableOutboxSources.Main, outboxId, operatorName, reason, nowUtc);
    }

    private async Task<IReadOnlyList<ReliableOutboxSnapshot>> ClaimDueCoreAsync<TEntity>(
        string sourceDatabase,
        int batchSize,
        string workerId,
        DateTime nowUtc,
        DateTime leaseExpiredAtUtc)
        where TEntity : ReliableOutboxMessageBase, new()
    {
        var db = GetConnection(sourceDatabase);
        var candidates = await db.Queryable<TEntity>()
            .Where(message =>
                (message.Status == ReliableOutboxStatuses.Pending && message.AvailableAtUtc <= nowUtc) ||
                (message.Status == ReliableOutboxStatuses.Processing && message.LockedAtUtc < leaseExpiredAtUtc))
            .OrderBy(message => message.AvailableAtUtc)
            .Take(batchSize)
            .ToListAsync();

        var claimed = new List<ReliableOutboxSnapshot>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var affectedRows = await db.Updateable<TEntity>()
                .SetColumns(message => new TEntity
                {
                    Status = ReliableOutboxStatuses.Processing,
                    LockedAtUtc = nowUtc,
                    LockedBy = workerId,
                    ModifyTime = nowUtc
                })
                .Where(message => message.Id == candidate.Id &&
                    ((message.Status == ReliableOutboxStatuses.Pending && message.AvailableAtUtc <= nowUtc) ||
                     (message.Status == ReliableOutboxStatuses.Processing && message.LockedAtUtc < leaseExpiredAtUtc)))
                .ExecuteCommandAsync();

            if (affectedRows > 0)
            {
                candidate.Status = ReliableOutboxStatuses.Processing;
                claimed.Add(ToSnapshot(sourceDatabase, candidate)!);
            }
        }

        return claimed;
    }

    private async Task MarkSucceededCoreAsync<TEntity>(string sourceDatabase, long outboxId, DateTime completedAtUtc)
        where TEntity : ReliableOutboxMessageBase, new()
    {
        await GetConnection(sourceDatabase).Updateable<TEntity>()
            .SetColumns(message => new TEntity
            {
                Status = ReliableOutboxStatuses.Succeeded,
                ProcessedAtUtc = completedAtUtc,
                LockedAtUtc = null,
                LockedBy = null,
                LastErrorCode = null,
                LastErrorSummary = null,
                ModifyTime = completedAtUtc
            })
            .Where(message => message.Id == outboxId && message.Status == ReliableOutboxStatuses.Processing)
            .ExecuteCommandAsync();
    }

    private async Task MarkFailedCoreAsync<TEntity>(
        string sourceDatabase,
        long outboxId,
        string errorCode,
        string errorSummary,
        DateTime failedAtUtc,
        DateTime? retryAtUtc)
        where TEntity : ReliableOutboxMessageBase, new()
    {
        var current = await GetConnection(sourceDatabase).Queryable<TEntity>().InSingleAsync(outboxId);
        if (current == null || current.Status != ReliableOutboxStatuses.Processing)
        {
            return;
        }

        var attemptCount = current.AttemptCount + 1;
        var canRetry = retryAtUtc.HasValue && attemptCount < current.MaxAttempts;
        await GetConnection(sourceDatabase).Updateable<TEntity>()
            .SetColumns(message => new TEntity
            {
                Status = canRetry ? ReliableOutboxStatuses.Pending : ReliableOutboxStatuses.DeadLetter,
                AttemptCount = attemptCount,
                AvailableAtUtc = canRetry ? retryAtUtc!.Value : message.AvailableAtUtc,
                LockedAtUtc = null,
                LockedBy = null,
                LastErrorCode = Normalize(errorCode, 80),
                LastErrorSummary = Normalize(errorSummary, 1000),
                ModifyTime = failedAtUtc
            })
            .Where(message => message.Id == outboxId && message.Status == ReliableOutboxStatuses.Processing)
            .ExecuteCommandAsync();
    }

    private async Task<bool> ReplayCoreAsync<TEntity>(
        string sourceDatabase,
        long outboxId,
        string operatorName,
        string reason,
        DateTime nowUtc)
        where TEntity : ReliableOutboxMessageBase, new()
    {
        var affectedRows = await GetConnection(sourceDatabase).Updateable<TEntity>()
            .SetColumns(message => new TEntity
            {
                Status = ReliableOutboxStatuses.Pending,
                AttemptCount = 0,
                AvailableAtUtc = nowUtc,
                LockedAtUtc = null,
                LockedBy = null,
                LastErrorCode = null,
                LastErrorSummary = null,
                ReplayCount = message.ReplayCount + 1,
                LastReplayBy = Normalize(operatorName, 120),
                LastReplayReason = Normalize(reason, 500),
                ModifyTime = nowUtc
            })
            .Where(message => message.Id == outboxId && message.Status == ReliableOutboxStatuses.DeadLetter)
            .ExecuteCommandAsync();

        return affectedRows > 0;
    }

    private static TEntity CreateEntity<TEntity>(ReliableOutboxDraft draft, DateTime nowUtc)
        where TEntity : ReliableOutboxMessageBase, new()
    {
        return new TEntity
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            TenantId = draft.TenantId,
            TaskType = draft.TaskType.Trim(),
            SchemaVersion = draft.SchemaVersion,
            IdempotencyKey = draft.IdempotencyKey.Trim(),
            AggregateType = draft.AggregateType.Trim(),
            AggregateId = draft.AggregateId.Trim(),
            PayloadJson = draft.PayloadJson,
            Status = ReliableOutboxStatuses.Pending,
            MaxAttempts = Math.Clamp(draft.MaxAttempts, 1, 20),
            OccurredAtUtc = nowUtc,
            AvailableAtUtc = nowUtc,
            CreateTime = nowUtc
        };
    }

    private static ReliableOutboxSnapshot? ToSnapshot(string sourceDatabase, ReliableOutboxMessageBase? entity)
    {
        return entity == null
            ? null
            : new ReliableOutboxSnapshot(
                sourceDatabase,
                entity.Id,
                entity.TenantId,
                entity.TaskType,
                entity.SchemaVersion,
                entity.IdempotencyKey,
                entity.AggregateType,
                entity.AggregateId,
                entity.PayloadJson,
                entity.Status,
                entity.AttemptCount,
                entity.MaxAttempts,
                entity.OccurredAtUtc,
                entity.AvailableAtUtc,
                entity.LastErrorCode,
                entity.LastErrorSummary);
    }

    private ISqlSugarClient GetConnection(string sourceDatabase)
    {
        return _dbScope.GetConnectionScope(sourceDatabase.ToLowerInvariant());
    }

    private static bool IsChat(string sourceDatabase)
    {
        return string.Equals(sourceDatabase, ReliableOutboxSources.Chat, StringComparison.OrdinalIgnoreCase);
    }

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static void ValidateDraft(ReliableOutboxDraft draft)
    {
        if (!string.Equals(draft.SourceDatabase, ReliableOutboxSources.Main, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(draft.SourceDatabase, ReliableOutboxSources.Chat, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Outbox 来源数据库只允许 Main 或 Chat", nameof(draft));
        }

        if (string.IsNullOrWhiteSpace(draft.TaskType) ||
            string.IsNullOrWhiteSpace(draft.IdempotencyKey) ||
            string.IsNullOrWhiteSpace(draft.AggregateType) ||
            string.IsNullOrWhiteSpace(draft.AggregateId) ||
            string.IsNullOrWhiteSpace(draft.PayloadJson))
        {
            throw new ArgumentException("Outbox 任务类型、幂等键、聚合与载荷不能为空", nameof(draft));
        }
    }
}
