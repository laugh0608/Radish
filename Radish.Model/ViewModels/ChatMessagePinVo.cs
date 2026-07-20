namespace Radish.Model.ViewModels;

/// <summary>聊天消息置顶条目。</summary>
public sealed class ChatMessagePinVo
{
    public long VoId { get; set; }

    public long VoMessageId { get; set; }

    public ChannelMessageVo VoMessage { get; set; } = new();

    public long VoPinnedByUserId { get; set; }

    public string VoPinnedByName { get; set; } = string.Empty;

    public DateTime VoPinnedAt { get; set; }
}

/// <summary>频道消息置顶权威快照。</summary>
public sealed class ChatMessagePinStateVo
{
    public long VoChannelId { get; set; }

    public long VoRevision { get; set; }

    public List<ChatMessagePinVo> VoItems { get; set; } = [];
}

/// <summary>聊天消息置顶写入结果。</summary>
public sealed class ChatMessagePinMutationVo
{
    public ChatMessagePinStateVo VoState { get; set; } = new();

    public bool VoChanged { get; set; }
}
