namespace Radish.IService;

public interface IBaseService<TEntity, TVo> where TEntity : class
{
    /// <summary>测试泛型查询方法示例</summary>
    /// <remarks>已加入实体类和视图模型的泛型对象关系映射</remarks>
    /// <returns></returns>
    Task<List<TVo>> QueryAsync();
}