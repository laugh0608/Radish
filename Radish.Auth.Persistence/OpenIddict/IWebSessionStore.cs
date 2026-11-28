namespace Radish.Auth.OpenIddict;

public interface IWebSessionStore
{
    Task AddLoginAsync(WebAuthLogin login, DateTime now, CancellationToken cancellationToken);
    Task<WebAuthLogin?> FindLoginAsync(string stateDigest, string browserDigest, DateTime now, CancellationToken cancellationToken);
    Task<bool> AttachAuthenticatedSessionAsync(WebAuthLogin login, WebAuthSession session, DateTime now,
        Func<Task<bool>> isAccountEnabled, CancellationToken cancellationToken);
    Task<bool> ConsumeLoginAsync(WebAuthLogin login, DateTime now, CancellationToken cancellationToken);

    Task<WebAuthSession?> FindByCookieAsync(string digest, CancellationToken cancellationToken);
    Task<bool> ActivateAsync(WebAuthSession session, string digest, string protectedTokens, DateTime accessExpiresAt, DateTime now, CancellationToken cancellationToken);
    Task<bool> AcquireRefreshLeaseAsync(WebAuthSession session, Guid lease, DateTime now, DateTime leaseUntil, CancellationToken cancellationToken);
    Task<bool> CompleteRefreshAsync(Guid id, Guid lease, string protectedTokens, DateTime accessExpiresAt, DateTime now, CancellationToken cancellationToken);
    Task ReleaseRefreshLeaseAsync(Guid id, Guid lease, DateTime retryAt, CancellationToken cancellationToken);
    Task<bool> RevokeFailedRefreshAsync(Guid id, Guid lease, DateTime now, CancellationToken cancellationToken);
}
