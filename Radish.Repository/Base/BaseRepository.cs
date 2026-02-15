using System.Linq.Expressions;
using System.Reflection;
using System.Collections.Concurrent;
using Radish.Common.CoreTool;
using Radish.Common.OptionTool;
using Radish.Common.TenantTool;
using Radish.Common.TimeTool;
using Radish.Infrastructure.Tenant;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.Model;
using Radish.Model.Root;
using Radish.Repository.UnitOfWorks;
using SqlSugar;

namespace Radish.Repository.Base;

/// <summary>泛型基类仓储</summary>
// 这里的 where TEntity : class, new() 的意思是对泛型进行约束，首先必须是类 class，其次必须可以被实例化 new()
public class BaseRepository<TEntity> : IBaseRepository<TEntity> where TEntity : class, new()
{
    private readonly SqlSugarScope _dbScopeBase;
    private readonly IUnitOfWorkManage _unitOfWorkManage;
    private static readonly ConcurrentDictionary<Type, List<PropertyInfo>> DateTimePropertyCache = new();

    /// <summary>供 BaseRepository 及子类使用的 ISqlSugarClient 数据库实例</summary>
    /// <remarks>支持多租户切换数据库</remarks>
    protected ISqlSugarClient DbProtectedClient => DbClientBase;

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
        NormalizeEntityDateTimeToUtc(entity);

        // 确保软删除字段正确初始化
        if (entity is IDeleteFilter softDeleteEntity)
        {
            softDeleteEntity.IsDeleted = false;
            softDeleteEntity.DeletedAt = null;
            softDeleteEntity.DeletedBy = null;
        }

        // 自动检测实体是否配置了分表，如果是则自动调用 .SplitTable()
        var splitTableAttr = typeof(TEntity).GetCustomAttribute<SplitTableAttribute>();

