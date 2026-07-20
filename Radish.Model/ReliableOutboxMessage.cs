using Radish.Model.Root;
using Radish.Model.Tenants;
using SqlSugar;

namespace Radish.Model;

public static class ReliableOutboxSources
{
    public const string Main = "Main";
    public const string Chat = "Chat";
}

public static class ReliableOutboxStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Succeeded = "Succeeded";
    public const string DeadLetter = "DeadLetter";
}

public abstract class ReliableOutboxMessageBase : RootEntityTKey<long>, ITenantEntity
{
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; }

    [SugarColumn(Length = 80, IsNullable = false)]
    public string TaskType { get; set; } = string.Empty;

    [SugarColumn(IsNullable = false)]
    public int SchemaVersion { get; set; } = 1;

    [SugarColumn(Length = 220, IsNullable = false)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [SugarColumn(Length = 80, IsNullable = false)]
    public string AggregateType { get; set; } = string.Empty;

    [SugarColumn(Length = 120, IsNullable = false)]
    public string AggregateId { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string PayloadJson { get; set; } = "{}";

    [SugarColumn(Length = 20, IsNullable = false)]
    public string Status { get; set; } = ReliableOutboxStatuses.Pending;

    [SugarColumn(IsNullable = false)]
    public int AttemptCount { get; set; }

    [SugarColumn(IsNullable = false)]
    public int MaxAttempts { get; set; } = 6;

    [SugarColumn(IsNullable = false)]
    public DateTime OccurredAtUtc { get; set; }

    [SugarColumn(IsNullable = false)]
    public DateTime AvailableAtUtc { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? LockedAtUtc { get; set; }

    [SugarColumn(Length = 120, IsNullable = true)]
    public string? LockedBy { get; set; }

    [SugarColumn(Length = 80, IsNullable = true)]
    public string? LastErrorCode { get; set; }

    [SugarColumn(Length = 1000, IsNullable = true)]
    public string? LastErrorSummary { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? ProcessedAtUtc { get; set; }

    [SugarColumn(IsNullable = false)]
    public int ReplayCount { get; set; }

    [SugarColumn(Length = 120, IsNullable = true)]
    public string? LastReplayBy { get; set; }

    [SugarColumn(Length = 500, IsNullable = true)]
    public string? LastReplayReason { get; set; }

    [SugarColumn(IsNullable = false)]
    public DateTime CreateTime { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? ModifyTime { get; set; }
}

[SugarTable("ReliableOutboxMessage")]
[SugarIndex("idx_outbox_main_tenant_key", nameof(TenantId), OrderByType.Asc, nameof(IdempotencyKey), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_outbox_main_dispatch", nameof(Status), OrderByType.Asc, nameof(AvailableAtUtc), OrderByType.Asc)]
public sealed class ReliableOutboxMessage : ReliableOutboxMessageBase
{
}

[Tenant(configId: "Chat")]
[SugarTable("ReliableOutboxMessage")]
[SugarIndex("idx_outbox_chat_tenant_key", nameof(TenantId), OrderByType.Asc, nameof(IdempotencyKey), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_outbox_chat_dispatch", nameof(Status), OrderByType.Asc, nameof(AvailableAtUtc), OrderByType.Asc)]
public sealed class ChatReliableOutboxMessage : ReliableOutboxMessageBase
{
}

public sealed record ReliableOutboxDraft(
    string SourceDatabase,
    long TenantId,
    string TaskType,
    int SchemaVersion,
    string IdempotencyKey,
    string AggregateType,
    string AggregateId,
    string PayloadJson,
    DateTime OccurredAtUtc,
    int MaxAttempts = 6);

public sealed record ReliableOutboxSnapshot(
    string SourceDatabase,
    long Id,
    long TenantId,
    string TaskType,
    int SchemaVersion,
    string IdempotencyKey,
    string AggregateType,
    string AggregateId,
    string PayloadJson,
    string Status,
    int AttemptCount,
    int MaxAttempts,
    DateTime OccurredAtUtc,
    DateTime AvailableAtUtc,
    string? LastErrorCode,
    string? LastErrorSummary);
