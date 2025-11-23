namespace Radish.Common.TenantTool;
/// <summary>
/// 标识多租户的业务表
/// </summary>
/// <remarks>
/// <para>默认设置是多库</para>
/// <para>公共表无需区分，直接使用主库，各自业务在各自库中</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public class MultiTenantAttribute : Attribute
{
    public MultiTenantAttribute()
    {
    }

    public MultiTenantAttribute(TenantTypeEnum tenantType)
    {
        TenantType = tenantType;
    }
    
    /// <summary>
    /// 租户隔离方案
    /// </summary>
    public TenantTypeEnum TenantType { get; set; }
}