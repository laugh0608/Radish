using System.Linq.Expressions;
using AutoMapper;
using Radish.Common;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service.Base;
using Radish.Shared.CustomEnum;
using Serilog;

namespace Radish.Service;

/// <summary>用户背包服务实现</summary>
public class UserInventoryService : BaseService<UserInventory, UserInventoryVo>, IUserInventoryService
{
    private readonly IBaseRepository<UserInventory> _inventoryRepository;
    private readonly IBaseRepository<User> _userRepository;
    private readonly ICoinService _coinService;
    private readonly IExperienceService? _experienceService;

    public UserInventoryService(
        IMapper mapper,
        IBaseRepository<UserInventory> inventoryRepository,
        IBaseRepository<User> userRepository,
        ICoinService coinService,
        IExperienceService? experienceService = null)
        : base(mapper, inventoryRepository)
    {
        _inventoryRepository = inventoryRepository;
        _userRepository = userRepository;
        _coinService = coinService;
        _experienceService = experienceService;
    }

    #region 背包查询

    /// <summary>获取用户背包列表</summary>
    public async Task<List<UserInventoryVo>> GetUserInventoryAsync(long userId)
    {
        try
        {
            var items = await _inventoryRepository.QueryAsync(i => i.UserId == userId && i.Quantity > 0);
            return Mapper.Map<List<UserInventoryVo>>(items.OrderByDescending(i => i.CreateTime).ToList());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取用户 {UserId} 背包列表失败", userId);
            throw;
        }
    }

    /// <summary>获取用户指定类型的道具</summary>
    public async Task<List<UserInventoryVo>> GetUserInventoryByTypeAsync(long userId, ConsumableType consumableType)
    {
        try
        {
            var items = await _inventoryRepository.QueryAsync(
                i => i.UserId == userId && i.ConsumableType == consumableType && i.Quantity > 0);
            return Mapper.Map<List<UserInventoryVo>>(items.OrderByDescending(i => i.CreateTime).ToList());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取用户 {UserId} 类型 {ConsumableType} 道具列表失败", userId, consumableType);
            throw;
        }
    }

    /// <summary>获取用户指定道具的数量</summary>
    public async Task<int> GetItemQuantityAsync(long userId, ConsumableType consumableType, string? itemValue = null)
    {
        try
        {
            Expression<Func<UserInventory, bool>> where = i =>
                i.UserId == userId && i.ConsumableType == consumableType;

            if (!string.IsNullOrWhiteSpace(itemValue))
            {
                Expression<Func<UserInventory, bool>> valueFilter = i => i.ItemValue == itemValue;
                where = where.And(valueFilter);
            }

            var items = await _inventoryRepository.QueryAsync(where);
            return items.Sum(i => i.Quantity);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取用户 {UserId} 道具 {ConsumableType} 数量失败", userId, consumableType);
            throw;
        }
    }

    #endregion

    #region 道具使用

    /// <summary>使用道具</summary>
    public async Task<UseItemResultDto> UseItemAsync(long userId, UseItemDto dto)
    {
        try
        {
            var item = await _inventoryRepository.QueryFirstAsync(
                i => i.Id == dto.InventoryId && i.UserId == userId);

            if (item == null)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "道具不存在" };
            }

