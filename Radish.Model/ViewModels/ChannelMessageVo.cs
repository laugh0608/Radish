namespace Radish.Model.ViewModels;

/// <summary>频道消息视图模型</summary>
public class ChannelMessageVo
{
    public long VoId { get; set; }

    public long VoChannelId { get; set; }

    public long VoUserId { get; set; }

    public string VoUserName { get; set; } = string.Empty;

    public string? VoUserAvatarUrl { get; set; }

    public MessageType VoType { get; set; }

    public string? VoContent { get; set; }

    public long? VoReplyToId { get; set; }

    public ChannelMessageVo? VoReplyTo { get; set; }

    public long? VoAttachmentId { get; set; }

    public string? VoImageUrl { get; set; }

    public string? VoImageThumbnailUrl { get; set; }

    public bool VoIsRecalled { get; set; }

    public DateTime VoCreateTime { get; set; }
}
