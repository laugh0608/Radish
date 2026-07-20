using Radish.Model.Root;
using Radish.Model.Tenants;
using SqlSugar;

namespace Radish.Model;

/// <summary>用户按通知分类维护的入箱与实时预览偏好。</summary>
[Tenant(configId: "Message")]
[SugarTable("NotificationSetting")]
[SugarIndex("idx_notification_setting_user_category", nameof(TenantId), OrderByType.Asc, nameof(UserId), OrderByType.Asc, nameof(Category), OrderByType.Asc, IsUnique = true)]
public sealed class NotificationSetting : RootEntityTKey<long>, ITenantEntity
{
    [SugarColumn(IsNullable = false)]
    public long UserId { get; set; }

    [SugarColumn(Length = 32, IsNullable = false)]
    public string Category { get; set; } = string.Empty;

    [SugarColumn(IsNullable = false)]
    public bool InAppEnabled { get; set; } = true;

    [SugarColumn(IsNullable = false)]
    public bool RealtimePreviewEnabled { get; set; } = true;

    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; }

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