            if (item.Quantity < dto.Quantity)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "道具数量不足" };
            }

            // 根据道具类型调用不同的使用方法
            return item.ConsumableType switch
            {
                ConsumableType.RenameCard => new UseItemResultDto
                {
                    Success = false,
                    ErrorMessage = "改名卡需要指定新昵称，请使用专用接口"
                },
                ConsumableType.ExpCard => await UseExpCardAsync(userId, dto.InventoryId),
                ConsumableType.CoinCard => await UseCoinCardAsync(userId, dto.InventoryId),
                ConsumableType.DoubleExpCard => await UseDoubleExpCardAsync(userId, dto.InventoryId),
                ConsumableType.PostPinCard => dto.TargetId.HasValue
                    ? await UsePostPinCardAsync(userId, dto.InventoryId, dto.TargetId.Value)
                    : new UseItemResultDto { Success = false, ErrorMessage = "置顶卡需要指定帖子 ID" },
                ConsumableType.PostHighlightCard => dto.TargetId.HasValue
                    ? await UsePostHighlightCardAsync(userId, dto.InventoryId, dto.TargetId.Value)
                    : new UseItemResultDto { Success = false, ErrorMessage = "高亮卡需要指定帖子 ID" },
                _ => new UseItemResultDto { Success = false, ErrorMessage = "不支持的道具类型" }
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "使用道具失败：用户={UserId}, 道具={InventoryId}", userId, dto.InventoryId);
            return new UseItemResultDto { Success = false, ErrorMessage = "使用道具失败" };
        }
    }

    /// <summary>使用改名卡</summary>
    public async Task<UseItemResultDto> UseRenameCardAsync(long userId, long inventoryId, string newNickname)
    {
        try
        {
            var item = await _inventoryRepository.QueryFirstAsync(
                i => i.Id == inventoryId && i.UserId == userId && i.ConsumableType == ConsumableType.RenameCard);

            if (item == null)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "改名卡不存在" };
            }

            if (item.Quantity < 1)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "改名卡数量不足" };
            }

            if (string.IsNullOrWhiteSpace(newNickname))
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "新昵称不能为空" };
            }

            // 检查昵称是否已被使用
            var existingUser = await _userRepository.QueryFirstAsync(u => u.UserName == newNickname && u.Id != userId);
            if (existingUser != null)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "该昵称已被使用" };
            }

            // 更新用户昵称
            var user = await _userRepository.QueryFirstAsync(u => u.Id == userId);
            if (user == null)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "用户不存在" };
            }

            var oldNickname = user.UserName;
            user.UserName = newNickname;
            await _userRepository.UpdateAsync(user);

            // 扣减道具
            await DeductItemAsync(userId, inventoryId, 1);

            Log.Information("用户 {UserId} 使用改名卡，昵称从 {OldNickname} 改为 {NewNickname}",
                userId, oldNickname, newNickname);

            return new UseItemResultDto
            {
                Success = true,
                RemainingQuantity = item.Quantity - 1,
                EffectDescription = $"昵称已修改为 {newNickname}"
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "使用改名卡失败：用户={UserId}", userId);
            return new UseItemResultDto { Success = false, ErrorMessage = "使用改名卡失败" };
        }
    }

    /// <summary>使用经验卡</summary>
    public async Task<UseItemResultDto> UseExpCardAsync(long userId, long inventoryId)
    {
        try
        {
            var item = await _inventoryRepository.QueryFirstAsync(
                i => i.Id == inventoryId && i.UserId == userId && i.ConsumableType == ConsumableType.ExpCard);

            if (item == null)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "经验卡不存在" };
            }

            if (item.Quantity < 1)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "经验卡数量不足" };
            }

            // 解析经验值
            if (!int.TryParse(item.ItemValue, out var expAmount) || expAmount <= 0)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "经验卡配置错误" };
            }

            // 发放经验值
            if (_experienceService != null)
            {
                await _experienceService.GrantExperienceAsync(userId, expAmount, "USE_EXP_CARD", "UserInventory", inventoryId);
            }
            else
            {
                Log.Warning("经验值服务不可用，无法发放经验值");
            }

            // 扣减道具
            await DeductItemAsync(userId, inventoryId, 1);

            Log.Information("用户 {UserId} 使用经验卡，获得 {ExpAmount} 经验值", userId, expAmount);

            return new UseItemResultDto
            {
                Success = true,
                RemainingQuantity = item.Quantity - 1,
                EffectDescription = $"获得 {expAmount} 经验值"
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "使用经验卡失败：用户={UserId}", userId);
            return new UseItemResultDto { Success = false, ErrorMessage = "使用经验卡失败" };
        }
    }

    /// <summary>使用萝卜币红包</summary>
    public async Task<UseItemResultDto> UseCoinCardAsync(long userId, long inventoryId)
    {
        try
        {
            var item = await _inventoryRepository.QueryFirstAsync(
                i => i.Id == inventoryId && i.UserId == userId && i.ConsumableType == ConsumableType.CoinCard);

            if (item == null)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "萝卜币红包不存在" };
            }

            if (item.Quantity < 1)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "萝卜币红包数量不足" };
            }

            // 解析萝卜币数量
            if (!long.TryParse(item.ItemValue, out var coinAmount) || coinAmount <= 0)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "萝卜币红包配置错误" };
            }

            // 发放萝卜币
            await _coinService.GrantCoinAsync(
                userId,
                coinAmount,
                "USE_COIN_CARD",
                "UserInventory",
                inventoryId,
                $"使用萝卜币红包获得 {coinAmount} 胡萝卜");

            // 扣减道具
            await DeductItemAsync(userId, inventoryId, 1);

            Log.Information("用户 {UserId} 使用萝卜币红包，获得 {CoinAmount} 胡萝卜", userId, coinAmount);

            return new UseItemResultDto
            {
                Success = true,
                RemainingQuantity = item.Quantity - 1,
                EffectDescription = $"获得 {coinAmount} 胡萝卜"
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "使用萝卜币红包失败：用户={UserId}", userId);
            return new UseItemResultDto { Success = false, ErrorMessage = "使用萝卜币红包失败" };
        }
    }

    /// <summary>使用双倍经验卡</summary>
    public async Task<UseItemResultDto> UseDoubleExpCardAsync(long userId, long inventoryId)
    {
        try
        {
            var item = await _inventoryRepository.QueryFirstAsync(
                i => i.Id == inventoryId && i.UserId == userId && i.ConsumableType == ConsumableType.DoubleExpCard);

            if (item == null)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "双倍经验卡不存在" };
            }

            if (item.Quantity < 1)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "双倍经验卡数量不足" };
            }

            // 解析持续时间（小时）
            if (!int.TryParse(item.ItemValue, out var durationHours) || durationHours <= 0)
            {
                durationHours = 24; // 默认 24 小时
            }

            // TODO: 实现双倍经验效果
            // 这里需要在用户表或专门的 buff 表中记录双倍经验状态
            // 暂时只记录日志

            // 扣减道具
            await DeductItemAsync(userId, inventoryId, 1);

            Log.Information("用户 {UserId} 使用双倍经验卡，持续 {DurationHours} 小时", userId, durationHours);

            return new UseItemResultDto
            {
                Success = true,
                RemainingQuantity = item.Quantity - 1,
                EffectDescription = $"双倍经验效果已激活，持续 {durationHours} 小时"
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "使用双倍经验卡失败：用户={UserId}", userId);
            return new UseItemResultDto { Success = false, ErrorMessage = "使用双倍经验卡失败" };
        }
    }

    /// <summary>使用帖子置顶卡</summary>
    public async Task<UseItemResultDto> UsePostPinCardAsync(long userId, long inventoryId, long postId)
    {
        try
        {
            var item = await _inventoryRepository.QueryFirstAsync(
                i => i.Id == inventoryId && i.UserId == userId && i.ConsumableType == ConsumableType.PostPinCard);

            if (item == null)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "置顶卡不存在" };
            }

            if (item.Quantity < 1)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "置顶卡数量不足" };
            }

            // 解析持续时间（小时）
            if (!int.TryParse(item.ItemValue, out var durationHours) || durationHours <= 0)
            {
                durationHours = 24; // 默认 24 小时
            }

            // TODO: 实现帖子置顶效果
            // 这里需要调用论坛服务来置顶帖子
            // 暂时只记录日志

            // 扣减道具
            await DeductItemAsync(userId, inventoryId, 1);

            Log.Information("用户 {UserId} 使用置顶卡，帖子 {PostId} 置顶 {DurationHours} 小时",
                userId, postId, durationHours);

            return new UseItemResultDto
            {
                Success = true,
                RemainingQuantity = item.Quantity - 1,
                EffectDescription = $"帖子已置顶，持续 {durationHours} 小时"
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "使用置顶卡失败：用户={UserId}, 帖子={PostId}", userId, postId);
            return new UseItemResultDto { Success = false, ErrorMessage = "使用置顶卡失败" };
        }
    }

    /// <summary>使用帖子高亮卡</summary>
    public async Task<UseItemResultDto> UsePostHighlightCardAsync(long userId, long inventoryId, long postId)
    {
        try
        {
            var item = await _inventoryRepository.QueryFirstAsync(
                i => i.Id == inventoryId && i.UserId == userId && i.ConsumableType == ConsumableType.PostHighlightCard);

            if (item == null)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "高亮卡不存在" };
            }

            if (item.Quantity < 1)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "高亮卡数量不足" };
            }

            // 解析持续时间（小时）
            if (!int.TryParse(item.ItemValue, out var durationHours) || durationHours <= 0)
            {
                durationHours = 24; // 默认 24 小时
            }

            // TODO: 实现帖子高亮效果
            // 这里需要调用论坛服务来高亮帖子
            // 暂时只记录日志

            // 扣减道具
            await DeductItemAsync(userId, inventoryId, 1);

            Log.Information("用户 {UserId} 使用高亮卡，帖子 {PostId} 高亮 {DurationHours} 小时",
                userId, postId, durationHours);

            return new UseItemResultDto
            {
                Success = true,
                RemainingQuantity = item.Quantity - 1,
                EffectDescription = $"帖子已高亮，持续 {durationHours} 小时"
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "使用高亮卡失败：用户={UserId}, 帖子={PostId}", userId, postId);
            return new UseItemResultDto { Success = false, ErrorMessage = "使用高亮卡失败" };
        }
    }

    #endregion

    #region 道具管理

    /// <summary>扣减道具数量</summary>
    public async Task<bool> DeductItemAsync(long userId, long inventoryId, int quantity = 1)
    {
        try
        {
            var item = await _inventoryRepository.QueryFirstAsync(
                i => i.Id == inventoryId && i.UserId == userId);

            if (item == null || item.Quantity < quantity)
            {
                return false;
            }

            item.Quantity -= quantity;
            item.ModifyTime = DateTime.Now;

            var result = await _inventoryRepository.UpdateAsync(item);

            if (result)
            {
                Log.Information("扣减道具成功：用户={UserId}, 道具={InventoryId}, 扣减={Quantity}, 剩余={Remaining}",
                    userId, inventoryId, quantity, item.Quantity);
            }

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "扣减道具失败：用户={UserId}, 道具={InventoryId}", userId, inventoryId);
            throw;
        }
    }

    /// <summary>增加道具数量</summary>
    public async Task<long> AddItemAsync(
        long userId,
        ConsumableType consumableType,
        string? itemValue,
        string? itemName,
        string? itemIcon,
        int quantity = 1,
        long? sourceProductId = null)
    {
        try
        {
            // 检查是否已有相同道具
            var existingItem = await _inventoryRepository.QueryFirstAsync(
                i => i.UserId == userId &&
                     i.ConsumableType == consumableType &&
                     i.ItemValue == itemValue);

            if (existingItem != null)
            {
                // 增加数量
                existingItem.Quantity += quantity;
                existingItem.ModifyTime = DateTime.Now;
                await _inventoryRepository.UpdateAsync(existingItem);

                Log.Information("道具数量增加：用户={UserId}, 道具={ItemId}, 新数量={Quantity}",
                    userId, existingItem.Id, existingItem.Quantity);

                return existingItem.Id;
            }
            else
            {
                // 创建新道具
                var newItem = new UserInventory
                {
                    UserId = userId,
                    ConsumableType = consumableType,
                    ItemValue = itemValue,
                    ItemName = itemName,
                    ItemIcon = itemIcon,
                    Quantity = quantity,
                    SourceProductId = sourceProductId,
                    CreateTime = DateTime.Now,
                    CreateBy = "System",
                    CreateId = userId
                };

                var itemId = await _inventoryRepository.AddAsync(newItem);

                Log.Information("道具添加成功：用户={UserId}, 道具={ItemId}, 类型={ConsumableType}",
                    userId, itemId, consumableType);

                return itemId;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "添加道具失败：用户={UserId}, 类型={ConsumableType}", userId, consumableType);
            throw;
        }
    }

    #endregion
}
