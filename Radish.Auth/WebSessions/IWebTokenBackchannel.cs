using Radish.Auth.OpenIddict;

namespace Radish.Auth.WebSessions;

public sealed record WebSessionTokens(string AccessToken, string RefreshToken, DateTimeOffset AccessExpiresAt);

public enum WebTokenBackchannelStatus { Success, Rejected, Unavailable }

public sealed record WebTokenBackchannelResult(WebTokenBackchannelStatus Status, WebSessionTokens? Tokens = null);

public interface IWebTokenBackchannel
{
    Task<WebTokenBackchannelResult> ExchangeAsync(string code, string verifier, string nonce, WebAuthSession session,
        CancellationToken cancellationToken);

    Task<WebTokenBackchannelResult> RefreshAsync(string refreshToken, Guid lease, WebAuthSession session,
        CancellationToken cancellationToken);
}
