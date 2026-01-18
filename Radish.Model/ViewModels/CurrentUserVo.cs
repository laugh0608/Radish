namespace Radish.Model.ViewModels;

/// <summary>
/// 当前用户信息ViewModel
/// </summary>
public class CurrentUserVo
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 租户ID
    /// </summary>
    public long TenantId { get; set; }

    /// <summary>
    /// 头像URL
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// 头像缩略图URL
    /// </summary>
    public string? AvatarThumbnailUrl { get; set; }
}