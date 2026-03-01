using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>单个表情实体</summary>
[SugarTable("Sticker")]
[SugarIndex("idx_sticker_group_code", nameof(GroupId), OrderByType.Asc, nameof(Code), OrderByType.Asc)]
[SugarIndex("idx_sticker_group_sort", nameof(GroupId), OrderByType.Asc, nameof(Sort), OrderByType.Asc)]
public class Sticker : RootEntityTKey<long>, IDeleteFilter
{
    /// <summary>所属分组 Id</summary>
    [SugarColumn(IsNullable = false)]
    public long GroupId { get; set; }

    /// <summary>组内编码，仅允许 [a-z0-9_]</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string Code { get; set; } = string.Empty;

    /// <summary>展示名称</summary>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    /// <summary>原图 URL（GIF 时为动图）</summary>
    [SugarColumn(Length = 500, IsNullable = false)]
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>缩略图 URL（可空）</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? ThumbnailUrl { get; set; }

    /// <summary>是否为动图</summary>
    [SugarColumn(IsNullable = false)]
    public bool IsAnimated { get; set; } = false;

    /// <summary>是否允许内嵌正文</summary>
    [SugarColumn(IsNullable = false)]
    public bool AllowInline { get; set; } = true;

    /// <summary>关联附件 Id</summary>
    [SugarColumn(IsNullable = true)]
    public long? AttachmentId { get; set; }

    /// <summary>使用次数</summary>
    [SugarColumn(IsNullable = false)]
    public int UseCount { get; set; } = 0;

    /// <summary>排序值</summary>
    [SugarColumn(IsNullable = false)]
    public int Sort { get; set; } = 0;

    /// <summary>是否启用</summary>
    [SugarColumn(IsNullable = false)]
    public bool IsEnabled { get; set; } = true;

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
