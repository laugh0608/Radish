namespace Radish.Model.ViewModels;

/// <summary>
/// 等级排行榜项视图模型
/// </summary>
public class LeaderboardItemVo
{
    /// <summary>
    /// 排名
    /// </summary>
    public int VoRank { get; set; }

    /// <summary>
    /// 用户 ID
    /// </summary>
    public long VoUserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string VoUserName { get; set; } = string.Empty;

    /// <summary>
    /// 头像 URL
    /// </summary>
    public string? VoAvatarUrl { get; set; }

    /// <summary>
    /// 当前等级
    /// </summary>
    public int VoCurrentLevel { get; set; }

    /// <summary>
    /// 当前等级昵称
    /// </summary>
    public string VoCurrentLevelName { get; set; } = string.Empty;

    /// <summary>
    /// 累计总经验值
    /// </summary>
    public long VoTotalExp { get; set; }

    /// <summary>
    /// 主题色
    /// </summary>
    /// <remarks>十六进制颜色值，如 #FFC107</remarks>
    public string? VoThemeColor { get; set; }

    /// <summary>
    /// 等级徽章 URL
    /// </summary>
    public string? VoBadgeUrl { get; set; }

    /// <summary>
    /// 是否是当前用户
    /// </summary>
    /// <remarks>用于前端高亮显示当前用户</remarks>
    public bool VoIsCurrentUser { get; set; }
}
