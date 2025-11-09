namespace Radish.IService;

public interface IBaseServices<TEntity> where TEntity : class
{
    public Task<List<TEntity>> QueryAsync();
}