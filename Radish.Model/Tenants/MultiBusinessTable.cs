using Radish.Common.TenantTool;
using Radish.Model.Root;

namespace Radish.Model.Tenants;

/// <summary>
/// 分表，测试业务数据
/// </summary>
/// <remarks>多租户 (不同 TenantId 的数据在不同的表里隔离)</remarks>
[MultiTenant(TenantTypeEnum.Tables)]
public class MultiBusinessTable : RootEntityTKey<long>
{
    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 金额
    /// </summary>
    public decimal Amount { get; set; }
}