namespace Radish.IRepository;

/// <summary>泛型基类仓储接口</summary>
// 这里的 where TEntity : class 的意思是对泛型进行约束，必须是类 class
public interface IBaseRepository<TEntity> where TEntity : class
{
    /// <summary>测试泛型查询方法示例</summary>
    /// <returns></returns>
    Task<List<TEntity>> QueryAsync();
}