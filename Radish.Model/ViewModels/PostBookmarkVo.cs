namespace Radish.Model.ViewModels;

public static class PostBookmarkTargetStatuses
{
    public const string Available = "Available";
    public const string Unavailable = "Unavailable";
}

public sealed class PostBookmarkStateVo
{
    public string? VoBookmarkPublicId { get; set; }

    public string VoPostPublicId { get; set; } = string.Empty;

    public bool VoIsBookmarked { get; set; }

    public int VoCollectCount { get; set; }

    public DateTime? VoBookmarkedAt { get; set; }
}

public sealed class PostBookmarkRemoveVo
{
    public string VoBookmarkPublicId { get; set; } = string.Empty;

    public bool VoRemoved { get; set; } = true;
}

public sealed class UserPostBookmarkTagVo
{
    public string VoName { get; set; } = string.Empty;

    public string VoSlug { get; set; } = string.Empty;
}

public sealed class UserPostBookmarkVo
{
    public string VoBookmarkPublicId { get; set; } = string.Empty;

    public DateTime VoBookmarkedAt { get; set; }

    public string VoTargetStatus { get; set; } = PostBookmarkTargetStatuses.Unavailable;

    public string? VoPostPublicId { get; set; }

    public string? VoTitle { get; set; }

    public string? VoSummary { get; set; }

    public string? VoAuthorPublicId { get; set; }

    public string? VoAuthorName { get; set; }

    public DateTime? VoPublishTime { get; set; }

    public string? VoCategoryName { get; set; }

    public List<UserPostBookmarkTagVo> VoTags { get; set; } = [];

    public long? VoCoverAttachmentId { get; set; }

    public int? VoViewCount { get; set; }

    public int? VoLikeCount { get; set; }

    public int? VoCommentCount { get; set; }

    public int? VoCollectCount { get; set; }
}
