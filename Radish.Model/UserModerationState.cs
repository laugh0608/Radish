using Radish.Model.Root;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Model;

/// <summary>用户在单一治理策略维度上的权威当前状态。</summary>
[SugarTable("UserModerationState")]
[SugarIndex("idx_user_moderation_state_unique", nameof(TenantId), OrderByType.Asc, nameof(TargetUserId), OrderByType.Asc, nameof(PolicyType), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_user_moderation_state_expiry", nameof(TenantId), OrderByType.Asc, nameof(State), OrderByType.Asc, nameof(EffectiveUntil), OrderByType.Asc)]
public sealed class UserModerationState : RootEntityTKey<long>, ITenantEntity
{
    public long TenantId { get; set; }
    public long TargetUserId { get; set; }
    public int PolicyType { get; set; }
    public int State { get; set; } = (int)UserModerationStateValue.Inactive;
    [SugarColumn(IsNullable = true)]
    public DateTime? EffectiveFrom { get; set; }
    [SugarColumn(IsNullable = true)]
    public DateTime? EffectiveUntil { get; set; }
    public int Version { get; set; } = 1;
    [SugarColumn(IsNullable = true)]
    public long? CurrentActionId { get; set; }
    [SugarColumn(IsNullable = true)]
    public long? SourceCaseId { get; set; }
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
}
