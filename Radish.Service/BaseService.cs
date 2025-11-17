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

    /// <summary>按照泛型实体类查询表中所有数据</summary>
    /// <remarks>已加入实体类和视图模型的泛型对象关系映射</remarks>
    /// <returns>List TEntity</returns>
    public async Task<List<TVo>> QueryAsync()
    {
        // Repository 不是单例，所以多次查询的 HASH 是不一样的，对应的是 Repository 层的 DbBase 是单例
        await Console.Out.WriteLineAsync($"Repository HashCode: {_baseRepository.GetHashCode().ToString()}");
        return _mapper.Map<List<TVo>>(await _baseRepository.QueryAsync());
    }

    /// <summary>写入一条实体类数据</summary>
    /// <param name="entity">泛型实体类</param>
    /// <returns>插入数据的 SnowflakeId, 类型为 long</returns>
    public async Task<long> AddAsync(TEntity entity)
    {
        return await _baseRepository.AddAsync(entity);
    }
}