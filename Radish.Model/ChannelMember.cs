using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>频道成员关系与已读状态</summary>
[SugarTable("ChannelMember")]
[Tenant(configId: "Chat")]
[SugarIndex("idx_channel_member_channel_user", nameof(ChannelId), OrderByType.Asc, nameof(UserId), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_channel_member_user", nameof(UserId), OrderByType.Asc)]
public class ChannelMember : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    /// <summary>频道 Id</summary>
    [SugarColumn(IsNullable = false)]
    public long ChannelId { get; set; }

    /// <summary>用户 Id</summary>
    [SugarColumn(IsNullable = false)]
    public long UserId { get; set; }

    /// <summary>成员角色</summary>
    [SugarColumn(IsNullable = false)]
    public MemberRole Role { get; set; } = MemberRole.Member;

    /// <summary>最后已读消息 Id</summary>
    [SugarColumn(IsNullable = true)]
    public long? LastReadMessageId { get; set; }

    /// <summary>加入时间</summary>
    [SugarColumn(IsNullable = false)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime JoinedAt { get; set; } = DateTime.Now;

    /// <summary>租户 Id</summary>
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; }

    /// <summary>是否软删除</summary>
    [SugarColumn(IsNullable = false)]
    public bool IsDeleted { get; set; } = false;

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
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>创建者</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建者 Id</summary>
    [SugarColumn(IsNullable = false)]
    public long CreateId { get; set; } = 0;

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

/// <summary>频道成员角色</summary>
public enum MemberRole
{
    /// <summary>普通成员</summary>
    Member = 1,

    /// <summary>版主</summary>
    Moderator = 2,

    /// <summary>所有者</summary>
    Owner = 3
}
