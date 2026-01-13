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

    /// <summary>使用改名卡</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="inventoryId">背包项 ID</param>
    /// <param name="newNickname">新昵称</param>
    /// <returns>使用结果</returns>
    Task<UseItemResultDto> UseRenameCardAsync(long userId, long inventoryId, string newNickname);

    /// <summary>使用经验卡</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="inventoryId">背包项 ID</param>
    /// <returns>使用结果</returns>
    Task<UseItemResultDto> UseExpCardAsync(long userId, long inventoryId);

    /// <summary>使用萝卜币红包</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="inventoryId">背包项 ID</param>
    /// <returns>使用结果</returns>
    Task<UseItemResultDto> UseCoinCardAsync(long userId, long inventoryId);

    /// <summary>使用双倍经验卡</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="inventoryId">背包项 ID</param>
    /// <returns>使用结果</returns>
    Task<UseItemResultDto> UseDoubleExpCardAsync(long userId, long inventoryId);

    /// <summary>使用帖子置顶卡</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="inventoryId">背包项 ID</param>
    /// <param name="postId">帖子 ID</param>
    /// <returns>使用结果</returns>
    Task<UseItemResultDto> UsePostPinCardAsync(long userId, long inventoryId, long postId);

    /// <summary>使用帖子高亮卡</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="inventoryId">背包项 ID</param>
    /// <param name="postId">帖子 ID</param>
    /// <returns>使用结果</returns>
    Task<UseItemResultDto> UsePostHighlightCardAsync(long userId, long inventoryId, long postId);

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
    /// <param name="itemIcon">道具图标</param>
    /// <param name="quantity">增加数量</param>
    /// <param name="sourceProductId">来源商品 ID</param>
    /// <returns>背包项 ID</returns>
    Task<long> AddItemAsync(
        long userId,
        ConsumableType consumableType,
        string? itemValue,
        string? itemName,
        string? itemIcon,
        int quantity = 1,
        long? sourceProductId = null);

    #endregion
}
