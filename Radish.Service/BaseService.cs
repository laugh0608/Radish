using System.Linq.Expressions;
using AutoMapper;
using Radish.IRepository;
using Radish.IService;
using SqlSugar;

namespace Radish.Service;

public class BaseService<TEntity, TVo> : IBaseService<TEntity, TVo> where TEntity : class, new()
{
    private readonly IBaseRepository<TEntity> _baseRepository;
    private readonly IMapper _mapper;

    protected IMapper Mapper => _mapper;

    /// <summary>构造函数依赖注入</summary>
    /// <param name="baseRepository">IBaseRepository</param>
    /// <param name="mapper">IMapper</param>
    public BaseService(IMapper mapper, IBaseRepository<TEntity> baseRepository)
    {
        _baseRepository = baseRepository;
        _mapper = mapper;
    }

    #region 增

    /// <summary>写入一条实体类数据</summary>
    /// <param name="entity">泛型实体类</param>
    /// <returns>插入数据的 SnowflakeId, 类型为 long</returns>
    public async Task<long> AddAsync(TEntity entity)
    {
        return await _baseRepository.AddAsync(entity);
    }

    /// <summary>批量写入实体数据</summary>
    /// <param name="entities">实体列表</param>
    /// <returns>受影响的行数</returns>
    public async Task<int> AddRangeAsync(List<TEntity> entities)
    {
        return await _baseRepository.AddRangeAsync(entities);
    }

    /// <summary>分表-写入实体数据</summary>
    /// <param name="entity">泛型实体类</param>
    /// <returns>插入数据的 SnowflakeId, 类型为 long</returns>
    public async Task<List<long>> AddSplitAsync(TEntity entity)
    {
        return await _baseRepository.AddSplitAsync(entity);
    }

    #endregion

    #region 删

    /// <summary>软删除：根据 ID 删除实体</summary>
    /// <param name="id">实体 ID</param>
    /// <param name="deletedBy">删除操作者，可空</param>
    /// <returns>是否成功</returns>
    public async Task<bool> SoftDeleteByIdAsync(long id, string? deletedBy = null)
    {
        return await _baseRepository.SoftDeleteByIdAsync(id, deletedBy);
    }

    /// <summary>软删除：根据条件删除实体</summary>
    /// <param name="whereExpression">Where 表达式</param>
    /// <param name="deletedBy">删除操作者，可空</param>
    /// <returns>受影响的行数</returns>
    public async Task<int> SoftDeleteAsync(Expression<Func<TEntity, bool>> whereExpression, string? deletedBy = null)
    {
        return await _baseRepository.SoftDeleteAsync(whereExpression, deletedBy);
    }

    /// <summary>恢复软删除：根据 ID 恢复实体</summary>
    /// <param name="id">实体 ID</param>
    /// <returns>是否成功</returns>
    public async Task<bool> RestoreByIdAsync(long id)
    {
        return await _baseRepository.RestoreByIdAsync(id);
    }

    /// <summary>恢复软删除：根据条件恢复实体</summary>
    /// <param name="whereExpression">Where 表达式</param>
    /// <returns>受影响的行数</returns>
    public async Task<int> RestoreAsync(Expression<Func<TEntity, bool>> whereExpression)
    {
        return await _baseRepository.RestoreAsync(whereExpression);
    }

    /// <summary>根据 ID 删除实体（物理删除）</summary>
    /// <param name="id">实体 ID</param>
    /// <returns>是否成功</returns>
    [Obsolete("请使用 SoftDeleteByIdAsync 进行软删除，避免物理删除业务数据")]
    public async Task<bool> DeleteByIdAsync(long id)
    {
        return await _baseRepository.DeleteByIdAsync(id);
    }

    /// <summary>根据实体删除（物理删除）</summary>
    /// <param name="entity">实体对象</param>
    /// <returns>是否成功</returns>
    [Obsolete("请使用 SoftDeleteAsync 进行软删除，避免物理删除业务数据")]
    public async Task<bool> DeleteAsync(TEntity entity)
    {
        return await _baseRepository.DeleteAsync(entity);
    }

    /// <summary>根据条件删除（物理删除）</summary>
    /// <param name="whereExpression">Where 表达式</param>
    /// <returns>受影响的行数</returns>
    [Obsolete("请使用 SoftDeleteAsync 进行软删除，避免物理删除业务数据")]
    public async Task<int> DeleteAsync(Expression<Func<TEntity, bool>> whereExpression)
    {
        return await _baseRepository.DeleteAsync(whereExpression);
    }

    /// <summary>批量删除（物理删除）</summary>
    /// <param name="ids">ID 列表</param>
    /// <returns>受影响的行数</returns>
    [Obsolete("请使用 SoftDeleteAsync 进行软删除，避免物理删除业务数据")]
    public async Task<int> DeleteByIdsAsync(List<long> ids)
    {
        return await _baseRepository.DeleteByIdsAsync(ids);
    }

    #endregion

    #region 改

    /// <summary>更新实体数据</summary>
    /// <param name="entity">泛型实体类</param>
    /// <returns>是否成功</returns>
    public async Task<bool> UpdateAsync(TEntity entity)
    {
        return await _baseRepository.UpdateAsync(entity);
    }

    /// <summary>批量更新实体数据</summary>
    /// <param name="entities">实体列表</param>
    /// <returns>受影响的行数</returns>
    public async Task<int> UpdateRangeAsync(List<TEntity> entities)
    {
        return await _baseRepository.UpdateRangeAsync(entities);
    }

