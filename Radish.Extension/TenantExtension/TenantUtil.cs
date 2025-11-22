using System.Reflection;
using Radish.Common.TenantTool;
using SqlSugar;

namespace Radish.Extension.TenantExtension;

public static class TenantUtil
{
    /// <summary>
    /// 拿到所有 Model 层的实体类
    /// </summary>
    /// <param name="tenantType"></param>
    /// <returns></returns>
    public static List<Type> GetTenantEntityTypes(TenantTypeEnum? tenantType = null)
    {
        return RepositorySetting.Entitys
            .Where(u => !u.IsInterface && !u.IsAbstract && u.IsClass)
            .Where(s => IsTenantEntity(s, tenantType))
            .ToList();
    }

    /// <summary>
    /// 判断拿到的实体类是否标识了租户特性
    /// </summary>
    /// <param name="u"></param>
    /// <param name="tenantType"></param>
    /// <returns></returns>
    public static bool IsTenantEntity(this Type u, TenantTypeEnum? tenantType = null)
    {
        var mta = u.GetCustomAttribute<MultiTenantAttribute>();
        if (mta is null)
        {
            return false;
        }

        if (tenantType != null)
        {
            if (mta.TenantType != tenantType)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 获取租户实体类的表名
    /// </summary>
    /// <param name="type"></param>
    /// <param name="db"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    public static string GetTenantTableName(this Type type, ISqlSugarClient db, string id)
    {
        var entityInfo = db.EntityMaintenance.GetEntityInfo(type);
        return $@"{entityInfo.DbTableName}_{id}";
    }

    /// <summary>
    /// 在执行查询时将表名替换为 $@"{entityInfo.DbTableName}_{TenantId}";
    /// </summary>
    /// <param name="db"></param>
    /// <param name="id"></param>
    public static void SetTenantTable(this ISqlSugarClient db, string id)
    {
        var types = GetTenantEntityTypes(TenantTypeEnum.Tables);

        foreach (var type in types)
        {
            db.MappingTables.Add(type.Name, type.GetTenantTableName(db, id));
        }
    }

    // public static ConnectionConfig GetConnectionConfig(this SysTenant tenant)
    // {
    //     if (tenant.DbType is null)
    //     {
    //         throw new ArgumentException("Tenant DbType Must");
    //     }
    //
    //     return new ConnectionConfig()
    //     {
    //         ConfigId = tenant.ConfigId,
    //         DbType = tenant.DbType.Value,
    //         ConnectionString = tenant.Connection,
    //         IsAutoCloseConnection = true,
    //         MoreSettings = new ConnMoreSettings()
    //         {
    //             IsAutoRemoveDataCache = true,
    //             SqlServerCodeFirstNvarchar = true,
    //         },
    //     };
    // }
}