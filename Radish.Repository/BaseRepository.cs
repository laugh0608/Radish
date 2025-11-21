using System.Linq.Expressions;
using Radish.IRepository;
using Radish.Repository.UnitOfWorks;
using SqlSugar;
using System.Reflection;

namespace Radish.Repository;

/// <summary>泛型基类仓储</summary>
// 这里的 where TEntity : class, new() 的意思是对泛型进行约束，首先必须是类 class，其次必须可以被实例化 new()
public class BaseRepository<TEntity> : IBaseRepository<TEntity> where TEntity : class, new()
{
    private readonly SqlSugarScope _dbScopeBase;
    private readonly IUnitOfWorkManage _unitOfWorkManage;

    /// <summary>供 BaseRepository 内部使用 ISqlSugarClient 数据库实例</summary>
    /// <remarks>支持多租户切换数据库</remarks>
    private ISqlSugarClient _dbClientBase
    {
        get
        {
            ISqlSugarClient db = _dbScopeBase;

            // 自动切库实现
            // 使用 Model 的特性字段作为切换数据库条件，用 SqlSugar TenantAttribute 存放数据库 ConnId
            // 参考: https://www.donet5.com/Home/Doc?typeId=2246
            var tenantAttr = typeof(TEntity).GetCustomAttribute<TenantAttribute>();
            if (tenantAttr != null)
            {
                // 统一处理 configId 小写
                db = _dbScopeBase.GetConnectionScope(tenantAttr.configId.ToString().ToLower());
                return db;
            }

            // 多租户实现
            //var mta = typeof(TEntity).GetCustomAttribute<MultiTenantAttribute>();
            //if (mta is { TenantType: TenantTypeEnum.Db })
            //{
            //    // 获取租户信息，租户信息可以提前缓存下来 
            //    if (App.User is { TenantId: > 0 })
            //    {
            //        //.WithCache()
            //        var tenant = db.Queryable<SysTenant>().WithCache().Where(s => s.Id == App.User.TenantId).First();
            //        if (tenant != null)
            //        {
            //            var iTenant = db.AsTenant();
            //            if (!iTenant.IsAnyConnection(tenant.ConfigId))
            //            {
            //                iTenant.AddConnection(tenant.GetConnectionConfig());
            //            }
            //            return iTenant.GetConnectionScope(tenant.ConfigId);
            //        }
            //    }
            //}

            return db;
        }
    }

    /// <summary>供外部使用的公开 ISqlSugarClient 数据库实例</summary>
    /// <remarks>继承自私有 ISqlSugarClient _dbClientBase 进而支持多租户切换数据库</remarks>
    public ISqlSugarClient DbBase => _dbClientBase;

    /// <summary>构造函数，注入依赖</summary>
    /// <param name="unitOfWorkManage"></param>
    public BaseRepository(IUnitOfWorkManage unitOfWorkManage)
    {
        _unitOfWorkManage = unitOfWorkManage;
        _dbScopeBase = unitOfWorkManage.GetDbClient();
    }

    #region 增

    /// <summary>写入一条实体类数据</summary>
    /// <param name="entity">泛型实体类</param>
    /// <returns>插入数据的 SnowflakeId, 类型为 long</returns>
    public async Task<long> AddAsync(TEntity entity)
    {
        var insert = _dbClientBase.Insertable(entity);
        return await insert.ExecuteReturnSnowflakeIdAsync();
    }

    /// <summary>分表-写入实体数据</summary>
    /// <param name="entity">泛型实体类</param>
    /// <returns>插入数据的 SnowflakeId, 类型为 long</returns>
    public async Task<List<long>> AddSplitAsync(TEntity entity)
    {
        var insert = _dbClientBase.Insertable(entity).SplitTable();
        // 插入并返回雪花ID并且自动赋值 Id
        return await insert.ExecuteReturnSnowflakeIdListAsync();
    }

    #endregion

    #region 删

    // 删

    #endregion

    #region 改

    // 改

    #endregion

    #region 查

    /// <summary>按照 Where 表达式查询</summary>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <returns>List TEntity</returns>
    public async Task<List<TEntity>> QueryAsync(Expression<Func<TEntity, bool>>? whereExpression = null)
    {
        // DbBase 是 ISqlSugarClient 单例注入的，所以多次查询的 HASH 是一样的，对应的是 Service 层的 Repository 不是单例
        // await Console.Out.WriteLineAsync($"DbBase HashCode: {DbBase.GetHashCode().ToString()}");
        return await _dbClientBase.Queryable<TEntity>().WhereIF(whereExpression != null, whereExpression).ToListAsync();
    }


    /// <summary>分表-按照 Where 表达式查询</summary>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <param name="orderByFields">排序字段，默认为 Id，其他如 Name, Age</param>
    /// <returns>List TEntity</returns>
    public async Task<List<TEntity>> QuerySplitAsync(Expression<Func<TEntity, bool>>? whereExpression,
        string orderByFields = "Id")
    {
        return await _dbClientBase.Queryable<TEntity>()
            .SplitTable()
            .OrderByIF(!string.IsNullOrEmpty(orderByFields), orderByFields)
            .WhereIF(whereExpression != null, whereExpression)
            .ToListAsync();
    }

    #endregion
}