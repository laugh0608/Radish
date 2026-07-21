using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>治理案件的追加式安全证据快照。</summary>
[SugarTable("ContentModerationEvidence")]
[SugarIndex("idx_moderation_evidence_case_sequence", nameof(TenantId), OrderByType.Asc, nameof(CaseId), OrderByType.Asc, nameof(EvidenceSequence), OrderByType.Asc, IsUnique = true)]
public sealed class ContentModerationEvidence : RootEntityTKey<long>, ITenantEntity
{
    public long TenantId { get; set; }
    public long CaseId { get; set; }
    public int EvidenceSequence { get; set; }
    public int EvidenceType { get; set; }
    [SugarColumn(IsNullable = true)]
    public long? SourceReportId { get; set; }
    [SugarColumn(IsNullable = true)]
    public long? RelatedActionId { get; set; }
    public int TargetState { get; set; }

    [SugarColumn(Length = 200, IsNullable = true)]
    public string? SnapshotTitle { get; set; }
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? SnapshotSummary { get; set; }
    public long TargetUserId { get; set; }
    [SugarColumn(Length = 100, IsNullable = true)]
    public string? TargetUserName { get; set; }
    [SugarColumn(IsNullable = true)]
    public long? TargetPostId { get; set; }
    [SugarColumn(IsNullable = true)]
    public long? TargetCommentId { get; set; }
    [SugarColumn(IsNullable = true)]
    public long? TargetChannelId { get; set; }
    [SugarColumn(IsNullable = true)]
    public long? TargetMessageId { get; set; }
    [SugarColumn(IsNullable = true)]
    public int? ContentRevision { get; set; }
    [SugarColumn(IsNullable = true)]
    public DateTime? TargetModifiedAt { get; set; }
    [SugarColumn(Length = 64, IsNullable = false)]
    public string SnapshotHash { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public long CapturedById { get; set; }
    [SugarColumn(Length = 100, IsNullable = false)]
    public string CapturedByName { get; set; } = "System";
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
    [SugarColumn(Length = 100, IsNullable = false)]
    public string CreateBy { get; set; } = "System";
    public long CreateId { get; set; }
}
