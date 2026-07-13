using System.Security.Cryptography;
using System.Text;
using System.Linq.Expressions;
using AutoMapper;
using Radish.Common;
using Radish.Common.AttributeTool;
using Radish.Common.CoreTool;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service.Base;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;
using Serilog;
using SqlSugar;

namespace Radish.Service;

/// <summary>用户背包服务实现</summary>
public class UserInventoryService : BaseService<UserInventory, UserInventoryVo>, IUserInventoryService
{
    private readonly IBaseRepository<UserInventory> _inventoryRepository;
    private readonly IUserInventoryRepository _userInventoryRepository;
    private readonly IBaseRepository<ShopEntitlementOperation> _operationRepository;
    private readonly IUserService _userService;
    private readonly ICoinService _coinService;
    private readonly IOperationIdempotencyService _operationIdempotencyService;
    private readonly IAttachmentUrlResolver _attachmentUrlResolver;
    private readonly IExperienceService _experienceService;
    private readonly TimeProvider _timeProvider;

    public UserInventoryService(
        IMapper mapper,
        IBaseRepository<UserInventory> inventoryRepository,
        IUserInventoryRepository userInventoryRepository,
        IBaseRepository<ShopEntitlementOperation> operationRepository,
        IUserService userService,
        ICoinService coinService,
        IOperationIdempotencyService operationIdempotencyService,
        IAttachmentUrlResolver attachmentUrlResolver,
        IExperienceService experienceService,
        TimeProvider? timeProvider = null)
        : base(mapper, inventoryRepository)
    {
        _inventoryRepository = inventoryRepository;
        _userInventoryRepository = userInventoryRepository;
        _operationRepository = operationRepository;
        _userService = userService;
        _coinService = coinService;
        _operationIdempotencyService = operationIdempotencyService;
        _attachmentUrlResolver = attachmentUrlResolver;
        _experienceService = experienceService;
        _timeProvider = timeProvider ?? TimeProvider.System;
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
    [UseTran(Propagation = Propagation.Required)]
    public async Task<UseItemResultDto> UseItemAsync(long userId, UseItemDto dto)
    {
        return await UseInventoryItemAsync(
            userId,
            dto.InventoryId,
            dto.Quantity,
            dto.TargetId,
            newDisplayName: null,
            idempotencyKey: dto.IdempotencyKey,
            expectedConsumableType: null);
    }

    /// <summary>使用改名卡</summary>
    [UseTran(Propagation = Propagation.Required)]
    public async Task<UseItemResultDto> UseRenameCardAsync(long userId, UseRenameCardDto dto)
    {
        return await UseInventoryItemAsync(
            userId,
            dto.InventoryId,
            quantity: 1,
            targetId: null,
            newDisplayName: dto.NewDisplayName,
            idempotencyKey: dto.IdempotencyKey,
            expectedConsumableType: ConsumableType.RenameCard);
    }

    private async Task<UseItemResultDto> UseInventoryItemAsync(
        long userId,
        long inventoryId,
        int quantity,
        long? targetId,
        string? newDisplayName,
        string? idempotencyKey,
        ConsumableType? expectedConsumableType)
    {
        if (quantity < 1)
        {
            return Failure("使用数量必须大于 0");
        }

        var normalizedKey = _operationIdempotencyService.NormalizeKey(idempotencyKey);
        if (normalizedKey == null)
        {
            return Failure("幂等键不能为空");
        }

        var item = await _inventoryRepository.QueryFirstAsync(
            inventory => inventory.Id == inventoryId && inventory.UserId == userId && !inventory.IsDeleted);
        if (item == null)
        {
            return Failure("道具不存在");
        }

        if (expectedConsumableType.HasValue && item.ConsumableType != expectedConsumableType.Value)
        {
            return Failure("背包项与请求的道具类型不匹配");
        }

        if (IsUnavailableConsumableType(item.ConsumableType))
        {
            return CreateUnavailableConsumableResult(item.ConsumableType);
        }

        if (item.ConsumableType == ConsumableType.RenameCard && string.IsNullOrWhiteSpace(newDisplayName))
        {
            return Failure("改名卡需要指定新展示名，请使用专用接口");
        }

        if (item.ConsumableType is not (ConsumableType.RenameCard or ConsumableType.ExpCard or ConsumableType.CoinCard))
        {
            return Failure("不支持的道具类型");
        }

        var requestSnapshot = _operationIdempotencyService.CreateRequestSnapshot(
            new Dictionary<string, object?>
            {
                ["OperationType"] = ShopEntitlementOperationTypes.Use,
                ["InventoryId"] = inventoryId,
                ["Quantity"] = quantity,
                ["TargetId"] = targetId,
                ["NewDisplayNameHash"] = HashSensitiveValue(newDisplayName?.Trim())
            });
        var persistedReplay = await ResolvePersistedOperationAsync(
            item.TenantId,
            userId,
            normalizedKey,
            requestSnapshot.RequestHash);
        if (persistedReplay != null)
        {
            return persistedReplay;
        }

        if (item.Quantity < quantity)
        {
            return Failure("道具数量不足");
        }

        var idempotency = await _operationIdempotencyService.BeginAsync(new OperationIdempotencyBeginRequest
        {
            TenantId = item.TenantId,
            UserId = userId,
            OperationType = OperationIdempotencyOperationTypes.ShopInventoryUse,
            IdempotencyKey = normalizedKey,
            RequestHash = requestSnapshot.RequestHash,
            RequestSummary = requestSnapshot.RequestSummary
        });
        var earlyResult = ResolveIdempotencyResult(idempotency);
        if (earlyResult != null)
        {
            return earlyResult;
        }

        var operationId = SnowFlakeSingle.Instance.NextId();
        var deduction = await _userInventoryRepository.TryDeductItemAsync(userId, inventoryId, quantity);
        if (!deduction.Success)
        {
            throw new BusinessException("道具数量不足");
        }

        var effect = await ApplyItemEffectAsync(
            userId,
            item,
            quantity,
            operationId,
            newDisplayName?.Trim());
        var result = new UseItemResultDto
        {
            Success = true,
            OperationId = operationId,
            RemainingQuantity = deduction.RemainingQuantity,
            EffectDescription = effect.Description,
            EffectType = effect.EffectType,
            EffectValue = effect.EffectValue,
            EffectResourceType = effect.ResourceType,
            EffectResourceId = effect.ResourceId,
            EffectResourceNo = effect.ResourceNo
        };
        var resultPayload = _operationIdempotencyService.SerializeResponse(result);

        await _operationRepository.AddAsync(new ShopEntitlementOperation
        {
            Id = operationId,
            TenantId = item.TenantId,
            UserId = userId,
            InventoryId = inventoryId,
            OperationType = ShopEntitlementOperationTypes.Use,
            ConsumableType = item.ConsumableType,
            Quantity = quantity,
            ItemValue = item.ItemValue,
            TargetType = effect.TargetType,
            TargetId = effect.TargetId,
            IdempotencyKey = normalizedKey,
            RequestHash = requestSnapshot.RequestHash,
            EffectType = effect.EffectType,
            EffectValue = effect.EffectValue,
            EffectResourceType = effect.ResourceType,
            EffectResourceId = effect.ResourceId,
            EffectResourceNo = effect.ResourceNo,
            ResultPayload = resultPayload,
            CreateTime = GetUtcNow(),
            CreateBy = $"User_{userId}",
            CreateId = userId
        });

        await _operationIdempotencyService.CompleteSuccessAsync(new OperationIdempotencyCompletionRequest
        {
            RecordId = idempotency.RecordId!.Value,
            ResourceType = OperationIdempotencyResourceTypes.ShopEntitlementOperation,
            ResourceId = operationId,
            ResponsePayload = resultPayload
        });

        Log.Information(
            "用户 {UserId} 使用道具 {InventoryId}，操作={OperationId}, 类型={ConsumableType}, 数量={Quantity}",
            userId,
            inventoryId,
            operationId,
            item.ConsumableType,
            quantity);
        return result;
    }

    private async Task<UseItemEffectResult> ApplyItemEffectAsync(
        long userId,
        UserInventory item,
        int quantity,
        long operationId,
        string? newDisplayName)
    {
        return item.ConsumableType switch
        {
            ConsumableType.RenameCard => await ApplyRenameCardEffectAsync(userId, operationId, newDisplayName!),
            ConsumableType.ExpCard => await ApplyExperienceCardEffectAsync(userId, item, quantity, operationId),
            ConsumableType.CoinCard => await ApplyCoinCardEffectAsync(userId, item, quantity, operationId),
            _ => throw new InvalidOperationException($"不支持的道具类型：{item.ConsumableType}")
        };
    }

    private async Task<UseItemEffectResult> ApplyRenameCardEffectAsync(
        long userId,
        long operationId,
        string newDisplayName)
    {
        bool changed;
        try
        {
            changed = await _userService.ChangeDisplayNameAsync(
                userId,
                newDisplayName,
                new UserDisplayNameChangeContext
                {
                    OperatorUserId = userId,
                    OperatorUserName = $"User_{userId}",
                    Source = UserDisplayNameChangeSources.RenameCard,
                    Reason = $"使用改名卡，商城操作流水 {operationId}"
                });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            throw new BusinessException(ex.Message, ex);
        }

        if (!changed)
        {
            throw new BusinessException("新展示名与当前展示名相同");
        }

        return new UseItemEffectResult(
            ShopEntitlementEffectTypes.DisplayName,
            newDisplayName,
            $"展示名已修改为 {newDisplayName}",
            ShopEntitlementResourceTypes.User,
            userId,
            null,
            ShopEntitlementResourceTypes.User,
            userId);
    }

    private async Task<UseItemEffectResult> ApplyExperienceCardEffectAsync(
        long userId,
        UserInventory item,
        int quantity,
        long operationId)
    {
        if (!int.TryParse(item.ItemValue, out var amount) || amount <= 0)
        {
            throw new BusinessException("经验卡配置错误");
        }

        int totalAmount;
        try
        {
            totalAmount = checked(amount * quantity);
        }
        catch (OverflowException ex)
        {
            throw new BusinessException("经验卡配置超出范围", ex);
        }

        var grantResult = await _experienceService.GrantExperienceOnceAsync(
            userId,
            totalAmount,
            "USE_EXP_CARD",
            BuildEffectBusinessKey(operationId),
            OperationIdempotencyResourceTypes.ShopEntitlementOperation,
            operationId,
            $"使用经验卡获得 {totalAmount} 经验值");
        if (!grantResult.Granted && !grantResult.AlreadyGranted)
        {
            throw new BusinessException(grantResult.Reason ?? "经验值发放失败，请稍后再试");
        }

        return new UseItemEffectResult(
            ShopEntitlementEffectTypes.Experience,
            totalAmount.ToString(),
            $"获得 {totalAmount} 经验值",
            ShopEntitlementResourceTypes.ExpTransaction,
            null,
            null,
            null,
            null);
    }

    private async Task<UseItemEffectResult> ApplyCoinCardEffectAsync(
        long userId,
        UserInventory item,
        int quantity,
        long operationId)
    {
        if (!long.TryParse(item.ItemValue, out var amount) || amount <= 0)
        {
            throw new BusinessException("萝卜币红包配置错误");
        }

        long totalAmount;
        try
        {
            totalAmount = checked(amount * quantity);
        }
        catch (OverflowException ex)
        {
            throw new BusinessException("萝卜币红包配置超出范围", ex);
        }

        var grantResult = await _coinService.GrantCoinOnceAsync(
            userId,
            totalAmount,
            "USE_COIN_CARD",
            BuildEffectBusinessKey(operationId),
            OperationIdempotencyResourceTypes.ShopEntitlementOperation,
            operationId,
            $"使用萝卜币红包获得 {totalAmount} 胡萝卜");

        return new UseItemEffectResult(
            ShopEntitlementEffectTypes.Coin,
            totalAmount.ToString(),
            $"获得 {totalAmount} 胡萝卜",
            ShopEntitlementResourceTypes.CoinTransaction,
            null,
            grantResult.TransactionNo,
            null,
            null);
    }

    private async Task<UseItemResultDto?> ResolvePersistedOperationAsync(
        long tenantId,
        long userId,
        string idempotencyKey,
        string requestHash)
    {
        var operation = await _operationRepository.QueryFirstAsync(item =>
            item.TenantId == tenantId &&
            item.UserId == userId &&
            item.OperationType == ShopEntitlementOperationTypes.Use &&
            item.IdempotencyKey == idempotencyKey);
        if (operation == null)
        {
            return null;
        }

        if (!string.Equals(operation.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return Failure("幂等键已被不同请求使用");
        }

        var result = _operationIdempotencyService.DeserializeResponse<UseItemResultDto>(operation.ResultPayload)
                     ?? throw new InvalidOperationException("商城操作流水缺少可回放结果");
        result.IsIdempotentReplay = true;
        return result;
    }

    private UseItemResultDto? ResolveIdempotencyResult(OperationIdempotencyBeginResult idempotency)
    {
        switch (idempotency.Status)
        {
            case OperationIdempotencyBeginStatus.Started when idempotency.RecordId.HasValue:
                return null;

            case OperationIdempotencyBeginStatus.Succeeded:
            {
                var result = _operationIdempotencyService.DeserializeResponse<UseItemResultDto>(
                    idempotency.ResponsePayload);
                if (result == null)
                {
                    throw new InvalidOperationException("幂等记录缺少可回放结果");
                }

                result.IsIdempotentReplay = true;
                return result;
            }

            case OperationIdempotencyBeginStatus.Processing:
            case OperationIdempotencyBeginStatus.Conflict:
            case OperationIdempotencyBeginStatus.InvalidKey:
                return Failure(idempotency.Message ?? "幂等请求无法处理");

            default:
                throw new InvalidOperationException("幂等记录状态无效");
        }
    }

    private static UseItemResultDto Failure(string message)
    {
        return new UseItemResultDto { Success = false, ErrorMessage = message };
    }

    private static string BuildEffectBusinessKey(long operationId)
    {
        return $"shop-entitlement-use:{operationId}";
    }

    private static string? HashSensitiveValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private DateTime GetUtcNow()
    {
        return _timeProvider.GetUtcNow().UtcDateTime;
    }

    #endregion

    #region 使用流水查询

    /// <summary>分页查询用户的商城消耗品使用流水（管理后台）。</summary>
    public async Task<PageModel<ShopEntitlementOperationVo>> GetOperationsForAdminAsync(
        long userId,
        ConsumableType? consumableType = null,
        int pageIndex = 1,
        int pageSize = 20)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId), "用户 ID 必须大于 0");
        }

        pageIndex = Math.Max(1, pageIndex);
        pageSize = Math.Clamp(pageSize, 1, 100);
        Expression<Func<ShopEntitlementOperation, bool>> where = operation =>
            operation.UserId == userId &&
            operation.OperationType == ShopEntitlementOperationTypes.Use;
        if (consumableType.HasValue)
        {
            where = where.And(operation => operation.ConsumableType == consumableType.Value);
        }

        var (operations, totalCount) = await _operationRepository.QueryPageAsync(
            whereExpression: where,
            pageIndex: pageIndex,
            pageSize: pageSize,
            orderByExpression: operation => operation.CreateTime,
            orderByType: OrderByType.Desc);
        return new PageModel<ShopEntitlementOperationVo>
        {
            Page = pageIndex,
            PageSize = pageSize,
            DataCount = totalCount,
            PageCount = (int)Math.Ceiling((double)totalCount / pageSize),
            Data = operations.Select(operation => new ShopEntitlementOperationVo
            {
                VoId = operation.Id,
                VoUserId = operation.UserId,
                VoInventoryId = operation.InventoryId,
                VoOperationType = operation.OperationType,
                VoConsumableType = operation.ConsumableType,
                VoQuantity = operation.Quantity,
                VoEffectType = operation.EffectType,
                VoEffectValue = operation.EffectValue,
                VoEffectResourceType = operation.EffectResourceType,
                VoEffectResourceId = operation.EffectResourceId,
                VoEffectResourceNo = operation.EffectResourceNo,
                VoCreateTime = operation.CreateTime
            }).ToList()
        };
    }

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

    private sealed record UseItemEffectResult(
        string EffectType,
        string? EffectValue,
        string Description,
        string? ResourceType,
        long? ResourceId,
        string? ResourceNo,
        string? TargetType,
        long? TargetId);
}
