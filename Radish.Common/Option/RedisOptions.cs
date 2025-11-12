using Radish.Common.Option.Core;

namespace Radish.Common.Option;

/// <summary>Redis 缓存配置选项</summary>
public sealed class RedisOptions : IConfigurableOptions
{
    /// <summary>是否启用</summary>
    public bool Enable { get; set; }

    /// <summary>Redis 连接字符串</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>键值前缀</summary>
    public string InstanceName { get; set; } = string.Empty;
}