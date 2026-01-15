using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Model;

/// <summary>用户背包实体</summary>
/// <remarks>
/// 记录用户拥有的消耗品道具
/// 使用雪花 ID 作为主键
/// </remarks>
[SugarTable("ShopUserInventory")]
[SugarIndex("idx_inventory_user", nameof(UserId), OrderByType.Asc)]
[SugarIndex("idx_inventory_type", nameof(ConsumableType), OrderByType.Asc)]
[SugarIndex("idx_inventory_user_type", nameof(UserId), OrderByType.Asc, nameof(ConsumableType), OrderByType.Asc)]
public class UserInventory : RootEntityTKey<long>, IHasUserId
{
    /// <summary>初始化默认用户背包实例</summary>
    public UserInventory()
    {
        InitializeDefaults();
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        UserId = 0;
        ConsumableType = Shared.CustomEnum.ConsumableType.RenameCard;
        Quantity = 0;
        TenantId = 0;
        CreateTime = DateTime.Now;
        CreateBy = "System";
        CreateId = 0;
    }

    #region 用户信息

    /// <summary>用户 ID</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "用户ID")]
    public long UserId { get; set; } = 0;

    #endregion

    #region 道具信息

    /// <summary>消耗品类型</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "消耗品类型")]
    public ConsumableType ConsumableType { get; set; }

    /// <summary>道具值</summary>
    /// <remarks>
    /// 根据类型不同含义不同：
    /// - 经验卡：经验值数量
    /// - 萝卜币红包：萝卜币数量
    /// - 双倍经验卡：持续小时数
    /// - 置顶卡/高亮卡：持续小时数
    /// </remarks>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "道具值")]
    public string? ItemValue { get; set; }

    /// <summary>道具名称</summary>
    [SugarColumn(Length = 200, IsNullable = true, ColumnDescription = "道具名称")]
    public string? ItemName { get; set; }

    /// <summary>道具图标</summary>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "道具图标")]
    public string? ItemIcon { get; set; }

    /// <summary>数量</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "数量")]
    public int Quantity { get; set; } = 0;

    #endregion

    #region 来源信息

    /// <summary>来源商品 ID</summary>
    /// <remarks>最后一次购买的商品 ID</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "来源商品ID")]
    public long? SourceProductId { get; set; }

    #endregion

    #region 租户信息

    /// <summary>租户 Id</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "租户ID")]
    public long TenantId { get; set; } = 0;

    #endregion

    #region 审计信息

    /// <summary>创建时间</summary>
    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>创建者名称</summary>
    [SugarColumn(Length = 50, IsNullable = false, ColumnDescription = "创建者名称")]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建者 Id</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "创建者ID")]
    public long CreateId { get; set; } = 0;

    /// <summary>修改时间</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "修改时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }

    /// <summary>修改者名称</summary>
    [SugarColumn(Length = 50, IsNullable = true, ColumnDescription = "修改者名称")]
    public string? ModifyBy { get; set; }

    /// <summary>修改者 Id</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "修改者ID")]
    public long? ModifyId { get; set; }

    #endregion
}
