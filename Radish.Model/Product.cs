using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Model;

/// <summary>商品实体</summary>
/// <remarks>
/// 商城商品信息，支持权益类、消耗品类商品
/// 使用雪花 ID 作为主键
/// </remarks>
[SugarTable("ShopProduct")]
[SugarIndex("idx_product_category", nameof(CategoryId), OrderByType.Asc)]
[SugarIndex("idx_product_type", nameof(ProductType), OrderByType.Asc)]
[SugarIndex("idx_product_status", nameof(IsOnSale), OrderByType.Asc, nameof(IsEnabled), OrderByType.Asc)]
[SugarIndex("idx_product_sort", nameof(SortOrder), OrderByType.Asc)]
public class Product : RootEntityTKey<long>
{
    /// <summary>初始化默认商品实例</summary>
    public Product()
    {
        InitializeDefaults();
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        Name = string.Empty;
        CategoryId = string.Empty;
        ProductType = ProductType.Benefit;
        Price = 0;
        OriginalPrice = null;
        StockType = StockType.Unlimited;
        Stock = 0;
        SoldCount = 0;
        LimitPerUser = 0;
        SortOrder = 0;
        IsOnSale = false;
        IsEnabled = true;
        TenantId = 0;
        CreateTime = DateTime.Now;
        CreateBy = "System";
        CreateId = 0;
    }

    #region 基础信息

    /// <summary>商品名称</summary>
    [SugarColumn(Length = 200, IsNullable = false, ColumnDescription = "商品名称")]
    public string Name { get; set; } = string.Empty;

    /// <summary>商品描述</summary>
    [SugarColumn(Length = 2000, IsNullable = true, ColumnDescription = "商品描述")]
    public string? Description { get; set; }

    /// <summary>商品图标</summary>
    /// <remarks>图标类名或图片 URL</remarks>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "商品图标")]
    public string? Icon { get; set; }

    /// <summary>商品封面图</summary>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "商品封面图")]
    public string? CoverImage { get; set; }

    /// <summary>分类 ID</summary>
    /// <remarks>关联 ProductCategory 表</remarks>
    [SugarColumn(Length = 50, IsNullable = false, ColumnDescription = "分类ID")]
    public string CategoryId { get; set; } = string.Empty;

    #endregion

    #region 商品类型

    /// <summary>商品类型</summary>
    /// <remarks>权益/消耗品/实物</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "商品类型")]
    public ProductType ProductType { get; set; } = ProductType.Benefit;

    /// <summary>权益类型</summary>
    /// <remarks>仅当 ProductType = Benefit 时有效</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "权益类型")]
    public BenefitType? BenefitType { get; set; }

    /// <summary>消耗品类型</summary>
    /// <remarks>仅当 ProductType = Consumable 时有效</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "消耗品类型")]
    public ConsumableType? ConsumableType { get; set; }

    /// <summary>权益/消耗品值</summary>
    /// <remarks>
    /// 根据类型不同含义不同：
    /// - 徽章/头像框/称号：资源标识符
    /// - 经验卡：经验值数量
    /// - 萝卜币红包：萝卜币数量
    /// - 双倍经验卡：持续小时数
    /// </remarks>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "权益值")]
    public string? BenefitValue { get; set; }

    #endregion

    #region 价格信息

    /// <summary>售价（胡萝卜）</summary>
    /// <remarks>以胡萝卜为单位</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "售价")]
    public long Price { get; set; } = 0;

    /// <summary>原价（胡萝卜）</summary>
    /// <remarks>用于显示划线价，可空表示无折扣</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "原价")]
    public long? OriginalPrice { get; set; }

    #endregion

    #region 库存信息

    /// <summary>库存类型</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "库存类型")]
    public StockType StockType { get; set; } = StockType.Unlimited;

    /// <summary>库存数量</summary>
    /// <remarks>仅当 StockType = Limited 时有效</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "库存数量")]
    public int Stock { get; set; } = 0;

    /// <summary>已售数量</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "已售数量")]
    public int SoldCount { get; set; } = 0;

    /// <summary>每人限购数量</summary>
    /// <remarks>0 表示不限购</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "每人限购")]
    public int LimitPerUser { get; set; } = 0;

    #endregion

    #region 有效期配置

    /// <summary>有效期类型</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "有效期类型")]
    public DurationType DurationType { get; set; } = DurationType.Permanent;

    /// <summary>有效期天数</summary>
    /// <remarks>仅当 DurationType = Days 时有效</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "有效期天数")]
    public int? DurationDays { get; set; }

    /// <summary>固定到期时间</summary>
    /// <remarks>仅当 DurationType = FixedDate 时有效</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "固定到期时间")]
    public DateTime? ExpiresAt { get; set; }

    #endregion

    #region 状态控制

    /// <summary>排序权重</summary>
    /// <remarks>数值越小越靠前</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "排序权重")]
    public int SortOrder { get; set; } = 0;

    /// <summary>是否上架</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "是否上架")]
    public bool IsOnSale { get; set; } = false;

    /// <summary>是否启用</summary>
    /// <remarks>禁用后不显示在商城</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "是否启用")]
    public bool IsEnabled { get; set; } = true;

    /// <summary>上架时间</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "上架时间")]
    public DateTime? OnSaleTime { get; set; }

    /// <summary>下架时间</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "下架时间")]
    public DateTime? OffSaleTime { get; set; }

    #endregion

    #region 乐观锁

    /// <summary>乐观锁版本号</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "乐观锁版本号")]
    public int Version { get; set; } = 0;

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

    /// <summary>是否删除</summary>
    /// <remarks>不可为空，默认为 false，软删除标记</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "是否删除")]
    public bool IsDeleted { get; set; } = false;

    #endregion
}
