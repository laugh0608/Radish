using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>Wiki 文档版本实体</summary>
[SugarTable("WikiDocumentRevision")]
[SugarIndex("idx_wikirevision_doc_version", nameof(DocumentId), OrderByType.Asc, nameof(Version), OrderByType.Desc, IsUnique = true)]
[SugarIndex("idx_wikirevision_doc_time", nameof(DocumentId), OrderByType.Asc, nameof(CreateTime), OrderByType.Desc)]
public class WikiDocumentRevision : RootEntityTKey<long>, ITenantEntity
{
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; } = 0;

    [SugarColumn(IsNullable = false)]
    public long DocumentId { get; set; }

    [SugarColumn(IsNullable = false)]
    public int Version { get; set; }

    [SugarColumn(Length = 200, IsNullable = false)]
    public string Title { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string MarkdownContent { get; set; } = string.Empty;

    [SugarColumn(Length = 300, IsNullable = true)]
    public string? ChangeSummary { get; set; }

    [SugarColumn(Length = 30, IsNullable = false)]
    public string SourceType { get; set; } = "Manual";

    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    [SugarColumn(IsNullable = false)]
    public long CreateId { get; set; } = 0;
}
