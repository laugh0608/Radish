using Newtonsoft.Json;
using Radish.IRepository;

namespace Radish.Repository;

/// <summary>泛型基类仓储</summary>
// 这里的 where TEntity : class, new() 的意思是对泛型进行约束，首先必须是类 class，其次必须可以被实例化 new()
public class BaseRepository<TEntity> : IBaseRepository<TEntity> where TEntity : class, new()
{
    /// <summary>测试泛型查询方法示例</summary>
    /// <returns></returns>
    public async Task<List<TEntity>> QueryAsync()
    {
        await Task.CompletedTask;
        const string data = "[{\"Id\": 1, \"Data\": \"test test test\"}]";
        return JsonConvert.DeserializeObject<List<TEntity>>(data) ?? [];
    }
}