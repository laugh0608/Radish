using Newtonsoft.Json;
using Radish.IRepository;
using SqlSugar;

namespace Radish.Repository;

/// <summary>泛型基类仓储</summary>
// 这里的 where TEntity : class, new() 的意思是对泛型进行约束，首先必须是类 class，其次必须可以被实例化 new()
public class BaseRepository<TEntity> : IBaseRepository<TEntity> where TEntity : class, new()
{
    /// <summary>只读，仓储内部使用的 ISqlSugarClient 数据库实例</summary>
    private readonly ISqlSugarClient _dbBase;

    /// <summary>供外部使用的公开 ISqlSugarClient 数据库实例</summary>
    public ISqlSugarClient DbBase => _dbBase;

    /// <summary>构造函数，注入依赖</summary>
    /// <param name="dbBase"></param>
    public BaseRepository(ISqlSugarClient dbBase)
    {
        _dbBase = dbBase;
    }

    /// <summary>按照泛型实体类查询表中所有数据</summary>
    /// <returns>List TEntity</returns>
    public async Task<List<TEntity>> QueryAsync()
    {
        // DbBase 是 ISqlSugarClient 单例注入的，所以多次查询的 HASH 是一样的，对应的是 Service 层的 Repository 不是单例
        await Console.Out.WriteLineAsync($"DbBase HashCode: {DbBase.GetHashCode().ToString()}");
        return await _dbBase.Queryable<TEntity>().ToListAsync();
    }

    /// <summary>写入一条实体类数据</summary>
    /// <param name="entity">泛型实体类</param>
    /// <returns>插入数据的 SnowflakeId, 类型为 long</returns>
    public async Task<long> AddAsync(TEntity entity)
    {
        var insert = _dbBase.Insertable(entity);
        return await insert.ExecuteReturnSnowflakeIdAsync();
    }
}