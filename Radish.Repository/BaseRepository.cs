using System.Linq.Expressions;
using Radish.IRepository;
using Radish.Repository.UnitOfWorks;
using SqlSugar;
using System.Reflection;
using Radish.Common.CoreTool;
using Radish.Common.TenantTool;
using Radish.Infrastructure.Tenant;
using Radish.Model;

namespace Radish.Repository;

/// <summary>泛型基类仓储</summary>
// 这里的 where TEntity : class, new() 的意思是对泛型进行约束，首先必须是类 class，其次必须可以被实例化 new()
public class BaseRepository<TEntity> : IBaseRepository<TEntity> where TEntity : class, new()
{
    private readonly SqlSugarScope _dbScopeBase;
    private readonly IUnitOfWorkManage _unitOfWorkManage;

    /// <summary>供 BaseRepository 内部使用 ISqlSugarClient 数据库实例</summary>
    /// <remarks>支持多租户切换数据库</remarks>
    private ISqlSugarClient DbClientBase
    {
        get
        {
            ISqlSugarClient db = _dbScopeBase;

            // 自动切库实现（非租户，只是业务切库）
            // 使用 Model 的特性字段作为切换数据库条件，用 SqlSugar TenantAttribute 存放数据库 ConnId
            // 参考: https://www.donet5.com/Home/Doc?typeId=2246
            var tenantAttr = typeof(TEntity).GetCustomAttribute<TenantAttribute>();
            if (tenantAttr != null)
            {
                // 统一处理 configId 小写
                db = _dbScopeBase.GetConnectionScope(tenantAttr.configId.ToString().ToLower());
                return db;
            }

            // 多租户实现-分不同的数据库，需要配置 Tenant 表中的数据库连接字符串
            var mta = typeof(TEntity).GetCustomAttribute<MultiTenantAttribute>();
            if (mta is { TenantType: TenantTypeEnum.DataBases })
            {
                // 获取租户信息，租户信息可以提前缓存下来 
                if (App.HttpContextUser is { TenantId: > 0 })
                {
                    // .WithCache() 走缓存查询
                    var tenant = db.Queryable<Tenant>().WithCache().Where(s => s.Id == App.HttpContextUser.TenantId)
                        .First();
                    if (tenant != null)
                    {
                        var iTenant = db.AsTenant();
                        if (!iTenant.IsAnyConnection(tenant.TenantConfigId))
                        {
                            iTenant.AddConnection(tenant.GetConnectionConfig());
                        }

                        return iTenant.GetConnectionScope(tenant.TenantConfigId);
                    }
                }
            }

            return db;
        }
    }

