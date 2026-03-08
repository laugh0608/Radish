using Radish.Common.CoreTool;
using Radish.Model.Root;
using Radish.Model.Tenants;
using SqlSugar;

namespace Radish.Infrastructure.Tenant;

/// <summary>
/// SqlSugar 仓储配置扩展
/// </summary>
public static class RepositorySetting
{
    /// <summary>
    /// 配置租户数据过滤（字段隔离 + 分表）
    /// </summary>
    public static void SetTenantEntityFilter(SqlSugarScopeProvider db)
    {
        var tenantId = App.CurrentUser.TenantId;

        // 多租户-单表（字段）
        // 也就是说所有人的数据都在一张表里，然后根据这张表里的 TenantId 字段来区分是哪个租户的数据
        // 规则：
        // 1. tenantId > 0：可见“当前租户 + 公共租户(0)”数据
        // 2. tenantId <= 0：仅可见公共租户(0)数据，避免越权读取其他租户
        if (tenantId > 0)
        {
            db.QueryFilter.AddTableFilter<ITenantEntity>(it => it.TenantId == tenantId || it.TenantId == 0);
        }
        else
        {
            db.QueryFilter.AddTableFilter<ITenantEntity>(it => it.TenantId == 0);
        }

        // 多租户-多表
        // 仅在实际租户上下文时启用分表切换；tenantId<=0 走公共表
        if (tenantId > 0)
        {
            db.SetTenantTable(tenantId.ToString());
        }
    }

    private static readonly Lazy<IEnumerable<Type>> AllEntitys = new(() =>
    {
        return typeof(RootEntityTKey<>).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(it => it.FullName != null && it.FullName.StartsWith("Radish.Model"));
    });

    public static IEnumerable<Type> Entitys => AllEntitys.Value;
}
