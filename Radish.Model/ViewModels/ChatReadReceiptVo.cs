namespace Radish.Model.ViewModels;

/// <summary>个人频道已读游标推进结果。</summary>
public sealed class ChannelReadStateVo
{
    public long VoChannelId { get; set; }

    public long VoLastReadMessageId { get; set; }

    public int VoUnreadCount { get; set; }

    public bool VoHasMention { get; set; }

    public bool VoChanged { get; set; }
}

/// <summary>单条自己所发消息的阅读回执摘要。</summary>
public sealed class ChatReadReceiptSummaryItemVo
{
    public long VoMessageId { get; set; }

    public int? VoReadCount { get; set; }

    public bool? VoPeerHasRead { get; set; }
}

/// <summary>当前频道中一批自己所发消息的阅读回执摘要。</summary>
public sealed class ChatReadReceiptSummariesVo
{
    public long VoChannelId { get; set; }

    public string VoMode { get; set; } = ChatReadReceiptModes.None;

    public List<ChatReadReceiptSummaryItemVo> VoItems { get; set; } = [];
}

/// <summary>普通 Private 消息的一名当前有效读者。</summary>
public sealed class ChatReadReceiptReaderVo
{
    public long VoUserId { get; set; }

    public string? VoPublicId { get; set; }

    public long? VoPublicIndex { get; set; }

    public string VoDisplayName { get; set; } = string.Empty;

    public long? VoAvatarAttachmentId { get; set; }

    public string? VoAvatarUrl { get; set; }
}

/// <summary>普通 Private 消息读者的 keyset 分页。</summary>
public sealed class ChatReadReceiptReaderPageVo
{
    public long VoChannelId { get; set; }

    public long VoMessageId { get; set; }

    public List<ChatReadReceiptReaderVo> VoItems { get; set; } = [];

    public string? VoNextCursor { get; set; }

    public bool VoHasMore { get; set; }
}

/// <summary>阅读回执摘要展示模式。</summary>
public static class ChatReadReceiptModes
{
    public const string None = "none";

    public const string PrivateGroup = "private_group";

    public const string Direct = "direct";
}
