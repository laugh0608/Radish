namespace Radish.Model.DtoModels;

/// <summary>订单履约结果</summary>
public sealed class OrderFulfillmentResultDto
{
    /// <summary>已发放的持续权益 ID</summary>
    public long? GrantedBenefitId { get; init; }

    /// <summary>已发放的消耗品背包 ID</summary>
    public long? GrantedInventoryId { get; init; }

    /// <summary>履约产生的权益到期时间</summary>
    public DateTime? ExpiresAt { get; init; }
}

/// <summary>
/// 管理端订单备注请求
/// </summary>
public class AdminRemarkOrderDto
{
    /// <summary>
    /// 备注内容
    /// </summary>
    public string? Remark { get; set; }
}
