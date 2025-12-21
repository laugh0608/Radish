using Radish.Model.Root;

namespace Radish.Model.Tenants;

/// <summary>
/// 分字段，测试业务数据
/// </summary>
/// <remarks>多租户 (一张表里，靠 TenantId 隔离)</remarks>
public class BusinessTable : ITenantEntity
{
    /// <summary>
    /// 无需手动赋值，租户 Id
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