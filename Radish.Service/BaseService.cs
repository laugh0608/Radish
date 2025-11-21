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

    public ISqlSugarClient Db => _baseRepository.DbBase;

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

    /// <summary>分表-写入实体数据</summary>
    /// <param name="entity">泛型实体类</param>
    /// <returns>插入数据的 SnowflakeId, 类型为 long</returns>
    public async Task<List<long>> AddSplitAsync(TEntity entity)
    {
        return await _baseRepository.AddSplitAsync(entity);
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
    public async Task<List<TVo>> QueryAsync(Expression<Func<TEntity, bool>>? whereExpression = null)
    {
        // Repository 不是单例，所以多次查询的 HASH 是不一样的，对应的是 Repository 层的 DbBase 是单例
        // await Console.Out.WriteLineAsync($"Repository HashCode: {_baseRepository.GetHashCode().ToString()}");
        return _mapper.Map<List<TVo>>(await _baseRepository.QueryAsync(whereExpression));
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