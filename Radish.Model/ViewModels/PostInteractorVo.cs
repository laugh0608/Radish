namespace Radish.Model.ViewModels;

/// <summary>
/// 帖子最新互动用户视图模型
/// </summary>
public class PostInteractorVo
{
    /// <summary>
    /// 用户 Id
    /// </summary>
    public long VoUserId { get; set; }

    /// <summary>
    /// 用户名称
    /// </summary>
    public string VoUserName { get; set; } = string.Empty;

    /// <summary>
    /// 用户头像 URL
    /// </summary>
    public string? VoAvatarUrl { get; set; }
}
