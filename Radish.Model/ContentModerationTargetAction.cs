using Radish.Model.Root;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Model;

/// <summary>治理案件对领域目标执行限制或恢复的不可变动作流水。</summary>
[SugarTable("ContentModerationTargetAction")]
[SugarIndex("idx_moderation_target_action_operation", nameof(TenantId), OrderByType.Asc, nameof(OperationKey), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_moderation_target_action_case", nameof(TenantId), OrderByType.Asc, nameof(CaseId), OrderByType.Asc, nameof(CreateTime), OrderByType.Asc)]
[SugarIndex("idx_moderation_target_action_appeal", nameof(TenantId), OrderByType.Asc, nameof(AppealId), OrderByType.Asc, nameof(CreateTime), OrderByType.Asc)]
public sealed class ContentModerationTargetAction : RootEntityTKey<long>, ITenantEntity
{
    public long TenantId { get; set; }
    public long CaseId { get; set; }
    [SugarColumn(IsNullable = true)]
    public long? AppealId { get; set; }
    public int TargetType { get; set; }
    public long TargetContentId { get; set; }
    public long TargetUserId { get; set; }
    public int ActionType { get; set; } = (int)ContentModerationTargetActionType.Restrict;
    [SugarColumn(IsNullable = true)]
    public long? SourceTargetActionId { get; set; }
    [SugarColumn(Length = 160, IsNullable = false)]
    public string OperationKey { get; set; } = string.Empty;
    public int Status { get; set; } = (int)ContentModerationTargetActionStatus.Pending;
    [SugarColumn(IsNullable = true)]
    public int? ExpectedTargetVersion { get; set; }
    [SugarColumn(IsNullable = true)]
    public int? ResultTargetVersion { get; set; }
    public bool ChangedTargetState { get; set; }
    [SugarColumn(Length = 80, IsNullable = true)]
    public string? ResultCode { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    [SugarColumn(IsNullable = true)]
    public DateTime? CompletedAt { get; set; }
    public long OperatorUserId { get; set; }
    [SugarColumn(Length = 100, IsNullable = false)]
    public string OperatorName { get; set; } = "System";
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
    [SugarColumn(Length = 100, IsNullable = false)]
    public string CreateBy { get; set; } = "System";
    public long CreateId { get; set; }
}
