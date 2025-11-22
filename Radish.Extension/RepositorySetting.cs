using Radish.Common.CoreTool;
using Radish.Model.Root;
using Radish.Model.Tenants;
using SqlSugar;

namespace Radish.Extension;

public class RepositorySetting
{
    // private static readonly Lazy<IEnumerable<Type>> AllEntitys = new(() =>
    // {
    //     return typeof(BaseEntity).Assembly
    //         .GetTypes()
    //         .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(BaseEntity)))
    //         .Where(it => it.FullName != null && it.FullName.StartsWith("Blog.Core.Model.Models"));
    // });
    //
    // public static IEnumerable<Type> Entitys => AllEntitys.Value;
    //
    // /// <summary>
    // /// 配置实体软删除过滤器<br/>
    // /// 统一过滤 软删除 无需自己写条件
    // /// </summary>
    // public static void SetDeletedEntityFilter(SqlSugarScopeProvider db)
    // {
    //     db.QueryFilter.AddTableFilter<IDeleteFilter>(it => it.IsDeleted == false);
    // }
    //
    // /// <summary>
    // /// 配置租户
    // /// </summary>
    // public static void SetTenantEntityFilter(SqlSugarScopeProvider db)
    // {
    //     if (App.HttpContextUser is not { UserId: > 0, TenantId: > 0 })
    //     {
    //         return;
    //     }
    //
    //     //多租户 单表
    //     db.QueryFilter.AddTableFilter<ITenantEntity>(it => it.TenantId == App.HttpContextUser.TenantId || it.TenantId == 0);
    //
    //     //多租户 多表
    //     db.SetTenantTable(App.HttpContextUser.TenantId.ToString());
    // }
    
    /// <summary>
    /// 配置租户
    /// </summary>
    public static void SetTenantEntityFilter(SqlSugarScopeProvider db)
    {
        if (App.HttpContextUser is not { UserId: > 0, TenantId: > 0 })
        {
            return;
        }

        // 多租户-单表（字段）
        // 也就是说所有人的数据都在一张表里，然后根据这张表里的 TenantId 字段来区分是哪个租户的数据
        // 以 BusinessTable 表为例
        db.QueryFilter.AddTableFilter<ITenantEntity>(it => it.TenantId == App.HttpContextUser.TenantId || it.TenantId == 0);

        // 多租户 多表
        // db.SetTenantTable(App.User.TenantId.ToString());
    }

    // private static readonly Lazy<IEnumerable<Type>> AllEntitys = new(() =>
    // {
    //     return typeof(RootEntityTKey<>).Assembly
    //         .GetTypes()
    //         .Where(t => t.IsClass && !t.IsAbstract)
    //         .Where(it => it.FullName != null && it.FullName.StartsWith("Radish.Model"));
    // });
    // public static IEnumerable<Type> Entitys => AllEntitys.Value;
}