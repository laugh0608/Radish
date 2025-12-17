using System.Linq.Expressions;
using SqlSugar;

namespace Radish.IRepository;

/// <summary>泛型基类仓储接口</summary>
// 这里的 where TEntity : class 的意思是对泛型进行约束，必须是类 class
public interface IBaseRepository<TEntity> where TEntity : class
{
    /// <summary>供外部使用的公开 ISqlSugarClient 数据库实例</summary>
    ISqlSugarClient DbBase { get; }

    #region 增

    /// <summary>写入一条实体类数据</summary>
    /// <param name="entity">泛型实体类</param>
    /// <returns>插入数据的 SnowflakeId, 类型为 long</returns>
    Task<long> AddAsync(TEntity entity);

    /// <summary>批量写入实体数据</summary>
    /// <param name="entities">实体列表</param>
    /// <returns>受影响的行数</returns>
    Task<int> AddRangeAsync(List<TEntity> entities);

    /// <summary>分表-写入实体数据</summary>
    /// <param name="entity">泛型实体类</param>
    /// <returns>插入数据的 SnowflakeId, 类型为 long</returns>
    Task<List<long>> AddSplitAsync(TEntity entity);

    #endregion

    #region 删

    /// <summary>根据 ID 删除实体（物理删除）</summary>
    /// <param name="id">实体 ID</param>
    /// <returns>是否成功</returns>
    Task<bool> DeleteByIdAsync(long id);

    /// <summary>根据实体删除（物理删除）</summary>
    /// <param name="entity">实体对象</param>
    /// <returns>是否成功</returns>
    Task<bool> DeleteAsync(TEntity entity);

    /// <summary>根据条件删除（物理删除）</summary>
    /// <param name="whereExpression">Where 表达式</param>
    /// <returns>受影响的行数</returns>
    Task<int> DeleteAsync(Expression<Func<TEntity, bool>> whereExpression);

    /// <summary>批量删除（物理删除）</summary>
    /// <param name="ids">ID 列表</param>
    /// <returns>受影响的行数</returns>
    Task<int> DeleteByIdsAsync(List<long> ids);

    #endregion

    #region 改

    /// <summary>更新实体数据</summary>
    /// <param name="entity">泛型实体类</param>
    /// <returns>是否成功</returns>
    Task<bool> UpdateAsync(TEntity entity);

    /// <summary>批量更新实体数据</summary>
    /// <param name="entities">实体列表</param>
    /// <returns>受影响的行数</returns>
    Task<int> UpdateRangeAsync(List<TEntity> entities);

    /// <summary>更新指定列</summary>
    /// <param name="entity">实体对象</param>
    /// <param name="updateColumns">要更新的列表达式</param>
    /// <returns>是否成功</returns>
    /// <example>
    /// UpdateColumnsAsync(entity, it => new { it.Name, it.Age })
    /// </example>
    Task<bool> UpdateColumnsAsync(TEntity entity, Expression<Func<TEntity, object>> updateColumns);

    /// <summary>根据条件更新指定列</summary>
    /// <param name="updateColumns">更新列表达式</param>
    /// <param name="whereExpression">Where 条件</param>
    /// <returns>受影响的行数</returns>
    /// <example>
    /// UpdateColumnsAsync(it => new TEntity { Name = "newName" }, it => it.Id == 1)
    /// </example>
    Task<int> UpdateColumnsAsync(Expression<Func<TEntity, TEntity>> updateColumns, Expression<Func<TEntity, bool>> whereExpression);

    #endregion

    #region 查

    /// <summary>根据 ID 查询单个实体</summary>
    /// <param name="id">实体 ID</param>
    /// <returns>实体对象，如果不存在则返回 null</returns>
    Task<TEntity?> QueryByIdAsync(long id);

    /// <summary>查询第一条数据</summary>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <returns>实体对象，如果不存在则返回 null</returns>
    Task<TEntity?> QueryFirstAsync(Expression<Func<TEntity, bool>>? whereExpression = null);

    /// <summary>查询单条数据（多条会抛异常）</summary>
    /// <param name="whereExpression">Where 表达式</param>
    /// <returns>实体对象，如果不存在则返回 null</returns>
    Task<TEntity?> QuerySingleAsync(Expression<Func<TEntity, bool>> whereExpression);

    /// <summary>按照 Where 表达式查询</summary>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <returns>List TEntity</returns>
    Task<List<TEntity>> QueryAsync(Expression<Func<TEntity, bool>>? whereExpression = null);

    /// <summary>按照 Where 表达式查询（使用缓存）</summary>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <param name="cacheTime">使用缓存查询的时间，默认 10 s</param>
    /// <returns>List TEntity</returns>
    Task<List<TEntity>> QueryWithCacheAsync(Expression<Func<TEntity, bool>>? whereExpression = null,
        int cacheTime = 10);

    /// <summary>分页查询</summary>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="orderByExpression">排序表达式，可空</param>
    /// <param name="orderByType">排序类型（Asc/Desc），默认 Asc</param>
    /// <returns>分页数据和总数</returns>
    Task<(List<TEntity> data, int totalCount)> QueryPageAsync(
        Expression<Func<TEntity, bool>>? whereExpression = null,
        int pageIndex = 1,
        int pageSize = 20,
        Expression<Func<TEntity, object>>? orderByExpression = null,
        OrderByType orderByType = OrderByType.Asc);

    /// <summary>分页查询（支持二级排序）</summary>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="orderByExpression">主排序表达式，可空</param>
    /// <param name="orderByType">主排序类型（Asc/Desc），默认 Asc</param>
    /// <param name="thenByExpression">次级排序表达式，可空</param>
    /// <param name="thenByType">次级排序类型（Asc/Desc），默认 Asc</param>
    /// <returns>分页数据和总数</returns>
    Task<(List<TEntity> data, int totalCount)> QueryPageAsync(
        Expression<Func<TEntity, bool>>? whereExpression,
        int pageIndex,
        int pageSize,
        Expression<Func<TEntity, object>>? orderByExpression,
        OrderByType orderByType,
        Expression<Func<TEntity, object>>? thenByExpression,
        OrderByType thenByType);

    /// <summary>查询数量</summary>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <returns>记录数</returns>
    Task<int> QueryCountAsync(Expression<Func<TEntity, bool>>? whereExpression = null);

    /// <summary>查询是否存在</summary>
    /// <param name="whereExpression">Where 表达式</param>
    /// <returns>是否存在</returns>
    Task<bool> QueryExistsAsync(Expression<Func<TEntity, bool>> whereExpression);

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
    Task<List<TResult>> QueryMuchAsync<T, T2, T3, TResult>(
        Expression<Func<T, T2, T3, object[]>> joinExpression,
        Expression<Func<T, T2, T3, TResult>> selectExpression,
        Expression<Func<T, T2, T3, bool>>? whereLambda = null) where T : class, new();

    /// <summary>分表-按照 Where 表达式查询</summary>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <param name="orderByFields">排序字段，默认为 Id，其他如 Name, Age</param>
    /// <returns>List TEntity</returns>
    Task<List<TEntity>> QuerySplitAsync(Expression<Func<TEntity, bool>>? whereExpression,
        string orderByFields = "Id");

    #endregion
}