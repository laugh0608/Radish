using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>批量读取聊天消息回应状态。</summary>
public sealed class GetChatMessageReactionStatesDto
{
    [Range(1, long.MaxValue)]
    public long ChannelId { get; set; }

    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public List<long> MessageIds { get; set; } = [];
}

/// <summary>把当前用户对一条消息的指定回应设置为目标状态。</summary>
public sealed class SetChatMessageReactionDto
{
    [Range(1, long.MaxValue)]
    public long ChannelId { get; set; }

    [Range(1, long.MaxValue)]
    public long MessageId { get; set; }

    [Required]
    [StringLength(20)]
    public string EmojiType { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string EmojiValue { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string ClientOperationId { get; set; } = string.Empty;
}
