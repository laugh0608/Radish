using System.Linq.Expressions;
using SqlSugar;

namespace Radish.IRepository;

/// <summary>泛型基类仓储接口</summary>
// 这里的 where TEntity : class 的意思是对泛型进行约束，必须是类 class
public interface IBaseRepository<TEntity> where TEntity : class
{
    /// <summary>供外部使用的公开 ISqlSugarClient 数据库实例</summary>
    ISqlSugarClient DbBase { get; }

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
    /// <returns>List TEntity</returns>
    Task<List<TEntity>> QueryAsync();

    /// <summary>分表-按照泛型实体类查询表中所有数据</summary>
    /// <param name="whereExpression">条件表达式</param>
    /// <param name="orderByFields">排序字段，默认为 Id，其他如 Name, Age</param>
    /// <returns>List TEntity</returns>
    Task<List<TEntity>> QuerySplitAsync(Expression<Func<TEntity, bool>> whereExpression,
        string orderByFields = "Id");

    #endregion
}