    /// <summary>供外部使用的公开 ISqlSugarClient 数据库实例</summary>
    /// <remarks>继承自私有 ISqlSugarClient _dbClientBase 进而支持多租户切换数据库</remarks>
    public ISqlSugarClient DbBase => DbClientBase;

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
        var insert = DbClientBase.Insertable(entity);
        return await insert.ExecuteReturnSnowflakeIdAsync();
    }

    /// <summary>批量写入实体数据</summary>
    /// <param name="entities">实体列表</param>
    /// <returns>受影响的行数</returns>
    public async Task<int> AddRangeAsync(List<TEntity> entities)
    {
        return await DbClientBase.Insertable(entities).ExecuteCommandAsync();
    }

    /// <summary>分表-写入实体数据</summary>
    /// <param name="entity">泛型实体类</param>
    /// <returns>插入数据的 SnowflakeId, 类型为 long</returns>
    public async Task<List<long>> AddSplitAsync(TEntity entity)
    {
        var insert = DbClientBase.Insertable(entity).SplitTable();
        // 插入并返回雪花ID并且自动赋值 Id
        return await insert.ExecuteReturnSnowflakeIdListAsync();
    }

    #endregion

    #region 删

    /// <summary>根据 ID 删除实体（物理删除）</summary>
    /// <param name="id">实体 ID</param>
    /// <returns>是否成功</returns>
    public async Task<bool> DeleteByIdAsync(long id)
    {
        return await DbClientBase.Deleteable<TEntity>().In(id).ExecuteCommandHasChangeAsync();
    }

    /// <summary>根据实体删除（物理删除）</summary>
    /// <param name="entity">实体对象</param>
    /// <returns>是否成功</returns>
    public async Task<bool> DeleteAsync(TEntity entity)
    {
        return await DbClientBase.Deleteable(entity).ExecuteCommandHasChangeAsync();
    }

    /// <summary>根据条件删除（物理删除）</summary>
    /// <param name="whereExpression">Where 表达式</param>
    /// <returns>受影响的行数</returns>
    public async Task<int> DeleteAsync(Expression<Func<TEntity, bool>> whereExpression)
    {
        return await DbClientBase.Deleteable<TEntity>().Where(whereExpression).ExecuteCommandAsync();
    }

    /// <summary>批量删除（物理删除）</summary>
    /// <param name="ids">ID 列表</param>
    /// <returns>受影响的行数</returns>
    public async Task<int> DeleteByIdsAsync(List<long> ids)
    {
        return await DbClientBase.Deleteable<TEntity>().In(ids).ExecuteCommandAsync();
    }

    #endregion

    #region 改

    /// <summary>更新实体数据</summary>
    /// <param name="entity">泛型实体类</param>
    /// <returns>是否成功</returns>
    public async Task<bool> UpdateAsync(TEntity entity)
    {
        return await DbClientBase.Updateable(entity).ExecuteCommandHasChangeAsync();
    }

    /// <summary>批量更新实体数据</summary>
    /// <param name="entities">实体列表</param>
    /// <returns>受影响的行数</returns>
    public async Task<int> UpdateRangeAsync(List<TEntity> entities)
    {
        return await DbClientBase.Updateable(entities).ExecuteCommandAsync();
    }

    /// <summary>更新指定列</summary>
    /// <param name="entity">实体对象</param>
    /// <param name="updateColumns">要更新的列表达式</param>
    /// <returns>是否成功</returns>
    /// <example>
    /// UpdateColumnsAsync(entity, it => new { it.Name, it.Age })
    /// </example>
    public async Task<bool> UpdateColumnsAsync(TEntity entity, Expression<Func<TEntity, object>> updateColumns)
    {
        return await DbClientBase.Updateable(entity).UpdateColumns(updateColumns).ExecuteCommandHasChangeAsync();
    }

    /// <summary>根据条件更新指定列</summary>
    /// <param name="updateColumns">更新列表达式</param>
    /// <param name="whereExpression">Where 条件</param>
    /// <returns>受影响的行数</returns>
    /// <example>
    /// UpdateColumnsAsync(it => new TEntity { Name = "newName" }, it => it.Id == 1)
    /// </example>
    public async Task<int> UpdateColumnsAsync(Expression<Func<TEntity, TEntity>> updateColumns, Expression<Func<TEntity, bool>> whereExpression)
    {
        return await DbClientBase.Updateable<TEntity>().SetColumns(updateColumns).Where(whereExpression).ExecuteCommandAsync();
    }

    #endregion

    #region 查

    /// <summary>根据 ID 查询单个实体</summary>
    /// <param name="id">实体 ID</param>
    /// <returns>实体对象，如果不存在则返回 null</returns>
    public async Task<TEntity?> QueryByIdAsync(long id)
    {
        return await DbClientBase.Queryable<TEntity>().InSingleAsync(id);
    }

    /// <summary>查询第一条数据</summary>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <returns>实体对象，如果不存在则返回 null</returns>
    public async Task<TEntity?> QueryFirstAsync(Expression<Func<TEntity, bool>>? whereExpression = null)
    {
        return await DbClientBase.Queryable<TEntity>()
            .WhereIF(whereExpression != null, whereExpression)
            .FirstAsync();
    }

    /// <summary>查询单条数据（多条会抛异常）</summary>
    /// <param name="whereExpression">Where 表达式</param>
    /// <returns>实体对象，如果不存在则返回 null</returns>
    public async Task<TEntity?> QuerySingleAsync(Expression<Func<TEntity, bool>> whereExpression)
    {
        return await DbClientBase.Queryable<TEntity>().SingleAsync(whereExpression);
    }

    /// <summary>按照 Where 表达式查询</summary>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <returns>List TEntity</returns>
    public async Task<List<TEntity>> QueryAsync(Expression<Func<TEntity, bool>>? whereExpression = null)
    {
        // DbBase 是 ISqlSugarClient 单例注入的，所以多次查询的 HASH 是一样的，对应的是 Service 层的 Repository 不是单例
        // await Console.Out.WriteLineAsync($"DbBase HashCode: {DbBase.GetHashCode().ToString()}");
        return await DbClientBase.Queryable<TEntity>().WhereIF(whereExpression != null, whereExpression).ToListAsync();
    }

    /// <summary>按照 Where 表达式查询（使用缓存）</summary>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <param name="cacheTime">使用缓存查询的时间，默认 10 s</param>
    /// <returns>List TEntity</returns>
    public async Task<List<TEntity>> QueryWithCacheAsync(Expression<Func<TEntity, bool>>? whereExpression = null, int cacheTime = 10)
    {
        // return await DbClientBase.Queryable<TEntity>().WhereIF(whereExpression != null, whereExpression).WithCache().ToListAsync();
        // 缓存时间默认为 10 s
        return await DbClientBase.Queryable<TEntity>().WhereIF(whereExpression != null, whereExpression).WithCacheIF(true, cacheTime).ToListAsync();
    }

    /// <summary>分页查询</summary>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="orderByExpression">排序表达式，可空</param>
    /// <param name="orderByType">排序类型（Asc/Desc），默认 Asc</param>
    /// <returns>分页数据和总数</returns>
    public async Task<(List<TEntity> data, int totalCount)> QueryPageAsync(
        Expression<Func<TEntity, bool>>? whereExpression = null,
        int pageIndex = 1,
        int pageSize = 20,
        Expression<Func<TEntity, object>>? orderByExpression = null,
        OrderByType orderByType = OrderByType.Asc)
    {
        RefAsync<int> totalCount = 0;
        var query = DbClientBase.Queryable<TEntity>()
            .WhereIF(whereExpression != null, whereExpression);

        if (orderByExpression != null)
        {
            query = orderByType == OrderByType.Asc
                ? query.OrderBy(orderByExpression)
                : query.OrderByDescending(orderByExpression);
        }

        var data = await query.ToPageListAsync(pageIndex, pageSize, totalCount);

        return (data, totalCount);
    }

    /// <summary>查询数量</summary>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <returns>记录数</returns>
    public async Task<int> QueryCountAsync(Expression<Func<TEntity, bool>>? whereExpression = null)
    {
        return await DbClientBase.Queryable<TEntity>()
            .WhereIF(whereExpression != null, whereExpression)
            .CountAsync();
    }

    /// <summary>查询是否存在</summary>
    /// <param name="whereExpression">Where 表达式</param>
    /// <returns>是否存在</returns>
    public async Task<bool> QueryExistsAsync(Expression<Func<TEntity, bool>> whereExpression)
    {
        return await DbClientBase.Queryable<TEntity>().AnyAsync(whereExpression);
    }

    /// <summary>
    /// 三表联查
    /// </summary>
    /// <typeparam name="T">实体1</typeparam>
    /// <typeparam name="T2">实体2</typeparam>
    /// <typeparam name="T3">实体3</typeparam>
    /// <typeparam name="TResult">返回对象</typeparam>
    /// <param name="joinExpression">关联表达式 (join1,join2) => new object[] {JoinType.Left,join1.UserNo==join2.UserNo}</param>
    /// <param name="selectExpression">返回表达式 (s1, s2) => new { Id =s1.UserNo, Id1 = s2.UserNo}</param>
    /// <param name="whereLambda">查询表达式 (w1, w2) =>w1.UserNo == "")</param>
    /// <returns>List TResult</returns>
    public async Task<List<TResult>> QueryMuchAsync<T, T2, T3, TResult>(
        Expression<Func<T, T2, T3, object[]>> joinExpression,
        Expression<Func<T, T2, T3, TResult>> selectExpression,
        Expression<Func<T, T2, T3, bool>>? whereLambda = null) where T : class, new()
    {
        if (whereLambda == null)
        {
            return await DbClientBase.Queryable(joinExpression).Select(selectExpression).ToListAsync();
        }

        return await DbClientBase.Queryable(joinExpression).Where(whereLambda).Select(selectExpression).ToListAsync();
    }

    /// <summary>分表-按照 Where 表达式查询</summary>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <param name="orderByFields">排序字段，默认为 Id，其他如 Name, Age</param>
    /// <returns>List TEntity</returns>
    public async Task<List<TEntity>> QuerySplitAsync(Expression<Func<TEntity, bool>>? whereExpression,
        string orderByFields = "Id")
    {
        return await DbClientBase.Queryable<TEntity>()
            .SplitTable()
            .OrderByIF(!string.IsNullOrEmpty(orderByFields), orderByFields)
            .WhereIF(whereExpression != null, whereExpression)
            .ToListAsync();
    }

    #endregion
}