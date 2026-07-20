namespace Radish.Shared.CustomEnum;

/// <summary>商城商品可用性策略</summary>
public static class ShopProductAvailabilityPolicy
{
    public static bool IsUnavailablePublicProduct(ProductType productType, BenefitType? benefitType, ConsumableType? consumableType)
    {
        return productType switch
        {
            ProductType.Benefit => benefitType == null || IsUnavailableBenefitType(benefitType),
            ProductType.Consumable => consumableType == null || IsUnavailableConsumableType(consumableType),
            _ => true
        };
    }

    public static bool IsUnavailableBenefitType(BenefitType? benefitType)
    {
        return benefitType is BenefitType.AvatarFrame
            or BenefitType.Signature
            or BenefitType.NameColor
            or BenefitType.LikeEffect;
    }

    public static bool CanActivateBenefitType(BenefitType? benefitType)
    {
        return benefitType is BenefitType.Badge or BenefitType.Title or BenefitType.Theme;
    }

    public static string? GetUnavailableReason(
        ProductType productType,
        BenefitType? benefitType,
        ConsumableType? consumableType)
    {
        if (!IsUnavailablePublicProduct(productType, benefitType, consumableType))
        {
            return null;
        }

        return productType switch
        {
            ProductType.Benefit => $"{GetBenefitTypeDisplayName(benefitType)}尚未完成公开消费面，当前不可售或启用",
            ProductType.Consumable => $"{GetConsumableTypeDisplayName(consumableType)}当前不可售或使用",
            ProductType.Physical => "当前版本不支持实物商品",
            _ => "当前商品类型不可用"
        };
    }

    public static string? GetUnavailableReasonKey(
        ProductType productType,
        BenefitType? benefitType,
        ConsumableType? consumableType)
    {
        if (!IsUnavailablePublicProduct(productType, benefitType, consumableType))
        {
            return null;
        }

        return productType switch
        {
            ProductType.Benefit => "products.capability.unavailable.benefit",
            ProductType.Consumable => "products.capability.unavailable.consumable",
            ProductType.Physical => "products.capability.unavailable.physical",
            _ => "products.capability.unavailable.unknown"
        };
    }

    public static IReadOnlyList<string> GetConfigurationRequirements(
        ProductType productType,
        BenefitType? benefitType,
        ConsumableType? consumableType)
    {
        if (productType == ProductType.Benefit)
        {
            return benefitType switch
            {
                BenefitType.Badge => ["资源标识必填，长度 1-100，仅允许字母、数字、点、下划线和连字符", "必须配置公开有效的徽章图标附件"],
                BenefitType.Title => ["称号文本必填，长度 1-40"],
                BenefitType.Theme => ["主题资源标识必须为 theme-dark-night 或 theme-sakura"],
                _ => ["权益资源值必填"]
            };
        }

        if (productType == ProductType.Consumable)
        {
            return consumableType switch
            {
                ConsumableType.ExpCard => ["经验值必须为正整数"],
                ConsumableType.CoinCard => ["红包面额必须为正整数"],
                ConsumableType.RenameCard => ["无需额外资源值"],
                _ => ["该消耗品尚未开放"]
            };
        }

        return ["当前版本不支持该商品类型"];
    }

    public static IReadOnlyList<string> GetConfigurationRequirementKeys(
        ProductType productType,
        BenefitType? benefitType,
        ConsumableType? consumableType)
    {
        if (productType == ProductType.Benefit)
        {
            return benefitType switch
            {
                BenefitType.Badge =>
                [
                    "products.capability.requirement.badgeResource",
                    "products.capability.requirement.badgeIcon"
                ],
                BenefitType.Title => ["products.capability.requirement.titleText"],
                BenefitType.Theme => ["products.capability.requirement.themeResource"],
                _ => ["products.capability.requirement.benefitValue"]
            };
        }

        if (productType == ProductType.Consumable)
        {
            return consumableType switch
            {
                ConsumableType.ExpCard => ["products.capability.requirement.expValue"],
                ConsumableType.CoinCard => ["products.capability.requirement.coinValue"],
                ConsumableType.RenameCard => ["products.capability.requirement.noExtraValue"],
                _ => ["products.capability.requirement.consumableUnavailable"]
            };
        }

        return ["products.capability.requirement.productTypeUnavailable"];
    }

#pragma warning disable CS0618
    public static bool IsUnavailableConsumableType(ConsumableType? consumableType)
    {
        return consumableType is ConsumableType.PostPinCard
            or ConsumableType.PostHighlightCard
            or ConsumableType.DoubleExpCard
            or ConsumableType.LotteryTicket;
    }
#pragma warning restore CS0618

    public static string GetUnavailableProductDisplayName(BenefitType? benefitType, ConsumableType? consumableType)
    {
        if (IsUnavailableBenefitType(benefitType))
        {
            return GetBenefitTypeDisplayName(benefitType);
        }

        if (IsUnavailableConsumableType(consumableType))
        {
            return GetConsumableTypeDisplayName(consumableType);
        }

        return "该商品";
    }

    public static string GetBenefitTypeDisplayName(BenefitType? benefitType)
    {
        return benefitType switch
        {
            BenefitType.Badge => "徽章",
            BenefitType.AvatarFrame => "头像框",
            BenefitType.Title => "称号",
            BenefitType.Theme => "主题",
            BenefitType.Signature => "签名档",
            BenefitType.NameColor => "用户名颜色",
            BenefitType.LikeEffect => "点赞特效",
            _ => "该权益"
        };
    }

#pragma warning disable CS0618
    public static string GetConsumableTypeDisplayName(ConsumableType? consumableType)
    {
        return consumableType switch
        {
            ConsumableType.PostPinCard => "帖子置顶卡",
            ConsumableType.PostHighlightCard => "帖子高亮卡",
            ConsumableType.DoubleExpCard => "双倍经验卡",
            ConsumableType.LotteryTicket => "抽奖券",
            _ => "该消耗品"
        };
    }
#pragma warning restore CS0618
}
