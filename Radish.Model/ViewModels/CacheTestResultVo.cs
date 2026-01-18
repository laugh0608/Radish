namespace Radish.Model.ViewModels;

/// <summary>缓存测试结果 ViewModel</summary>
/// <remarks>用于替代 CacheTest() 方法的匿名对象</remarks>
public class CacheTestResultVo
{
    /// <summary>缓存键</summary>
    public string VoCacheKey { get; set; } = string.Empty;

    /// <summary>设置前的缓存键列表</summary>
    public List<string> VoCacheKeysBeforeSet { get; set; } = new();

    /// <summary>设置后的缓存键列表</summary>
    public List<string> VoCacheKeysAfterSet { get; set; } = new();

    /// <summary>缓存值</summary>
    public string? VoCacheValue { get; set; }

    /// <summary>删除后的缓存键列表</summary>
    public List<string> VoCacheKeysAfterRemove { get; set; } = new();
}