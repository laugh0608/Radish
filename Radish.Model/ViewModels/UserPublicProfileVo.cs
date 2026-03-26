namespace Radish.Model.ViewModels;

/// <summary>
/// 用户公开资料视图模型
/// </summary>
public class UserPublicProfileVo
{
    public long VoUserId { get; set; }

    public string VoUserName { get; set; } = string.Empty;

    public string? VoDisplayName { get; set; }

    public DateTime VoCreateTime { get; set; }

    public string? VoAvatarUrl { get; set; }

    public string? VoAvatarThumbnailUrl { get; set; }
}
