namespace Radish.Model.ViewModels;

/// <summary>缓存测试结果 ViewModel</summary>
/// <remarks>用于替代 CacheTest() 方法的匿名对象</remarks>
public class CacheTestResultVo
{
    /// <summary>缓存键</summary>
    public string CacheKey { get; set; } = string.Empty;

    /// <summary>设置前的缓存键列表</summary>
    public List<string> CacheKeysBeforeSet { get; set; } = new();

    /// <summary>设置后的缓存键列表</summary>
    public List<string> CacheKeysAfterSet { get; set; } = new();

    /// <summary>缓存值</summary>
    public string? CacheValue { get; set; }

    /// <summary>删除后的缓存键列表</summary>
    public List<string> CacheKeysAfterRemove { get; set; } = new();
}