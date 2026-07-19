using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>把当前账号的频道已读游标推进到实际展示过的消息。</summary>
public sealed class AdvanceChannelReadStateDto
{
    [Range(1, long.MaxValue)]
    public long ChannelId { get; set; }

    [Range(1, long.MaxValue)]
    public long ReadThroughMessageId { get; set; }
}

/// <summary>批量读取当前账号自己所发消息的阅读回执摘要。</summary>
public sealed class GetChatReadReceiptSummariesDto
{
    [Range(1, long.MaxValue)]
    public long ChannelId { get; set; }

    [Required]
    [MinLength(1)]
    public List<long> MessageIds { get; set; } = [];
}
