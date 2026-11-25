namespace Radish.Auth.OpenIddict;

public sealed class WebAuthSession
{
    public Guid Id { get; init; }
    public required string Subject { get; init; }
    public required string TenantId { get; init; }
    public string? AuthorizationId { get; set; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime LastActiveAtUtc { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public long Version { get; init; }
    public string? CookieDigest { get; init; }
    public string? ProtectedTokens { get; init; }
    public DateTime? AccessTokenExpiresAtUtc { get; init; }
    public Guid? RefreshLeaseId { get; init; }
    public DateTime? RefreshLeaseUntilUtc { get; init; }
    public DateTime? RefreshRetryAtUtc { get; init; }
}

public static class WebAuthSessionLimits
{
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromDays(7);
    public static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromDays(30);
}
