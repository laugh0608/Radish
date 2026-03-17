using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>发送频道消息 DTO</summary>
public class SendChannelMessageDto
{
    /// <summary>客户端请求 Id，用于前端乐观消息关联</summary>
    [StringLength(100, ErrorMessage = "clientRequestId 长度不能超过 100")]
    public string? ClientRequestId { get; set; }

    /// <summary>频道 Id</summary>
    [Required(ErrorMessage = "channelId 不能为空")]
    public long ChannelId { get; set; }

    /// <summary>消息类型</summary>
    public MessageType Type { get; set; } = MessageType.Text;

    /// <summary>消息内容</summary>
    [StringLength(4000, ErrorMessage = "消息内容长度不能超过 4000")]
    public string? Content { get; set; }

    /// <summary>引用消息 Id</summary>
    public long? ReplyToId { get; set; }

    /// <summary>图片附件 Id</summary>
    public long? AttachmentId { get; set; }

    /// <summary>图片地址</summary>
    [StringLength(500, ErrorMessage = "图片地址长度不能超过 500")]
    public string? ImageUrl { get; set; }

    /// <summary>图片缩略图地址</summary>
    [StringLength(500, ErrorMessage = "图片缩略图地址长度不能超过 500")]
    public string? ImageThumbnailUrl { get; set; }
}
