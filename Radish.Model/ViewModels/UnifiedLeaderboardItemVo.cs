using Radish.Shared.CustomEnum;

namespace Radish.Model.ViewModels;

/// <summary>
/// 统一排行榜项视图模型
/// </summary>
/// <remarks>
/// 支持用户类和商品类排行榜的统一数据结构
/// 前端根据 VoCategory 决定渲染方式
/// </remarks>
public class UnifiedLeaderboardItemVo
{
    #region 通用字段

    /// <summary>
    /// 排行榜类型
    /// </summary>
    public LeaderboardType VoLeaderboardType { get; set; }

    /// <summary>
    /// 排行榜分类（用户类/商品类）
    /// </summary>
    public LeaderboardCategory VoCategory { get; set; }

    /// <summary>
    /// 排名
    /// </summary>
    public int VoRank { get; set; }

    #endregion

    #region 用户信息（用户类排行榜）

    /// <summary>
    /// 用户 ID
    /// </summary>
    public long? VoUserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string? VoUserName { get; set; }

    /// <summary>
    /// 头像 URL
    /// </summary>
    public string? VoAvatarUrl { get; set; }

    /// <summary>
    /// 当前等级
    /// </summary>
    public int? VoCurrentLevel { get; set; }

    /// <summary>
    /// 当前等级昵称
    /// </summary>
    public string? VoCurrentLevelName { get; set; }

    /// <summary>
    /// 主题色
    /// </summary>
    /// <remarks>十六进制颜色值，如 #FFC107</remarks>
    public string? VoThemeColor { get; set; }

    /// <summary>
    /// 是否是当前用户
    /// </summary>
    /// <remarks>用于前端高亮显示当前用户</remarks>
    public bool VoIsCurrentUser { get; set; }

    #endregion

    #region 商品信息（商品类排行榜）

    /// <summary>
    /// 商品 ID
    /// </summary>
    public long? VoProductId { get; set; }

    /// <summary>
    /// 商品名称
    /// </summary>
    public string? VoProductName { get; set; }

    /// <summary>
    /// 商品图标 URL
    /// </summary>
    public string? VoProductIcon { get; set; }

    /// <summary>
    /// 商品价格
    /// </summary>
    public long? VoProductPrice { get; set; }

    #endregion

    #region 统计数值

    /// <summary>
    /// 主要数值
    /// </summary>
    /// <remarks>
    /// 根据排行榜类型不同含义不同：
    /// - 等级排行：总经验值
    /// - 余额排行：当前余额
    /// - 花销排行：累计消费
    /// - 购买数量：购买商品总数
    /// - 热门商品：销量
    /// - 发帖达人：帖子数
    /// - 评论达人：评论数
    /// - 人气排行：总点赞数
    /// </remarks>
    public long VoPrimaryValue { get; set; }

    /// <summary>
    /// 主要数值标签
    /// </summary>
    /// <remarks>如"总经验值"、"萝卜余额"、"销量"等</remarks>
    public string VoPrimaryLabel { get; set; } = string.Empty;

    /// <summary>
    /// 次要数值（可选）
    /// </summary>
    /// <remarks>
    /// 用于展示额外信息：
    /// - 等级排行：当前等级
    /// - 余额排行：累计获得
    /// - 热门商品：价格
    /// </remarks>
    public long? VoSecondaryValue { get; set; }

    /// <summary>
    /// 次要数值标签
    /// </summary>
    public string? VoSecondaryLabel { get; set; }

    #endregion
}
