namespace Radish.Shared.Constants;

/// <summary>商城权益业务操作类型。</summary>
public static class ShopEntitlementOperationTypes
{
    public const string Use = "Use";
    public const string Activate = "Activate";
    public const string Deactivate = "Deactivate";
    public const string Expire = "Expire";
    public const string Revoke = "Revoke";
}

/// <summary>商城消耗品实际效果类型。</summary>
public static class ShopEntitlementEffectTypes
{
    public const string DisplayName = "DisplayName";
    public const string Experience = "Experience";
    public const string Coin = "Coin";
    public const string BenefitSelection = "BenefitSelection";
    public const string BenefitExpiration = "BenefitExpiration";
    public const string BenefitRevocation = "BenefitRevocation";
}

/// <summary>商城权益效果资源类型。</summary>
public static class ShopEntitlementResourceTypes
{
    public const string User = "User";
    public const string ExpTransaction = "ExpTransaction";
    public const string CoinTransaction = "CoinTransaction";
    public const string UserBenefit = "UserBenefit";
}
