namespace Radish.Model.Tenants;

/// <summary>
/// 测试业务数据
/// </summary>
/// <remarks>多租户 (Id 隔离)</remarks>
public class BusinessTable : ITenantEntity
{
    /// <summary>
    /// 无需手动赋值
    /// </summary>
    public long TenantId { get; set; }
    
    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 金额
    /// </summary>
    public decimal Amount { get; set; }
}