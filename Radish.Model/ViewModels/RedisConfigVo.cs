namespace Radish.Model.ViewModels;

/// <summary>
/// Redis 配置信息视图对象
/// </summary>
public class RedisConfigVo
{
    /// <summary>
    /// Redis 是否启用（通过 AppSettingsTool.RadishApp 获取）
    /// </summary>
    public string? VoEnableFromRadishApp { get; set; }

    /// <summary>
    /// Redis 连接字符串（通过 AppSettingsTool.GetValue 获取）
    /// </summary>
    public string? VoConnectionStringFromGetValue { get; set; }

    /// <summary>
    /// Redis 实例名称（通过 IOptions 注入获取）
    /// </summary>
    public string? VoInstanceNameFromOptions { get; set; }

    /// <summary>
    /// Redis 完整配置对象（通过 App.GetOptions 获取）
    /// </summary>
    public object? VoFullOptionsFromApp { get; set; }
}
