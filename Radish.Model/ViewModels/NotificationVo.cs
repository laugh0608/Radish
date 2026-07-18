namespace Radish.Model.ViewModels;

/// <summary>
/// 通知视图模型
/// </summary>
/// <remarks>
/// 用于返回通知基本信息（不包含用户特定的已读状态）
/// 主要用于通知模板渲染和推送
/// </remarks>
public class NotificationVo
{
    /// <summary>
    /// 通知 Id
    /// </summary>
    public long VoId { get; set; }

    /// <summary>
    /// 通知类型
    /// </summary>
    /// <example>CommentReplied, PostLiked, CommentLiked, Mentioned, GodComment, Sofa</example>
    public string VoType { get; set; } = string.Empty;

    /// <summary>
    /// 通知优先级
    /// </summary>
    /// <remarks>1-低, 2-普通, 3-高, 4-紧急</remarks>
    public int VoPriority { get; set; }

    /// <summary>
    /// 通知标题
    /// </summary>
    public string VoTitle { get; set; } = string.Empty;

    /// <summary>
    /// 通知内容
    /// </summary>
    public string VoContent { get; set; } = string.Empty;

    /// <summary>
    /// 业务类型
    /// </summary>
    /// <example>Post, Comment, User, System</example>
    public string? VoBusinessType { get; set; }

    /// <summary>
    /// 业务 ID
    /// </summary>
    public long? VoBusinessId { get; set; }

    /// <summary>
    /// 触发者 ID
    /// </summary>
    public long? VoTriggerId { get; set; }

    /// <summary>
    /// 触发者名称
    /// </summary>
    public string? VoTriggerName { get; set; }

    /// <summary>
    /// 触发者头像
    /// </summary>
    public string? VoTriggerAvatar { get; set; }

    /// <summary>
    /// 扩展数据
    /// </summary>
    public string? VoExtData { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime VoCreateTime { get; set; }
}

/// <summary>
/// 用户通知视图模型
/// </summary>
/// <remarks>
/// 用于返回包含用户已读状态的通知信息
/// 主要用于通知列表和通知中心展示
/// </remarks>
public class UserNotificationVo
{
    /// <summary>
    /// 用户通知关系 Id
    /// </summary>
    public long VoId { get; set; }

    /// <summary>
    /// 用户 Id
    /// </summary>
    public long VoUserId { get; set; }

    /// <summary>
    /// 通知 Id
    /// </summary>
    public long VoNotificationId { get; set; }

    /// <summary>
    /// 是否已读
    /// </summary>
    public bool VoIsRead { get; set; }

    /// <summary>
    /// 已读时间
    /// </summary>
    public DateTime? VoReadAt { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime VoCreateTime { get; set; }

    /// <summary>
    /// 通知详情
    /// </summary>
    /// <remarks>关联的通知对象</remarks>
    public NotificationVo? VoNotification { get; set; }
}
