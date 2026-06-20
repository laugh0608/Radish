using System.Linq.Expressions;
using AutoMapper;
using Radish.Common;
using Radish.Common.AttributeTool;
using Radish.Common.Exceptions;
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
    private readonly IUserInventoryRepository _userInventoryRepository;
    private readonly IBaseRepository<User> _userRepository;
    private readonly ICoinService _coinService;
    private readonly IAttachmentUrlResolver _attachmentUrlResolver;
    private readonly IExperienceService? _experienceService;

    public UserInventoryService(
        IMapper mapper,
        IBaseRepository<UserInventory> inventoryRepository,
        IUserInventoryRepository userInventoryRepository,
        IBaseRepository<User> userRepository,
        ICoinService coinService,
        IAttachmentUrlResolver attachmentUrlResolver,
        IExperienceService? experienceService = null)
        : base(mapper, inventoryRepository)
    {
        _inventoryRepository = inventoryRepository;
        _userInventoryRepository = userInventoryRepository;
        _userRepository = userRepository;
        _coinService = coinService;
        _attachmentUrlResolver = attachmentUrlResolver;
        _experienceService = experienceService;
    }

    #region 背包查询

    /// <summary>获取用户背包列表</summary>
    public async Task<List<UserInventoryVo>> GetUserInventoryAsync(long userId)
    {
        try
        {
            var items = await _inventoryRepository.QueryAsync(i => i.UserId == userId && i.Quantity > 0);
            var itemVos = Mapper.Map<List<UserInventoryVo>>(items.OrderByDescending(i => i.CreateTime).ToList());
            FillInventoryUrls(itemVos);
            return itemVos;
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
            var itemVos = Mapper.Map<List<UserInventoryVo>>(items.OrderByDescending(i => i.CreateTime).ToList());
            FillInventoryUrls(itemVos);
            return itemVos;
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
#pragma warning disable CS0618
    [UseTran(Propagation = Propagation.Required)]
    public async Task<UseItemResultDto> UseItemAsync(long userId, UseItemDto dto)
    {
        if (dto.Quantity < 1)
        {
            return new UseItemResultDto { Success = false, ErrorMessage = "使用数量必须大于 0" };
        }

        var item = await _inventoryRepository.QueryFirstAsync(
            i => i.Id == dto.InventoryId && i.UserId == userId && !i.IsDeleted);

        if (item == null)
        {
            return new UseItemResultDto { Success = false, ErrorMessage = "道具不存在" };
        }

        if (item.Quantity < dto.Quantity)
        {
            return new UseItemResultDto { Success = false, ErrorMessage = "道具数量不足" };
        }

        if (IsUnavailableConsumableType(item.ConsumableType))
        {
            return CreateUnavailableConsumableResult(item.ConsumableType);
        }

        // 根据道具类型调用不同的使用方法
        return item.ConsumableType switch
        {
            ConsumableType.RenameCard => new UseItemResultDto
            {
                Success = false,
                ErrorMessage = "改名卡需要指定新昵称，请使用专用接口"
            },
            ConsumableType.ExpCard => await UseExpCardAsync(userId, dto.InventoryId, dto.Quantity),
            ConsumableType.CoinCard => await UseCoinCardAsync(userId, dto.InventoryId, dto.Quantity),
            ConsumableType.DoubleExpCard => await UseDoubleExpCardAsync(userId, dto.InventoryId),
            ConsumableType.PostPinCard => dto.TargetId.HasValue
                ? await UsePostPinCardAsync(userId, dto.InventoryId, dto.TargetId.Value)
                : new UseItemResultDto { Success = false, ErrorMessage = "置顶卡需要指定帖子 ID" },
            ConsumableType.PostHighlightCard => dto.TargetId.HasValue
                ? await UsePostHighlightCardAsync(userId, dto.InventoryId, dto.TargetId.Value)
                : new UseItemResultDto { Success = false, ErrorMessage = "高亮卡需要指定帖子 ID" },
            ConsumableType.LotteryTicket => CreateUnavailableConsumableResult(ConsumableType.LotteryTicket),
            _ => new UseItemResultDto { Success = false, ErrorMessage = "不支持的道具类型" }
        };
    }
#pragma warning restore CS0618

    /// <summary>使用改名卡</summary>
    [UseTran(Propagation = Propagation.Required)]
    public async Task<UseItemResultDto> UseRenameCardAsync(long userId, long inventoryId, string newNickname)
    {
        var item = await _inventoryRepository.QueryFirstAsync(
            i => i.Id == inventoryId &&
                 i.UserId == userId &&
                 i.ConsumableType == ConsumableType.RenameCard &&
                 !i.IsDeleted);

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

        var user = await _userRepository.QueryFirstAsync(u => u.Id == userId);
        if (user == null)
        {
            return new UseItemResultDto { Success = false, ErrorMessage = "用户不存在" };
        }

        var deduction = await _userInventoryRepository.TryDeductItemAsync(userId, inventoryId, 1);
        if (!deduction.Success)
        {
            return new UseItemResultDto { Success = false, ErrorMessage = "改名卡数量不足" };
        }

        var oldNickname = user.UserName;
        user.UserName = newNickname;
        var updated = await _userRepository.UpdateAsync(user);
        if (!updated)
        {
            throw new BusinessException("昵称修改失败，请稍后再试");
        }

        Log.Information("用户 {UserId} 使用改名卡，昵称从 {OldNickname} 改为 {NewNickname}",
            userId, oldNickname, newNickname);

        return new UseItemResultDto
        {
            Success = true,
            RemainingQuantity = deduction.RemainingQuantity,
            EffectDescription = $"昵称已修改为 {newNickname}"
        };
    }

    /// <summary>使用经验卡</summary>
    [UseTran(Propagation = Propagation.Required)]
    public async Task<UseItemResultDto> UseExpCardAsync(long userId, long inventoryId)
    {
        return await UseExpCardAsync(userId, inventoryId, 1);
    }

    private async Task<UseItemResultDto> UseExpCardAsync(long userId, long inventoryId, int quantity)
    {
        try
        {
            var item = await _inventoryRepository.QueryFirstAsync(
                i => i.Id == inventoryId &&
                     i.UserId == userId &&
                     i.ConsumableType == ConsumableType.ExpCard &&
                     !i.IsDeleted);

            if (item == null)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "经验卡不存在" };
            }

            if (quantity < 1)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "使用数量必须大于 0" };
            }

            if (item.Quantity < quantity)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "经验卡数量不足" };
            }

            // 解析经验值
            if (!int.TryParse(item.ItemValue, out var expAmount) || expAmount <= 0)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "经验卡配置错误" };
            }

            if (_experienceService == null)
            {
                Log.Error("经验值服务不可用，无法发放经验卡奖励");
                return new UseItemResultDto { Success = false, ErrorMessage = "经验服务暂不可用，请稍后再试" };
            }

            int totalExpAmount;
            try
            {
                totalExpAmount = checked(expAmount * quantity);
            }
            catch (OverflowException)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "经验卡配置超出范围" };
            }

            var deduction = await _userInventoryRepository.TryDeductItemAsync(userId, inventoryId, quantity);
            if (!deduction.Success)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "经验卡数量不足" };
            }

            var grantSuccess = await _experienceService.GrantExperienceAsync(
                userId,
                totalExpAmount,
                "USE_EXP_CARD",
                "UserInventory",
                inventoryId);

            if (!grantSuccess)
            {
                Log.Error("用户 {UserId} 使用经验卡失败，经验值服务未完成发放", userId);
                throw new BusinessException("经验值发放失败，请稍后再试");
            }

            Log.Information("用户 {UserId} 使用经验卡 {Quantity} 张，获得 {ExpAmount} 经验值",
                userId, quantity, totalExpAmount);

            return new UseItemResultDto
            {
                Success = true,
                RemainingQuantity = deduction.RemainingQuantity,
                EffectDescription = $"获得 {totalExpAmount} 经验值"
            };
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "使用经验卡失败：用户={UserId}", userId);
            throw;
        }
    }

    /// <summary>使用萝卜币红包</summary>
    [UseTran(Propagation = Propagation.Required)]
    public async Task<UseItemResultDto> UseCoinCardAsync(long userId, long inventoryId)
    {
        return await UseCoinCardAsync(userId, inventoryId, 1);
    }

    private async Task<UseItemResultDto> UseCoinCardAsync(long userId, long inventoryId, int quantity)
    {
        try
        {
            var item = await _inventoryRepository.QueryFirstAsync(
                i => i.Id == inventoryId &&
                     i.UserId == userId &&
                     i.ConsumableType == ConsumableType.CoinCard &&
                     !i.IsDeleted);

            if (item == null)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "萝卜币红包不存在" };
            }

            if (quantity < 1)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "使用数量必须大于 0" };
            }

            if (item.Quantity < quantity)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "萝卜币红包数量不足" };
            }

            // 解析萝卜币数量
            if (!long.TryParse(item.ItemValue, out var coinAmount) || coinAmount <= 0)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "萝卜币红包配置错误" };
            }

            long totalCoinAmount;
            try
            {
                totalCoinAmount = checked(coinAmount * quantity);
            }
            catch (OverflowException)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "萝卜币红包配置超出范围" };
            }

            var deduction = await _userInventoryRepository.TryDeductItemAsync(userId, inventoryId, quantity);
            if (!deduction.Success)
            {
                return new UseItemResultDto { Success = false, ErrorMessage = "萝卜币红包数量不足" };
            }

            try
            {
                await _coinService.GrantCoinAsync(
                    userId,
                    totalCoinAmount,
                    "USE_COIN_CARD",
                    "UserInventory",
                    inventoryId,
                    $"使用萝卜币红包获得 {totalCoinAmount} 胡萝卜");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "用户 {UserId} 使用萝卜币红包失败，萝卜币服务未完成发放", userId);
                throw new BusinessException("萝卜币发放失败，请稍后再试", ex);
            }

            Log.Information("用户 {UserId} 使用萝卜币红包 {Quantity} 个，获得 {CoinAmount} 胡萝卜",
                userId, quantity, totalCoinAmount);

            return new UseItemResultDto
            {
                Success = true,
                RemainingQuantity = deduction.RemainingQuantity,
                EffectDescription = $"获得 {totalCoinAmount} 胡萝卜"
            };
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "使用萝卜币红包失败：用户={UserId}", userId);
            throw;
        }
    }

    /// <summary>使用双倍经验卡</summary>
