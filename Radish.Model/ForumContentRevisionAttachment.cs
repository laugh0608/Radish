using Radish.Model.Root;
using Radish.Model.Tenants;
using Radish.Shared.Constants;
using SqlSugar;

namespace Radish.Model;

/// <summary>论坛内容版本与附件的不可变引用。</summary>
[SugarTable("ForumContentRevisionAttachment")]
[SugarIndex(
    "idx_forumrevisionattachment_revision_attachment",
    nameof(TenantId),
    OrderByType.Asc,
    nameof(TargetType),
    OrderByType.Asc,
    nameof(RevisionId),
    OrderByType.Asc,
    nameof(AttachmentId),
    OrderByType.Asc,
    nameof(ReferenceKind),
    OrderByType.Asc,
    IsUnique = true)]
[SugarIndex(
    "idx_forumrevisionattachment_attachment",
    nameof(TenantId),
    OrderByType.Asc,
    nameof(AttachmentId),
    OrderByType.Asc)]
[SugarIndex(
    "idx_forumrevisionattachment_target",
    nameof(TenantId),
    OrderByType.Asc,
    nameof(TargetType),
    OrderByType.Asc,
    nameof(TargetId),
    OrderByType.Asc,
    nameof(RevisionId),
    OrderByType.Asc)]
public sealed class ForumContentRevisionAttachment : RootEntityTKey<long>, ITenantEntity
{
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; }

    [SugarColumn(Length = 20, IsNullable = false)]
    public string TargetType { get; set; } = ForumContentRevisionTargetTypes.Post;

    [SugarColumn(IsNullable = false)]
    public long TargetId { get; set; }

    [SugarColumn(IsNullable = false)]
    public long RevisionId { get; set; }

    [SugarColumn(IsNullable = false)]
    public long AttachmentId { get; set; }

    [SugarColumn(Length = 20, IsNullable = false)]
    public string ReferenceKind { get; set; } = ForumContentRevisionReferenceKinds.Content;

    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    [SugarColumn(IsNullable = false)]
    public long CreateId { get; set; }
}
