using Radish.IService.Base;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;

namespace Radish.IService;

/// <summary>用户权益服务接口</summary>
public interface IUserBenefitService : IBaseService<UserBenefit, UserBenefitVo>
{
    #region 权益查询

    /// <summary>获取用户所有权益</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="includeExpired">是否包含已过期的</param>
    /// <returns>权益列表</returns>
    Task<List<UserBenefitVo>> GetUserBenefitsAsync(long userId, bool includeExpired = false);

    /// <summary>获取用户指定类型的权益</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="benefitType">权益类型</param>
    /// <param name="includeExpired">是否包含已过期的</param>
    /// <returns>权益列表</returns>
    Task<List<UserBenefitVo>> GetUserBenefitsByTypeAsync(long userId, BenefitType benefitType, bool includeExpired = false);

    /// <summary>获取用户当前激活的权益</summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>激活的权益列表</returns>
    Task<List<UserBenefitVo>> GetActiveBenefitsAsync(long userId);

    /// <summary>检查用户是否拥有指定权益</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="benefitType">权益类型</param>
    /// <param name="benefitValue">权益值（可选）</param>
    /// <returns>是否拥有</returns>
    Task<bool> HasBenefitAsync(long userId, BenefitType benefitType, string? benefitValue = null);

    #endregion

    #region 权益发放

    /// <summary>发放权益</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="product">商品信息</param>
    /// <param name="orderId">订单 ID</param>
    /// <returns>用户权益 ID</returns>
    Task<long> GrantBenefitAsync(long userId, Product product, long orderId);

    /// <summary>系统赠送权益</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="benefitType">权益类型</param>
    /// <param name="benefitValue">权益值</param>
    /// <param name="benefitName">权益名称</param>
    /// <param name="benefitIcon">权益图标</param>
    /// <param name="durationType">有效期类型</param>
    /// <param name="durationDays">有效期天数</param>
    /// <returns>用户权益 ID</returns>
    Task<long> SystemGrantBenefitAsync(
        long userId,
        BenefitType benefitType,
        string benefitValue,
        string? benefitName = null,
        string? benefitIcon = null,
        DurationType durationType = DurationType.Permanent,
        int? durationDays = null);

    #endregion

    #region 权益激活

    /// <summary>激活权益</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="benefitId">权益 ID</param>
    /// <returns>是否成功</returns>
    Task<bool> ActivateBenefitAsync(long userId, long benefitId);

    /// <summary>取消激活权益</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="benefitId">权益 ID</param>
    /// <returns>是否成功</returns>
    Task<bool> DeactivateBenefitAsync(long userId, long benefitId);

    #endregion

    #region 权益过期处理

    /// <summary>检查并更新过期权益</summary>
    /// <returns>更新的数量</returns>
    Task<int> CheckAndExpireBenefitsAsync();

    #endregion
}
