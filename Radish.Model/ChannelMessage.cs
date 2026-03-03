using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>聊天室消息实体</summary>
[SugarTable("ChannelMessage")]
[Tenant(configId: "Chat")]
[SugarIndex("idx_channel_message_channel_id", nameof(ChannelId), OrderByType.Asc, nameof(Id), OrderByType.Desc)]
[SugarIndex("idx_channel_message_user_time", nameof(UserId), OrderByType.Asc, nameof(CreateTime), OrderByType.Desc)]
public class ChannelMessage : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    /// <summary>频道 Id</summary>
    [SugarColumn(IsNullable = false)]
    public long ChannelId { get; set; }

    /// <summary>发送者用户 Id</summary>
    [SugarColumn(IsNullable = false)]
    public long UserId { get; set; }

    /// <summary>发送者用户名（冗余）</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>发送者头像（冗余）</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? UserAvatarUrl { get; set; }

    /// <summary>消息类型</summary>
    [SugarColumn(IsNullable = false)]
    public MessageType Type { get; set; } = MessageType.Text;

    /// <summary>消息内容</summary>
    [SugarColumn(Length = 4000, IsNullable = true)]
    public string? Content { get; set; }

    /// <summary>回复消息 Id</summary>
    [SugarColumn(IsNullable = true)]
    public long? ReplyToId { get; set; }

    /// <summary>图片附件 Id</summary>
    [SugarColumn(IsNullable = true)]
    public long? AttachmentId { get; set; }

    /// <summary>图片原图地址</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? ImageUrl { get; set; }

    /// <summary>图片缩略图地址</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? ImageThumbnailUrl { get; set; }

    /// <summary>租户 Id</summary>
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; }

    /// <summary>创建时间</summary>
    [SugarColumn(IsNullable = false)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>是否软删除（撤回）</summary>
    [SugarColumn(IsNullable = false)]
    public bool IsDeleted { get; set; } = false;

    /// <summary>删除时间（撤回时间）</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? DeletedAt { get; set; }

    /// <summary>删除人（撤回人）</summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? DeletedBy { get; set; }
}

/// <summary>消息类型</summary>
public enum MessageType
{
    /// <summary>文本消息</summary>
    Text = 1,

    /// <summary>图片消息</summary>
    Image = 2,

    /// <summary>系统消息</summary>
    System = 3
}
