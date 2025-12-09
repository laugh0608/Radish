using Radish.Common.TenantTool;
using Radish.Model.Root;

namespace Radish.Model.Tenants;

/// <summary>
/// 分库，测试业务数据
/// </summary>
/// <remarks>
/// <para>多租户 (不同 TenantId 的数据在不同的数据库里隔离)</para>
/// <para>需要配置 Tenant 表中的数据库连接字符串</para>
/// </remarks>
[MultiTenant(TenantTypeEnum.DataBases)]
public class SubLibBusinessTable : RootEntityTKey<long>
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