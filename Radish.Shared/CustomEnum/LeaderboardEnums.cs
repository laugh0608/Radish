namespace Radish.Shared.CustomEnum;

/// <summary>排行榜类型枚举</summary>
/// <remarks>
/// 定义历史线协议中的排行榜类型数值。
/// 公开可用范围由 <see cref="LeaderboardPublicPolicy"/> 裁决，敏感类型只保留旧值兼容。
/// </remarks>
public enum LeaderboardType
{
    /// <summary>等级排行榜</summary>
    /// <remarks>按用户总经验值排序</remarks>
    Experience = 1,

    /// <summary>历史萝卜余额排行榜（不再公开）</summary>
    /// <remarks>保留旧数值，公开 API 必须拒绝该类型</remarks>
    Balance = 2,

    /// <summary>历史萝卜花销排行榜（不再公开）</summary>
    /// <remarks>保留旧数值，公开 API 必须拒绝该类型</remarks>
    TotalSpent = 3,

    /// <summary>历史购买达人排行榜（不再公开）</summary>
    /// <remarks>保留旧数值，公开 API 必须拒绝该类型</remarks>
    PurchaseCount = 4,

    /// <summary>热门商品排行榜</summary>
    /// <remarks>按商品销量排序</remarks>
    HotProduct = 5,

    /// <summary>发帖达人排行榜</summary>
    /// <remarks>按用户发帖数量排序</remarks>
    PostCount = 6,

    /// <summary>评论达人排行榜</summary>
    /// <remarks>按用户评论数量排序</remarks>
    CommentCount = 7,

    /// <summary>人气排行榜</summary>
    /// <remarks>按用户获得的总点赞数排序（帖子+评论）</remarks>
    Popularity = 8
}

/// <summary>排行榜分类枚举</summary>
/// <remarks>
/// 用于前端区分排行榜项的渲染方式
/// </remarks>
public enum LeaderboardCategory
{
    /// <summary>用户类排行榜</summary>
    /// <remarks>展示用户信息（头像、昵称、等级等）</remarks>
    User = 1,

    /// <summary>商品类排行榜</summary>
    /// <remarks>展示商品信息（图标、名称、价格等）</remarks>
    Product = 2
}