#pragma warning disable CS0618
    public Task<UseItemResultDto> UseDoubleExpCardAsync(long userId, long inventoryId)
    {
        return Task.FromResult(CreateUnavailableConsumableResult(ConsumableType.DoubleExpCard));
    }

    /// <summary>使用帖子置顶卡</summary>
    public Task<UseItemResultDto> UsePostPinCardAsync(long userId, long inventoryId, long postId)
    {
        return Task.FromResult(CreateUnavailableConsumableResult(ConsumableType.PostPinCard));
    }

    /// <summary>使用帖子高亮卡</summary>
    public Task<UseItemResultDto> UsePostHighlightCardAsync(long userId, long inventoryId, long postId)
    {
        return Task.FromResult(CreateUnavailableConsumableResult(ConsumableType.PostHighlightCard));
    }
#pragma warning restore CS0618

    #endregion

    #region 道具管理

    /// <summary>扣减道具数量</summary>
    public async Task<bool> DeductItemAsync(long userId, long inventoryId, int quantity = 1)
    {
        try
        {
            var deduction = await _userInventoryRepository.TryDeductItemAsync(userId, inventoryId, quantity);

            if (deduction.Success)
            {
                Log.Information("扣减道具成功：用户={UserId}, 道具={InventoryId}, 扣减={Quantity}, 剩余={Remaining}",
                    userId, inventoryId, quantity, deduction.RemainingQuantity);
            }

            return deduction.Success;
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
        long? itemIconAttachmentId,
        int quantity = 1,
        long? sourceProductId = null)
    {
        try
        {
            var normalizedItemValue = NormalizeItemValue(itemValue);

            // 检查是否已有相同道具
            var existingItem = await _inventoryRepository.QueryFirstAsync(
                i => i.UserId == userId &&
                     i.ConsumableType == consumableType &&
                     i.ItemValue == normalizedItemValue &&
                     !i.IsDeleted);

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
                    ItemValue = normalizedItemValue,
                    ItemName = itemName,
                    ItemIconAttachmentId = itemIconAttachmentId,
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

    private void FillInventoryUrls(List<UserInventoryVo> items)
    {
        foreach (var item in items)
        {
            item.VoItemIcon = ResolveAttachmentUrl(item.VoItemIconAttachmentId);
        }
    }

#pragma warning disable CS0618
    private static bool IsUnavailableConsumableType(ConsumableType consumableType)
    {
        return consumableType is ConsumableType.PostPinCard
            or ConsumableType.PostHighlightCard
            or ConsumableType.DoubleExpCard
            or ConsumableType.LotteryTicket;
    }

    private static UseItemResultDto CreateUnavailableConsumableResult(ConsumableType consumableType)
    {
        var displayName = consumableType switch
        {
            ConsumableType.PostPinCard => "帖子置顶卡",
            ConsumableType.PostHighlightCard => "帖子高亮卡",
            ConsumableType.DoubleExpCard => "双倍经验卡",
            ConsumableType.LotteryTicket => "抽奖券",
            _ => "该道具"
        };

        return new UseItemResultDto
        {
            Success = false,
            ErrorMessage = $"{displayName}暂未开放，当前不可使用"
        };
    }
#pragma warning restore CS0618

    private string? ResolveAttachmentUrl(long? attachmentId)
    {
        if (!attachmentId.HasValue || attachmentId.Value <= 0)
        {
            return null;
        }

        return _attachmentUrlResolver.ResolveAttachmentUrl(attachmentId.Value);
    }

    private static string NormalizeItemValue(string? itemValue)
    {
        return itemValue ?? string.Empty;
    }
}
