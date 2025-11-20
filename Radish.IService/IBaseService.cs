using System.Linq.Expressions;

namespace Radish.IService;

public interface IBaseService<TEntity, TVo> where TEntity : class
{
    #region 增

    /// <summary>写入一条实体类数据</summary>
    /// <param name="entity">泛型实体类</param>
    /// <returns>插入数据的 SnowflakeId, 类型为 long</returns>
    Task<long> AddAsync(TEntity entity);


    /// <summary>分表-写入实体数据</summary>
    /// <param name="entity">泛型实体类</param>
    /// <returns>插入数据的 SnowflakeId, 类型为 long</returns>
    Task<List<long>> AddSplitAsync(TEntity entity);

    #endregion

    #region 删

    // 删

    #endregion

    #region 改

    // 改

    #endregion

    #region 查

    /// <summary>按照泛型实体类查询表中所有数据</summary>
    /// <remarks>已加入实体类和视图模型的泛型对象关系映射</remarks>
    /// <returns>List TEntity</returns>
    Task<List<TVo>> QueryAsync();

    /// <summary>分表-按照泛型实体类查询表中所有数据</summary>
    /// <param name="whereExpression">条件表达式</param>
    /// <param name="orderByFields">排序字段，默认为 Id，其他如 Name, Age</param>
    /// <returns>List TEntity</returns>
    Task<List<TEntity>> QuerySplitAsync(Expression<Func<TEntity, bool>> whereExpression,
        string orderByFields = "Id");

    #endregion
}