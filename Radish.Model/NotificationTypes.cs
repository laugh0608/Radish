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

    /// <summary>帖子收到顶级评论</summary>
    public const string PostCommented = "PostCommented";

    /// <summary>帖子被点赞</summary>
    /// <remarks>优先级：低（1）</remarks>
    public const string PostLiked = "PostLiked";

    /// <summary>评论被点赞</summary>
    /// <remarks>优先级：低（1）</remarks>
    public const string CommentLiked = "CommentLiked";

    /// <summary>被 @ 提及</summary>
    /// <remarks>优先级：高（3）</remarks>
    public const string Mentioned = "Mentioned";

    /// <summary>在聊天频道中被提及</summary>
    public const string ChatMentioned = "ChatMentioned";

    /// <summary>新增粉丝</summary>
    /// <remarks>优先级：普通（2）</remarks>
    public const string Followed = "Followed";

    /// <summary>帖子收到轻回应</summary>
    /// <remarks>优先级：普通（2）</remarks>
    public const string PostQuickReplied = "PostQuickReplied";

    /// <summary>收到陌生人私信请求</summary>
    /// <remarks>优先级：普通（2）</remarks>
    public const string DirectMessageRequested = "DirectMessageRequested";

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

    /// <summary>抽奖中奖</summary>
    /// <remarks>优先级：高（3）</remarks>
    public const string LotteryWon = "LotteryWon";

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

    /// <summary>订单购买成功</summary>
    public const string PurchaseSucceeded = "PurchaseSucceeded";

    /// <summary>用户权益到期</summary>
    public const string BenefitExpired = "BenefitExpired";

    #endregion
}

/// <summary>通知中心稳定分类。</summary>
public static class NotificationCategory
{
    public const string Discussion = "Discussion";
    public const string Reaction = "Reaction";
    public const string Message = "Message";
    public const string Relationship = "Relationship";
    public const string Commerce = "Commerce";
    public const string Growth = "Growth";
    public const string Governance = "Governance";
    public const string Knowledge = "Knowledge";
    public const string System = "System";

    public static IReadOnlyList<string> All { get; } =
    [
        Discussion,
        Reaction,
        Message,
        Relationship,
        Commerce,
        Growth,
        Governance,
        Knowledge,
        System
    ];
}

/// <summary>通知可导航目标类型。</summary>
public static class NotificationTargetKind
{
    public const string None = "None";
    public const string ForumPost = "ForumPost";
    public const string ChatConversation = "ChatConversation";
    public const string UserProfile = "UserProfile";
    public const string ShopOrder = "ShopOrder";
    public const string Inventory = "Inventory";
    public const string Experience = "Experience";
    public const string DocsDocument = "DocsDocument";
    public const string GovernanceCase = "GovernanceCase";
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

    /// <summary>Wiki 文档</summary>
    public const string Wiki = "Wiki";
}
