using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>表情包分组实体</summary>
[SugarTable("StickerGroup")]
[SugarIndex("idx_sticker_group_tenant_code", nameof(TenantId), OrderByType.Asc, nameof(Code), OrderByType.Asc)]
[SugarIndex("idx_sticker_group_sort", nameof(Sort), OrderByType.Asc)]
public class StickerGroup : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    /// <summary>分组编码，仅允许 [a-z0-9_]</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string Code { get; set; } = string.Empty;

    /// <summary>分组显示名称</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    /// <summary>分组描述</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Description { get; set; }

    /// <summary>封面附件 Id</summary>
    [SugarColumn(IsNullable = true)]
    public long? CoverAttachmentId { get; set; }

    /// <summary>分组类型</summary>
    [SugarColumn(IsNullable = false)]
    public StickerGroupType GroupType { get; set; } = StickerGroupType.Official;

    /// <summary>是否启用</summary>
    [SugarColumn(IsNullable = false)]
    public bool IsEnabled { get; set; } = true;

    /// <summary>排序值</summary>
    [SugarColumn(IsNullable = false)]
    public int Sort { get; set; } = 0;

    /// <summary>租户 Id</summary>
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; } = 0;

    /// <summary>是否软删除</summary>
    [SugarColumn(IsNullable = false)]
    public bool IsDeleted { get; set; } = false;

    /// <summary>删除时间</summary>
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    [SugarColumn(IsNullable = true)]
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

/// <summary>表情包分组类型</summary>
public enum StickerGroupType
{
    /// <summary>官方表情包</summary>
    Official = 1,

    /// <summary>付费表情包（预留）</summary>
    Premium = 2
}
