namespace Radish.Auth.WebSessions;

public sealed record WebLoginStart(Uri AuthorizeUri, string CookieName, string BrowserSecret, DateTimeOffset ExpiresAt);

public sealed record WebLoginResult(WebTokenBackchannelStatus Status, string? Cookie = null, DateTimeOffset? ExpiresAt = null, string? ReturnPath = null);

public interface IWebSessionLoginService
{
    Task<WebLoginStart> StartAsync(string returnPath, CancellationToken cancellationToken);
    Task<bool> CompleteInteractiveAsync(string? state, string? browser, string subject, string tenant, CancellationToken cancellationToken);
    Task<bool> IsPendingAsync(string? state, string? browser, CancellationToken cancellationToken);
    Task<Guid?> FindAuthenticatedSessionAsync(string? state, string? browser, string? subject, string? tenant, CancellationToken cancellationToken);
    Task<WebLoginResult> CompleteAsync(string? state, string? browser, string? code, CancellationToken cancellationToken);
}
