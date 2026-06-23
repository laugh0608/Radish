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
    /// 展示名（兼容旧前端字段）
    /// </summary>
    public string VoUserName { get; set; } = string.Empty;

    /// <summary>
    /// 展示名
    /// </summary>
    public string VoDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 展示名与公开索引组合后的展示句柄
    /// </summary>
    public string? VoDisplayHandle { get; set; }

    /// <summary>
    /// 用户公开访问标识
    /// </summary>
    public string? VoPublicId { get; set; }

    /// <summary>
    /// 用户公开索引号
    /// </summary>
    public long? VoPublicIndex { get; set; }

    /// <summary>
    /// 昵称
    /// </summary>
    public string VoNickname { get; set; } = string.Empty;

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

    /// <summary>
    /// 当前用户角色列表
    /// </summary>
    public List<string> VoRoles { get; set; } = new();

    /// <summary>
    /// 当前用户权限列表
    /// </summary>
    public List<string> VoPermissions { get; set; } = new();
}
