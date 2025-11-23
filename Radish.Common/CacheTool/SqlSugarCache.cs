using Radish.Common.CoreTool;
using SqlSugar;

namespace Radish.Common.CacheTool;

/// <summary>
/// 实现 SqlSugar 的 ICacheService 接口
/// </summary>
/// <remarks>
/// <para>建议另行实现业务缓存，注入 ICaching 直接用即可</para>
/// <para>不建议使用 SqlSugar 缓存，性能有很大问题，会导致 Redis 堆积</para>
/// <para>核心问题在于 SqlSugar，每次 Query（注：不管有没有启用，所有表的查询）都会查缓存, insert\update\delete，又会频繁 GetAllKey，导致性能特别差</para>
/// </remarks>
public class SqlSugarCache : ICacheService
{
    private readonly Lazy<ICaching> _caching = new(() => App.GetService<ICaching>(false));
    private ICaching Caching => _caching.Value;

    public void Add<V>(string key, V value)
    {
        Caching.Set(key, value);
    }

    public void Add<V>(string key, V value, int cacheDurationInSeconds)
    {
        Caching.Set(key, value, TimeSpan.FromSeconds(cacheDurationInSeconds));
    }

    public bool ContainsKey<V>(string key)
    {
        return Caching.Exists(key);
    }

    public V Get<V>(string key)
    {
        return Caching.Get<V>(key);
    }

    public IEnumerable<string> GetAllKey<V>()
    {
        return Caching.GetAllCacheKeys();
    }

    public V GetOrCreate<V>(string cacheKey, Func<V> create, int cacheDurationInSeconds = int.MaxValue)
    {
        if (!ContainsKey<V>(cacheKey))
        {
            var value = create();
            Caching.Set(cacheKey, value, TimeSpan.FromSeconds(cacheDurationInSeconds));
            return value;
        }

        return Caching.Get<V>(cacheKey);
    }

    public void Remove<V>(string key)
    {
        Caching.Remove(key);
    }

    public bool RemoveAll()
    {
        Caching.RemoveAll();
        return true;
    }
}