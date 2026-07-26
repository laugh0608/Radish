using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>Wiki 草稿审核的追加式审计事件。</summary>
[SugarTable("WikiDocumentReviewEvent")]
[SugarIndex("idx_wikireview_document_time", nameof(TenantId), OrderByType.Asc, nameof(DocumentId), OrderByType.Asc, nameof(CreateTime), OrderByType.Desc)]
[SugarIndex("idx_wikireview_draft_time", nameof(TenantId), OrderByType.Asc, nameof(DraftId), OrderByType.Asc, nameof(CreateTime), OrderByType.Desc)]
public sealed class WikiDocumentReviewEvent : RootEntityTKey<long>, ITenantEntity
{
    public long TenantId { get; set; }
    public long DocumentId { get; set; }
    public long DraftId { get; set; }
    [SugarColumn(Length = 30, IsNullable = false)]
    public string Action { get; set; } = string.Empty;
    public long ActorUserId { get; set; }
    [SugarColumn(Length = 50, IsNullable = false)]
    public string ActorName { get; set; } = "System";
    [SugarColumn(Length = 1000, IsNullable = true)]
    public string? Comment { get; set; }
    public int DocumentVersion { get; set; }
    public int DraftVersion { get; set; }
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
}
