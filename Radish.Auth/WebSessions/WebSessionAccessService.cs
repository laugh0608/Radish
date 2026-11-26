using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Radish.Auth.OpenIddict;

namespace Radish.Auth.WebSessions;

public sealed class WebSessionAccessService(IWebSessionStore store, IWebAuthSessionService sessions,
    IWebTokenBackchannel backchannel, WebSessionProtection protection, IOpenIddictAuthorizationManager authorizations,
    IConfiguration configuration, TimeProvider clock) : IWebSessionAccessService
{
    private static readonly TimeSpan RenewBefore = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RefreshLease = TimeSpan.FromSeconds(20);

    public async Task<bool> IsAuthenticatedAsync(string? cookie, CancellationToken cancellationToken)
    {
        var session = await FindAsync(cookie, cancellationToken);
        return IsActive(session, clock.GetUtcNow().UtcDateTime);
    }

    public async Task<WebSessionResultStatus> RecordActivityAsync(string? cookie, CancellationToken cancellationToken)
    {
        var session = await FindAsync(cookie, cancellationToken);
        if (session is null || !IsActive(session, clock.GetUtcNow().UtcDateTime))
        {
            return WebSessionResultStatus.SignedOut;
        }

        if (await sessions.RecordActivityAsync(session.Id, cancellationToken))
        {
            return WebSessionResultStatus.Success;
        }

        return await IsAuthenticatedAsync(cookie, cancellationToken) ? WebSessionResultStatus.Retry : WebSessionResultStatus.SignedOut;
    }

    public async Task<WebSessionResultStatus> LogoutAsync(string? cookie, CancellationToken cancellationToken)
    {
        var session = await FindAsync(cookie, cancellationToken);
        if (session is null)
        {
            return WebSessionResultStatus.SignedOut;
        }

        if (!await sessions.RevokeAsync(session.Id, cancellationToken))
        {
            return WebSessionResultStatus.Retry;
        }

        return await RevokeAuthorizationAsync(session.AuthorizationId, cancellationToken)
            ? WebSessionResultStatus.Success : WebSessionResultStatus.Retry;
    }

    public async Task<WebAccessTokenResult> GetTokenAsync(string? cookie, CancellationToken cancellationToken)
    {
        try
        {
            var session = await FindAsync(cookie, cancellationToken);
            if (session is null || !IsActive(session, clock.GetUtcNow().UtcDateTime))
            {
                return new(WebSessionResultStatus.SignedOut);
            }

            if (session.ProtectedTokens is not { } protectedTokens)
            {
                return new(WebSessionResultStatus.SignedOut);
            }

            var stored = protection.ReadTokens<WebSessionTokens>(session.Id, protectedTokens);
            var now = clock.GetUtcNow().UtcDateTime;
            if (!IsActive(session, now))
            {
                return new(WebSessionResultStatus.SignedOut);
            }

            if (session.AccessTokenExpiresAtUtc > now + RenewBefore)
            {
                return Access(stored);
            }

            var lease = Guid.NewGuid();
            var leaseUntil = now + RefreshLease;
            if (!await store.AcquireRefreshLeaseAsync(session, lease, now, leaseUntil, cancellationToken))
            {
                return await ReadAfterContendedRefreshAsync(cookie, cancellationToken);
            }

            return await RefreshAsync(session, stored.RefreshToken, lease, leaseUntil, cookie);
        }
        catch (Exception error) when (error is CryptographicException or JsonException or DbException or DbUpdateException)
        {
            // Preserve the cookie and persisted credentials when infrastructure is temporarily inaccessible.
            return new(WebSessionResultStatus.Retry);
        }
    }

    private async Task<WebAccessTokenResult> RefreshAsync(WebAuthSession session, string refresh, Guid lease,
        DateTime leaseUntil, string? cookie)
    {
        // Once leased, let the bounded exchange complete even if the browser closes its connection.
        var refreshed = await backchannel.RefreshAsync(refresh, lease, session, CancellationToken.None);
        var now = clock.GetUtcNow().UtcDateTime;

        // Persist any completed exchange independently of the browser connection that requested it.
        if (refreshed is { Status: WebTokenBackchannelStatus.Success, Tokens: { } issued } && issued.AccessExpiresAt.UtcDateTime > now)
        {
            var protectedTokens = protection.ProtectTokens(session.Id, issued);
            if (await store.CompleteRefreshAsync(session.Id, lease, protectedTokens, issued.AccessExpiresAt.UtcDateTime, now,
                    CancellationToken.None))
            {
                var current = await FindAsync(cookie, CancellationToken.None);
                return IsActive(current, clock.GetUtcNow().UtcDateTime) ? Access(issued) : new(WebSessionResultStatus.SignedOut);
            }

            return await ReadAfterContendedRefreshAsync(cookie, CancellationToken.None);
        }

        if (refreshed.Status == WebTokenBackchannelStatus.Rejected)
        {
            if (await store.RevokeFailedRefreshAsync(session.Id, lease, now, CancellationToken.None))
            {
                await RevokeAuthorizationAsync(session.AuthorizationId, CancellationToken.None);
                return new(WebSessionResultStatus.SignedOut);
            }

            return await ReadAfterContendedRefreshAsync(cookie, CancellationToken.None);
        }

        // A missing response may mean the issuer received the grant. Defer retries until the lease would have expired.
        await store.ReleaseRefreshLeaseAsync(session.Id, lease, leaseUntil, CancellationToken.None);
        // Activity, expiry or logout may have changed the session during the exchange.
        return await IsAuthenticatedAsync(cookie, CancellationToken.None)
            ? new(WebSessionResultStatus.Retry)
            : new(WebSessionResultStatus.SignedOut);
    }

    private async Task<WebAccessTokenResult> ReadAfterContendedRefreshAsync(string? cookie, CancellationToken cancellationToken)
    {
        var session = await FindAsync(cookie, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        if (session is null || !IsActive(session, now))
        {
            return new(WebSessionResultStatus.SignedOut);
        }

        if (session.AccessTokenExpiresAtUtc > now && session.ProtectedTokens is { } protectedTokens)
        {
            return Access(protection.ReadTokens<WebSessionTokens>(session.Id, protectedTokens));
        }

        return new(WebSessionResultStatus.Retry);
    }

    private async Task<bool> RevokeAuthorizationAsync(string? id, CancellationToken cancellationToken)
    {
        if (id is null || await authorizations.FindByIdAsync(id, cancellationToken) is not { } authorization ||
            await authorizations.HasStatusAsync(authorization, OpenIddictConstants.Statuses.Revoked, cancellationToken))
        {
            return true;
        }

        return await authorizations.TryRevokeAsync(authorization, cancellationToken);
    }

    private Task<WebAuthSession?> FindAsync(string? cookie, CancellationToken cancellationToken) =>
        WebAuthClient.IsEnabled(configuration) && WebSessionSecrets.IsSecret(cookie)
            ? store.FindByCookieAsync(WebSessionSecrets.Digest(cookie), cancellationToken)
            : Task.FromResult<WebAuthSession?>(null);

    private static bool IsActive(WebAuthSession? session, DateTime now) =>
        session is { ActivatedAtUtc: not null, RevokedAtUtc: null } && session.CreatedAtUtc <= now &&
        session.CreatedAtUtc + WebAuthSessionLimits.AbsoluteLifetime > now && session.LastActiveAtUtc + WebAuthSessionLimits.IdleTimeout > now;

    private static WebAccessTokenResult Access(WebSessionTokens token) => new(WebSessionResultStatus.Success, token.AccessToken, token.AccessExpiresAt);
}
