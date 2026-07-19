namespace Radish.Model.ViewModels;

/// <summary>Chat 历史消息搜索分页。</summary>
public sealed class ChannelMessageSearchPageVo
{
    public List<ChannelMessageSearchItemVo> VoItems { get; set; } = [];

    public string? VoNextCursor { get; set; }

    public bool VoHasMore { get; set; }
}

/// <summary>Chat 历史消息搜索结果。</summary>
public sealed class ChannelMessageSearchItemVo
{
    public long VoChannelId { get; set; }

    public long VoMessageId { get; set; }

    public string VoChannelDisplayName { get; set; } = string.Empty;

    public string? VoChannelIcon { get; set; }

    public string VoConversationKind { get; set; } = "public";

    public long? VoPeerUserId { get; set; }

    public string? VoPeerPublicId { get; set; }

    public string? VoPeerAvatarUrl { get; set; }

    public long VoSenderUserId { get; set; }

    public string VoSenderDisplayName { get; set; } = string.Empty;

    public string? VoSenderAvatarUrl { get; set; }

    public string VoSnippet { get; set; } = string.Empty;

    public DateTime VoCreateTime { get; set; }

    public MessageType VoMessageType { get; set; }
}
