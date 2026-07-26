using Radish.Model.Root;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Model;

/// <summary>被处置用户针对已结治理案件提交的一次独立申诉。</summary>
[SugarTable("ContentModerationAppeal")]
[SugarIndex("idx_moderation_appeal_public_id", nameof(TenantId), OrderByType.Asc, nameof(PublicId), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_moderation_appeal_case_appellant", nameof(TenantId), OrderByType.Asc, nameof(CaseId), OrderByType.Asc, nameof(AppellantUserId), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_moderation_appeal_queue", nameof(TenantId), OrderByType.Asc, nameof(Status), OrderByType.Asc, nameof(ModifyTime), OrderByType.Desc)]
[SugarIndex("idx_moderation_appeal_submission_operation", nameof(TenantId), OrderByType.Asc, nameof(SubmissionOperationKey), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_moderation_appeal_decision_operation", nameof(TenantId), OrderByType.Asc, nameof(DecisionOperationKey), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_moderation_appeal_withdrawal_operation", nameof(TenantId), OrderByType.Asc, nameof(WithdrawalOperationKey), OrderByType.Asc, IsUnique = true)]
public sealed class ContentModerationAppeal : RootEntityTKey<long>, ITenantEntity
{
    public const string PublicIdPrefix = "apl_";

    public long TenantId { get; set; }
    public long CaseId { get; set; }
    public long AppellantUserId { get; set; }

    [SugarColumn(Length = 40, IsNullable = false)]
    public string PublicId { get; set; } = GeneratePublicId();

    public int Status { get; set; } = (int)ContentModerationAppealStatus.Submitted;
    public int Outcome { get; set; } = (int)ContentModerationAppealOutcome.None;
    public int EligibleScopeSnapshot { get; set; }
    public int GrantedScope { get; set; }
    public int Version { get; set; } = 1;

    [SugarColumn(Length = 1000, IsNullable = false)]
    public string Statement { get; set; } = string.Empty;

    [SugarColumn(Length = 50, IsNullable = true)]
    public string? PublicResultCode { get; set; }

    [SugarColumn(Length = 1000, IsNullable = true)]
    public string? PublicResultSummary { get; set; }

    [SugarColumn(Length = 1000, IsNullable = true)]
    public string? InternalRemark { get; set; }

    [SugarColumn(Length = 160, IsNullable = false)]
    public string SubmissionOperationKey { get; set; } = string.Empty;

    [SugarColumn(Length = 160, IsNullable = true)]
    public string? DecisionOperationKey { get; set; }

    [SugarColumn(Length = 160, IsNullable = true)]
    public string? WithdrawalOperationKey { get; set; }

    public DateTime EligibleUntilUtc { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    [SugarColumn(IsNullable = true)]
    public DateTime? ReviewedAt { get; set; }
    [SugarColumn(IsNullable = true)]
    public DateTime? ResolvedAt { get; set; }
    [SugarColumn(IsNullable = true)]
    public DateTime? WithdrawnAt { get; set; }
    [SugarColumn(IsNullable = true)]
    public long? ReviewedById { get; set; }
    [SugarColumn(Length = 100, IsNullable = true)]
    public string? ReviewedByName { get; set; }

    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
    [SugarColumn(Length = 100, IsNullable = false)]
    public string CreateBy { get; set; } = "System";
    public long CreateId { get; set; }
    [SugarColumn(IsNullable = true)]
    public DateTime? ModifyTime { get; set; }
    [SugarColumn(Length = 100, IsNullable = true)]
    public string? ModifyBy { get; set; }
    [SugarColumn(IsNullable = true)]
    public long? ModifyId { get; set; }

    public static string GeneratePublicId() => $"{PublicIdPrefix}{Guid.CreateVersion7():N}";
}
