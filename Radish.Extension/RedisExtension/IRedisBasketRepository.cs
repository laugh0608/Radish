using System.ComponentModel;
using StackExchange.Redis;

namespace Radish.Extension.RedisExtension;

/// <summary>Redis 缓存接口</summary>
[Description("普通缓存考虑直接使用 ICaching, 如果要使用 Redis 队列还是使用此类")]
public interface IRedisBasketRepository
{
    /// <summary>
    /// 获取 Redis 缓存值
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    Task<string> GetValue(string key);

    /// <summary>
    /// 获取值，并序列化
    /// </summary>
    /// <param name="key"></param>
    /// <typeparam name="TEntity"></typeparam>
    /// <returns></returns>
    Task<TEntity> Get<TEntity>(string key);

    /// <summary>
    /// 保存
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="cacheTime"></param>
    /// <returns></returns>
    Task Set(string key, object value, TimeSpan cacheTime);

    /// <summary>
    /// 判断是否存在
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    Task<bool> Exist(string key);

    /// <summary>
    /// 移除某一个缓存值
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    Task Remove(string key);

    /// <summary>
    /// 全部清除
    /// </summary>
    /// <returns></returns>
    Task Clear();

    Task<RedisValue[]> ListRangeAsync(string redisKey);
    Task<long> ListLeftPushAsync(string redisKey, string redisValue, int db = -1);
    Task<long> ListRightPushAsync(string redisKey, string redisValue, int db = -1);
    Task<long> ListRightPushAsync(string redisKey, IEnumerable<string> redisValue, int db = -1);
    Task<T> ListLeftPopAsync<T>(string redisKey, int db = -1) where T : class;
    Task<T> ListRightPopAsync<T>(string redisKey, int db = -1) where T : class;
    Task<string> ListLeftPopAsync(string redisKey, int db = -1);
    Task<string> ListRightPopAsync(string redisKey, int db = -1);
    Task<long> ListLengthAsync(string redisKey, int db = -1);
    Task<IEnumerable<string>> ListRangeAsync(string redisKey, int db = -1);
    Task<IEnumerable<string>> ListRangeAsync(string redisKey, int start, int stop, int db = -1);
    Task<long> ListDelRangeAsync(string redisKey, string redisValue, long type = 0, int db = -1);
    Task ListClearAsync(string redisKey, int db = -1);
}