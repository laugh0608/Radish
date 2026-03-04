using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>聊天室频道实体</summary>
[SugarTable("Channel")]
[Tenant(configId: "Chat")]
[SugarIndex("idx_channel_tenant_sort", nameof(TenantId), OrderByType.Asc, nameof(Sort), OrderByType.Asc)]
[SugarIndex("idx_channel_tenant_slug", nameof(TenantId), OrderByType.Asc, nameof(Slug), OrderByType.Asc)]
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
