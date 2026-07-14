using System.Linq.Expressions;
using AutoMapper;
using Radish.Common;
using Radish.Common.AttributeTool;
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

/// <summary>用户权益服务实现</summary>
public class UserBenefitService : BaseService<UserBenefit, UserBenefitVo>, IUserBenefitService
{
    private readonly IBaseRepository<UserBenefit> _userBenefitRepository;
    private readonly IBaseRepository<ShopEntitlementOperation> _operationRepository;
    private readonly IUserBenefitRepository _userBenefitCustomRepository;
    private readonly IUserInventoryRepository _userInventoryRepository;
    private readonly IAttachmentUrlResolver _attachmentUrlResolver;
    private readonly IReliableOutboxService? _reliableOutboxService;
    private readonly TimeProvider _timeProvider;

    public UserBenefitService(
        IMapper mapper,
        IBaseRepository<UserBenefit> userBenefitRepository,
        IBaseRepository<ShopEntitlementOperation> operationRepository,
        IUserBenefitRepository userBenefitCustomRepository,
        IUserInventoryRepository userInventoryRepository,
        IAttachmentUrlResolver attachmentUrlResolver,
        IReliableOutboxService? reliableOutboxService = null,
        TimeProvider? timeProvider = null)
        : base(mapper, userBenefitRepository)
    {
        _userBenefitRepository = userBenefitRepository;
        _operationRepository = operationRepository;
        _userBenefitCustomRepository = userBenefitCustomRepository;
        _userInventoryRepository = userInventoryRepository;
        _attachmentUrlResolver = attachmentUrlResolver;
        _reliableOutboxService = reliableOutboxService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    #region 权益查询

    /// <summary>获取用户所有权益</summary>
    public async Task<List<UserBenefitVo>> GetUserBenefitsAsync(long userId, bool includeExpired = false)
    {
        try
        {
            var now = GetUtcNow();
            var benefits = await _userBenefitRepository.QueryAsync(b => b.UserId == userId && !b.IsDeleted);
            if (!includeExpired)
            {
                benefits = benefits
                    .Where(benefit => IsUsableAt(benefit, now))
                    .ToList();
            }
            var selections = await _userBenefitCustomRepository.GetActiveSelectionsAsync(userId);
            var benefitVos = Mapper.Map<List<UserBenefitVo>>(benefits.OrderByDescending(b => b.CreateTime).ToList());
            FillBenefitState(benefits, benefitVos, selections, now);
            return benefitVos;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取用户 {UserId} 权益列表失败", userId);
            throw;
        }
    }

    /// <summary>获取用户指定类型的权益</summary>
    public async Task<List<UserBenefitVo>> GetUserBenefitsByTypeAsync(long userId, BenefitType benefitType, bool includeExpired = false)
    {
        try
        {
            var now = GetUtcNow();
            var benefits = await _userBenefitRepository.QueryAsync(b =>
                b.UserId == userId &&
                b.BenefitType == benefitType &&
                !b.IsDeleted);
            if (!includeExpired)
            {
                benefits = benefits
                    .Where(benefit => IsUsableAt(benefit, now))
                    .ToList();
            }
            var selections = await _userBenefitCustomRepository.GetActiveSelectionsAsync(userId);
            var benefitVos = Mapper.Map<List<UserBenefitVo>>(benefits.OrderByDescending(b => b.CreateTime).ToList());
            FillBenefitState(benefits, benefitVos, selections, now);
            return benefitVos;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取用户 {UserId} 类型 {BenefitType} 权益列表失败", userId, benefitType);
            throw;
        }
    }

    /// <summary>获取用户当前激活的权益</summary>
    public async Task<List<UserBenefitVo>> GetActiveBenefitsAsync(long userId)
    {
        try
        {
            var benefits = await GetUserBenefitsAsync(userId);
            return benefits.Where(benefit => benefit.VoStatus == UserBenefitStatus.Active).ToList();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取用户 {UserId} 激活权益列表失败", userId);
            throw;
        }
    }

    /// <summary>检查用户是否拥有指定权益</summary>
    public async Task<bool> HasBenefitAsync(long userId, BenefitType benefitType, string? benefitValue = null)
    {
        try
        {
            var now = GetUtcNow();
            Expression<Func<UserBenefit, bool>> where = b =>
                b.UserId == userId &&
                b.BenefitType == benefitType &&
                !b.IsDeleted &&
                b.RevokedAt == null;

            if (!string.IsNullOrWhiteSpace(benefitValue))
            {
                where = where.And(b => b.BenefitValue == benefitValue);
            }

            var benefits = await _userBenefitRepository.QueryAsync(where);
            return benefits.Any(benefit =>
                benefit.EffectiveAt <= now &&
                IsUsableAt(benefit, now));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "检查用户 {UserId} 是否拥有权益 {BenefitType} 失败", userId, benefitType);
            throw;
        }
    }

