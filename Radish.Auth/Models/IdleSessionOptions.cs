namespace Radish.Auth.Models;

/// <summary>
/// 用户会话不活跃过期配置。
/// </summary>
public sealed class IdleSessionOptions
{
    public bool Enable { get; set; } = true;

    public int IdleTimeoutDays { get; set; } = 7;

    public int ClockSkewSeconds { get; set; } = 60;

    public int ClientActivityFutureToleranceSeconds { get; set; } = 60;

    public TimeSpan IdleTimeout => TimeSpan.FromDays(Math.Max(1, IdleTimeoutDays));

    public TimeSpan ClockSkew => TimeSpan.FromSeconds(Math.Max(0, ClockSkewSeconds));

    public TimeSpan ClientActivityFutureTolerance => TimeSpan.FromSeconds(Math.Max(0, ClientActivityFutureToleranceSeconds));
}
