namespace Radish.IService;

public interface IBaseServices<TEntity> where TEntity : class
{
    /// <summary>测试泛型查询方法示例</summary>
    /// <returns></returns>
    public Task<List<TEntity>> QueryAsync();
}