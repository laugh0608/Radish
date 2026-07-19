using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>把一条聊天消息的共享置顶设置为目标状态。</summary>
public sealed class SetChatMessagePinDto
{
    [Range(1, long.MaxValue)]
    public long ChannelId { get; set; }

    [Range(1, long.MaxValue)]
    public long MessageId { get; set; }

    public bool IsPinned { get; set; }
}
