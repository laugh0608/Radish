using Radish.IService.Base;
using Radish.Model;
using Radish.Model.DtoModels;
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

    /// <summary>按照订单快照完成商品履约</summary>
    /// <param name="order">包含不可变商品快照和支付状态的订单</param>
    /// <returns>明确区分持续权益与消耗品背包的履约结果</returns>
    Task<OrderFulfillmentResultDto> GrantOrderFulfillmentAsync(Order order);

    /// <summary>系统赠送权益</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="benefitType">权益类型</param>
    /// <param name="benefitValue">权益值</param>
    /// <param name="benefitName">权益名称</param>
    /// <param name="benefitIconAttachmentId">权益图标附件 Id</param>
    /// <param name="durationType">有效期类型</param>
    /// <param name="durationDays">有效期天数</param>
    /// <returns>用户权益 ID</returns>
    Task<long> SystemGrantBenefitAsync(
        long userId,
        BenefitType benefitType,
        string benefitValue,
        string? benefitName = null,
        long? benefitIconAttachmentId = null,
        DurationType durationType = DurationType.Permanent,
        int? durationDays = null);

    #endregion

    #region 权益激活

    /// <summary>激活权益</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="benefitId">权益 ID</param>
    /// <returns>选择结果与该类型当前权益</returns>
    Task<UserBenefitActionResultVo> ActivateBenefitAsync(long userId, long benefitId);

    /// <summary>取消激活权益</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="benefitId">权益 ID</param>
    /// <returns>停用结果与该类型当前权益</returns>
    Task<UserBenefitActionResultVo> DeactivateBenefitAsync(long userId, long benefitId);

    /// <summary>管理员撤销一份持续权益。</summary>
    Task<UserBenefitActionResultVo> RevokeBenefitAsync(
        long benefitId,
        string reason,
        long operatorId,
        string operatorName);

    #endregion

    #region 权益过期处理

    /// <summary>查询一批已到达 UTC 到期时间且尚未物化的权益 ID。</summary>
    Task<List<long>> GetDueBenefitIdsAsync(int take = 100);

    /// <summary>物化一份权益的过期事实，并可靠请求到期通知。</summary>
    Task<bool> ExpireBenefitAsync(long benefitId);

    /// <summary>管理后台查询用户权益。</summary>
    Task<List<UserBenefitVo>> GetUserBenefitsForAdminAsync(long userId);

    /// <summary>管理后台分页查询商城权益与消耗品业务流水。</summary>
    Task<PageModel<ShopEntitlementOperationVo>> GetOperationsForAdminAsync(
        long userId,
        string? operationType = null,
        BenefitType? benefitType = null,
        ConsumableType? consumableType = null,
        int pageIndex = 1,
        int pageSize = 20);

    #endregion
}
