using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Radish.Auth.OpenIddict;
using Radish.Common.HttpContextTool;

namespace Radish.Auth.WebSessions;

public sealed class WebSessionLoginService(IWebSessionStore store, IWebAuthSessionRepository sessions,
    IWebTokenBackchannel tokens, WebSessionProtection protection, IConfiguration configuration, TimeProvider clock)
    : IWebSessionLoginService
{
    private static readonly TimeSpan LoginLifetime = TimeSpan.FromMinutes(10);

    public async Task<WebLoginStart> StartAsync(string returnPath, CancellationToken cancellationToken)
    {
        var issuer = WebAuthClient.GetIssuer(configuration);
        var state = WebSessionSecrets.NewSecret();
        var browser = WebSessionSecrets.NewSecret();
        var verifier = WebSessionSecrets.NewSecret();
        var nonce = WebSessionSecrets.NewSecret();
        var now = clock.GetUtcNow();
        var expires = now + LoginLifetime;
        var stateDigest = WebSessionSecrets.Digest(state);
        await store.AddLoginAsync(new WebAuthLogin
        {
            StateDigest = stateDigest,
            BrowserDigest = WebSessionSecrets.Digest(browser),
            ProtectedRequest = protection.ProtectLogin(stateDigest, new LoginRequest(verifier, nonce, returnPath)),
            ExpiresAtUtc = expires.UtcDateTime
        }, now.UtcDateTime, cancellationToken);

        var authorize = QueryHelpers.AddQueryString(new Uri(WebAuthClient.GetOrigin(issuer), "/connect/authorize").AbsoluteUri,
            new Dictionary<string, string?>
            {
                ["client_id"] = WebAuthClient.ClientId,
                ["redirect_uri"] = WebAuthClient.GetCallback(issuer).AbsoluteUri,
                ["response_type"] = "code",
                ["scope"] = $"{UserScopes.OpenId} {UserScopes.Profile} {UserScopes.OfflineAccess} {UserScopes.RadishApi}",
                ["state"] = state,
                ["nonce"] = nonce,
                ["prompt"] = "login",
                ["code_challenge_method"] = "S256",
                ["code_challenge"] = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            });
        return new(new Uri(authorize), browser, expires);
    }

    public async Task<bool> CompleteInteractiveAsync(string? state, string? browser, string subject, string tenant,
        CancellationToken cancellationToken)
    {
        var login = await FindLoginAsync(state, browser, cancellationToken);
        if (login is null || login.SessionId.HasValue)
        {
            return false;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var session = new WebAuthSession
        {
            Id = Guid.NewGuid(),
            Subject = subject,
            TenantId = tenant,
            CreatedAtUtc = now,
            LastActiveAtUtc = now
        };
        return await store.AttachAuthenticatedSessionAsync(login, session, now, cancellationToken);
    }

    public async Task<bool> IsPendingAsync(string? state, string? browser, CancellationToken cancellationToken)
    {
        return await FindLoginAsync(state, browser, cancellationToken) is { SessionId: null };
    }

    public async Task<Guid?> FindAuthenticatedSessionAsync(string? state, string? browser, string? subject, string? tenant,
        CancellationToken cancellationToken)
    {
        var login = await FindLoginAsync(state, browser, cancellationToken);
        if (login?.SessionId is not { } id)
        {
            return null;
        }

        var session = await sessions.FindAsync(id, cancellationToken);
        return session is { AuthorizationId: null, ActivatedAtUtc: null, RevokedAtUtc: null } &&
               session.Subject == subject && session.TenantId == tenant ? session.Id : null;
    }

    public async Task<WebLoginResult> CompleteAsync(string? state, string? browser, string? code, CancellationToken cancellationToken)
    {
        var login = await FindLoginAsync(state, browser, cancellationToken);
        if (string.IsNullOrWhiteSpace(code) || code.Length > 24_576 || login?.SessionId is not { } id)
        {
            return new(WebTokenBackchannelStatus.Rejected);
        }

        var session = await sessions.FindAsync(id, cancellationToken);
        if (session is not { AuthorizationId: not null, ActivatedAtUtc: null, RevokedAtUtc: null })
        {
            return new(WebTokenBackchannelStatus.Rejected);
        }

        var request = protection.ReadLogin<LoginRequest>(login.StateDigest, login.ProtectedRequest);
        if (!await store.ConsumeLoginAsync(login, clock.GetUtcNow().UtcDateTime, cancellationToken))
        {
            return new(WebTokenBackchannelStatus.Rejected);
        }

        var exchange = await tokens.ExchangeAsync(code, request.Verifier, request.Nonce, session, cancellationToken);
        if (exchange.Tokens is not { } issued || exchange.Status != WebTokenBackchannelStatus.Success)
        {
            return new(exchange.Status);
        }

        var cookie = WebSessionSecrets.NewSecret();
        var now = clock.GetUtcNow().UtcDateTime;
        if (issued.AccessExpiresAt.UtcDateTime <= now || !await store.ActivateAsync(session, WebSessionSecrets.Digest(cookie),
                protection.ProtectTokens(session.Id, issued), issued.AccessExpiresAt.UtcDateTime, now, cancellationToken))
        {
            return new(WebTokenBackchannelStatus.Rejected);
        }

        return new(WebTokenBackchannelStatus.Success, cookie,
            new DateTimeOffset(session.CreatedAtUtc + WebAuthSessionLimits.AbsoluteLifetime, TimeSpan.Zero), request.ReturnPath);
    }

    private Task<WebAuthLogin?> FindLoginAsync(string? state, string? browser, CancellationToken cancellationToken) =>
        WebAuthClient.IsEnabled(configuration) && WebSessionSecrets.IsSecret(state) && WebSessionSecrets.IsSecret(browser)
            ? store.FindLoginAsync(WebSessionSecrets.Digest(state), WebSessionSecrets.Digest(browser), clock.GetUtcNow().UtcDateTime, cancellationToken)
            : Task.FromResult<WebAuthLogin?>(null);

    private sealed record LoginRequest(string Verifier, string Nonce, string ReturnPath);
}
