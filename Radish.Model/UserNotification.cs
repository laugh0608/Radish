using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using Radish.Model.Tenants;
using SqlSugar;

namespace Radish.Model;

/// <summary>用户与可靠通知事件的关系及已读状态。</summary>
[Tenant(configId: "Message")]
[SugarTable("UserNotification")]
[SugarIndex("idx_user_notification_tenant_user_notification", nameof(TenantId), OrderByType.Asc, nameof(UserId), OrderByType.Asc, nameof(NotificationId), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_user_notification_group_unread", nameof(TenantId), OrderByType.Asc, nameof(UserId), OrderByType.Asc, nameof(InboxGroupId), OrderByType.Asc, nameof(IsRead), OrderByType.Asc, nameof(IsDeleted), OrderByType.Asc)]
public class UserNotification : RootEntityTKey<long>, ITenantEntity
{
    public UserNotification()
    {
    }

    public UserNotification(UserNotificationInitializationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        UserId = options.UserId;
        NotificationId = options.NotificationId;
        InboxGroupId = options.InboxGroupId ?? 0;
        OccurredAtUtc = options.OccurredAtUtc ?? DateTime.UtcNow;
        IsRead = options.IsRead ?? false;
        ReadAt = options.ReadAt;
        IsDeleted = options.IsDeleted ?? false;
        DeletedAt = options.DeletedAt;
        TenantId = options.TenantId ?? 0;
        CreateTime = DateTime.UtcNow;
    }

    [SugarColumn(IsNullable = false, ColumnDescription = "用户ID")]
    public long UserId { get; set; }

    [SugarColumn(IsNullable = false, ColumnDescription = "通知ID")]
    public long NotificationId { get; set; }

    [SugarColumn(IsNullable = false, ColumnDescription = "收件箱分组ID")]
    public long InboxGroupId { get; set; }

    [SugarColumn(IsNullable = false, ColumnDescription = "事件发生UTC时间")]
    public DateTime OccurredAtUtc { get; set; }

    [SugarColumn(IsNullable = false, ColumnDescription = "是否已读")]
    public bool IsRead { get; set; }

    [SugarColumn(IsNullable = true, ColumnDescription = "已读时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ReadAt { get; set; }

    [SugarColumn(IsNullable = false, ColumnDescription = "是否已删除")]
    public bool IsDeleted { get; set; }

    [SugarColumn(IsNullable = true, ColumnDescription = "删除时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? DeletedAt { get; set; }

    [SugarColumn(IsNullable = false, ColumnDescription = "租户ID")]
    public long TenantId { get; set; }

    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; }

    [SugarColumn(Length = 50, IsNullable = false, ColumnDescription = "创建者名称")]
    public string CreateBy { get; set; } = "System";

    [SugarColumn(IsNullable = false, ColumnDescription = "创建者ID")]
    public long CreateId { get; set; }
}

public sealed class UserNotificationInitializationOptions
{
    public UserNotificationInitializationOptions(long userId, long notificationId)
    {
        if (userId <= 0)
        {
            throw new ArgumentException("用户 ID 必须大于 0。", nameof(userId));
        }

        if (notificationId <= 0)
        {
            throw new ArgumentException("通知 ID 必须大于 0。", nameof(notificationId));
        }

        UserId = userId;
        NotificationId = notificationId;
    }

    public long UserId { get; }
    public long NotificationId { get; }
    public long? InboxGroupId { get; set; }
    public DateTime? OccurredAtUtc { get; set; }
    public bool? IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool? IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public long? TenantId { get; set; }
}
