using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>Wiki 文档、草稿或正式版本与附件之间的权威引用。</summary>
[SugarTable("WikiAttachmentReference")]
[SugarIndex(
    "idx_wikiattachment_source_attachment",
    nameof(TenantId), OrderByType.Asc,
    nameof(ReferenceKind), OrderByType.Asc,
    nameof(ReferenceSourceId), OrderByType.Asc,
    nameof(AttachmentId), OrderByType.Asc,
    IsUnique = true)]
[SugarIndex(
    "idx_wikiattachment_attachment_active",
    nameof(TenantId), OrderByType.Asc,
    nameof(AttachmentId), OrderByType.Asc,
    nameof(IsDeleted), OrderByType.Asc)]
[SugarIndex(
    "idx_wikiattachment_document_kind_active",
    nameof(TenantId), OrderByType.Asc,
    nameof(DocumentId), OrderByType.Asc,
    nameof(ReferenceKind), OrderByType.Asc,
    nameof(IsDeleted), OrderByType.Asc)]
public sealed class WikiAttachmentReference : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; }

    [SugarColumn(IsNullable = false)]
    public long DocumentId { get; set; }

    [SugarColumn(IsNullable = false)]
    public long AttachmentId { get; set; }

    [SugarColumn(IsNullable = false)]
    public int ReferenceKind { get; set; }

    [SugarColumn(IsNullable = false)]
    public long ReferenceSourceId { get; set; }

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
