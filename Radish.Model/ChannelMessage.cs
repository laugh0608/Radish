using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>聊天室消息实体</summary>
[SugarTable("ChannelMessage")]
[Tenant(configId: "Chat")]
[SugarIndex("idx_channel_message_channel_id", nameof(ChannelId), OrderByType.Asc, nameof(Id), OrderByType.Desc)]
[SugarIndex("idx_channel_message_user_time", nameof(UserId), OrderByType.Asc, nameof(CreateTime), OrderByType.Desc)]
[SugarIndex("idx_channel_message_client_request", nameof(TenantId), OrderByType.Asc, nameof(UserId), OrderByType.Asc, nameof(ClientRequestId), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_channel_message_attachment", nameof(TenantId), OrderByType.Asc, nameof(AttachmentId), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_channel_message_channel_search_order", nameof(ChannelId), OrderByType.Asc, nameof(CreateTime), OrderByType.Desc, nameof(Id), OrderByType.Desc)]
[SugarIndex("idx_channel_message_tenant_search_order", nameof(TenantId), OrderByType.Asc, nameof(CreateTime), OrderByType.Desc, nameof(Id), OrderByType.Desc)]
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

    /// <summary>客户端请求 Id；同一租户、发送者内用于消息发送幂等</summary>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string? ClientRequestId { get; set; }

    /// <summary>发送者头像附件快照 Id</summary>
    [SugarColumn(IsNullable = true)]
    public long? UserAvatarAttachmentIdSnapshot { get; set; }

    /// <summary>消息类型</summary>
    [SugarColumn(IsNullable = false)]
    public MessageType Type { get; set; } = MessageType.Text;

    /// <summary>消息内容</summary>
    [SugarColumn(Length = 4000, IsNullable = true)]
    public string? Content { get; set; }

    /// <summary>由当前可见正文派生的规范化搜索文本；可重建且不对客户端暴露</summary>
    [SugarColumn(Length = 4000, IsNullable = true)]
    public string? SearchText { get; set; }

    /// <summary>回复消息 Id</summary>
    [SugarColumn(IsNullable = true)]
    public long? ReplyToId { get; set; }

    /// <summary>图片附件 Id</summary>
    [SugarColumn(IsNullable = true)]
    public long? AttachmentId { get; set; }

    /// <summary>消息回应聚合修订号；仅在实际回应状态变化时递增。</summary>
    [SugarColumn(IsNullable = false, DefaultValue = "0")]
    public long ReactionRevision { get; set; }

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