        if (splitTableAttr != null)
        {
            var splitInsert = DbClientBase.Insertable(entity).SplitTable();
            return await splitInsert.ExecuteReturnSnowflakeIdAsync();
        }
        else
        {
            var insert = DbClientBase.Insertable(entity);
            return await insert.ExecuteReturnSnowflakeIdAsync();
        }
    }

    /// <summary>批量写入实体数据</summary>
    /// <param name="entities">实体列表</param>
    /// <returns>受影响的行数</returns>
    public async Task<int> AddRangeAsync(List<TEntity> entities)
    {
        // 确保所有实体的软删除字段正确初始化
        foreach (var entity in entities)
        {
            NormalizeEntityDateTimeToUtc(entity);

            if (entity is IDeleteFilter softDeleteEntity)
            {
                softDeleteEntity.IsDeleted = false;
                softDeleteEntity.DeletedAt = null;
                softDeleteEntity.DeletedBy = null;
            }
        }

        // 自动检测实体是否配置了分表
        var splitTableAttr = typeof(TEntity).GetCustomAttribute<SplitTableAttribute>();

        // 🚀 使用 ExecuteReturnSnowflakeIdListAsync 为每条记录生成唯一的 Snowflake ID
        // 避免批量插入时产生重复 ID 导致 UNIQUE constraint 错误
        if (splitTableAttr != null)
        {
            var splitInsertable = DbClientBase.Insertable(entities).SplitTable();
            var ids = await splitInsertable.ExecuteReturnSnowflakeIdListAsync();
            return ids.Count;
        }
        else
        {
            var insertable = DbClientBase.Insertable(entities);
            var ids = await insertable.ExecuteReturnSnowflakeIdListAsync();
            return ids.Count;
        }
    }

    /// <summary>分表-写入实体数据</summary>
    /// <param name="entity">泛型实体类</param>
    /// <returns>插入数据的 SnowflakeId, 类型为 long</returns>
    public async Task<List<long>> AddSplitAsync(TEntity entity)
    {
        NormalizeEntityDateTimeToUtc(entity);

        // 确保软删除字段正确初始化
        if (entity is IDeleteFilter softDeleteEntity)
        {
            softDeleteEntity.IsDeleted = false;
            softDeleteEntity.DeletedAt = null;
            softDeleteEntity.DeletedBy = null;
        }

        var insert = DbClientBase.Insertable(entity).SplitTable();
        // 插入并返回雪花ID并且自动赋值 Id
        return await insert.ExecuteReturnSnowflakeIdListAsync();
    }

    #endregion

    #region 删

    /// <summary>软删除：根据 ID 删除实体</summary>
    /// <param name="id">实体 ID</param>
    /// <param name="deletedBy">删除操作者，可空</param>
    /// <returns>是否成功</returns>
    public async Task<bool> SoftDeleteByIdAsync(long id, string? deletedBy = null)
    {
        // 检查实体是否支持软删除
        if (!typeof(IDeleteFilter).IsAssignableFrom(typeof(TEntity)))
        {
            throw new NotSupportedException($"实体 {typeof(TEntity).Name} 不支持软删除功能，请实现 IDeleteFilter 接口");
        }

        // 先查询实体是否存在
        var entity = await QueryByIdAsync(id);
        if (entity == null)
        {
            return false;
        }

        // 设置软删除字段
        if (entity is IDeleteFilter softDeleteEntity)
        {
            softDeleteEntity.IsDeleted = true;
            softDeleteEntity.DeletedAt = DateTime.UtcNow;
            softDeleteEntity.DeletedBy = deletedBy;
        }

        // 更新实体
        return await UpdateAsync(entity);
    }

    /// <summary>软删除：根据条件删除实体</summary>
    /// <param name="whereExpression">Where 表达式</param>
    /// <param name="deletedBy">删除操作者，可空</param>
    /// <returns>受影响的行数</returns>
    public async Task<int> SoftDeleteAsync(Expression<Func<TEntity, bool>> whereExpression, string? deletedBy = null)
    {
        // 检查实体是否支持软删除
        if (!typeof(IDeleteFilter).IsAssignableFrom(typeof(TEntity)))
        {
            throw new NotSupportedException($"实体 {typeof(TEntity).Name} 不支持软删除功能，请实现 IDeleteFilter 接口");
        }

        // 查询要删除的实体
        var entities = await QueryAsync(whereExpression);
        if (!entities.Any())
        {
            return 0;
        }

        // 设置软删除字段
        foreach (var entity in entities)
        {
            if (entity is IDeleteFilter softDeleteEntity)
            {
                softDeleteEntity.IsDeleted = true;
                softDeleteEntity.DeletedAt = DateTime.UtcNow;
                softDeleteEntity.DeletedBy = deletedBy;
            }
        }

        // 批量更新
        return await UpdateRangeAsync(entities);
    }

    /// <summary>恢复软删除：根据 ID 恢复实体</summary>
    /// <param name="id">实体 ID</param>
    /// <returns>是否成功</returns>
    public async Task<bool> RestoreByIdAsync(long id)
    {
        // 检查实体是否支持软删除
        if (!typeof(IDeleteFilter).IsAssignableFrom(typeof(TEntity)))
        {
            throw new NotSupportedException($"实体 {typeof(TEntity).Name} 不支持软删除功能，请实现 IDeleteFilter 接口");
        }

        // 先查询实体是否存在（包括已删除的）
        var entity = await DbClientBase.Queryable<TEntity>().InSingleAsync(id);
        if (entity == null)
        {
            return false;
        }

        // 设置恢复字段
        if (entity is IDeleteFilter softDeleteEntity)
        {
            softDeleteEntity.IsDeleted = false;
            softDeleteEntity.DeletedAt = null;
            softDeleteEntity.DeletedBy = null;
        }

        // 更新实体
        return await UpdateAsync(entity);
    }

    /// <summary>恢复软删除：根据条件恢复实体</summary>
    /// <param name="whereExpression">Where 表达式</param>
    /// <returns>受影响的行数</returns>
    public async Task<int> RestoreAsync(Expression<Func<TEntity, bool>> whereExpression)
    {
        // 检查实体是否支持软删除
        if (!typeof(IDeleteFilter).IsAssignableFrom(typeof(TEntity)))
        {
            throw new NotSupportedException($"实体 {typeof(TEntity).Name} 不支持软删除功能，请实现 IDeleteFilter 接口");
        }

        // 查询要恢复的实体（包括已删除的）
        var entities = await DbClientBase.Queryable<TEntity>().Where(whereExpression).ToListAsync();
        if (!entities.Any())
        {
            return 0;
        }

        // 设置恢复字段
        foreach (var entity in entities)
        {
            if (entity is IDeleteFilter softDeleteEntity)
            {
                softDeleteEntity.IsDeleted = false;
                softDeleteEntity.DeletedAt = null;
                softDeleteEntity.DeletedBy = null;
            }
        }

        // 批量更新
        return await UpdateRangeAsync(entities);
    }

    /// <summary>根据 ID 删除实体（物理删除）</summary>
    /// <param name="id">实体 ID</param>
    /// <returns>是否成功</returns>
    [Obsolete("请使用 SoftDeleteByIdAsync 进行软删除，避免物理删除业务数据")]
    public async Task<bool> DeleteByIdAsync(long id)
    {
        return await DbClientBase.Deleteable<TEntity>().In(id).ExecuteCommandHasChangeAsync();
    }

    /// <summary>根据实体删除（物理删除）</summary>
    /// <param name="entity">实体对象</param>
    /// <returns>是否成功</returns>
    [Obsolete("请使用 SoftDeleteAsync 进行软删除，避免物理删除业务数据")]
    public async Task<bool> DeleteAsync(TEntity entity)
    {
        return await DbClientBase.Deleteable(entity).ExecuteCommandHasChangeAsync();
    }

    /// <summary>根据条件删除（物理删除）</summary>
    /// <param name="whereExpression">Where 表达式</param>
    /// <returns>受影响的行数</returns>
    [Obsolete("请使用 SoftDeleteAsync 进行软删除，避免物理删除业务数据")]
    public async Task<int> DeleteAsync(Expression<Func<TEntity, bool>> whereExpression)
    {
        return await DbClientBase.Deleteable<TEntity>().Where(whereExpression).ExecuteCommandAsync();
    }

    /// <summary>批量删除（物理删除）</summary>
    /// <param name="ids">ID 列表</param>
    /// <returns>受影响的行数</returns>
    [Obsolete("请使用 SoftDeleteAsync 进行软删除，避免物理删除业务数据")]
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
        NormalizeEntityDateTimeToUtc(entity);
        return await DbClientBase.Updateable(entity).ExecuteCommandHasChangeAsync();
    }

    /// <summary>批量更新实体数据</summary>
    /// <param name="entities">实体列表</param>
    /// <returns>受影响的行数</returns>
    public async Task<int> UpdateRangeAsync(List<TEntity> entities)
    {
        foreach (var entity in entities)
        {
            NormalizeEntityDateTimeToUtc(entity);
        }

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
        NormalizeEntityDateTimeToUtc(entity);
        return await DbClientBase.Updateable(entity).UpdateColumns(updateColumns).ExecuteCommandHasChangeAsync();
    }

    /// <summary>统一将实体中的 DateTime/DateTime? 字段规范为 UTC，保证落库一致性</summary>
    private static void NormalizeEntityDateTimeToUtc(TEntity entity)
    {
        if (entity == null)
        {
            return;
        }

        var entityType = typeof(TEntity);
        var dateTimeProps = DateTimePropertyCache.GetOrAdd(entityType, type =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(prop =>
                    prop.CanRead &&
                    prop.CanWrite &&
                    (prop.PropertyType == typeof(DateTime) || prop.PropertyType == typeof(DateTime?)))
                .ToList());

        foreach (var prop in dateTimeProps)
        {
            if (prop.PropertyType == typeof(DateTime))
            {
                var current = (DateTime)prop.GetValue(entity)!;
                prop.SetValue(entity, NormalizeDateTimeToUtc(current));
                continue;
            }

            var nullable = (DateTime?)prop.GetValue(entity);
            if (nullable.HasValue)
            {
                prop.SetValue(entity, NormalizeDateTimeToUtc(nullable.Value));
            }
        }
    }

    private static DateTime NormalizeDateTimeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), ResolveDefaultTimeZone())
        };
    }

    private static TimeZoneInfo ResolveDefaultTimeZone()
    {
        var configuredTimeZoneId = App.GetOptions<TimeOptions>()?.DefaultTimeZoneId;
        return TimeZoneResolver.ResolveOrUtc(configuredTimeZoneId);
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

    /// <summary>分页查询（支持二级排序）</summary>
    public async Task<(List<TEntity> data, int totalCount)> QueryPageAsync(
        Expression<Func<TEntity, bool>>? whereExpression,
        int pageIndex,
        int pageSize,
        Expression<Func<TEntity, object>>? orderByExpression,
        OrderByType orderByType,
        Expression<Func<TEntity, object>>? thenByExpression,
        OrderByType thenByType)
    {
        RefAsync<int> totalCount = 0;
        var query = DbClientBase.Queryable<TEntity>()
            .WhereIF(whereExpression != null, whereExpression);

        // 主排序
        if (orderByExpression != null)
        {
            query = orderByType == OrderByType.Asc
                ? query.OrderBy(orderByExpression)
                : query.OrderByDescending(orderByExpression);
        }

        // 次级排序
        if (thenByExpression != null)
        {
            query = query.OrderBy(thenByExpression, thenByType);
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

    /// <summary>查询不同的字段值列表（去重）</summary>
    /// <typeparam name="TResult">返回字段类型</typeparam>
    /// <param name="selectExpression">选择字段表达式（例如：c => c.PostId）</param>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <returns>去重后的字段值列表</returns>
    public async Task<List<TResult>> QueryDistinctAsync<TResult>(
        Expression<Func<TEntity, TResult>> selectExpression,
        Expression<Func<TEntity, bool>>? whereExpression = null)
    {
        return await DbClientBase.Queryable<TEntity>()
            .WhereIF(whereExpression != null, whereExpression)
            .Select(selectExpression)
            .Distinct()
            .ToListAsync();
    }

    /// <summary>查询字段求和（聚合）</summary>
    /// <typeparam name="TResult">返回类型（通常为 int, long, decimal）</typeparam>
    /// <param name="selectExpression">选择要求和的字段（例如：t => t.Amount）</param>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <returns>求和结果</returns>
    public async Task<TResult> QuerySumAsync<TResult>(
        Expression<Func<TEntity, TResult>> selectExpression,
        Expression<Func<TEntity, bool>>? whereExpression = null)
    {
        return await DbClientBase.Queryable<TEntity>()
            .WhereIF(whereExpression != null, whereExpression)
            .SumAsync(selectExpression);
    }

    /// <summary>根据多个 ID 批量查询实体</summary>
    /// <param name="ids">ID 列表</param>
    /// <returns>实体列表</returns>
    public async Task<List<TEntity>> QueryByIdsAsync(List<long> ids)
    {
        if (ids == null || !ids.Any())
        {
            return new List<TEntity>();
        }

        return await DbClientBase.Queryable<TEntity>().In(ids).ToListAsync();
    }

    /// <summary>查询字段最大值（聚合）</summary>
    /// <typeparam name="TResult">返回类型</typeparam>
    /// <param name="selectExpression">选择要查询最大值的字段</param>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <returns>最大值</returns>
    public async Task<TResult> QueryMaxAsync<TResult>(
        Expression<Func<TEntity, TResult>> selectExpression,
        Expression<Func<TEntity, bool>>? whereExpression = null)
    {
        return await DbClientBase.Queryable<TEntity>()
            .WhereIF(whereExpression != null, whereExpression)
            .MaxAsync(selectExpression);
    }

    /// <summary>查询字段最小值（聚合）</summary>
    /// <typeparam name="TResult">返回类型</typeparam>
    /// <param name="selectExpression">选择要查询最小值的字段</param>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <returns>最小值</returns>
    public async Task<TResult> QueryMinAsync<TResult>(
        Expression<Func<TEntity, TResult>> selectExpression,
        Expression<Func<TEntity, bool>>? whereExpression = null)
    {
        return await DbClientBase.Queryable<TEntity>()
            .WhereIF(whereExpression != null, whereExpression)
            .MinAsync(selectExpression);
    }

    /// <summary>查询字段平均值（聚合）</summary>
    /// <param name="selectExpression">选择要查询平均值的字段</param>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <returns>平均值</returns>
    public async Task<decimal> QueryAverageAsync(
        Expression<Func<TEntity, decimal>> selectExpression,
        Expression<Func<TEntity, bool>>? whereExpression = null)
    {
        return await DbClientBase.Queryable<TEntity>()
            .WhereIF(whereExpression != null, whereExpression)
            .AvgAsync(selectExpression);
    }

    /// <summary>带排序的列表查询</summary>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <param name="orderByExpression">排序表达式</param>
    /// <param name="orderByType">排序类型</param>
    /// <param name="take">获取数量，0 表示不限制</param>
    /// <returns>实体列表</returns>
    public async Task<List<TEntity>> QueryWithOrderAsync(
        Expression<Func<TEntity, bool>>? whereExpression,
        Expression<Func<TEntity, object>> orderByExpression,
        OrderByType orderByType = OrderByType.Asc,
        int take = 0)
    {
        var query = DbClientBase.Queryable<TEntity>()
            .WhereIF(whereExpression != null, whereExpression);

        query = orderByType == OrderByType.Asc
            ? query.OrderBy(orderByExpression)
            : query.OrderByDescending(orderByExpression);

        if (take > 0)
        {
            query = query.Take(take);
        }

        return await query.ToListAsync();
    }

    #endregion
}
