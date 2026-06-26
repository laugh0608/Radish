using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Model;

/// <summary>用户背包发放流水</summary>
/// <remarks>记录订单消耗品发放事实，聚合背包行只保存当前数量。</remarks>
[SugarTable("ShopUserInventoryGrantRecord")]
[SugarIndex("idx_inventory_grant_tenant_source_order", nameof(TenantId), OrderByType.Asc, nameof(SourceOrderId), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_inventory_grant_inventory", nameof(TenantId), OrderByType.Asc, nameof(InventoryId), OrderByType.Asc)]
public class UserInventoryGrantRecord : RootEntityTKey<long>, IHasUserId, ITenantEntity
{
    /// <summary>用户 ID</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "用户ID")]
    public long UserId { get; set; }

    /// <summary>背包项 ID</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "背包项ID")]
    public long InventoryId { get; set; }

    /// <summary>来源订单 ID</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "来源订单ID")]
    public long SourceOrderId { get; set; }

    /// <summary>来源商品 ID</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "来源商品ID")]
    public long SourceProductId { get; set; }

    /// <summary>消耗品类型</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "消耗品类型")]
    public ConsumableType ConsumableType { get; set; }

    /// <summary>道具值快照</summary>
    [SugarColumn(Length = 500, IsNullable = false, ColumnDescription = "道具值快照")]
    public string ItemValue { get; set; } = string.Empty;

    /// <summary>本次发放数量</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "本次发放数量")]
    public int Quantity { get; set; }

    /// <summary>租户 Id</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "租户ID")]
    public long TenantId { get; set; }

    /// <summary>创建时间</summary>
    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>创建者名称</summary>
    [SugarColumn(Length = 50, IsNullable = false, ColumnDescription = "创建者名称")]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建者 Id</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "创建者ID")]
    public long CreateId { get; set; }

    /// <summary>修改时间</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "修改时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }
}
