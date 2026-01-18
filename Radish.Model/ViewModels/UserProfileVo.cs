namespace Radish.Model.ViewModels;

public class UserProfileVo
{
    public long VoUserId { get; set; }

    public string VoUserName { get; set; } = string.Empty;

    public string VoUserEmail { get; set; } = string.Empty;

    public string VoRealName { get; set; } = string.Empty;

    public int VoSex { get; set; }

    public int VoAge { get; set; }

    public DateTime? VoBirth { get; set; }

    public string VoAddress { get; set; } = string.Empty;

    public DateTime VoCreateTime { get; set; }

    public long? VoAvatarAttachmentId { get; set; }

    public string? VoAvatarUrl { get; set; }

    public string? VoAvatarThumbnailUrl { get; set; }
}
