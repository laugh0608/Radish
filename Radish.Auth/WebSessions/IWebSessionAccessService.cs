namespace Radish.Auth.WebSessions;

public enum WebSessionResultStatus { Success, SignedOut, Retry }

public sealed record WebAccessTokenResult(WebSessionResultStatus Status, string? AccessToken = null, DateTimeOffset? ExpiresAt = null);

public interface IWebSessionAccessService
{
    Task<bool> IsAuthenticatedAsync(string? cookie, CancellationToken cancellationToken);
    Task<WebAccessTokenResult> GetTokenAsync(string? cookie, CancellationToken cancellationToken);
    Task<WebSessionResultStatus> RecordActivityAsync(string? cookie, CancellationToken cancellationToken);
    Task<WebSessionResultStatus> LogoutAsync(string? cookie, CancellationToken cancellationToken);
}
