namespace Radish.Model.ViewModels;

/// <summary>
/// 公开站点设置 VO
/// </summary>
public class PublicSiteSettingsVo
{
    /// <summary>当前生效的站点 favicon 地址</summary>
    public string VoSiteFaviconUrl { get; set; } = string.Empty;

    /// <summary>是否回退到默认 favicon</summary>
    public bool VoUsingDefaultSiteFavicon { get; set; }
}
