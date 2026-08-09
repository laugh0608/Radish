namespace Radish.Model.ViewModels;

/// <summary>
/// 用户公开资料视图模型
/// </summary>
public class UserPublicProfileVo
{
    public long VoUserId { get; set; }

    public string? VoPublicId { get; set; }

    public long? VoPublicIndex { get; set; }

    public string VoUserName { get; set; } = string.Empty;

    public string? VoDisplayName { get; set; }

    public string? VoDisplayHandle { get; set; }

    public DateTime VoCreateTime { get; set; }

    /// <summary>公开等级；不包含经验进度、排名或冻结状态。</summary>
    public int VoCurrentLevel { get; set; }

    /// <summary>公开等级名称。</summary>
    public string VoCurrentLevelName { get; set; } = string.Empty;

    public string? VoAvatarUrl { get; set; }

    public string? VoAvatarThumbnailUrl { get; set; }

    public UserAdornmentVo? VoAdornment { get; set; }

    public PetPublicCardVo? VoPet { get; set; }
}
