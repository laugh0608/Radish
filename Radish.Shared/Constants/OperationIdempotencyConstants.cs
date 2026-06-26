namespace Radish.Shared.Constants;

/// <summary>资产写操作幂等治理常量。</summary>
public static class OperationIdempotencyOperationTypes
{
    public const string ShopPurchase = "ShopPurchase";
    public const string CoinTransfer = "CoinTransfer";
}

/// <summary>幂等记录处理状态。</summary>
public static class OperationIdempotencyStatuses
{
    public const string Processing = "Processing";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
}

/// <summary>幂等记录关联资源类型。</summary>
public static class OperationIdempotencyResourceTypes
{
    public const string Order = "Order";
    public const string CoinTransaction = "CoinTransaction";
}
