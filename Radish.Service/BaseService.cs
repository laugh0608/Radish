using AutoMapper;
using Radish.IRepository;
using Radish.IService;

namespace Radish.Service;

public class BaseServices<TEntity, TVo> : IBaseServices<TEntity, TVo> where TEntity : class, new()
{
    private readonly IBaseRepository<TEntity> _repository;
    private readonly IMapper _mapper;

    /// <summary>构造函数依赖注入</summary>
    /// <param name="repository">IBaseRepository</param>
    /// <param name="mapper">IMapper</param>
    public BaseServices(IBaseRepository<TEntity> repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    /// <summary>测试泛型查询方法示例</summary>
    /// <remarks>已加入实体类和视图模型的泛型对象关系映射</remarks>
    /// <returns></returns>
    public async Task<List<TVo>> QueryAsync()
    {
        return _mapper.Map<List<TVo>>(await _repository.QueryAsync());
    }
}