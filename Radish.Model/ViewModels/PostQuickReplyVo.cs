namespace Radish.Model.ViewModels;

/// <summary>帖子轻回应视图模型</summary>
public class PostQuickReplyVo
{
    public long VoId { get; set; }

    public long VoPostId { get; set; }

    public long VoAuthorId { get; set; }

    public string VoAuthorName { get; set; } = string.Empty;

    public string? VoAuthorAvatarUrl { get; set; }

    public string VoContent { get; set; } = string.Empty;

    public int VoStatus { get; set; }

    public DateTime VoCreateTime { get; set; }
}
