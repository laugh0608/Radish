namespace Radish.Model;

/// <summary>通知类型常量定义</summary>
/// <remarks>
/// 定义系统中所有通知类型的常量字符串
/// 用于通知生成和模板匹配
/// </remarks>
public static class NotificationType
{
    #region 互动类通知（P1 优先实现）

    /// <summary>评论被回复</summary>
    /// <remarks>优先级：普通（2）</remarks>
    public const string CommentReplied = "CommentReplied";

    /// <summary>帖子被点赞</summary>
    /// <remarks>优先级：低（1）</remarks>
    public const string PostLiked = "PostLiked";

    /// <summary>评论被点赞</summary>
    /// <remarks>优先级：低（1）</remarks>
    public const string CommentLiked = "CommentLiked";

    /// <summary>被 @ 提及</summary>
    /// <remarks>优先级：高（3）</remarks>
    public const string Mentioned = "Mentioned";

    #endregion

    #region 成就类通知（P2 后续实现）

    /// <summary>成为神评</summary>
    /// <remarks>优先级：高（3）</remarks>
    public const string GodComment = "GodComment";

    /// <summary>成为沙发</summary>
    /// <remarks>优先级：高（3）</remarks>
    public const string Sofa = "Sofa";

    /// <summary>等级提升</summary>
    /// <remarks>优先级：高（3）</remarks>
    public const string LevelUp = "LevelUp";

    #endregion

    #region 积分类通知（P2 后续实现）

    /// <summary>萝卜币余额变动</summary>
    /// <remarks>优先级：普通（2）</remarks>
    public const string CoinBalanceChanged = "CoinBalanceChanged";

    #endregion

    #region 系统类通知

    /// <summary>系统公告</summary>
    /// <remarks>优先级：紧急（4）</remarks>
    public const string SystemAnnouncement = "SystemAnnouncement";

    /// <summary>账号安全</summary>
    /// <remarks>优先级：紧急（4）</remarks>
    public const string AccountSecurity = "AccountSecurity";

    #endregion
}

/// <summary>通知优先级枚举</summary>
/// <remarks>
/// 定义通知的优先级级别
/// 优先级影响通知的展示顺序和推送策略
/// </remarks>
public enum NotificationPriority
{
    /// <summary>低优先级</summary>
    /// <remarks>适用于点赞等非紧急通知</remarks>
    Low = 1,

    /// <summary>普通优先级</summary>
    /// <remarks>适用于评论回复等常规互动通知</remarks>
    Normal = 2,

    /// <summary>高优先级</summary>
    /// <remarks>适用于 @提及、神评/沙发、等级提升等重要通知</remarks>
    High = 3,

    /// <summary>紧急优先级</summary>
    /// <remarks>适用于系统公告、账号安全等紧急通知</remarks>
    Critical = 4
}

/// <summary>通知推送状态常量定义</summary>
/// <remarks>
/// 定义通知推送的各种状态
/// 用于推送任务和重试逻辑
/// </remarks>
public static class DeliveryStatus
{
    /// <summary>已创建，等待推送</summary>
    public const string Created = "Created";

    /// <summary>已送达</summary>
    public const string Delivered = "Delivered";

    /// <summary>推送失败</summary>
    public const string Failed = "Failed";

    /// <summary>已放弃（重试次数超限）</summary>
    public const string Abandoned = "Abandoned";
}

/// <summary>业务类型常量定义</summary>
/// <remarks>
/// 定义通知关联的业务类型
/// 用于通知跳转和业务关联
/// </remarks>
public static class BusinessType
{
    /// <summary>帖子</summary>
    public const string Post = "Post";

    /// <summary>评论</summary>
    public const string Comment = "Comment";

    /// <summary>用户</summary>
    public const string User = "User";

    /// <summary>系统</summary>
    public const string System = "System";
}
