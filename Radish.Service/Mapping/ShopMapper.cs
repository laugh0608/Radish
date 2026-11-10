using System.Diagnostics.CodeAnalysis;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;
using Riok.Mapperly.Abstractions;

namespace Radish.Service.Mapping;

/// <summary>业务映射；字段忽略项保留服务补全、写入保护和既有默认值。</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target, UseDeepCloning = true,
    ThrowOnMappingNullMismatch = false, AutoUserMappings = false)]
internal static partial class ShopMapper
{
    // Order → OrderListItemVo
    [MapProperty(nameof(Order.Id), nameof(OrderListItemVo.VoId))]
    [MapProperty(nameof(Order.OrderNo), nameof(OrderListItemVo.VoOrderNo))]
    [MapProperty(nameof(Order.ProductName), nameof(OrderListItemVo.VoProductName))]
    [MapProperty(nameof(Order.ProductIconAttachmentId), nameof(OrderListItemVo.VoProductIconAttachmentId))]
    [MapperIgnoreTarget(nameof(OrderListItemVo.VoProductIcon))]
    [MapProperty(nameof(Order.Quantity), nameof(OrderListItemVo.VoQuantity))]
    [MapProperty(nameof(Order.TotalPrice), nameof(OrderListItemVo.VoTotalPrice))]
    [MapProperty(nameof(Order.Status), nameof(OrderListItemVo.VoStatus))]
    [MapProperty(nameof(Order.FailureStage), nameof(OrderListItemVo.VoFailureStage))]
    [MapProperty(nameof(Order.CreateTime), nameof(OrderListItemVo.VoCreateTime))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial OrderListItemVo? ToOrderListItemVo(Order? source);

    public static partial List<OrderListItemVo> ToOrderListItemVoList(IEnumerable<Order>? source);

    // Order → OrderVo
    [UserMapping(Default = true)]
    [return: NotNullIfNotNull(nameof(source))]
    public static OrderVo? ToOrderVo(Order? source)
    {
        if (source is null) return null;
        var target = MapOrderToOrderVo(source);
        target.VoDurationDisplay = GetDurationDisplay(source.DurationType, source.DurationDays, source.BenefitExpiresAt);
        return target;
    }

    [MapProperty(nameof(Order.Id), nameof(OrderVo.VoId))]
    [MapProperty(nameof(Order.OrderNo), nameof(OrderVo.VoOrderNo))]
    [MapProperty(nameof(Order.UserId), nameof(OrderVo.VoUserId))]
    [MapperIgnoreTarget(nameof(OrderVo.VoUserName))]
    [MapProperty(nameof(Order.ProductId), nameof(OrderVo.VoProductId))]
    [MapProperty(nameof(Order.ProductName), nameof(OrderVo.VoProductName))]
    [MapProperty(nameof(Order.ProductIconAttachmentId), nameof(OrderVo.VoProductIconAttachmentId))]
    [MapperIgnoreTarget(nameof(OrderVo.VoProductIcon))]
    [MapProperty(nameof(Order.ProductType), nameof(OrderVo.VoProductType))]
    [MapProperty(nameof(Order.BenefitType), nameof(OrderVo.VoBenefitType))]
    [MapProperty(nameof(Order.ConsumableType), nameof(OrderVo.VoConsumableType))]
    [MapProperty(nameof(Order.Quantity), nameof(OrderVo.VoQuantity))]
    [MapProperty(nameof(Order.UnitPrice), nameof(OrderVo.VoUnitPrice))]
    [MapProperty(nameof(Order.TotalPrice), nameof(OrderVo.VoTotalPrice))]
    [MapProperty(nameof(Order.Status), nameof(OrderVo.VoStatus))]
    [MapProperty(nameof(Order.FailureStage), nameof(OrderVo.VoFailureStage))]
    [MapProperty(nameof(Order.CoinTransactionId), nameof(OrderVo.VoCoinTransactionId))]
    [MapProperty(nameof(Order.GrantedBenefitId), nameof(OrderVo.VoGrantedBenefitId))]
    [MapProperty(nameof(Order.GrantedInventoryId), nameof(OrderVo.VoGrantedInventoryId))]
    [MapProperty(nameof(Order.BenefitExpiresAt), nameof(OrderVo.VoBenefitExpiresAt))]
    [MapProperty(nameof(Order.FixedExpiresAt), nameof(OrderVo.VoFixedExpiresAt))]
    [MapProperty(nameof(Order.DurationType), nameof(OrderVo.VoDurationType))]
    [MapProperty(nameof(Order.DurationDays), nameof(OrderVo.VoDurationDays))]
    [MapperIgnoreTarget(nameof(OrderVo.VoDurationDisplay))]
    [MapProperty(nameof(Order.CreateTime), nameof(OrderVo.VoCreateTime))]
    [MapProperty(nameof(Order.PaidTime), nameof(OrderVo.VoPaidTime))]
    [MapProperty(nameof(Order.CompletedTime), nameof(OrderVo.VoCompletedTime))]
    [MapProperty(nameof(Order.CancelledTime), nameof(OrderVo.VoCancelledTime))]
    [MapProperty(nameof(Order.CancelReason), nameof(OrderVo.VoCancelReason))]
    [MapProperty(nameof(Order.FailReason), nameof(OrderVo.VoFailReason))]
    [MapProperty(nameof(Order.UserRemark), nameof(OrderVo.VoUserRemark))]
    [MapProperty(nameof(Order.AdminRemark), nameof(OrderVo.VoAdminRemark))]
    [UserMapping(Default = false)]
    private static partial OrderVo MapOrderToOrderVo(Order source);

    public static partial List<OrderVo> ToOrderVoList(IEnumerable<Order>? source);

    // Product → ProductListItemVo
    [UserMapping(Default = true)]
    [return: NotNullIfNotNull(nameof(source))]
    public static ProductListItemVo? ToProductListItemVo(Product? source)
    {
        if (source is null) return null;
        var target = MapProductToProductListItemVo(source);
        target.VoInStock = source.StockType == StockType.Unlimited || source.Stock > 0;
        target.VoDurationDisplay = GetDurationDisplay(source.DurationType, source.DurationDays, source.ExpiresAt);
        return target;
    }

    [MapProperty(nameof(Product.Id), nameof(ProductListItemVo.VoId))]
    [MapProperty(nameof(Product.Name), nameof(ProductListItemVo.VoName))]
    [MapProperty(nameof(Product.IconAttachmentId), nameof(ProductListItemVo.VoIconAttachmentId))]
    [MapperIgnoreTarget(nameof(ProductListItemVo.VoIcon))]
    [MapProperty(nameof(Product.CoverAttachmentId), nameof(ProductListItemVo.VoCoverAttachmentId))]
    [MapperIgnoreTarget(nameof(ProductListItemVo.VoCoverImage))]
    [MapProperty(nameof(Product.CategoryId), nameof(ProductListItemVo.VoCategoryId))]
    [MapProperty(nameof(Product.ProductType), nameof(ProductListItemVo.VoProductType))]
    [MapProperty(nameof(Product.Price), nameof(ProductListItemVo.VoPrice))]
    [MapProperty(nameof(Product.OriginalPrice), nameof(ProductListItemVo.VoOriginalPrice))]
    [MapProperty(nameof(Product.SoldCount), nameof(ProductListItemVo.VoSoldCount))]
    [MapperIgnoreTarget(nameof(ProductListItemVo.VoInStock))]
    [MapperIgnoreTarget(nameof(ProductListItemVo.VoDurationDisplay))]
    [UserMapping(Default = false)]
    private static partial ProductListItemVo MapProductToProductListItemVo(Product source);

    public static partial List<ProductListItemVo> ToProductListItemVoList(IEnumerable<Product>? source);

    // Product → ProductVo
    [MapProperty(nameof(Product.Id), nameof(ProductVo.VoId))]
    [MapProperty(nameof(Product.Name), nameof(ProductVo.VoName))]
    [MapProperty(nameof(Product.Description), nameof(ProductVo.VoDescription))]
    [MapProperty(nameof(Product.IconAttachmentId), nameof(ProductVo.VoIconAttachmentId))]
    [MapperIgnoreTarget(nameof(ProductVo.VoIcon))]
    [MapProperty(nameof(Product.CoverAttachmentId), nameof(ProductVo.VoCoverAttachmentId))]
    [MapperIgnoreTarget(nameof(ProductVo.VoCoverImage))]
    [MapProperty(nameof(Product.CategoryId), nameof(ProductVo.VoCategoryId))]
    [MapperIgnoreTarget(nameof(ProductVo.VoCategoryName))]
    [MapProperty(nameof(Product.ProductType), nameof(ProductVo.VoProductType))]
    [MapProperty(nameof(Product.BenefitType), nameof(ProductVo.VoBenefitType))]
    [MapProperty(nameof(Product.ConsumableType), nameof(ProductVo.VoConsumableType))]
    [MapProperty(nameof(Product.BenefitValue), nameof(ProductVo.VoBenefitValue))]
    [MapProperty(nameof(Product.Price), nameof(ProductVo.VoPrice))]
    [MapProperty(nameof(Product.OriginalPrice), nameof(ProductVo.VoOriginalPrice))]
    [MapProperty(nameof(Product.StockType), nameof(ProductVo.VoStockType))]
    [MapProperty(nameof(Product.Stock), nameof(ProductVo.VoStock))]
    [MapProperty(nameof(Product.SoldCount), nameof(ProductVo.VoSoldCount))]
    [MapProperty(nameof(Product.LimitPerUser), nameof(ProductVo.VoLimitPerUser))]
    [MapProperty(nameof(Product.DurationType), nameof(ProductVo.VoDurationType))]
    [MapProperty(nameof(Product.DurationDays), nameof(ProductVo.VoDurationDays))]
    [MapProperty(nameof(Product.ExpiresAt), nameof(ProductVo.VoExpiresAt))]
    [MapProperty(nameof(Product.SortOrder), nameof(ProductVo.VoSortOrder))]
    [MapProperty(nameof(Product.IsOnSale), nameof(ProductVo.VoIsOnSale))]
    [MapProperty(nameof(Product.IsEnabled), nameof(ProductVo.VoIsEnabled))]
    [MapProperty(nameof(Product.OnSaleTime), nameof(ProductVo.VoOnSaleTime))]
    [MapProperty(nameof(Product.OffSaleTime), nameof(ProductVo.VoOffSaleTime))]
    [MapProperty(nameof(Product.Version), nameof(ProductVo.VoVersion))]
    [MapProperty(nameof(Product.CreateTime), nameof(ProductVo.VoCreateTime))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial ProductVo? ToProductVo(Product? source);

    public static partial List<ProductVo> ToProductVoList(IEnumerable<Product>? source);

    // ProductCategory → ProductCategoryVo
    [MapProperty(nameof(ProductCategory.Id), nameof(ProductCategoryVo.VoId))]
    [MapProperty(nameof(ProductCategory.Name), nameof(ProductCategoryVo.VoName))]
    [MapProperty(nameof(ProductCategory.IconAttachmentId), nameof(ProductCategoryVo.VoIconAttachmentId))]
    [MapperIgnoreTarget(nameof(ProductCategoryVo.VoIcon))]
    [MapProperty(nameof(ProductCategory.Description), nameof(ProductCategoryVo.VoDescription))]
    [MapProperty(nameof(ProductCategory.SortOrder), nameof(ProductCategoryVo.VoSortOrder))]
    [MapProperty(nameof(ProductCategory.IsEnabled), nameof(ProductCategoryVo.VoIsEnabled))]
    [MapperIgnoreTarget(nameof(ProductCategoryVo.VoProductCount))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial ProductCategoryVo? ToProductCategoryVo(ProductCategory? source);

    public static partial List<ProductCategoryVo> ToProductCategoryVoList(IEnumerable<ProductCategory>? source);

    // UserBenefit → UserBenefitVo
    [MapProperty(nameof(UserBenefit.Id), nameof(UserBenefitVo.VoId))]
    [MapProperty(nameof(UserBenefit.UserId), nameof(UserBenefitVo.VoUserId))]
    [MapProperty(nameof(UserBenefit.BenefitType), nameof(UserBenefitVo.VoBenefitType))]
    [MapProperty(nameof(UserBenefit.BenefitValue), nameof(UserBenefitVo.VoBenefitValue))]
    [MapProperty(nameof(UserBenefit.BenefitName), nameof(UserBenefitVo.VoBenefitName))]
    [MapProperty(nameof(UserBenefit.BenefitIconAttachmentId), nameof(UserBenefitVo.VoBenefitIconAttachmentId))]
    [MapperIgnoreTarget(nameof(UserBenefitVo.VoBenefitIcon))]
    [MapProperty(nameof(UserBenefit.SourceOrderId), nameof(UserBenefitVo.VoSourceOrderId))]
    [MapProperty(nameof(UserBenefit.SourceProductId), nameof(UserBenefitVo.VoSourceProductId))]
    [MapProperty(nameof(UserBenefit.SourceType), nameof(UserBenefitVo.VoSourceType))]
    [MapProperty(nameof(UserBenefit.DurationType), nameof(UserBenefitVo.VoDurationType))]
    [MapProperty(nameof(UserBenefit.EffectiveAt), nameof(UserBenefitVo.VoEffectiveAt))]
    [MapProperty(nameof(UserBenefit.ExpiresAt), nameof(UserBenefitVo.VoExpiresAt))]
    [MapProperty(nameof(UserBenefit.IsExpired), nameof(UserBenefitVo.VoIsExpired))]
    [MapProperty(nameof(UserBenefit.IsActive), nameof(UserBenefitVo.VoIsActive))]
    [MapperIgnoreTarget(nameof(UserBenefitVo.VoStatus))]
    [MapperIgnoreTarget(nameof(UserBenefitVo.VoCanActivate))]
    [MapperIgnoreTarget(nameof(UserBenefitVo.VoCanDeactivate))]
    [MapperIgnoreTarget(nameof(UserBenefitVo.VoUnavailableReason))]
    [MapProperty(nameof(UserBenefit.RevokedAt), nameof(UserBenefitVo.VoRevokedAt))]
    [MapProperty(nameof(UserBenefit.RevokedByName), nameof(UserBenefitVo.VoRevokedByName))]
    [MapProperty(nameof(UserBenefit.RevocationReason), nameof(UserBenefitVo.VoRevocationReason))]
    [MapperIgnoreTarget(nameof(UserBenefitVo.VoDurationDisplay))]
    [MapProperty(nameof(UserBenefit.CreateTime), nameof(UserBenefitVo.VoCreateTime))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial UserBenefitVo? ToUserBenefitVo(UserBenefit? source);

    public static partial List<UserBenefitVo> ToUserBenefitVoList(IEnumerable<UserBenefit>? source);

    // UserInventory → UserInventoryVo
    [MapProperty(nameof(UserInventory.Id), nameof(UserInventoryVo.VoId))]
    [MapProperty(nameof(UserInventory.UserId), nameof(UserInventoryVo.VoUserId))]
    [MapProperty(nameof(UserInventory.ConsumableType), nameof(UserInventoryVo.VoConsumableType))]
    [MapProperty(nameof(UserInventory.ItemValue), nameof(UserInventoryVo.VoItemValue))]
    [MapProperty(nameof(UserInventory.ItemName), nameof(UserInventoryVo.VoItemName))]
    [MapProperty(nameof(UserInventory.ItemIconAttachmentId), nameof(UserInventoryVo.VoItemIconAttachmentId))]
    [MapperIgnoreTarget(nameof(UserInventoryVo.VoItemIcon))]
    [MapProperty(nameof(UserInventory.Quantity), nameof(UserInventoryVo.VoQuantity))]
    [MapProperty(nameof(UserInventory.SourceProductId), nameof(UserInventoryVo.VoSourceProductId))]
    [MapProperty(nameof(UserInventory.CreateTime), nameof(UserInventoryVo.VoCreateTime))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial UserInventoryVo? ToUserInventoryVo(UserInventory? source);

    public static partial List<UserInventoryVo> ToUserInventoryVoList(IEnumerable<UserInventory>? source);

    // CreateProductDto → Product
    [MapperIgnoreTarget(nameof(Product.SoldCount))]
    [MapperIgnoreTarget(nameof(Product.IsOnSale))]
    [MapperIgnoreTarget(nameof(Product.ModerationTargetActionId))]
    [MapperIgnoreTarget(nameof(Product.IsEnabled))]
    [MapperIgnoreTarget(nameof(Product.OnSaleTime))]
    [MapperIgnoreTarget(nameof(Product.OffSaleTime))]
    [MapperIgnoreTarget(nameof(Product.Version))]
    [MapperIgnoreTarget(nameof(Product.TenantId))]
    [MapperIgnoreTarget(nameof(Product.CreateTime))]
    [MapperIgnoreTarget(nameof(Product.CreateBy))]
    [MapperIgnoreTarget(nameof(Product.CreateId))]
    [MapperIgnoreTarget(nameof(Product.ModifyTime))]
    [MapperIgnoreTarget(nameof(Product.ModifyBy))]
    [MapperIgnoreTarget(nameof(Product.ModifyId))]
    [MapperIgnoreTarget(nameof(Product.IsDeleted))]
    [MapperIgnoreTarget(nameof(Product.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial Product? ToProduct(CreateProductDto? source);

    public static partial List<Product> ToProductList(IEnumerable<CreateProductDto>? source);

    // UpdateProductDto → Product
    [MapperIgnoreTarget(nameof(Product.SoldCount))]
    [MapperIgnoreTarget(nameof(Product.IsOnSale))]
    [MapperIgnoreTarget(nameof(Product.ModerationTargetActionId))]
    [MapperIgnoreTarget(nameof(Product.IsEnabled))]
    [MapperIgnoreTarget(nameof(Product.OnSaleTime))]
    [MapperIgnoreTarget(nameof(Product.OffSaleTime))]
    [MapperIgnoreTarget(nameof(Product.Version))]
    [MapperIgnoreTarget(nameof(Product.TenantId))]
    [MapperIgnoreTarget(nameof(Product.CreateTime))]
    [MapperIgnoreTarget(nameof(Product.CreateBy))]
    [MapperIgnoreTarget(nameof(Product.CreateId))]
    [MapperIgnoreTarget(nameof(Product.ModifyTime))]
    [MapperIgnoreTarget(nameof(Product.ModifyBy))]
    [MapperIgnoreTarget(nameof(Product.ModifyId))]
    [MapperIgnoreTarget(nameof(Product.IsDeleted))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial Product? ToProduct(UpdateProductDto? source);

    public static partial List<Product> ToProductList(IEnumerable<UpdateProductDto>? source);

    [MapperIgnoreTarget(nameof(Product.SoldCount))]
    [MapperIgnoreTarget(nameof(Product.IsOnSale))]
    [MapperIgnoreTarget(nameof(Product.ModerationTargetActionId))]
    [MapperIgnoreTarget(nameof(Product.IsEnabled))]
    [MapperIgnoreTarget(nameof(Product.OnSaleTime))]
    [MapperIgnoreTarget(nameof(Product.OffSaleTime))]
    [MapperIgnoreTarget(nameof(Product.Version))]
    [MapperIgnoreTarget(nameof(Product.TenantId))]
    [MapperIgnoreTarget(nameof(Product.CreateTime))]
    [MapperIgnoreTarget(nameof(Product.CreateBy))]
    [MapperIgnoreTarget(nameof(Product.CreateId))]
    [MapperIgnoreTarget(nameof(Product.ModifyTime))]
    [MapperIgnoreTarget(nameof(Product.ModifyBy))]
    [MapperIgnoreTarget(nameof(Product.ModifyId))]
    [MapperIgnoreTarget(nameof(Product.IsDeleted))]
    public static partial void UpdateProduct(UpdateProductDto source, Product target);

    private static string GetDurationDisplay(DurationType durationType, int? durationDays, DateTime? expiresAt)
    {
        return durationType switch
        {
            DurationType.Permanent => "永久",
            DurationType.Days => $"{durationDays}天",
            DurationType.FixedDate => expiresAt?.ToString("yyyy-MM-dd") ?? "未知",
            _ => "未知"
        };
    }

}
