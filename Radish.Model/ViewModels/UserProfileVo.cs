namespace Radish.Model.ViewModels;

public class UserProfileVo
{
    public long UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string UserEmail { get; set; } = string.Empty;

    public string RealName { get; set; } = string.Empty;

    public int Sex { get; set; }

    public int Age { get; set; }

    public DateTime? Birth { get; set; }

    public string Address { get; set; } = string.Empty;

    public DateTime CreateTime { get; set; }

    public long? AvatarAttachmentId { get; set; }

    public string? AvatarUrl { get; set; }

    public string? AvatarThumbnailUrl { get; set; }
}
