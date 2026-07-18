using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>一对一私聊关系、请求与屏蔽状态</summary>
[SugarTable("DirectConversation")]
[Tenant(configId: "Chat")]
[SugarIndex("idx_direct_conversation_tenant_pair", nameof(TenantId), OrderByType.Asc, nameof(ParticipantLowUserId), OrderByType.Asc, nameof(ParticipantHighUserId), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_direct_conversation_tenant_channel", nameof(TenantId), OrderByType.Asc, nameof(ChannelId), OrderByType.Asc, IsUnique = true)]
public class DirectConversation : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    /// <summary>承载消息的 Private 频道 Id</summary>
    [SugarColumn(IsNullable = false)]
    public long ChannelId { get; set; }

    /// <summary>数值较小的参与者用户 Id</summary>
    [SugarColumn(IsNullable = false)]
    public long ParticipantLowUserId { get; set; }

    /// <summary>数值较大的参与者用户 Id</summary>
    [SugarColumn(IsNullable = false)]
    public long ParticipantHighUserId { get; set; }

    /// <summary>发起私聊请求的用户 Id</summary>
    [SugarColumn(IsNullable = false)]
    public long RequestedByUserId { get; set; }

    /// <summary>请求状态</summary>
    [SugarColumn(IsNullable = false)]
    public DirectConversationRequestStatus RequestStatus { get; set; } = DirectConversationRequestStatus.Pending;

    /// <summary>接受时间</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? AcceptedAt { get; set; }

    /// <summary>拒绝时间</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? DeclinedAt { get; set; }

    /// <summary>执行屏蔽的参与者用户 Id</summary>
    [SugarColumn(IsNullable = true)]
    public long? BlockedByUserId { get; set; }

    /// <summary>屏蔽时间</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? BlockedAt { get; set; }

    /// <summary>租户 Id</summary>
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; }

    /// <summary>是否软删除</summary>
    [SugarColumn(IsNullable = false)]
    public bool IsDeleted { get; set; }

    /// <summary>删除时间</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? DeletedAt { get; set; }

    /// <summary>删除人</summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? DeletedBy { get; set; }

    /// <summary>创建时间</summary>
    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>创建者</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建者 Id</summary>
    [SugarColumn(IsNullable = false)]
    public long CreateId { get; set; }

    /// <summary>修改时间</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }

    /// <summary>修改者</summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? ModifyBy { get; set; }

    /// <summary>修改者 Id</summary>
    [SugarColumn(IsNullable = true)]
    public long? ModifyId { get; set; }
}

/// <summary>一对一私聊请求状态</summary>
public enum DirectConversationRequestStatus
{
    /// <summary>等待对方处理</summary>
    Pending = 1,

    /// <summary>双方已建立会话</summary>
    Accepted = 2,

    /// <summary>对方已拒绝请求</summary>
    Declined = 3
}
