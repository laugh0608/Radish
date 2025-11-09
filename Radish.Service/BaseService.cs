using Radish.IRepository;
using Radish.IService;

namespace Radish.Service;

public class BaseServices<TEntity> : IBaseServices<TEntity> where TEntity : class, new()
{
    private IBaseRepository<TEntity> _repository;
    
    public Task<List<TEntity>> QueryAsync()
    {
        return _repository.QueryAsync();
    }
}