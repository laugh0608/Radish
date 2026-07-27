using Radish.Model.Root;
using Radish.Shared.Constants;
using SqlSugar;

namespace Radish.Model;

/// <summary>问答回答正文的不可变版本快照。</summary>
[SugarTable("PostAnswerContentRevision")]
[SugarIndex(
    "idx_postanswerrevision_tenant_answer_revision",
    nameof(TenantId),
    OrderByType.Asc,
    nameof(AnswerId),
    OrderByType.Asc,
    nameof(RevisionNumber),
    OrderByType.Desc,
    IsUnique = true)]
[SugarIndex(
    "idx_postanswerrevision_tenant_restore_source",
    nameof(TenantId),
    OrderByType.Asc,
    nameof(RestoredFromRevisionId),
    OrderByType.Asc)]
public sealed class PostAnswerContentRevision : RootEntityTKey<long>, ITenantEntity
{
    public long TenantId { get; set; }
    public long AnswerId { get; set; }
    public long PostId { get; set; }
    public int RevisionNumber { get; set; }

    [SugarColumn(Length = 20, IsNullable = false)]
    public string SourceType { get; set; } = ForumContentRevisionSourceTypes.Baseline;

    [SugarColumn(IsNullable = true)]
    public long? RestoredFromRevisionId { get; set; }

    [SugarColumn(Length = 30, IsNullable = false)]
    public string IntegrityStatus { get; set; } = ForumContentRevisionIntegrityStatuses.Complete;

    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string Content { get; set; } = string.Empty;

    public long EditorId { get; set; }

    [SugarColumn(Length = 100, IsNullable = false)]
    public string EditorName { get; set; } = string.Empty;

    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    public long CreateId { get; set; }
}
