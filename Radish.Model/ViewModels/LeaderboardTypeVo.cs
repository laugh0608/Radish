using Radish.Shared.CustomEnum;

namespace Radish.Model.ViewModels;

/// <summary>
/// 排行榜类型视图模型
/// </summary>
/// <remarks>
/// 用于前端获取所有可用的排行榜类型及其描述
/// </remarks>
public class LeaderboardTypeVo
{
    /// <summary>
    /// 排行榜类型
    /// </summary>
    public LeaderboardType VoType { get; set; }

    /// <summary>
    /// 排行榜分类（用户类/商品类）
    /// </summary>
    public LeaderboardCategory VoCategory { get; set; }

    /// <summary>
    /// 排行榜名称
    /// </summary>
    public string VoName { get; set; } = string.Empty;

    /// <summary>
    /// 排行榜描述
    /// </summary>
    public string VoDescription { get; set; } = string.Empty;

    /// <summary>
    /// 图标名称
    /// </summary>
    /// <remarks>使用 Iconify 图标名称，如 mdi:trophy</remarks>
    public string VoIcon { get; set; } = string.Empty;

    /// <summary>
    /// 主要数值标签
    /// </summary>
    /// <remarks>如"总经验值"、"萝卜余额"、"销量"等</remarks>
    public string VoPrimaryLabel { get; set; } = string.Empty;

    /// <summary>
    /// 排序顺序
    /// </summary>
    /// <remarks>用于前端 Tab 排序</remarks>
    public int VoSortOrder { get; set; }
}
