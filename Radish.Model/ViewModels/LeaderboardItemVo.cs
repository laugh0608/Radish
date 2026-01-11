namespace Radish.Model.ViewModels;

/// <summary>
/// 等级排行榜项视图模型
/// </summary>
public class LeaderboardItemVo
{
    /// <summary>
    /// 排名
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// 用户 ID
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 头像 URL
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// 当前等级
    /// </summary>
    public int CurrentLevel { get; set; }

    /// <summary>
    /// 当前等级昵称
    /// </summary>
    public string CurrentLevelName { get; set; } = string.Empty;

    /// <summary>
    /// 累计总经验值
    /// </summary>
    public long TotalExp { get; set; }

    /// <summary>
    /// 主题色
    /// </summary>
    /// <remarks>十六进制颜色值，如 #FFC107</remarks>
    public string? ThemeColor { get; set; }

    /// <summary>
    /// 等级徽章 URL
    /// </summary>
    public string? BadgeUrl { get; set; }

    /// <summary>
    /// 是否是当前用户
    /// </summary>
    /// <remarks>用于前端高亮显示当前用户</remarks>
    public bool IsCurrentUser { get; set; }
}
