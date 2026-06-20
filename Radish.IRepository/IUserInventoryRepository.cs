using Radish.Model;
using Radish.Shared.CustomEnum;

namespace Radish.IRepository;

/// <summary>用户背包专属仓储。</summary>
public interface IUserInventoryRepository
{
    /// <summary>
    /// 按订单发放消耗品，并保证同一订单只发放一次。
    /// </summary>
    Task<UserInventoryGrantPersistenceResult> GrantConsumableForOrderAsync(
        long tenantId,
        long userId,
        ConsumableType consumableType,
        string? itemValue,
        string? itemName,
        long? itemIconAttachmentId,
        int quantity,
        long sourceOrderId,
        long sourceProductId);

    /// <summary>
    /// 条件扣减背包数量，数量不足时不改变数据。
    /// </summary>
    Task<UserInventoryDeductPersistenceResult> TryDeductItemAsync(long userId, long inventoryId, int quantity);
}

/// <summary>背包发放持久化结果。</summary>
public sealed record UserInventoryGrantPersistenceResult(
    long InventoryId,
    bool CreatedGrantRecord,
    int QuantityDelta,
    int CurrentQuantity);

/// <summary>背包扣减持久化结果。</summary>
public sealed record UserInventoryDeductPersistenceResult(bool Success, int RemainingQuantity);
