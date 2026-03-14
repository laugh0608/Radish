using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>帖子问答回答</summary>
[SugarTable("PostAnswer")]
[SugarIndex("idx_postanswer_post_create", nameof(PostId), OrderByType.Asc, nameof(CreateTime), OrderByType.Asc)]
[SugarIndex("idx_postanswer_post_accept", nameof(PostId), OrderByType.Asc, nameof(IsAccepted), OrderByType.Asc)]
public class PostAnswer : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    /// <summary>帖子 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long PostId { get; set; }

    /// <summary>作者 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long AuthorId { get; set; }

    /// <summary>作者名称</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>回答内容</summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string Content { get; set; } = string.Empty;

    /// <summary>是否已采纳</summary>
    [SugarColumn(IsNullable = false)]
    public bool IsAccepted { get; set; } = false;

    /// <summary>租户 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; } = 0;

    /// <summary>软删除标记</summary>
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

    /// <summary>创建人</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建人 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long CreateId { get; set; }

    /// <summary>修改时间</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }

    /// <summary>修改人</summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? ModifyBy { get; set; }

    /// <summary>修改人 ID</summary>
    [SugarColumn(IsNullable = true)]
    public long? ModifyId { get; set; }
}
