using Radish.IRepository;
using Radish.IService;

namespace Radish.Service;

public class BaseServices<TEntity> : IBaseServices<TEntity> where TEntity : class, new()
{
    private readonly IBaseRepository<TEntity> _repository;

    /// <summary>构造函数依赖注入</summary>
    /// <param name="repository">IBaseRepository</param>
    public BaseServices(IBaseRepository<TEntity> repository)
    {
        _repository = repository;
    }

    /// <summary>测试泛型查询方法示例</summary>
    /// <returns></returns>
    public Task<List<TEntity>> QueryAsync()
    {
        return _repository.QueryAsync();
    }
}