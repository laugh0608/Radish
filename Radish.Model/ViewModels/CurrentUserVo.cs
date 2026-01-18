namespace Radish.Model.ViewModels;

/// <summary>
/// 当前用户信息ViewModel
/// </summary>
public class CurrentUserVo
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public long VoUserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string VoUserName { get; set; } = string.Empty;

    /// <summary>
    /// 租户ID
    /// </summary>
    public long VoTenantId { get; set; }

    /// <summary>
    /// 头像URL
    /// </summary>
    public string? VoAvatarUrl { get; set; }

    /// <summary>
    /// 头像缩略图URL
    /// </summary>
    public string? VoAvatarThumbnailUrl { get; set; }
}