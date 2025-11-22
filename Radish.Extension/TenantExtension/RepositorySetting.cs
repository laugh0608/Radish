using Radish.Common.CoreTool;
using Radish.Model.Root;
using Radish.Model.Tenants;
using SqlSugar;

namespace Radish.Extension.TenantExtension;

public class RepositorySetting
{
    /// <summary>
    /// 配置租户数据过滤
    /// </summary>
    public static void SetTenantEntityFilter(SqlSugarScopeProvider db)
    {
        // 如果当前请求的用户 UserId 或 TenantId 小于等于 0，则默认返回表中的所有数据
        if (App.HttpContextUser is not { UserId: > 0, TenantId: > 0 })
        {
            // 所以这里需要谨慎对待，TODO: 后续需要更细致的完善这部分数据的处理与隔离
            return;
        }

        // 如果用户或租户 Id 存在，则对查询的数据根据 TenantId 进行过滤
        
        // 多租户-单表（字段）
        // 也就是说所有人的数据都在一张表里，然后根据这张表里的 TenantId 字段来区分是哪个租户的数据
        // 以 BusinessTable 表为例
        db.QueryFilter.AddTableFilter<ITenantEntity>(it => it.TenantId == App.HttpContextUser.TenantId || it.TenantId == 0);

        // 多租户-多表
        db.SetTenantTable(App.HttpContextUser.TenantId.ToString());
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