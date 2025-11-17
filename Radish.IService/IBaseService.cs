namespace Radish.IService;

public interface IBaseService<TEntity, TVo> where TEntity : class
{
    /// <summary>写入一条实体类数据</summary>
    /// <param name="entity">泛型实体类</param>
    /// <returns>插入数据的 SnowflakeId, 类型为 long</returns>
    Task<long> AddAsync(TEntity entity);

    /// <summary>按照泛型实体类查询表中所有数据</summary>
    /// <remarks>已加入实体类和视图模型的泛型对象关系映射</remarks>
    /// <returns>List TEntity</returns>
    Task<List<TVo>> QueryAsync();
}