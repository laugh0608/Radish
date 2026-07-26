using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>治理申诉的追加式状态、决定与纠正事件。</summary>
[SugarTable("ContentModerationAppealEvent")]
[SugarIndex("idx_moderation_appeal_event_sequence", nameof(TenantId), OrderByType.Asc, nameof(AppealId), OrderByType.Asc, nameof(EventSequence), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_moderation_appeal_event_operation", nameof(TenantId), OrderByType.Asc, nameof(OperationKey), OrderByType.Asc, IsUnique = true)]
public sealed class ContentModerationAppealEvent : RootEntityTKey<long>, ITenantEntity
{
    public long TenantId { get; set; }
    public long AppealId { get; set; }
    public int EventSequence { get; set; }
    [SugarColumn(Length = 40, IsNullable = false)]
    public string EventType { get; set; } = string.Empty;
    [SugarColumn(Length = 160, IsNullable = true)]
    public string? OperationKey { get; set; }
    public int ExpectedAppealVersion { get; set; }
    public int ResultAppealVersion { get; set; }
    [SugarColumn(IsNullable = true)]
    public long? RelatedEvidenceId { get; set; }
    [SugarColumn(IsNullable = true)]
    public long? RelatedTargetActionId { get; set; }
    [SugarColumn(IsNullable = true)]
    public long? RelatedUserActionId { get; set; }
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