    /// <summary>更新指定列</summary>
    /// <param name="entity">实体对象</param>
    /// <param name="updateColumns">要更新的列表达式</param>
    /// <returns>是否成功</returns>
    public async Task<bool> UpdateColumnsAsync(TEntity entity, Expression<Func<TEntity, object>> updateColumns)
    {
        return await _baseRepository.UpdateColumnsAsync(entity, updateColumns);
    }

    /// <summary>根据条件更新指定列</summary>
    /// <param name="updateColumns">更新列表达式</param>
    /// <param name="whereExpression">Where 条件</param>
    /// <returns>受影响的行数</returns>
    public async Task<int> UpdateColumnsAsync(Expression<Func<TEntity, TEntity>> updateColumns, Expression<Func<TEntity, bool>> whereExpression)
    {
        return await _baseRepository.UpdateColumnsAsync(updateColumns, whereExpression);
    }

    #endregion

    #region 查

    /// <summary>根据 ID 查询单个实体（返回 ViewModel）</summary>
    /// <param name="id">实体 ID</param>
    /// <returns>ViewModel 对象，如果不存在则返回 null</returns>
    public async Task<TVo?> QueryByIdAsync(long id)
    {
        var entity = await _baseRepository.QueryByIdAsync(id);
        return entity == null ? default : _mapper.Map<TVo>(entity);
    }

    /// <summary>查询第一条数据（返回 ViewModel）</summary>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <returns>ViewModel 对象，如果不存在则返回 null</returns>
    public async Task<TVo?> QueryFirstAsync(Expression<Func<TEntity, bool>>? whereExpression = null)
    {
        var entity = await _baseRepository.QueryFirstAsync(whereExpression);
        return entity == null ? default : _mapper.Map<TVo>(entity);
    }

    /// <summary>查询单条数据（多条会抛异常，返回 ViewModel）</summary>
    /// <param name="whereExpression">Where 表达式</param>
    /// <returns>ViewModel 对象，如果不存在则返回 null</returns>
    public async Task<TVo?> QuerySingleAsync(Expression<Func<TEntity, bool>> whereExpression)
    {
        var entity = await _baseRepository.QuerySingleAsync(whereExpression);
        return entity == null ? default : _mapper.Map<TVo>(entity);
    }

    /// <summary>按照 Where 表达式查询（返回 ViewModel 列表）</summary>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <returns>List TVo</returns>
    public async Task<List<TVo>> QueryAsync(Expression<Func<TEntity, bool>>? whereExpression = null)
    {
        // Repository 不是单例，所以多次查询的 HASH 是不一样的，对应的是 Repository 层的 DbBase 是单例
        // await Console.Out.WriteLineAsync($"Repository HashCode: {_baseRepository.GetHashCode().ToString()}");
        return _mapper.Map<List<TVo>>(await _baseRepository.QueryAsync(whereExpression));
    }

    /// <summary>按照 Where 表达式查询（使用缓存，返回 ViewModel 列表）</summary>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <param name="cacheTime">使用缓存查询的时间，默认 10 s</param>
    /// <returns>List TVo</returns>
    public async Task<List<TVo>> QueryWithCacheAsync(Expression<Func<TEntity, bool>>? whereExpression = null, int cacheTime = 10)
    {
        return _mapper.Map<List<TVo>>(await _baseRepository.QueryWithCacheAsync(whereExpression, cacheTime));
    }

    /// <summary>分页查询（返回 ViewModel 列表）</summary>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="orderByExpression">排序表达式，可空</param>
    /// <param name="orderByType">排序类型（Asc/Desc），默认 Asc</param>
    /// <returns>分页数据和总数</returns>
    public async Task<(List<TVo> data, int totalCount)> QueryPageAsync(
        Expression<Func<TEntity, bool>>? whereExpression = null,
        int pageIndex = 1,
        int pageSize = 20,
        Expression<Func<TEntity, object>>? orderByExpression = null,
        OrderByType orderByType = OrderByType.Asc)
    {
        var (data, totalCount) = await _baseRepository.QueryPageAsync(whereExpression, pageIndex, pageSize, orderByExpression, orderByType);
        return (_mapper.Map<List<TVo>>(data), totalCount);
    }

    /// <summary>查询数量</summary>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <returns>记录数</returns>
    public async Task<int> QueryCountAsync(Expression<Func<TEntity, bool>>? whereExpression = null)
    {
        return await _baseRepository.QueryCountAsync(whereExpression);
    }

    /// <summary>查询是否存在</summary>
    /// <param name="whereExpression">Where 表达式</param>
    /// <returns>是否存在</returns>
    public async Task<bool> QueryExistsAsync(Expression<Func<TEntity, bool>> whereExpression)
    {
        return await _baseRepository.QueryExistsAsync(whereExpression);
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
        Expression<Func<T, T2, T3, object[]>> joinExpression, Expression<Func<T, T2, T3, TResult>> selectExpression,
        Expression<Func<T, T2, T3, bool>>? whereLambda = null) where T : class, new()
    {
        return await _baseRepository.QueryMuchAsync(joinExpression, selectExpression, whereLambda);
    }

    /// <summary>分表-按照 Where 表达式查询</summary>
    /// <param name="whereExpression">Where 表达式，可空</param>
    /// <param name="orderByFields">排序字段，默认为 Id，其他如 Name, Age</param>
    /// <returns>List TEntity</returns>
    public async Task<List<TEntity>> QuerySplitAsync(Expression<Func<TEntity, bool>>? whereExpression,
        string orderByFields = "Id")
    {
        return await _baseRepository.QuerySplitAsync(whereExpression, orderByFields);
    }

    #endregion
}
