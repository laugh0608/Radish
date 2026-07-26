using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Model;

/// <summary>Wiki 文档唯一活跃工作草稿及终态审核快照。</summary>
[SugarTable("WikiDocumentDraft")]
[SugarIndex("idx_wikidraft_document_state", nameof(TenantId), OrderByType.Asc, nameof(DocumentId), OrderByType.Asc, nameof(ReviewState), OrderByType.Asc)]
[SugarIndex("idx_wikidraft_review_time", nameof(TenantId), OrderByType.Asc, nameof(ReviewState), OrderByType.Asc, nameof(SubmittedAt), OrderByType.Desc)]
public sealed class WikiDocumentDraft : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; }

    [SugarColumn(IsNullable = false)]
    public long DocumentId { get; set; }

    [SugarColumn(IsNullable = false)]
    public int BaseDocumentVersion { get; set; }

    [SugarColumn(IsNullable = false)]
    public int DraftVersion { get; set; } = 1;

    [SugarColumn(Length = 200, IsNullable = false)]
    public string Title { get; set; } = string.Empty;

    [SugarColumn(Length = 80, IsNullable = false)]
    public string Slug { get; set; } = string.Empty;

    [SugarColumn(Length = 1000, IsNullable = true)]
    public string? Summary { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string MarkdownContent { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public long? CoverAttachmentId { get; set; }

    [SugarColumn(IsNullable = true)]
    public long? ProposedParentId { get; set; }

    [SugarColumn(IsNullable = false)]
    public int ReviewState { get; set; } = (int)WikiDocumentDraftState.Editing;

    [SugarColumn(Length = 300, IsNullable = true)]
    public string? ChangeSummary { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? SubmittedAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public long? SubmittedBy { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? ReviewedAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public long? ReviewedBy { get; set; }

    [SugarColumn(Length = 1000, IsNullable = true)]
    public string? ReviewComment { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? PayloadPurgedAt { get; set; }

    [SugarColumn(IsNullable = false)]
    public bool IsDeleted { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? DeletedAt { get; set; }

    [SugarColumn(Length = 50, IsNullable = true)]
    public string? DeletedBy { get; set; }

    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}")]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    [SugarColumn(IsNullable = false)]
    public long CreateId { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? ModifyTime { get; set; }

    [SugarColumn(Length = 50, IsNullable = true)]
    public string? ModifyBy { get; set; }

    [SugarColumn(IsNullable = true)]
    public long? ModifyId { get; set; }
}
