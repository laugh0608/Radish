using Radish.IService.Base;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;

namespace Radish.IService;

/// <summary>用户背包服务接口</summary>
public interface IUserInventoryService : IBaseService<UserInventory, UserInventoryVo>
{
    #region 背包查询

    /// <summary>获取用户背包列表</summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>背包物品列表</returns>
    Task<List<UserInventoryVo>> GetUserInventoryAsync(long userId);

    /// <summary>获取用户指定类型的道具</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="consumableType">消耗品类型</param>
    /// <returns>背包物品列表</returns>
    Task<List<UserInventoryVo>> GetUserInventoryByTypeAsync(long userId, ConsumableType consumableType);

    /// <summary>获取用户指定道具的数量</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="consumableType">消耗品类型</param>
    /// <param name="itemValue">道具值（可选）</param>
    /// <returns>道具数量</returns>
    Task<int> GetItemQuantityAsync(long userId, ConsumableType consumableType, string? itemValue = null);

    #endregion

    #region 道具使用

    /// <summary>使用道具</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="dto">使用道具 DTO</param>
    /// <returns>使用结果</returns>
    Task<UseItemResultDto> UseItemAsync(long userId, UseItemDto dto);

    /// <summary>使用改名卡。</summary>
    Task<UseItemResultDto> UseRenameCardAsync(long userId, UseRenameCardDto dto);

    /// <summary>分页查询用户的商城消耗品使用流水（管理后台）。</summary>
    Task<PageModel<ShopEntitlementOperationVo>> GetOperationsForAdminAsync(
        long userId,
        ConsumableType? consumableType = null,
        int pageIndex = 1,
        int pageSize = 20);

    #endregion

    #region 道具管理

    /// <summary>扣减道具数量</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="inventoryId">背包项 ID</param>
    /// <param name="quantity">扣减数量</param>
    /// <returns>是否成功</returns>
    Task<bool> DeductItemAsync(long userId, long inventoryId, int quantity = 1);

    /// <summary>增加道具数量</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="consumableType">消耗品类型</param>
    /// <param name="itemValue">道具值</param>
    /// <param name="itemName">道具名称</param>
    /// <param name="itemIconAttachmentId">道具图标附件 Id</param>
    /// <param name="quantity">增加数量</param>
    /// <param name="sourceProductId">来源商品 ID</param>
    /// <returns>背包项 ID</returns>
    Task<long> AddItemAsync(
        long userId,
        ConsumableType consumableType,
        string? itemValue,
        string? itemName,
        long? itemIconAttachmentId,
        int quantity = 1,
        long? sourceProductId = null);

    #endregion
}
