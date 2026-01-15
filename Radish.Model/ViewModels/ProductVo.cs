using Radish.Shared.CustomEnum;

namespace Radish.Model.ViewModels;

/// <summary>商品视图模型</summary>
public class ProductVo
{
    /// <summary>商品 ID</summary>
    public long Id { get; set; }

    /// <summary>商品名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>商品描述</summary>
    public string? Description { get; set; }

    /// <summary>商品图标</summary>
    public string? Icon { get; set; }

    /// <summary>商品封面图</summary>
    public string? CoverImage { get; set; }

    /// <summary>分类 ID</summary>
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>分类名称</summary>
    /// <remarks>运行时填充</remarks>
    public string? CategoryName { get; set; }

    /// <summary>商品类型</summary>
    public ProductType ProductType { get; set; }

    /// <summary>商品类型显示名称</summary>
    public string ProductTypeDisplay => ProductType switch
    {
        ProductType.Benefit => "权益",
        ProductType.Consumable => "消耗品",
        ProductType.Physical => "实物",
        _ => "未知"
    };

    /// <summary>权益类型</summary>
    public BenefitType? BenefitType { get; set; }

    /// <summary>消耗品类型</summary>
    public ConsumableType? ConsumableType { get; set; }

    /// <summary>权益/消耗品值</summary>
    public string? BenefitValue { get; set; }

    /// <summary>售价（胡萝卜）</summary>
    public long Price { get; set; }

    /// <summary>原价（胡萝卜）</summary>
    public long? OriginalPrice { get; set; }

    /// <summary>是否有折扣</summary>
    public bool HasDiscount => OriginalPrice.HasValue && OriginalPrice.Value > Price;

    /// <summary>折扣百分比</summary>
    /// <remarks>例如 80 表示 8 折</remarks>
    public int? DiscountPercent => HasDiscount && OriginalPrice > 0
        ? (int)(Price * 100 / OriginalPrice.Value)
        : null;

    /// <summary>库存类型</summary>
    public StockType StockType { get; set; }

    /// <summary>库存数量</summary>
    public int Stock { get; set; }

    /// <summary>已售数量</summary>
    public int SoldCount { get; set; }

    /// <summary>每人限购数量</summary>
    public int LimitPerUser { get; set; }

    /// <summary>是否有库存</summary>
    public bool InStock => StockType == StockType.Unlimited || Stock > 0;

    /// <summary>有效期类型</summary>
    public DurationType DurationType { get; set; }

    /// <summary>有效期天数</summary>
    public int? DurationDays { get; set; }

    /// <summary>固定到期时间</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>有效期显示文本</summary>
    public string DurationDisplay => DurationType switch
    {
        DurationType.Permanent => "永久",
        DurationType.Days => $"{DurationDays}天",
        DurationType.FixedDate => ExpiresAt?.ToString("yyyy-MM-dd") ?? "未知",
        _ => "未知"
    };

    /// <summary>排序权重</summary>
    public int SortOrder { get; set; }

    /// <summary>是否上架</summary>
    public bool IsOnSale { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; }

    /// <summary>上架时间</summary>
    public DateTime? OnSaleTime { get; set; }

    /// <summary>下架时间</summary>
    public DateTime? OffSaleTime { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }
}

/// <summary>商品列表项视图模型</summary>
/// <remarks>用于商品列表展示，字段较少</remarks>
public class ProductListItemVo
{
    /// <summary>商品 ID</summary>
    public long Id { get; set; }

    /// <summary>商品名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>商品图标</summary>
    public string? Icon { get; set; }

    /// <summary>商品封面图</summary>
    public string? CoverImage { get; set; }

    /// <summary>分类 ID</summary>
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>商品类型</summary>
    public ProductType ProductType { get; set; }

    /// <summary>售价（胡萝卜）</summary>
    public long Price { get; set; }

    /// <summary>原价（胡萝卜）</summary>
    public long? OriginalPrice { get; set; }

    /// <summary>是否有折扣</summary>
    public bool HasDiscount => OriginalPrice.HasValue && OriginalPrice.Value > Price;

    /// <summary>已售数量</summary>
    public int SoldCount { get; set; }

    /// <summary>是否有库存</summary>
    public bool InStock { get; set; }

    /// <summary>有效期显示文本</summary>
    public string DurationDisplay { get; set; } = string.Empty;
}

/// <summary>创建商品 DTO</summary>
public class CreateProductDto
{
    /// <summary>商品名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>商品描述</summary>
    public string? Description { get; set; }

    /// <summary>商品图标</summary>
    public string? Icon { get; set; }

    /// <summary>商品封面图</summary>
    public string? CoverImage { get; set; }

    /// <summary>分类 ID</summary>
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>商品类型</summary>
    public ProductType ProductType { get; set; }

    /// <summary>权益类型</summary>
    public BenefitType? BenefitType { get; set; }

    /// <summary>消耗品类型</summary>
    public ConsumableType? ConsumableType { get; set; }

    /// <summary>权益/消耗品值</summary>
    public string? BenefitValue { get; set; }

    /// <summary>售价（胡萝卜）</summary>
    public long Price { get; set; }

    /// <summary>原价（胡萝卜）</summary>
    public long? OriginalPrice { get; set; }

    /// <summary>库存类型</summary>
    public StockType StockType { get; set; } = StockType.Unlimited;

    /// <summary>库存数量</summary>
    public int Stock { get; set; }

    /// <summary>每人限购数量</summary>
    public int LimitPerUser { get; set; }

    /// <summary>有效期类型</summary>
    public DurationType DurationType { get; set; } = DurationType.Permanent;

    /// <summary>有效期天数</summary>
    public int? DurationDays { get; set; }

    /// <summary>固定到期时间</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>排序权重</summary>
    public int SortOrder { get; set; }

    /// <summary>是否上架</summary>
    public bool IsOnSale { get; set; }
}

/// <summary>更新商品 DTO</summary>
public class UpdateProductDto : CreateProductDto
{
    /// <summary>商品 ID</summary>
    public long Id { get; set; }
}
