namespace Radish.IService;

/// <summary>
/// 系统设置统一读取入口
/// </summary>
public interface ISystemSettingProvider
{
    Task<string> GetEffectiveValueAsync(string key);

    Task<int> GetInt32Async(string key);
}
