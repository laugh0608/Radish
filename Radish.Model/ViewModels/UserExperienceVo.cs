namespace Radish.Model.ViewModels;

/// <summary>
/// 用户经验值视图模型
/// </summary>
public class UserExperienceVo
{
    /// <summary>
    /// 用户 ID
    /// </summary>
    public long VoUserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    /// <remarks>用于前端显示，避免前端再次查询</remarks>
    public string? VoUserName { get; set; }

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
    /// <remarks>如：凡人、练气、筑基、金丹等</remarks>
    public string VoCurrentLevelName { get; set; } = string.Empty;

    /// <summary>
    /// 当前经验值
    /// </summary>
    /// <remarks>当前等级内的经验值进度</remarks>
    public long VoCurrentExp { get; set; }

    /// <summary>
    /// 累计总经验值
    /// </summary>
    public long VoTotalExp { get; set; }

    /// <summary>
    /// 距离下一级还需多少经验
    /// </summary>
    public long VoExpToNextLevel { get; set; }

    /// <summary>
    /// 下一级等级
    /// </summary>
    public int VoNextLevel { get; set; }

    /// <summary>
    /// 下一级等级昵称
    /// </summary>
    public string VoNextLevelName { get; set; } = string.Empty;

    /// <summary>
    /// 当前等级进度
    /// </summary>
    /// <remarks>0-1 之间的小数，用于进度条显示</remarks>
    public double VoLevelProgress { get; set; }

    /// <summary>
    /// 主题色
    /// </summary>
    /// <remarks>十六进制颜色值，如 #FFC107</remarks>
    public string VoThemeColor { get; set; } = string.Empty;

    /// <summary>
    /// 等级图标 URL
    /// </summary>
    public string? VoIconUrl { get; set; }

    /// <summary>
    /// 等级徽章 URL
    /// </summary>
    public string? VoBadgeUrl { get; set; }

    /// <summary>
    /// 最近升级时间
    /// </summary>
    public DateTime? VoLevelUpAt { get; set; }

    /// <summary>
    /// 经验值排名
    /// </summary>
    /// <remarks>可选，可能需要单独查询</remarks>
    public int? VoRank { get; set; }

    /// <summary>
    /// 经验值是否冻结
    /// </summary>
    public bool VoExpFrozen { get; set; }

    /// <summary>
    /// 冻结到期时间
    /// </summary>
    public DateTime? VoFrozenUntil { get; set; }

    /// <summary>
    /// 冻结原因
    /// </summary>
    public string? VoFrozenReason { get; set; }
}
