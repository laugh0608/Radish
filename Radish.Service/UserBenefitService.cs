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
using Radish.Shared.CustomEnum;
using Serilog;
using SqlSugar;

namespace Radish.Service;

/// <summary>用户权益服务实现</summary>
public class UserBenefitService : BaseService<UserBenefit, UserBenefitVo>, IUserBenefitService
{
    private readonly IBaseRepository<UserBenefit> _userBenefitRepository;
    private readonly IUserInventoryRepository _userInventoryRepository;
    private readonly IAttachmentUrlResolver _attachmentUrlResolver;
    private readonly TimeProvider _timeProvider;

    public UserBenefitService(
        IMapper mapper,
        IBaseRepository<UserBenefit> userBenefitRepository,
        IUserInventoryRepository userInventoryRepository,
        IAttachmentUrlResolver attachmentUrlResolver,
        TimeProvider? timeProvider = null)
        : base(mapper, userBenefitRepository)
    {
        _userBenefitRepository = userBenefitRepository;
        _userInventoryRepository = userInventoryRepository;
        _attachmentUrlResolver = attachmentUrlResolver;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    #region 权益查询

    /// <summary>获取用户所有权益</summary>
    public async Task<List<UserBenefitVo>> GetUserBenefitsAsync(long userId, bool includeExpired = false)
    {
        try
        {
            Expression<Func<UserBenefit, bool>> where = b => b.UserId == userId && !b.IsDeleted;

            if (!includeExpired)
            {
                where = where.And(b => !b.IsExpired);
            }

            var benefits = await _userBenefitRepository.QueryAsync(where);
            var benefitVos = Mapper.Map<List<UserBenefitVo>>(benefits.OrderByDescending(b => b.CreateTime).ToList());
            FillBenefitUrls(benefitVos);
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
            Expression<Func<UserBenefit, bool>> where = b => b.UserId == userId && b.BenefitType == benefitType;

            if (!includeExpired)
            {
                where = where.And(b => !b.IsExpired);
            }

            var benefits = await _userBenefitRepository.QueryAsync(where);
            var benefitVos = Mapper.Map<List<UserBenefitVo>>(benefits.OrderByDescending(b => b.CreateTime).ToList());
            FillBenefitUrls(benefitVos);
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
            var benefits = await _userBenefitRepository.QueryAsync(
                b => b.UserId == userId && b.IsActive && !b.IsExpired && !b.IsDeleted);
            var benefitVos = Mapper.Map<List<UserBenefitVo>>(benefits);
            FillBenefitUrls(benefitVos);
            return benefitVos;
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
            Expression<Func<UserBenefit, bool>> where = b =>
                b.UserId == userId &&
                b.BenefitType == benefitType &&
                !b.IsExpired;

            if (!string.IsNullOrWhiteSpace(benefitValue))
            {
                where = where.And(b => b.BenefitValue == benefitValue);
            }

            return await _userBenefitRepository.QueryExistsAsync(where);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "检查用户 {UserId} 是否拥有权益 {BenefitType} 失败", userId, benefitType);
            throw;
        }
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
            var benefit = new UserBenefit
            {
                UserId = userId,
                BenefitType = benefitType,
                BenefitValue = benefitValue,
                BenefitName = benefitName,
                BenefitIconAttachmentId = benefitIconAttachmentId,
                SourceType = "System",
                DurationType = durationType,
                EffectiveAt = GetUtcNow(),
                IsExpired = false,
                IsActive = false,
                CreateTime = GetUtcNow(),
                CreateBy = "System",
                CreateId = 0
            };

            if (durationType == DurationType.Days && durationDays.HasValue)
            {
                benefit.ExpiresAt = GetUtcNow().AddDays(durationDays.Value);
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
    public async Task<bool> ActivateBenefitAsync(long userId, long benefitId)
    {
        try
        {
            var benefit = await _userBenefitRepository.QueryFirstAsync(
                b => b.Id == benefitId && b.UserId == userId && !b.IsDeleted);

            if (benefit == null)
            {
                throw new InvalidOperationException("权益不存在");
            }

            if (benefit.IsExpired)
            {
                throw new InvalidOperationException("权益已过期");
            }

            if (ShopProductAvailabilityPolicy.IsUnavailableBenefitType(benefit.BenefitType))
            {
                throw new InvalidOperationException($"{ShopProductAvailabilityPolicy.GetBenefitTypeDisplayName(benefit.BenefitType)}暂未开放，当前不可激活");
            }

            // 取消同类型的其他激活权益
            var activeBenefits = await _userBenefitRepository.QueryAsync(
                b => b.UserId == userId &&
                     b.BenefitType == benefit.BenefitType &&
                     b.IsActive &&
                     b.Id != benefitId &&
                     !b.IsDeleted);

            foreach (var activeBenefit in activeBenefits)
            {
                activeBenefit.IsActive = false;
                activeBenefit.ModifyTime = GetUtcNow();
                await _userBenefitRepository.UpdateAsync(activeBenefit);
            }

            // 激活当前权益
            benefit.IsActive = true;
            benefit.ActivatedAt = GetUtcNow();
            benefit.ModifyTime = GetUtcNow();

            var result = await _userBenefitRepository.UpdateAsync(benefit);

            if (result)
            {
                Log.Information("权益激活成功：用户={UserId}, 权益ID={BenefitId}", userId, benefitId);
            }

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "激活权益失败：用户={UserId}, 权益ID={BenefitId}", userId, benefitId);
            throw;
        }
    }

    /// <summary>取消激活权益</summary>
    public async Task<bool> DeactivateBenefitAsync(long userId, long benefitId)
    {
        try
        {
            var affected = await _userBenefitRepository.UpdateColumnsAsync(
                b => new UserBenefit
                {
                    IsActive = false,
                    ModifyTime = GetUtcNow()
                },
                b => b.Id == benefitId && b.UserId == userId);

            if (affected > 0)
            {
                Log.Information("权益取消激活成功：用户={UserId}, 权益ID={BenefitId}", userId, benefitId);
            }

            return affected > 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "取消激活权益失败：用户={UserId}, 权益ID={BenefitId}", userId, benefitId);
            throw;
        }
    }

    #endregion

    #region 权益过期处理

    /// <summary>检查并更新过期权益</summary>
    public async Task<int> CheckAndExpireBenefitsAsync()
    {
        try
        {
            var now = GetUtcNow();

            // 查找已过期但未标记的权益
            var expiredBenefits = await _userBenefitRepository.QueryAsync(
                b => !b.IsExpired &&
                     b.ExpiresAt.HasValue &&
                     b.ExpiresAt.Value < now &&
                     !b.IsDeleted);

            if (expiredBenefits.Count == 0)
            {
                return 0;
            }

            foreach (var benefit in expiredBenefits)
            {
                benefit.IsExpired = true;
                benefit.IsActive = false;
                benefit.ModifyTime = now;
            }

            var affected = await _userBenefitRepository.UpdateRangeAsync(expiredBenefits);

            Log.Information("已更新 {Count} 个过期权益", affected);

            return affected;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "检查并更新过期权益失败");
            throw;
        }
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

    private void FillBenefitUrls(List<UserBenefitVo> benefits)
    {
        foreach (var benefit in benefits)
        {
            benefit.VoBenefitIcon = ResolveAttachmentUrl(benefit.VoBenefitIconAttachmentId);
        }
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
