using AutoMapper;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>商城系统实体映射配置</summary>
public class ShopProfile : Profile
{
    public ShopProfile()
    {
        ConfigureProductCategoryMapping();
        ConfigureProductMapping();
        ConfigureOrderMapping();
        ConfigureUserBenefitMapping();
        ConfigureUserInventoryMapping();
    }

    /// <summary>配置商品分类映射</summary>
    private void ConfigureProductCategoryMapping()
    {
        // ProductCategory -> ProductCategoryVo
        CreateMap<ProductCategory, ProductCategoryVo>()
            .ForMember(dest => dest.ProductCount, opt => opt.Ignore()); // 运行时计算

        // ProductCategoryVo -> ProductCategory
        CreateMap<ProductCategoryVo, ProductCategory>()
            .ForMember(dest => dest.CreateTime, opt => opt.Ignore())
            .ForMember(dest => dest.ModifyTime, opt => opt.Ignore());
    }

    /// <summary>配置商品映射</summary>
    private void ConfigureProductMapping()
    {
        // Product -> ProductVo (使用前缀识别 + 手动配置特殊字段)
        RecognizeDestinationPrefixes("Vo");
        CreateMap<Product, ProductVo>()
            .ForMember(dest => dest.VoCategoryName, opt => opt.Ignore()); // 运行时填充

        // Product -> ProductListItemVo (使用前缀识别 + 手动配置特殊字段)
        RecognizeDestinationPrefixes("Vo");
        CreateMap<Product, ProductListItemVo>()
            .ForMember(dest => dest.VoInStock, opt => opt.MapFrom(src =>
                src.StockType == StockType.Unlimited || src.Stock > 0))
            .ForMember(dest => dest.VoDurationDisplay, opt => opt.MapFrom(src =>
                GetDurationDisplay(src.DurationType, src.DurationDays, src.ExpiresAt)));

        // CreateProductDto -> Product
        CreateMap<CreateProductDto, Product>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.SoldCount, opt => opt.Ignore())
            .ForMember(dest => dest.Version, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreateTime, opt => opt.Ignore())
            .ForMember(dest => dest.CreateBy, opt => opt.Ignore())
            .ForMember(dest => dest.CreateId, opt => opt.Ignore())
            .ForMember(dest => dest.ModifyTime, opt => opt.Ignore())
            .ForMember(dest => dest.ModifyBy, opt => opt.Ignore())
            .ForMember(dest => dest.ModifyId, opt => opt.Ignore())
            .ForMember(dest => dest.OnSaleTime, opt => opt.Ignore())
            .ForMember(dest => dest.OffSaleTime, opt => opt.Ignore());

        // UpdateProductDto -> Product
        CreateMap<UpdateProductDto, Product>()
            .ForMember(dest => dest.SoldCount, opt => opt.Ignore())
            .ForMember(dest => dest.Version, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreateTime, opt => opt.Ignore())
            .ForMember(dest => dest.CreateBy, opt => opt.Ignore())
            .ForMember(dest => dest.CreateId, opt => opt.Ignore())
            .ForMember(dest => dest.ModifyTime, opt => opt.Ignore())
            .ForMember(dest => dest.ModifyBy, opt => opt.Ignore())
            .ForMember(dest => dest.ModifyId, opt => opt.Ignore())
            .ForMember(dest => dest.OnSaleTime, opt => opt.Ignore())
            .ForMember(dest => dest.OffSaleTime, opt => opt.Ignore());
    }

    /// <summary>配置订单映射</summary>
    private void ConfigureOrderMapping()
    {
        // Order -> OrderVo (使用前缀识别 + 手动配置特殊字段)
        RecognizeDestinationPrefixes("Vo");
        CreateMap<Order, OrderVo>()
            .ForMember(dest => dest.VoUserName, opt => opt.Ignore()) // 运行时填充
            .ForMember(dest => dest.VoDurationDisplay, opt => opt.MapFrom(src =>
                GetDurationDisplay(src.DurationType, src.DurationDays, src.BenefitExpiresAt)));

        // Order -> OrderListItemVo (使用前缀识别自动映射)
        RecognizeDestinationPrefixes("Vo");
        CreateMap<Order, OrderListItemVo>();
    }

    /// <summary>配置用户权益映射</summary>
    private void ConfigureUserBenefitMapping()
    {
        // UserBenefit -> UserBenefitVo
        CreateMap<UserBenefit, UserBenefitVo>();

        // UserBenefitVo -> UserBenefit
        CreateMap<UserBenefitVo, UserBenefit>()
            .ForMember(dest => dest.SourceOrderId, opt => opt.Ignore())
            .ForMember(dest => dest.SourceProductId, opt => opt.Ignore())
            .ForMember(dest => dest.ActivatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreateTime, opt => opt.Ignore())
            .ForMember(dest => dest.CreateBy, opt => opt.Ignore())
            .ForMember(dest => dest.CreateId, opt => opt.Ignore())
            .ForMember(dest => dest.ModifyTime, opt => opt.Ignore())
            .ForMember(dest => dest.ModifyBy, opt => opt.Ignore())
            .ForMember(dest => dest.ModifyId, opt => opt.Ignore());
    }

    /// <summary>配置用户背包映射</summary>
    private void ConfigureUserInventoryMapping()
    {
        // UserInventory -> UserInventoryVo
        CreateMap<UserInventory, UserInventoryVo>();

        // UserInventoryVo -> UserInventory
        CreateMap<UserInventoryVo, UserInventory>()
            .ForMember(dest => dest.SourceProductId, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreateTime, opt => opt.Ignore())
            .ForMember(dest => dest.CreateBy, opt => opt.Ignore())
            .ForMember(dest => dest.CreateId, opt => opt.Ignore())
            .ForMember(dest => dest.ModifyTime, opt => opt.Ignore())
            .ForMember(dest => dest.ModifyBy, opt => opt.Ignore())
            .ForMember(dest => dest.ModifyId, opt => opt.Ignore());
    }

    /// <summary>获取有效期显示文本</summary>
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
