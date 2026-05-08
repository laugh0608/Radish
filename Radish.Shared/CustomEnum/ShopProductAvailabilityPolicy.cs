namespace Radish.Shared.CustomEnum;

/// <summary>商城商品可用性策略</summary>
public static class ShopProductAvailabilityPolicy
{
    public static bool IsUnavailablePublicProduct(ProductType productType, BenefitType? benefitType, ConsumableType? consumableType)
    {
        return productType == ProductType.Benefit
            ? IsUnavailableBenefitType(benefitType)
            : productType == ProductType.Consumable && IsUnavailableConsumableType(consumableType);
    }

    public static bool IsUnavailableBenefitType(BenefitType? benefitType)
    {
        return benefitType is BenefitType.Badge
            or BenefitType.AvatarFrame
            or BenefitType.Title
            or BenefitType.Theme
            or BenefitType.Signature
            or BenefitType.NameColor
            or BenefitType.LikeEffect;
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
