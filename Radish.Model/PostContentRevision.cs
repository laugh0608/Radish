using Radish.Model.Root;
using Radish.Model.Tenants;
using Radish.Shared.Constants;
using SqlSugar;

namespace Radish.Model;

/// <summary>帖子作者内容的不可变版本快照。</summary>
[SugarTable("PostContentRevision")]
[SugarIndex(
    "idx_postcontentrevision_tenant_post_revision",
    nameof(TenantId),
    OrderByType.Asc,
    nameof(PostId),
    OrderByType.Asc,
    nameof(RevisionNumber),
    OrderByType.Desc,
    IsUnique = true)]
[SugarIndex(
    "idx_postcontentrevision_tenant_restore_source",
    nameof(TenantId),
    OrderByType.Asc,
    nameof(RestoredFromRevisionId),
    OrderByType.Asc)]
public sealed class PostContentRevision : RootEntityTKey<long>, ITenantEntity
{
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; }

    [SugarColumn(IsNullable = false)]
    public long PostId { get; set; }

    [SugarColumn(IsNullable = false)]
    public int RevisionNumber { get; set; }

    [SugarColumn(Length = 20, IsNullable = false)]
    public string SourceType { get; set; } = ForumContentRevisionSourceTypes.Baseline;

    [SugarColumn(IsNullable = true)]
    public long? RestoredFromRevisionId { get; set; }

    [SugarColumn(Length = 30, IsNullable = false)]
    public string IntegrityStatus { get; set; } = ForumContentRevisionIntegrityStatuses.Complete;

    [SugarColumn(Length = 200, IsNullable = false)]
    public string Title { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string Content { get; set; } = string.Empty;

    [SugarColumn(Length = 20, IsNullable = false)]
    public string ContentType { get; set; } = "markdown";

    [SugarColumn(IsNullable = false)]
    public long CategoryId { get; set; }

    [SugarColumn(Length = 100, IsNullable = false)]
    public string CategoryNameSnapshot { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public long? CoverAttachmentId { get; set; }

    [SugarColumn(IsNullable = false)]
    public long EditorId { get; set; }

    [SugarColumn(Length = 100, IsNullable = false)]
    public string EditorName { get; set; } = string.Empty;

    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    [SugarColumn(IsNullable = false)]
    public long CreateId { get; set; }
}
