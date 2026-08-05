using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>聊天室频道实体</summary>
[SugarTable("Channel")]
[Tenant(configId: "Chat")]
[SugarIndex("idx_channel_tenant_sort", nameof(TenantId), OrderByType.Asc, nameof(Sort), OrderByType.Asc)]
[SugarIndex("idx_channel_tenant_slug", nameof(TenantId), OrderByType.Asc, nameof(Slug), OrderByType.Asc)]
[SugarIndex("idx_channel_tenant_discover", nameof(TenantId), OrderByType.Asc, nameof(DiscoverVisibility), OrderByType.Asc, nameof(LastMessageTime), OrderByType.Desc)]
public class Channel : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    /// <summary>所属分类 Id（可空）</summary>
    [SugarColumn(IsNullable = true)]
    public long? CategoryId { get; set; }

    /// <summary>频道名称</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    /// <summary>频道 Slug（仅 [a-z0-9-]）</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>频道描述</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Description { get; set; }

    /// <summary>频道图标（Unicode emoji）</summary>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string? IconEmoji { get; set; }

    /// <summary>频道类型</summary>
    [SugarColumn(IsNullable = false)]
    public ChannelType Type { get; set; } = ChannelType.Public;

    /// <summary>匿名发现页可读取的频道摘要级别；与登录态频道类型相互独立。</summary>
    [SugarColumn(IsNullable = false, DefaultValue = "0")]
    public ChannelDiscoverVisibility DiscoverVisibility { get; set; } = ChannelDiscoverVisibility.Hidden;

    /// <summary>公开摘要可见性修订号；仅在可见性实际变化时递增。</summary>
    [SugarColumn(IsNullable = false, DefaultValue = "0")]
    public int DiscoverVisibilityVersion { get; set; }

    /// <summary>是否启用</summary>
    [SugarColumn(IsNullable = false)]
    public bool IsEnabled { get; set; } = true;

    /// <summary>排序值</summary>
    [SugarColumn(IsNullable = false)]
    public int Sort { get; set; } = 0;

    /// <summary>最后消息 Id（冗余）</summary>
    [SugarColumn(IsNullable = true)]
    public long? LastMessageId { get; set; }

    /// <summary>最后消息时间（冗余）</summary>
    [SugarColumn(IsNullable = true)]
    public DateTime? LastMessageTime { get; set; }

    /// <summary>消息置顶集合修订号；仅在活跃置顶集合实际变化时递增。</summary>
    [SugarColumn(IsNullable = false, DefaultValue = "0")]
    public long PinRevision { get; set; }

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

/// <summary>频道类型</summary>
public enum ChannelType
{
    /// <summary>公开频道</summary>
    Public = 1,

    /// <summary>公告频道（仅管理员可发言）</summary>
    Announcement = 2,

    /// <summary>私有频道（预留）</summary>
    Private = 3
}

/// <summary>频道在匿名社区发现页中的摘要可见性。</summary>
public enum ChannelDiscoverVisibility
{
    /// <summary>不进入匿名发现读模型。</summary>
    Hidden = 0,

    /// <summary>仅允许输出经过约束的频道摘要，不包含消息、成员或在线状态。</summary>
    Summary = 1
}
