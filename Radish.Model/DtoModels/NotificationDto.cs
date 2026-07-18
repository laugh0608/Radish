using Radish.Model;

namespace Radish.Model.DtoModels;

/// <summary>
/// 创建通知 DTO
/// </summary>
/// <remarks>
/// 用于通知生成服务创建新通知
/// </remarks>
public class CreateNotificationDto
{
    /// <summary>可靠任务预先确定的通知 ID；普通请求留空。</summary>
    public long? NotificationId { get; set; }

    /// <summary>通知业务幂等键；可靠任务必须填写。</summary>
    public string? BusinessKey { get; set; }

    /// <summary>
    /// 通知类型（必填）
    /// </summary>
    /// <example>CommentReplied, PostLiked, CommentLiked, Mentioned</example>
    public string Type { get; set; } = string.Empty;

    /// <summary>模板结构化参数；写入时序列化，不接受任意导航 URL。</summary>
    public Dictionary<string, string?> TemplateArguments { get; set; } = new(StringComparer.Ordinal);

    /// <summary>结构化业务目标。</summary>
    public NotificationTargetData? Target { get; set; }

    /// <summary>结构化业务目标类型。</summary>
    public string? TargetKind { get; set; }

    /// <summary>源事件发生 UTC 时间；可靠任务必须固定并随重放复用。</summary>
    public DateTime? OccurredAtUtc { get; set; }

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

/// <summary>通知收件箱分组查询。</summary>
public sealed class NotificationInboxQueryDto
{
    public string? Category { get; set; }
    public bool OnlyUnread { get; set; }
    public string? Cursor { get; set; }
    public int PageSize { get; set; } = 20;
}

/// <summary>批量标记收件箱分组已读。</summary>
public sealed class MarkInboxGroupsAsReadDto
{
    public List<long> GroupIds { get; set; } = [];
}

/// <summary>全部或按分类标记已读。</summary>
public sealed class MarkAllInboxAsReadDto
{
    public string? Category { get; set; }
}

/// <summary>单项通知分类偏好。</summary>
public sealed class UpdateNotificationPreferenceDto
{
    public string Category { get; set; } = string.Empty;
    public bool InAppEnabled { get; set; }
    public bool RealtimePreviewEnabled { get; set; }
}

/// <summary>批量更新通知分类偏好。</summary>
public sealed class UpdateNotificationPreferencesDto
{
    public List<UpdateNotificationPreferenceDto> Preferences { get; set; } = [];
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
