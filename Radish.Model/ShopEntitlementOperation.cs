using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Model;

/// <summary>商城权益与消耗品业务操作流水。</summary>
/// <remarks>只记录已经在同一事务内成功完成的业务动作，长期保留幂等结果和效果资源。</remarks>
[SugarTable("ShopEntitlementOperation")]
[SugarIndex(
    "idx_shop_entitlement_operation_idempotency",
    nameof(TenantId), OrderByType.Asc,
    nameof(UserId), OrderByType.Asc,
    nameof(OperationType), OrderByType.Asc,
    nameof(IdempotencyKey), OrderByType.Asc,
    IsUnique = true)]
[SugarIndex(
    "idx_shop_entitlement_operation_user_time",
    nameof(TenantId), OrderByType.Asc,
    nameof(UserId), OrderByType.Asc,
    nameof(CreateTime), OrderByType.Desc)]
[SugarIndex(
    "idx_shop_entitlement_operation_inventory_time",
    nameof(TenantId), OrderByType.Asc,
    nameof(InventoryId), OrderByType.Asc,
    nameof(CreateTime), OrderByType.Desc)]
[SugarIndex(
    "idx_shop_entitlement_operation_benefit_time",
    nameof(TenantId), OrderByType.Asc,
    nameof(BenefitId), OrderByType.Asc,
    nameof(CreateTime), OrderByType.Desc)]
public class ShopEntitlementOperation : RootEntityTKey<long>, IHasUserId, ITenantEntity
{
    /// <summary>租户 ID。</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "租户ID")]
    public long TenantId { get; set; }

    /// <summary>发起用户 ID。</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "用户ID")]
    public long UserId { get; set; }

    /// <summary>背包项 ID。</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "背包项ID")]
    public long? InventoryId { get; set; }

    /// <summary>持续权益 ID。</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "持续权益ID")]
    public long? BenefitId { get; set; }

    /// <summary>相关权益 ID，例如切换前的选择。</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "相关持续权益ID")]
    public long? RelatedBenefitId { get; set; }

    /// <summary>操作类型。</summary>
    [SugarColumn(Length = 40, IsNullable = false, ColumnDescription = "操作类型")]
    public string OperationType { get; set; } = string.Empty;

    /// <summary>消耗品类型快照。</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "消耗品类型")]
    public ConsumableType? ConsumableType { get; set; }

    /// <summary>权益类型快照。</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "权益类型")]
    public BenefitType? BenefitType { get; set; }

    /// <summary>使用数量。</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "使用数量")]
    public int? Quantity { get; set; }

    /// <summary>道具值快照。</summary>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "道具值快照")]
    public string? ItemValue { get; set; }

    /// <summary>权益值快照。</summary>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "权益值快照")]
    public string? BenefitValue { get; set; }

    /// <summary>操作原因。</summary>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "操作原因")]
    public string? Reason { get; set; }

    /// <summary>可选目标资源类型。</summary>
    [SugarColumn(Length = 40, IsNullable = true, ColumnDescription = "目标资源类型")]
    public string? TargetType { get; set; }

    /// <summary>可选目标资源 ID。</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "目标资源ID")]
    public long? TargetId { get; set; }

    /// <summary>客户端幂等键。</summary>
    [SugarColumn(Length = 80, IsNullable = false, ColumnDescription = "幂等键")]
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>安全请求摘要的 SHA-256。</summary>
    [SugarColumn(Length = 64, IsNullable = false, ColumnDescription = "请求摘要Hash")]
    public string RequestHash { get; set; } = string.Empty;

    /// <summary>效果类型。</summary>
    [SugarColumn(Length = 40, IsNullable = false, ColumnDescription = "效果类型")]
    public string EffectType { get; set; } = string.Empty;

    /// <summary>效果值。</summary>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "效果值")]
    public string? EffectValue { get; set; }

    /// <summary>效果产生的业务资源类型。</summary>
    [SugarColumn(Length = 40, IsNullable = true, ColumnDescription = "效果资源类型")]
    public string? EffectResourceType { get; set; }

    /// <summary>效果产生的业务资源 ID。</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "效果资源ID")]
    public long? EffectResourceId { get; set; }

    /// <summary>效果产生的业务资源编号。</summary>
    [SugarColumn(Length = 100, IsNullable = true, ColumnDescription = "效果资源编号")]
    public string? EffectResourceNo { get; set; }

    /// <summary>可安全回放的结构化结果。</summary>
    [SugarColumn(Length = 4000, IsNullable = false, ColumnDescription = "结果载荷")]
    public string ResultPayload { get; set; } = string.Empty;

    /// <summary>创建时间。</summary>
    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>创建者名称。</summary>
    [SugarColumn(Length = 50, IsNullable = false, ColumnDescription = "创建者名称")]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建者 ID。</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "创建者ID")]
    public long CreateId { get; set; }
}
