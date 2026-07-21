using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>治理案件的追加式状态与动作事件。</summary>
[SugarTable("ContentModerationCaseEvent")]
[SugarIndex("idx_moderation_event_case_sequence", nameof(TenantId), OrderByType.Asc, nameof(CaseId), OrderByType.Asc, nameof(EventSequence), OrderByType.Asc, IsUnique = true)]
public sealed class ContentModerationCaseEvent : RootEntityTKey<long>, ITenantEntity
{
    public long TenantId { get; set; }
    public long CaseId { get; set; }
    public int EventSequence { get; set; }
    [SugarColumn(Length = 40, IsNullable = false)]
    public string EventType { get; set; } = string.Empty;
    public int ExpectedCaseVersion { get; set; }
    public int ResultCaseVersion { get; set; }
    [SugarColumn(IsNullable = true)]
    public long? RelatedReportId { get; set; }
    [SugarColumn(IsNullable = true)]
    public long? RelatedActionId { get; set; }
    [SugarColumn(IsNullable = true)]
    public int? FromStatus { get; set; }
    [SugarColumn(IsNullable = true)]
    public int? ToStatus { get; set; }
    [SugarColumn(Length = 80, IsNullable = true)]
    public string? ResultCode { get; set; }
    [SugarColumn(Length = 1000, IsNullable = true)]
    public string? Remark { get; set; }
    public long ActorUserId { get; set; }
    [SugarColumn(Length = 100, IsNullable = false)]
    public string ActorName { get; set; } = "System";
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
}
