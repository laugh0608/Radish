namespace Radish.Model.ViewModels;

/// <summary>
/// 创建通知 DTO
/// </summary>
/// <remarks>
/// 用于通知生成服务创建新通知
/// </remarks>
public class CreateNotificationDto
{
    /// <summary>
    /// 通知类型（必填）
    /// </summary>
    /// <example>CommentReplied, PostLiked, CommentLiked, Mentioned</example>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 通知标题（必填）
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 通知内容（可选）
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// 通知优先级（可选）
    /// </summary>
    /// <remarks>1-低, 2-普通, 3-高, 4-紧急，不填则根据类型自动判断</remarks>
    public int? Priority { get; set; }

    /// <summary>
    /// 业务类型（可选）
    /// </summary>
    /// <example>Post, Comment, User, System</example>
    public string? BusinessType { get; set; }

    /// <summary>
    /// 业务 ID（可选）
    /// </summary>
    public long? BusinessId { get; set; }

    /// <summary>
    /// 触发者 ID（可选）
    /// </summary>
    public long? TriggerId { get; set; }

    /// <summary>
    /// 触发者名称（可选）
    /// </summary>
    public string? TriggerName { get; set; }

    /// <summary>
    /// 触发者头像（可选）
    /// </summary>
    public string? TriggerAvatar { get; set; }

    /// <summary>
    /// 接收者用户 ID 列表（必填）
    /// </summary>
    /// <remarks>通知将发送给这些用户</remarks>
    public List<long> ReceiverUserIds { get; set; } = new();

    /// <summary>
    /// 扩展数据（可选）
    /// </summary>
    /// <remarks>JSON 格式</remarks>
    public string? ExtData { get; set; }

    /// <summary>
    /// 租户 ID（可选）
    /// </summary>
    /// <remarks>不填则使用当前用户的租户 ID</remarks>
    public long? TenantId { get; set; }
}

/// <summary>
/// 通知列表查询 DTO
/// </summary>
/// <remarks>
/// 用于分页查询用户的通知列表
/// </remarks>
public class NotificationListQueryDto
{
    /// <summary>
    /// 页码（从 1 开始）
    /// </summary>
    public int PageIndex { get; set; } = 1;

    /// <summary>
    /// 每页数量
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// 通知类型筛选（可选）
    /// </summary>
    /// <remarks>不填则查询所有类型</remarks>
    public string? Type { get; set; }

    /// <summary>
    /// 是否只查询未读
    /// </summary>
    public bool? OnlyUnread { get; set; }

    /// <summary>
    /// 优先级筛选（可选）
    /// </summary>
    public int? Priority { get; set; }

    /// <summary>
    /// 开始时间（可选）
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 结束时间（可选）
    /// </summary>
    public DateTime? EndTime { get; set; }
}

/// <summary>
/// 标记已读 DTO
/// </summary>
public class MarkAsReadDto
{
    /// <summary>
    /// 通知 ID 列表
    /// </summary>
    /// <remarks>要标记已读的通知 ID</remarks>
    public List<long> NotificationIds { get; set; } = new();
}

/// <summary>
/// 未读数量响应 DTO
/// </summary>
public class UnreadCountDto
{
    /// <summary>
    /// 用户 ID
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 未读数量
    /// </summary>
    public long UnreadCount { get; set; }

    /// <summary>
    /// 按类型分组的未读数量（可选）
    /// </summary>
    /// <remarks>Key: 通知类型，Value: 未读数量</remarks>
    public Dictionary<string, long>? UnreadCountByType { get; set; }
}

/// <summary>
/// 更新通知设置 DTO
/// </summary>
public class UpdateNotificationSettingDto
{
    /// <summary>
    /// 通知类型
    /// </summary>
    public string NotificationType { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 是否启用站内推送
    /// </summary>
    public bool EnableInApp { get; set; }

    /// <summary>
    /// 是否启用邮件推送
    /// </summary>
    public bool EnableEmail { get; set; }

    /// <summary>
    /// 是否启用声音提示
    /// </summary>
    public bool EnableSound { get; set; }
}

/// <summary>
/// 批量更新通知设置 DTO
/// </summary>
public class BatchUpdateNotificationSettingDto
{
    /// <summary>
    /// 设置列表
    /// </summary>
    public List<UpdateNotificationSettingDto> Settings { get; set; } = new();
}
