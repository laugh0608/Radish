namespace Radish.Model.ViewModels;

/// <summary>单条聊天消息的权威回应聚合。</summary>
public sealed class ChatMessageReactionStateVo
{
    public long VoMessageId { get; set; }

    public long VoRevision { get; set; }

    public List<ReactionSummaryVo> VoItems { get; set; } = [];
}

/// <summary>聊天消息回应写入结果。</summary>
public sealed class ChatMessageReactionMutationVo
{
    public ChatMessageReactionStateVo VoState { get; set; } = new();

    public bool VoChanged { get; set; }

    public bool VoReplayed { get; set; }
}
