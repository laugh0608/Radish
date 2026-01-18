namespace Radish.Model.ViewModels;

/// <summary>
/// 等级配置视图模型
/// </summary>
public class LevelConfigVo
{
    /// <summary>
    /// 等级
    /// </summary>
    public int VoLevel { get; set; }

    /// <summary>
    /// 等级昵称
    /// </summary>
    /// <remarks>凡人、练气、筑基、金丹、元婴、化神、炼虚、合体、大乘、渡劫、飞升</remarks>
    public string VoLevelName { get; set; } = string.Empty;

    /// <summary>
    /// 升到下一级所需经验值
    /// </summary>
    public long VoExpRequired { get; set; }

    /// <summary>
    /// 累计经验值
    /// </summary>
    /// <remarks>达到此等级需要的总经验值</remarks>
    public long VoExpCumulative { get; set; }

    /// <summary>
    /// 主题色
    /// </summary>
    /// <remarks>十六进制颜色值，如 #FFC107</remarks>
    public string? VoThemeColor { get; set; }

    /// <summary>
    /// 等级图标 URL
    /// </summary>
    public string? VoIconUrl { get; set; }

    /// <summary>
    /// 等级徽章 URL
    /// </summary>
    public string? VoBadgeUrl { get; set; }

    /// <summary>
    /// 等级描述
    /// </summary>
    public string? VoDescription { get; set; }

    /// <summary>
    /// 特权列表
    /// </summary>
    /// <remarks>已解析的 JSON 数组</remarks>
    public List<string>? VoPrivileges { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool VoIsEnabled { get; set; }

    /// <summary>
    /// 排序
    /// </summary>
    public int VoSortOrder { get; set; }
}
