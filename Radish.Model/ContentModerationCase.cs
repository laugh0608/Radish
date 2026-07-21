using Radish.Model.Root;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Model;

/// <summary>同一目标在一个处理周期内的权威治理案件。</summary>
[SugarTable("ContentModerationCase")]
[SugarIndex("idx_moderation_case_public_id", nameof(PublicId), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_moderation_case_open_target", nameof(TenantId), OrderByType.Asc, nameof(OpenTargetKey), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_moderation_case_queue", nameof(TenantId), OrderByType.Asc, nameof(Status), OrderByType.Asc, nameof(ModifyTime), OrderByType.Desc)]
[SugarIndex("idx_moderation_case_decision_operation", nameof(TenantId), OrderByType.Asc, nameof(DecisionOperationKey), OrderByType.Asc, IsUnique = true)]
public sealed class ContentModerationCase : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    public const string PublicIdPrefix = "mod_";

    public long TenantId { get; set; }

    [SugarColumn(Length = 40, IsNullable = false)]
    public string PublicId { get; set; } = GeneratePublicId();

    public int TargetType { get; set; }
    public long TargetContentId { get; set; }
    public long TargetUserId { get; set; }

    [SugarColumn(Length = 80, IsNullable = true)]
    public string? OpenTargetKey { get; set; }

    public int Status { get; set; } = (int)ContentModerationCaseStatus.Open;
    public int Decision { get; set; } = (int)ContentModerationDecision.None;
    public int TargetDisposition { get; set; } = (int)ContentModerationTargetDisposition.None;
    public int Version { get; set; } = 1;

    [SugarColumn(Length = 160, IsNullable = true)]
    public string? DecisionOperationKey { get; set; }

    [SugarColumn(Length = 50, IsNullable = true)]
    public string? PublicResultCode { get; set; }

    [SugarColumn(Length = 1000, IsNullable = true)]
    public string? InternalRemark { get; set; }

    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    [SugarColumn(IsNullable = true)]
    public DateTime? ResolvedAt { get; set; }
    [SugarColumn(IsNullable = true)]
    public long? ResolvedById { get; set; }
    [SugarColumn(Length = 100, IsNullable = true)]
    public string? ResolvedByName { get; set; }

    public bool IsDeleted { get; set; }
    [SugarColumn(IsNullable = true)]
    public DateTime? DeletedAt { get; set; }
    [SugarColumn(Length = 100, IsNullable = true)]
    public string? DeletedBy { get; set; }
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
