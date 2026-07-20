using Radish.Model.Root;
using Radish.Model.Tenants;
using SqlSugar;

namespace Radish.Model;

/// <summary>用户通知收件箱的权威未读摘要与版本。</summary>
[Tenant(configId: "Message")]
[SugarTable("NotificationInboxState")]
[SugarIndex("idx_notification_inbox_state_user", nameof(TenantId), OrderByType.Asc, nameof(UserId), OrderByType.Asc, IsUnique = true)]
public sealed class NotificationInboxState : RootEntityTKey<long>, ITenantEntity
{
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; }

    [SugarColumn(IsNullable = false)]
    public long UserId { get; set; }

    [SugarColumn(IsNullable = false)]
    public long Revision { get; set; }

    [SugarColumn(IsNullable = false)]
    public long UnreadGroupCount { get; set; }

    [SugarColumn(IsNullable = false)]
    public long UnreadOccurrenceCount { get; set; }

    [SugarColumn(IsNullable = false)]
    public DateTime LastChangedAtUtc { get; set; }

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
