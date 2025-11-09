namespace Radish.IService;

public interface IBaseServices<TEntity> where TEntity : class
{
    Task<List<TEntity>> QueryAsync();
}