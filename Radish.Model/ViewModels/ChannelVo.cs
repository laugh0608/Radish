namespace Radish.Model.ViewModels;

/// <summary>频道视图模型</summary>
public class ChannelVo
{
    public long VoId { get; set; }

    public long? VoCategoryId { get; set; }

    public string VoName { get; set; } = string.Empty;

    public string VoSlug { get; set; } = string.Empty;

    public string? VoDescription { get; set; }

    public string? VoIconEmoji { get; set; }

    public ChannelType VoType { get; set; }

    public int VoSort { get; set; }

    public int VoUnreadCount { get; set; }

    public bool VoHasMention { get; set; }

    public ChannelMessageVo? VoLastMessage { get; set; }

    public string VoConversationKind { get; set; } = "public";

    public long? VoPeerUserId { get; set; }

    public string? VoPeerPublicId { get; set; }

    public string? VoPeerDisplayName { get; set; }

    public string? VoPeerAvatarUrl { get; set; }

    public string? VoDirectRequestStatus { get; set; }

    public bool VoCanSend { get; set; }

    public bool VoCanReact { get; set; }

    public bool VoCanPinMessages { get; set; }

    public bool VoCanAccept { get; set; }

    public bool VoCanDecline { get; set; }

    public bool VoCanBlock { get; set; }

    public bool VoCanUnblock { get; set; }

    public bool VoIsBlockedByCurrentUser { get; set; }

    public bool VoIsArchived { get; set; }

    public bool VoIsPeerAvailable { get; set; } = true;
}
