using AutoMapper;
using Radish.IRepository;
using Radish.IService;

namespace Radish.Service;

public class BaseService<TEntity, TVo> : IBaseService<TEntity, TVo> where TEntity : class, new()
{
    private readonly IBaseRepository<TEntity> _repository;
    private readonly IMapper _mapper;

    /// <summary>构造函数依赖注入</summary>
    /// <param name="repository">IBaseRepository</param>
    /// <param name="mapper">IMapper</param>
    public BaseService(IBaseRepository<TEntity> repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    /// <summary>测试泛型查询方法示例</summary>
    /// <remarks>已加入实体类和视图模型的泛型对象关系映射</remarks>
    /// <returns></returns>
    public async Task<List<TVo>> QueryAsync()
    {
        // Repository 不是单例，所以多次查询的 HASH 是不一样的，对应的是 Repository 层的 DbBase 是单例
        await Console.Out.WriteLineAsync($"Repository HashCode: {_repository.GetHashCode().ToString()}");
        return _mapper.Map<List<TVo>>(await _repository.QueryAsync());
    }
}