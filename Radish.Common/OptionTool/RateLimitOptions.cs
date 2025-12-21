using Radish.Common.OptionTool.Core;

namespace Radish.Common.OptionTool;

/// <summary>速率限制配置选项</summary>
public sealed class RateLimitOptions : IConfigurableOptions
{
    /// <summary>是否启用速率限制</summary>
    public bool Enable { get; set; } = true;

    /// <summary>是否启用限流日志</summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>全局限流配置</summary>
    public GlobalLimitConfig Global { get; set; } = new();

    /// <summary>登录限流配置</summary>
    public LoginLimitConfig Login { get; set; } = new();

    /// <summary>敏感操作限流配置</summary>
    public SensitiveLimitConfig Sensitive { get; set; } = new();

    /// <summary>并发限流配置</summary>
    public ConcurrencyLimitConfig Concurrency { get; set; } = new();

    /// <summary>IP 黑名单配置</summary>
    public BlacklistConfig Blacklist { get; set; } = new();

    /// <summary>IP 白名单配置</summary>
    public WhitelistConfig Whitelist { get; set; } = new();
}

/// <summary>全局限流配置</summary>
public sealed class GlobalLimitConfig
{
    /// <summary>是否启用</summary>
    public bool Enable { get; set; } = true;

    /// <summary>允许的请求数</summary>
    public int PermitLimit { get; set; } = 200;

    /// <summary>时间窗口（秒）</summary>
    public int WindowSeconds { get; set; } = 60;
}

/// <summary>登录限流配置</summary>
public sealed class LoginLimitConfig
{
    /// <summary>是否启用</summary>
    public bool Enable { get; set; } = true;

    /// <summary>允许的请求数</summary>
    public int PermitLimit { get; set; } = 10;

    /// <summary>时间窗口（秒）</summary>
    public int WindowSeconds { get; set; } = 900; // 15分钟

    /// <summary>滑动窗口分段数</summary>
    public int SegmentsPerWindow { get; set; } = 8;
}

/// <summary>敏感操作限流配置</summary>
public sealed class SensitiveLimitConfig
{
    /// <summary>是否启用</summary>
    public bool Enable { get; set; } = true;

    /// <summary>令牌桶容量</summary>
    public int TokenLimit { get; set; } = 20;

    /// <summary>每个周期补充的令牌数</summary>
    public int TokensPerPeriod { get; set; } = 20;

    /// <summary>补充周期（秒）</summary>
    public int ReplenishmentPeriodSeconds { get; set; } = 60;

    /// <summary>队列限制（0 表示不排队）</summary>
    public int QueueLimit { get; set; } = 0;
}

/// <summary>并发限流配置</summary>
public sealed class ConcurrencyLimitConfig
{
    /// <summary>是否启用</summary>
    public bool Enable { get; set; } = true;

    /// <summary>允许的并发请求数</summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>队列限制</summary>
    public int QueueLimit { get; set; } = 50;
}

/// <summary>IP 黑名单配置</summary>
public sealed class BlacklistConfig
{
    /// <summary>是否启用黑名单</summary>
    public bool Enable { get; set; } = false;

    /// <summary>黑名单 IP 列表</summary>
    public List<string> IpAddresses { get; set; } = new();

    /// <summary>触发限流多少次后自动加入黑名单（0 表示不自动加入）</summary>
    public int AutoBlockAfterRejections { get; set; } = 0;

    /// <summary>自动封禁时长（秒，0 表示永久）</summary>
    public int AutoBlockDurationSeconds { get; set; } = 3600; // 1小时
}

/// <summary>IP 白名单配置</summary>
public sealed class WhitelistConfig
{
    /// <summary>是否启用白名单</summary>
    public bool Enable { get; set; } = false;

    /// <summary>白名单 IP 列表</summary>
    public List<string> IpAddresses { get; set; } = new();
}
