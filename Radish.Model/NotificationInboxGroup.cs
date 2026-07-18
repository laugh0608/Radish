using Radish.Model.Root;
using Radish.Model.Tenants;
using SqlSugar;

namespace Radish.Model;

/// <summary>用户通知收件箱中的权威分组。</summary>
[Tenant(configId: "Message")]
[SugarTable("NotificationInboxGroup")]
[SugarIndex("idx_notification_inbox_group_key", nameof(TenantId), OrderByType.Asc, nameof(UserId), OrderByType.Asc, nameof(GroupKey), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_notification_inbox_group_page", nameof(TenantId), OrderByType.Asc, nameof(UserId), OrderByType.Asc, nameof(IsDeleted), OrderByType.Asc, nameof(LastOccurredAtUtc), OrderByType.Desc, nameof(Id), OrderByType.Desc)]
[SugarIndex("idx_notification_inbox_group_category", nameof(TenantId), OrderByType.Asc, nameof(UserId), OrderByType.Asc, nameof(Category), OrderByType.Asc, nameof(IsDeleted), OrderByType.Asc, nameof(LastOccurredAtUtc), OrderByType.Desc, nameof(Id), OrderByType.Desc)]
public sealed class NotificationInboxGroup : RootEntityTKey<long>, ITenantEntity
{
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; }

    [SugarColumn(IsNullable = false)]
    public long UserId { get; set; }

    [SugarColumn(Length = 96, IsNullable = false)]
    public string GroupKey { get; set; } = string.Empty;

    [SugarColumn(Length = 32, IsNullable = false)]
    public string Category { get; set; } = string.Empty;

    [SugarColumn(Length = 50, IsNullable = false)]
    public string Kind { get; set; } = string.Empty;

    [SugarColumn(IsNullable = false)]
    public long LatestNotificationId { get; set; }

    [SugarColumn(IsNullable = false)]
    public long OccurrenceCount { get; set; }

    [SugarColumn(IsNullable = false)]
    public long UnreadOccurrenceCount { get; set; }

    [SugarColumn(IsNullable = false)]
    public long DistinctTriggerCount { get; set; }

    [SugarColumn(IsNullable = false)]
    public DateTime FirstOccurredAtUtc { get; set; }

    [SugarColumn(IsNullable = false)]
    public DateTime LastOccurredAtUtc { get; set; }

    [SugarColumn(IsNullable = false)]
    public bool IsDeleted { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? DeletedAtUtc { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? ReadAtUtc { get; set; }

    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    public DateTime CreateTime { get; set; }

    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    [SugarColumn(IsNullable = false)]
    public long CreateId { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? ModifyTime { get; set; }

    [SugarColumn(Length = 50, IsNullable = true)]
    public string? ModifyBy { get; set; }

    [SugarColumn(IsNullable = true)]
    public long? ModifyId { get; set; }
}
