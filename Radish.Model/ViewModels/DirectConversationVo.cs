namespace Radish.Model.ViewModels;

/// <summary>一对一私聊当前用户视角摘要</summary>
public sealed class DirectConversationVo
{
    public long VoChannelId { get; set; }

    public string VoConversationKind { get; set; } = "stranger";

    public long VoPeerUserId { get; set; }

    public string? VoPeerPublicId { get; set; }

    public string VoPeerDisplayName { get; set; } = string.Empty;

    public string? VoPeerAvatarUrl { get; set; }

    public string VoDirectRequestStatus { get; set; } = "pending";

    public bool VoCanSend { get; set; }

    public bool VoCanAccept { get; set; }

    public bool VoCanDecline { get; set; }

    public bool VoCanBlock { get; set; }

    public bool VoCanUnblock { get; set; }

    public bool VoIsBlockedByCurrentUser { get; set; }

    public bool VoIsArchived { get; set; }

    public bool VoIsPeerAvailable { get; set; }

    public bool VoWasCreated { get; set; }
}
