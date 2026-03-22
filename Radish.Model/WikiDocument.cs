using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Model;

/// <summary>Wiki 文档实体</summary>
[SugarTable("WikiDocument")]
[SugarIndex("idx_wikidoc_tenant_slug", nameof(TenantId), OrderByType.Asc, nameof(Slug), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_wikidoc_parent_sort", nameof(TenantId), OrderByType.Asc, nameof(ParentId), OrderByType.Asc, nameof(Sort), OrderByType.Asc)]
[SugarIndex("idx_wikidoc_status_time", nameof(TenantId), OrderByType.Asc, nameof(Status), OrderByType.Asc, nameof(CreateTime), OrderByType.Desc)]
public class WikiDocument : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; } = 0;

    [SugarColumn(Length = 200, IsNullable = false)]
    public string Title { get; set; } = string.Empty;

    [SugarColumn(Length = 80, IsNullable = false)]
    public string Slug { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string MarkdownContent { get; set; } = string.Empty;

    [SugarColumn(Length = 1000, IsNullable = true)]
    public string? Summary { get; set; }

    [SugarColumn(IsNullable = true)]
    public long? CoverAttachmentId { get; set; }

    [SugarColumn(IsNullable = true)]
    public long? ParentId { get; set; }

    [SugarColumn(IsNullable = false)]
    public int Sort { get; set; } = 0;

    [SugarColumn(IsNullable = false)]
    public int Status { get; set; } = (int)WikiDocumentStatusEnum.Draft;

    [SugarColumn(IsNullable = false)]
    public int Visibility { get; set; } = (int)WikiDocumentVisibilityEnum.Authenticated;

    [SugarColumn(Length = 1000, IsNullable = true)]
    public string? AllowedRoles { get; set; }

    [SugarColumn(Length = 2000, IsNullable = true)]
    public string? AllowedPermissions { get; set; }

    [SugarColumn(Length = 30, IsNullable = false)]
    public string SourceType { get; set; } = "Manual";

    [SugarColumn(Length = 300, IsNullable = true)]
    public string? SourcePath { get; set; }

    [SugarColumn(IsNullable = false)]
    public int Version { get; set; } = 1;

    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? PublishedAt { get; set; }

    [SugarColumn(IsNullable = false)]
    public bool IsDeleted { get; set; } = false;

    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? DeletedAt { get; set; }

    [SugarColumn(Length = 50, IsNullable = true)]
    public string? DeletedBy { get; set; }

    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    [SugarColumn(IsNullable = false)]
    public long CreateId { get; set; } = 0;

    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }

    [SugarColumn(Length = 50, IsNullable = true)]
    public string? ModifyBy { get; set; }

    [SugarColumn(IsNullable = true)]
    public long? ModifyId { get; set; }
}
