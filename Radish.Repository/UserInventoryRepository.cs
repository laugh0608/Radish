using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using Radish.Shared.CustomEnum;

namespace Radish.Repository;

/// <summary>用户背包专属仓储。</summary>
public class UserInventoryRepository : BaseRepository<UserInventory>, IUserInventoryRepository
{
    public UserInventoryRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public async Task<UserInventoryGrantPersistenceResult> GrantConsumableForOrderAsync(
        long tenantId,
        long userId,
        ConsumableType consumableType,
        string? itemValue,
        string? itemName,
        long? itemIconAttachmentId,
        int quantity,
        long sourceOrderId,
        long sourceProductId)
    {
        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "发放数量必须大于 0");
        }

        if (sourceOrderId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceOrderId), "来源订单 ID 必须大于 0");
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                return await ExecuteDbOperationAsync(async () =>
                {
                    DbProtectedClient.Ado.BeginTran();
                    try
                    {
                        var result = await GrantConsumableForOrderCoreAsync(
                            tenantId,
                            userId,
                            consumableType,
                            NormalizeItemValue(itemValue),
                            itemName,
                            itemIconAttachmentId,
                            quantity,
                            sourceOrderId,
                            sourceProductId);

                        DbProtectedClient.Ado.CommitTran();
                        return result;
                    }
                    catch
                    {
                        DbProtectedClient.Ado.RollbackTran();
                        throw;
                    }
                });
            }
            catch (Exception ex) when (RepositorySqlHelper.IsUniqueConstraintException(ex))
            {
                var existingGrant = await ReadExistingGrantResultAsync(tenantId, sourceOrderId);
                if (existingGrant != null)
                {
                    return existingGrant;
                }

                if (attempt == 0)
                {
                    continue;
                }

                throw;
            }
        }

        throw new InvalidOperationException("消耗品发放失败，请稍后重试");
    }

    public async Task<UserInventoryDeductPersistenceResult> TryDeductItemAsync(long userId, long inventoryId, int quantity)
    {
        if (quantity < 1)
        {
            return new UserInventoryDeductPersistenceResult(false, 0);
        }

        return await ExecuteDbOperationAsync(async () =>
        {
            var affectedRows = await DbProtectedClient.Updateable<UserInventory>()
                .SetColumns(item => new UserInventory
                {
                    Quantity = item.Quantity - quantity,
                    ModifyTime = DateTime.UtcNow
                })
                .Where(item =>
                    item.Id == inventoryId &&
                    item.UserId == userId &&
                    !item.IsDeleted &&
                    item.Quantity >= quantity)
                .ExecuteCommandAsync();

            if (affectedRows <= 0)
            {
                return new UserInventoryDeductPersistenceResult(false, 0);
            }

            var remainingQuantity = await CreateTenantQueryableFor<UserInventory>()
                .Where(item => item.Id == inventoryId && item.UserId == userId)
                .Select(item => item.Quantity)
                .FirstAsync();

            return new UserInventoryDeductPersistenceResult(true, remainingQuantity);
        });
    }

    private async Task<UserInventoryGrantPersistenceResult> GrantConsumableForOrderCoreAsync(
        long tenantId,
        long userId,
        ConsumableType consumableType,
        string itemValue,
        string? itemName,
        long? itemIconAttachmentId,
        int quantity,
        long sourceOrderId,
        long sourceProductId)
    {
        var existingGrant = await QueryGrantRecordAsync(tenantId, sourceOrderId);
        if (existingGrant != null)
        {
            return await BuildExistingGrantResultAsync(existingGrant);
        }

        var inventory = await QueryInventoryItemAsync(tenantId, userId, consumableType, itemValue);
        var inventoryId = inventory?.Id ?? await CreateInventoryItemAsync(
            tenantId,
            userId,
            consumableType,
            itemValue,
            itemName,
            itemIconAttachmentId,
            sourceProductId);

        await DbProtectedClient.Insertable(new UserInventoryGrantRecord
        {
            TenantId = tenantId,
            UserId = userId,
            InventoryId = inventoryId,
            SourceOrderId = sourceOrderId,
            SourceProductId = sourceProductId,
            ConsumableType = consumableType,
            ItemValue = itemValue,
            Quantity = quantity,
            CreateTime = DateTime.UtcNow,
            CreateBy = "System",
            CreateId = userId
        }).ExecuteReturnSnowflakeIdAsync();

        var affectedRows = await DbProtectedClient.Updateable<UserInventory>()
            .SetColumns(item => new UserInventory
            {
                Quantity = item.Quantity + quantity,
                ItemName = itemName,
                ItemIconAttachmentId = itemIconAttachmentId,
                SourceProductId = sourceProductId,
                IsDeleted = false,
                ModifyTime = DateTime.UtcNow
            })
            .Where(item => item.Id == inventoryId)
            .ExecuteCommandAsync();

        if (affectedRows <= 0)
        {
            throw new InvalidOperationException("背包数量更新失败");
        }

        var currentQuantity = await QueryInventoryQuantityAsync(inventoryId);
        return new UserInventoryGrantPersistenceResult(inventoryId, true, quantity, currentQuantity);
    }

    private async Task<long> CreateInventoryItemAsync(
        long tenantId,
        long userId,
        ConsumableType consumableType,
        string itemValue,
        string? itemName,
        long? itemIconAttachmentId,
        long sourceProductId)
    {
        return await DbProtectedClient.Insertable(new UserInventory
        {
            TenantId = tenantId,
            UserId = userId,
            ConsumableType = consumableType,
            ItemValue = itemValue,
            ItemName = itemName,
            ItemIconAttachmentId = itemIconAttachmentId,
            Quantity = 0,
            SourceProductId = sourceProductId,
            CreateTime = DateTime.UtcNow,
            CreateBy = "System",
            CreateId = userId
        }).ExecuteReturnSnowflakeIdAsync();
    }

    private async Task<UserInventoryGrantPersistenceResult?> ReadExistingGrantResultAsync(long tenantId, long sourceOrderId)
    {
        return await ExecuteDbOperationAsync(async () =>
        {
            var grantRecord = await QueryGrantRecordAsync(tenantId, sourceOrderId);
            return grantRecord == null ? null : await BuildExistingGrantResultAsync(grantRecord);
        });
    }

    private async Task<UserInventoryGrantPersistenceResult> BuildExistingGrantResultAsync(UserInventoryGrantRecord grantRecord)
    {
        var currentQuantity = await QueryInventoryQuantityAsync(grantRecord.InventoryId);
        return new UserInventoryGrantPersistenceResult(grantRecord.InventoryId, false, 0, currentQuantity);
    }

    private async Task<UserInventoryGrantRecord?> QueryGrantRecordAsync(long tenantId, long sourceOrderId)
    {
        return await DbProtectedClient.Queryable<UserInventoryGrantRecord>()
            .Where(record => record.TenantId == tenantId && record.SourceOrderId == sourceOrderId)
            .FirstAsync();
    }

    private async Task<UserInventory?> QueryInventoryItemAsync(
        long tenantId,
        long userId,
        ConsumableType consumableType,
        string itemValue)
    {
        return await DbProtectedClient.Queryable<UserInventory>()
            .Where(item =>
                item.TenantId == tenantId &&
                item.UserId == userId &&
                item.ConsumableType == consumableType &&
                item.ItemValue == itemValue)
            .FirstAsync();
    }

    private async Task<int> QueryInventoryQuantityAsync(long inventoryId)
    {
        return await DbProtectedClient.Queryable<UserInventory>()
            .Where(item => item.Id == inventoryId)
            .Select(item => item.Quantity)
            .FirstAsync();
    }

    private static string NormalizeItemValue(string? itemValue)
    {
        return itemValue ?? string.Empty;
    }
}
