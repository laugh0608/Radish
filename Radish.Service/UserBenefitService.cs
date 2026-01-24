using System.Linq.Expressions;
using AutoMapper;
using Radish.Common;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;
using Serilog;
using SqlSugar;

namespace Radish.Service;

/// <summary>用户权益服务实现</summary>
public class UserBenefitService : BaseService<UserBenefit, UserBenefitVo>, IUserBenefitService
{
    private readonly IBaseRepository<UserBenefit> _userBenefitRepository;
    private readonly IBaseRepository<UserInventory> _userInventoryRepository;

    public UserBenefitService(
        IMapper mapper,
        IBaseRepository<UserBenefit> userBenefitRepository,
        IBaseRepository<UserInventory> userInventoryRepository)
        : base(mapper, userBenefitRepository)
    {
        _userBenefitRepository = userBenefitRepository;
        _userInventoryRepository = userInventoryRepository;
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
            return Mapper.Map<List<UserBenefitVo>>(benefits.OrderByDescending(b => b.CreateTime).ToList());
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
            return Mapper.Map<List<UserBenefitVo>>(benefits.OrderByDescending(b => b.CreateTime).ToList());
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
            return Mapper.Map<List<UserBenefitVo>>(benefits);
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

    /// <summary>发放权益</summary>
    public async Task<long> GrantBenefitAsync(long userId, Product product, long orderId)
    {
        try
        {
            Log.Information("开始发放权益：用户={UserId}, 商品={ProductId}, 订单={OrderId}",
                userId, product.Id, orderId);

            // 根据商品类型发放不同的权益
            if (product.ProductType == ProductType.Benefit && product.BenefitType.HasValue)
            {
                // 发放权益类商品
                return await GrantBenefitItemAsync(userId, product, orderId);
            }
            else if (product.ProductType == ProductType.Consumable && product.ConsumableType.HasValue)
            {
                // 发放消耗品到背包
                return await GrantConsumableItemAsync(userId, product, orderId);
            }
            else
            {
                throw new InvalidOperationException($"不支持的商品类型：{product.ProductType}");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "发放权益失败：用户={UserId}, 商品={ProductId}", userId, product.Id);
            throw;
        }
    }

    /// <summary>发放权益类商品</summary>
    private async Task<long> GrantBenefitItemAsync(long userId, Product product, long orderId)
    {
        var benefit = new UserBenefit
        {
            UserId = userId,
            BenefitType = product.BenefitType!.Value,
            BenefitValue = product.BenefitValue ?? string.Empty,
            BenefitName = product.Name,
            BenefitIcon = product.Icon,
            SourceOrderId = orderId,
            SourceProductId = product.Id,
            SourceType = "Purchase",
            DurationType = product.DurationType,
            EffectiveAt = DateTime.Now,
            IsExpired = false,
            IsActive = false,
            CreateTime = DateTime.Now,
            CreateBy = "System",
            CreateId = userId
        };

        // 计算到期时间
        if (product.DurationType == DurationType.Days && product.DurationDays.HasValue)
        {
            benefit.ExpiresAt = DateTime.Now.AddDays(product.DurationDays.Value);
        }
        else if (product.DurationType == DurationType.FixedDate && product.ExpiresAt.HasValue)
        {
            benefit.ExpiresAt = product.ExpiresAt;
        }

        var benefitId = await _userBenefitRepository.AddAsync(benefit);

        Log.Information("权益发放成功：用户={UserId}, 权益ID={BenefitId}, 类型={BenefitType}",
            userId, benefitId, product.BenefitType);

        return benefitId;
    }

    /// <summary>发放消耗品到背包</summary>
    private async Task<long> GrantConsumableItemAsync(long userId, Product product, long orderId)
    {
        // 检查背包中是否已有相同类型的道具
        var existingItem = await _userInventoryRepository.QueryFirstAsync(
            i => i.UserId == userId &&
                 i.ConsumableType == product.ConsumableType!.Value &&
                 i.ItemValue == product.BenefitValue &&
                 !i.IsDeleted);

        if (existingItem != null)
        {
            // 增加数量
            existingItem.Quantity += 1;
            existingItem.ModifyTime = DateTime.Now;
            await _userInventoryRepository.UpdateAsync(existingItem);

            Log.Information("消耗品数量增加：用户={UserId}, 道具ID={ItemId}, 新数量={Quantity}",
                userId, existingItem.Id, existingItem.Quantity);

            return existingItem.Id;
        }
        else
        {
            // 创建新的背包项
            var inventory = new UserInventory
            {
                UserId = userId,
                ConsumableType = product.ConsumableType!.Value,
                ItemValue = product.BenefitValue,
                ItemName = product.Name,
                ItemIcon = product.Icon,
                Quantity = 1,
                SourceProductId = product.Id,
                CreateTime = DateTime.Now,
                CreateBy = "System",
                CreateId = userId
            };

            var inventoryId = await _userInventoryRepository.AddAsync(inventory);

            Log.Information("消耗品发放成功：用户={UserId}, 道具ID={ItemId}, 类型={ConsumableType}",
                userId, inventoryId, product.ConsumableType);

            return inventoryId;
        }
    }

    /// <summary>系统赠送权益</summary>
    public async Task<long> SystemGrantBenefitAsync(
        long userId,
        BenefitType benefitType,
        string benefitValue,
        string? benefitName = null,
        string? benefitIcon = null,
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
                BenefitIcon = benefitIcon,
                SourceType = "System",
                DurationType = durationType,
                EffectiveAt = DateTime.Now,
                IsExpired = false,
                IsActive = false,
                CreateTime = DateTime.Now,
                CreateBy = "System",
                CreateId = 0
            };

            if (durationType == DurationType.Days && durationDays.HasValue)
            {
                benefit.ExpiresAt = DateTime.Now.AddDays(durationDays.Value);
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
                activeBenefit.ModifyTime = DateTime.Now;
                await _userBenefitRepository.UpdateAsync(activeBenefit);
            }

            // 激活当前权益
            benefit.IsActive = true;
            benefit.ActivatedAt = DateTime.Now;
            benefit.ModifyTime = DateTime.Now;

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
                    ModifyTime = DateTime.Now
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
            var now = DateTime.Now;

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
}