    #endregion

    #region 管理后台查询

    public Task<List<UserBenefitVo>> GetUserBenefitsForAdminAsync(long userId)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId), "用户 ID 必须大于 0");
        }

        return GetUserBenefitsAsync(userId, includeExpired: true);
    }

    public async Task<PageModel<ShopEntitlementOperationVo>> GetOperationsForAdminAsync(
        long userId,
        string? operationType = null,
        BenefitType? benefitType = null,
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
        Expression<Func<ShopEntitlementOperation, bool>> where = operation => operation.UserId == userId;
        if (!string.IsNullOrWhiteSpace(operationType))
        {
            var normalizedOperationType = operationType.Trim();
            where = where.And(operation => operation.OperationType == normalizedOperationType);
        }

        if (benefitType.HasValue)
        {
            where = where.And(operation => operation.BenefitType == benefitType.Value);
        }

        if (consumableType.HasValue)
        {
            where = where.And(operation => operation.ConsumableType == consumableType.Value);
        }

        var (operations, totalCount) = await _operationRepository.QueryPageAsync(
            where,
            pageIndex,
            pageSize,
            operation => operation.CreateTime,
            OrderByType.Desc);
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
                VoBenefitId = operation.BenefitId,
                VoRelatedBenefitId = operation.RelatedBenefitId,
                VoOperationType = operation.OperationType,
                VoConsumableType = operation.ConsumableType,
                VoBenefitType = operation.BenefitType,
                VoQuantity = operation.Quantity,
                VoReason = operation.Reason,
                VoEffectType = operation.EffectType,
                VoEffectValue = operation.EffectValue,
                VoEffectResourceType = operation.EffectResourceType,
                VoEffectResourceId = operation.EffectResourceId,
                VoEffectResourceNo = operation.EffectResourceNo,
                VoCreateTime = operation.CreateTime,
                VoCreateBy = operation.CreateBy
            }).ToList()
        };
    }

    #endregion

    #region 权益发放

    /// <summary>按照订单快照完成商品履约</summary>
    [UseTran(Propagation = Propagation.Required)]
    public async Task<OrderFulfillmentResultDto> GrantOrderFulfillmentAsync(Order order)
    {
        try
        {
            EnsureValidOrderSnapshot(order);

            Log.Information("开始发放权益：用户={UserId}, 商品={ProductId}, 订单={OrderId}",
                order.UserId, order.ProductId, order.Id);

            if (order.ProductType == ProductType.Benefit && order.BenefitType.HasValue)
            {
                return await GrantBenefitItemAsync(order);
            }

            if (order.ProductType == ProductType.Consumable && order.ConsumableType.HasValue)
            {
                return await GrantConsumableItemAsync(order);
            }

            throw new InvalidOperationException($"不支持的商品类型：{order.ProductType}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "发放权益失败：用户={UserId}, 商品={ProductId}, 订单={OrderId}",
                order.UserId, order.ProductId, order.Id);
            throw;
        }
    }

    /// <summary>发放权益类商品</summary>
    private async Task<OrderFulfillmentResultDto> GrantBenefitItemAsync(Order order)
    {
        var existingBenefit = await _userBenefitRepository.QueryFirstAsync(
            benefit => benefit.SourceOrderId == order.Id && !benefit.IsDeleted);
        if (existingBenefit != null)
        {
            return new OrderFulfillmentResultDto
            {
                GrantedBenefitId = existingBenefit.Id,
                ExpiresAt = existingBenefit.ExpiresAt
            };
        }

        var now = GetUtcNow();
        var expiresAt = ResolveBenefitExpiresAt(order, now);
        var benefit = new UserBenefit
        {
            TenantId = order.TenantId,
            UserId = order.UserId,
            BenefitType = order.BenefitType!.Value,
            BenefitValue = order.BenefitValue ?? string.Empty,
            BenefitName = order.ProductName,
            BenefitIconAttachmentId = order.ProductIconAttachmentId,
            SourceOrderId = order.Id,
            SourceProductId = order.ProductId,
            SourceType = "Purchase",
            DurationType = order.DurationType,
            EffectiveAt = now,
            ExpiresAt = expiresAt,
            IsExpired = false,
            IsActive = false,
            CreateTime = now,
            CreateBy = "System",
            CreateId = order.UserId
        };

        long benefitId;
        try
        {
            benefitId = await _userBenefitRepository.AddAsync(benefit);
        }
        catch (Exception ex) when (IsUniqueConstraintException(ex))
        {
            existingBenefit = await _userBenefitRepository.QueryFirstAsync(
                storedBenefit => storedBenefit.SourceOrderId == order.Id && !storedBenefit.IsDeleted);
            if (existingBenefit != null)
            {
                return new OrderFulfillmentResultDto
                {
                    GrantedBenefitId = existingBenefit.Id,
                    ExpiresAt = existingBenefit.ExpiresAt
                };
            }

            throw;
        }

        Log.Information("权益发放成功：用户={UserId}, 权益ID={BenefitId}, 类型={BenefitType}",
            order.UserId, benefitId, order.BenefitType);

        return new OrderFulfillmentResultDto
        {
            GrantedBenefitId = benefitId,
            ExpiresAt = expiresAt
        };
    }

    /// <summary>发放消耗品到背包</summary>
    private async Task<OrderFulfillmentResultDto> GrantConsumableItemAsync(Order order)
    {
        var grantResult = await _userInventoryRepository.GrantConsumableForOrderAsync(
            order.TenantId,
            order.UserId,
            order.ConsumableType!.Value,
            order.BenefitValue,
            order.ProductName,
            order.ProductIconAttachmentId,
            order.Quantity,
            order.Id,
            order.ProductId);

        Log.Information(
            grantResult.CreatedGrantRecord
                ? "消耗品发放成功：用户={UserId}, 道具ID={ItemId}, 类型={ConsumableType}, 数量={Quantity}"
                : "消耗品订单已发放，复用既有背包项：用户={UserId}, 道具ID={ItemId}, 类型={ConsumableType}, 当前数量={Quantity}",
            order.UserId,
            grantResult.InventoryId,
            order.ConsumableType,
            grantResult.CreatedGrantRecord ? grantResult.QuantityDelta : grantResult.CurrentQuantity);

        return new OrderFulfillmentResultDto
        {
            GrantedInventoryId = grantResult.InventoryId
        };
    }

    /// <summary>系统赠送权益</summary>
    public async Task<long> SystemGrantBenefitAsync(
        long userId,
        BenefitType benefitType,
        string benefitValue,
        string? benefitName = null,
        long? benefitIconAttachmentId = null,
        DurationType durationType = DurationType.Permanent,
        int? durationDays = null)
    {
        try
        {
            var now = GetUtcNow();
            var benefit = new UserBenefit
            {
                UserId = userId,
                BenefitType = benefitType,
                BenefitValue = benefitValue,
                BenefitName = benefitName,
                BenefitIconAttachmentId = benefitIconAttachmentId,
                SourceType = "System",
                DurationType = durationType,
                EffectiveAt = now,
                IsExpired = false,
                IsActive = false,
                CreateTime = now,
                CreateBy = "System",
                CreateId = 0
            };

            if (durationType == DurationType.Days && durationDays.HasValue)
            {
                benefit.ExpiresAt = now.AddDays(durationDays.Value);
            }

            var benefitId = await _userBenefitRepository.AddAsync(benefit);

            Log.Information("系统赠送权益成功：用户={UserId}, 权益ID={BenefitId}, 类型={BenefitType}",
                userId, benefitId, benefitType);

            return benefitId;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "系统赠送权益失败：用户={UserId}, 类型={BenefitType}", userId, benefitType);
            throw;
        }
    }

    #endregion

    #region 权益激活

    /// <summary>激活权益</summary>
    [UseTran(Propagation = Propagation.Required)]
    public async Task<UserBenefitActionResultVo> ActivateBenefitAsync(long userId, long benefitId)
    {
        var benefit = await _userBenefitRepository.QueryFirstAsync(
            item => item.Id == benefitId && item.UserId == userId && !item.IsDeleted)
            ?? throw new InvalidOperationException("权益不存在");
        var now = GetUtcNow();
        var status = ResolveStatus(benefit, false, now);
        if (status == UserBenefitStatus.Revoked)
        {
            throw new InvalidOperationException("权益已撤销");
        }

        if (status == UserBenefitStatus.Expired)
        {
            throw new InvalidOperationException("权益已过期");
        }

        if (ShopProductAvailabilityPolicy.IsUnavailableBenefitType(benefit.BenefitType))
        {
            throw new InvalidOperationException(
                $"{ShopProductAvailabilityPolicy.GetBenefitTypeDisplayName(benefit.BenefitType)}暂未开放，当前不可激活");
        }

        if (benefit.BenefitType == BenefitType.Theme && !ShopThemeResources.IsSupported(benefit.BenefitValue))
        {
            throw new InvalidOperationException("主题资源不存在或已下线，当前不可激活");
        }

        var result = await _userBenefitCustomRepository.ActivateAsync(userId, benefitId, userId, "User", now);
        Log.Information("权益选择完成：用户={UserId}, 权益ID={BenefitId}, Changed={Changed}",
            userId, benefitId, result.Changed);
        return await BuildActionResultAsync(result, ShopEntitlementOperationTypes.Activate, now);
    }

    /// <summary>取消激活权益</summary>
    [UseTran(Propagation = Propagation.Required)]
    public async Task<UserBenefitActionResultVo> DeactivateBenefitAsync(long userId, long benefitId)
    {
        var now = GetUtcNow();
        var result = await _userBenefitCustomRepository.DeactivateAsync(userId, benefitId, userId, "User", now);
        Log.Information("权益停用完成：用户={UserId}, 权益ID={BenefitId}, Changed={Changed}",
            userId, benefitId, result.Changed);
        return await BuildActionResultAsync(result, ShopEntitlementOperationTypes.Deactivate, now);
    }

    /// <summary>管理员撤销持续权益。</summary>
    [UseTran(Propagation = Propagation.Required)]
    public async Task<UserBenefitActionResultVo> RevokeBenefitAsync(
        long benefitId,
        string reason,
        long operatorId,
        string operatorName)
    {
        var normalizedReason = reason.Trim();
        if (normalizedReason.Length is < 2 or > 500)
        {
            throw new InvalidOperationException("撤销原因长度必须为 2-500 个字符");
        }

        var now = GetUtcNow();
        var result = await _userBenefitCustomRepository.RevokeAsync(
            benefitId,
            normalizedReason,
            operatorId,
            operatorName,
            now);
        Log.Information("管理员撤销权益完成：权益ID={BenefitId}, 操作者={OperatorId}, Changed={Changed}",
            benefitId, operatorId, result.Changed);
        return await BuildActionResultAsync(result, ShopEntitlementOperationTypes.Revoke, now);
    }

    #endregion

    #region 权益过期处理

    public Task<List<long>> GetDueBenefitIdsAsync(int take = 100)
    {
        return _userBenefitCustomRepository.GetDueBenefitIdsAsync(GetUtcNow(), take);
    }

    /// <summary>物化单份权益的过期事实。</summary>
    [UseTran(Propagation = Propagation.Required)]
    public async Task<bool> ExpireBenefitAsync(long benefitId)
    {
        var now = GetUtcNow();
        var result = await _userBenefitCustomRepository.ExpireAsync(benefitId, now);
        if (result is not { Changed: true })
        {
            return false;
        }

        var reliableOutboxService = _reliableOutboxService
            ?? throw new InvalidOperationException("可靠 Outbox 服务未注册");
        var notification = new CreateNotificationDto
        {
            NotificationId = SnowFlakeSingle.Instance.NextId(),
            BusinessKey = $"notification:benefit-expired:{benefitId}",
            Type = "BENEFIT_EXPIRED",
            Title = "权益已到期",
            Content = $"您的权益“{result.Benefit.BenefitName ?? result.Benefit.BenefitValue}”已到期",
            BusinessType = "UserBenefit",
            BusinessId = benefitId,
            ReceiverUserIds = [result.Benefit.UserId],
            TenantId = result.Benefit.TenantId
        };
        await reliableOutboxService.AddAsync(
            ReliableOutboxSources.Main,
            result.Benefit.TenantId,
            ReliableTaskTypes.NotificationRequested,
            $"task:notification:benefit-expired:{benefitId}",
            "UserBenefit",
            benefitId.ToString(),
            new NotificationRequestedTaskPayload(notification),
            now);
        return true;
    }

    #endregion

    private static void EnsureValidOrderSnapshot(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (order.Id <= 0 || order.UserId <= 0 || order.ProductId <= 0)
        {
            throw new InvalidOperationException("订单履约快照缺少有效的订单、用户或商品 ID");
        }

        if (order.Quantity < 1)
        {
            throw new InvalidOperationException("订单履约数量必须大于 0");
        }

        if (order.Status != OrderStatus.Paid &&
            !(order.Status == OrderStatus.Failed && order.FailureStage == OrderFailureStage.Fulfillment))
        {
            throw new InvalidOperationException("只有已支付或履约失败的订单可以发放商品");
        }

        if (order.ProductType == ProductType.Benefit && !order.BenefitType.HasValue)
        {
            throw new InvalidOperationException("权益订单快照缺少权益类型");
        }

        if (order.ProductType == ProductType.Consumable && !order.ConsumableType.HasValue)
        {
            throw new InvalidOperationException("消耗品订单快照缺少消耗品类型");
        }
    }

    private static DateTime? ResolveBenefitExpiresAt(Order order, DateTime now)
    {
        return order.DurationType switch
        {
            DurationType.Permanent => null,
            DurationType.Days when order.DurationDays is > 0 => now.AddDays(order.DurationDays.Value),
            DurationType.Days => throw new InvalidOperationException("权益订单快照缺少有效期天数"),
            DurationType.FixedDate when order.FixedExpiresAt.HasValue && order.FixedExpiresAt.Value > now =>
                order.FixedExpiresAt.Value,
            DurationType.FixedDate when order.FixedExpiresAt.HasValue =>
                throw new InvalidOperationException("权益订单的固定到期时间已过，需人工处理"),
            DurationType.FixedDate => throw new InvalidOperationException("权益订单快照缺少固定到期时间"),
            _ => throw new InvalidOperationException($"不支持的有效期类型：{order.DurationType}")
        };
    }

    private async Task<UserBenefitActionResultVo> BuildActionResultAsync(
        UserBenefitPersistenceResult result,
        string action,
        DateTime nowUtc)
    {
        UserBenefitVo? currentBenefit = null;
        if (result.CurrentBenefitId.HasValue)
        {
            var current = await _userBenefitRepository.QueryFirstAsync(
                benefit => benefit.Id == result.CurrentBenefitId.Value && !benefit.IsDeleted);
            if (current != null)
            {
                currentBenefit = Mapper.Map<UserBenefitVo>(current);
                FillBenefitState(
                    [current],
                    [currentBenefit],
                    [new UserActiveBenefit
                    {
                        TenantId = current.TenantId,
                        UserId = current.UserId,
                        BenefitType = current.BenefitType,
                        BenefitId = current.Id,
                        SelectedAt = nowUtc
                    }],
                    nowUtc);
            }
        }

        return new UserBenefitActionResultVo
        {
            VoChanged = result.Changed,
            VoAction = action,
            VoBenefitId = result.Benefit.Id,
            VoBenefitType = result.Benefit.BenefitType,
            VoStatus = ResolveStatus(
                result.Benefit,
                result.CurrentBenefitId == result.Benefit.Id,
                nowUtc),
            VoCurrentBenefitId = result.CurrentBenefitId,
            VoCurrentBenefit = currentBenefit
        };
    }

    private void FillBenefitState(
        IReadOnlyList<UserBenefit> benefits,
        IReadOnlyList<UserBenefitVo> benefitVos,
        IReadOnlyList<UserActiveBenefit> selections,
        DateTime nowUtc)
    {
        var activeBenefitIds = selections.Select(selection => selection.BenefitId).ToHashSet();
        var entitiesById = benefits.ToDictionary(benefit => benefit.Id);
        foreach (var benefitVo in benefitVos)
        {
            if (!entitiesById.TryGetValue(benefitVo.VoId, out var benefit))
            {
                continue;
            }

            var status = ResolveStatus(benefit, activeBenefitIds.Contains(benefit.Id), nowUtc);
            benefitVo.VoStatus = status;
            benefitVo.VoIsExpired = status == UserBenefitStatus.Expired;
            benefitVo.VoIsActive = status == UserBenefitStatus.Active;
            benefitVo.VoCanDeactivate = status == UserBenefitStatus.Active;
            benefitVo.VoCanActivate = status == UserBenefitStatus.Available &&
                                      benefit.EffectiveAt <= nowUtc &&
                                      !ShopProductAvailabilityPolicy.IsUnavailableBenefitType(benefit.BenefitType) &&
                                      (benefit.BenefitType != BenefitType.Theme ||
                                       ShopThemeResources.IsSupported(benefit.BenefitValue));
            benefitVo.VoUnavailableReason = GetUnavailableReason(benefit, status, nowUtc);
            benefitVo.VoDurationDisplay = GetDurationDisplay(benefit, status);
            benefitVo.VoBenefitIcon = ResolveAttachmentUrl(benefitVo.VoBenefitIconAttachmentId);
        }
    }

    private static UserBenefitStatus ResolveStatus(UserBenefit benefit, bool hasActiveSelection, DateTime nowUtc)
    {
        if (benefit.RevokedAt.HasValue)
        {
            return UserBenefitStatus.Revoked;
        }

        if (benefit.ExpiresAt.HasValue && benefit.ExpiresAt.Value <= nowUtc)
        {
            return UserBenefitStatus.Expired;
        }

        return hasActiveSelection ? UserBenefitStatus.Active : UserBenefitStatus.Available;
    }

    private static bool IsUsableAt(UserBenefit benefit, DateTime nowUtc)
    {
        return !benefit.RevokedAt.HasValue &&
               (!benefit.ExpiresAt.HasValue || benefit.ExpiresAt.Value > nowUtc);
    }

    private static string? GetUnavailableReason(
        UserBenefit benefit,
        UserBenefitStatus status,
        DateTime nowUtc)
    {
        return status switch
        {
            UserBenefitStatus.Revoked => benefit.RevocationReason ?? "权益已撤销",
            UserBenefitStatus.Expired => "权益已过期",
            _ when benefit.BenefitType == BenefitType.Theme && !ShopThemeResources.IsSupported(benefit.BenefitValue) =>
                "主题资源不存在或已下线",
            UserBenefitStatus.Active => null,
            _ when benefit.EffectiveAt > nowUtc => "权益尚未生效",
            _ when ShopProductAvailabilityPolicy.IsUnavailableBenefitType(benefit.BenefitType) =>
                $"{ShopProductAvailabilityPolicy.GetBenefitTypeDisplayName(benefit.BenefitType)}暂未开放",
            _ => null
        };
    }

    private static string GetDurationDisplay(UserBenefit benefit, UserBenefitStatus status)
    {
        if (status == UserBenefitStatus.Revoked)
        {
            return "已撤销";
        }

        if (status == UserBenefitStatus.Expired)
        {
            return "已过期";
        }

        if (benefit.DurationType == DurationType.Permanent)
        {
            return "永久";
        }

        return benefit.ExpiresAt.HasValue
            ? $"有效至 {benefit.ExpiresAt.Value:yyyy-MM-dd HH:mm} UTC"
            : "未知";
    }

    private string? ResolveAttachmentUrl(long? attachmentId)
    {
        if (!attachmentId.HasValue || attachmentId.Value <= 0)
        {
            return null;
        }

        return _attachmentUrlResolver.ResolveAttachmentUrl(attachmentId.Value);
    }

    private static bool IsUniqueConstraintException(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private DateTime GetUtcNow()
    {
        return _timeProvider.GetUtcNow().UtcDateTime;
    }
}
