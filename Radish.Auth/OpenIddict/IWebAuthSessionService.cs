namespace Radish.Auth.OpenIddict;

public readonly record struct WebAuthSessionBinding(Guid SessionId, string Subject, string TenantId, string AuthorizationId);

public enum WebAuthSessionGrantStatus
{
    Invalid,
    IdleExpired,
    AbsoluteExpired,
    Allowed
}

public readonly record struct WebAuthSessionGrant(
    WebAuthSessionGrantStatus Status,
    DateTimeOffset AccessTokenExpiresAt = default,
    DateTimeOffset RefreshTokenExpiresAt = default);

public interface IWebAuthSessionService
{
    Task<Guid> CreatePendingAsync(string subject, string tenantId, CancellationToken cancellationToken);
    Task<bool> BindAuthorizationAsync(WebAuthSessionBinding binding, CancellationToken cancellationToken);
    Task<bool> ActivateAsync(WebAuthSessionBinding binding, CancellationToken cancellationToken);
    Task<bool> RecordActivityAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<bool> RevokeAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<WebAuthSessionGrant> EvaluateGrantAsync(WebAuthSessionBinding binding, bool isRefresh, CancellationToken cancellationToken,
        Guid? requiredLease = null);
}
