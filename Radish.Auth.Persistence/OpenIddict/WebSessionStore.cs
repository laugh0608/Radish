using Microsoft.EntityFrameworkCore;

namespace Radish.Auth.OpenIddict;

public sealed class WebSessionStore(AuthOpenIddictDbContext db) : IWebSessionStore
{
    public async Task AddLoginAsync(WebAuthLogin login, DateTime now, CancellationToken cancellationToken)
    {
        await db.WebAuthLogins.Where(item => item.ExpiresAtUtc <= now).ExecuteDeleteAsync(cancellationToken);
        var entry = db.WebAuthLogins.Add(login);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            entry.State = EntityState.Detached;
        }
    }

    public Task<WebAuthLogin?> FindLoginAsync(string stateDigest, string browserDigest, DateTime now, CancellationToken cancellationToken)
    {
        return Login(stateDigest, browserDigest, now).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> AttachAuthenticatedSessionAsync(
        WebAuthLogin login, WebAuthSession session, DateTime now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var updated = await Login(login.StateDigest, login.BrowserDigest, now).Where(item => item.SessionId == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.SessionId, session.Id), cancellationToken);
        if (updated != 1)
        {
            return false;
        }

        var entry = db.WebAuthSessions.Add(session);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        finally
        {
            entry.State = EntityState.Detached;
        }
    }

    public async Task<bool> ConsumeLoginAsync(WebAuthLogin login, DateTime now, CancellationToken cancellationToken)
    {
        if (login.SessionId is null)
        {
            return false;
        }

        return await Login(login.StateDigest, login.BrowserDigest, now).Where(item => item.SessionId == login.SessionId)
            .ExecuteDeleteAsync(cancellationToken) == 1;
    }

    public Task<WebAuthSession?> FindByCookieAsync(string digest, CancellationToken cancellationToken)
    {
        return db.WebAuthSessions.AsNoTracking().SingleOrDefaultAsync(session => session.CookieDigest == digest, cancellationToken);
    }

    public async Task<bool> ActivateAsync(WebAuthSession session, string digest, string protectedTokens,
        DateTime accessExpiresAt, DateTime now, CancellationToken cancellationToken)
    {
        return await CurrentSession(session.Id, now)
            .Where(item => item.Version == session.Version && item.AuthorizationId != null && item.ActivatedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.ActivatedAtUtc, now)
                .SetProperty(item => item.CookieDigest, digest)
                .SetProperty(item => item.ProtectedTokens, protectedTokens)
                .SetProperty(item => item.AccessTokenExpiresAtUtc, accessExpiresAt)
                .SetProperty(item => item.Version, item => item.Version + 1), cancellationToken) == 1;
    }

    public async Task<bool> AcquireRefreshLeaseAsync(WebAuthSession session, Guid lease, DateTime now,
        DateTime leaseUntil, CancellationToken cancellationToken)
    {
        return await CurrentSession(session.Id, now)
            .Where(item => item.ActivatedAtUtc != null && item.ProtectedTokens != null && item.Version == session.Version &&
                           (item.RefreshLeaseUntilUtc == null || item.RefreshLeaseUntilUtc <= now) &&
                           (item.RefreshRetryAtUtc == null || item.RefreshRetryAtUtc <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.RefreshLeaseId, lease)
                .SetProperty(item => item.RefreshLeaseUntilUtc, leaseUntil)
                .SetProperty(item => item.Version, item => item.Version + 1), cancellationToken) == 1;
    }

    public async Task<bool> CompleteRefreshAsync(Guid id, Guid lease, string protectedTokens, DateTime accessExpiresAt,
        DateTime now, CancellationToken cancellationToken)
    {
        return await CurrentSession(id, now).Where(item => item.RefreshLeaseId == lease && item.RefreshLeaseUntilUtc > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.ProtectedTokens, protectedTokens)
                .SetProperty(item => item.AccessTokenExpiresAtUtc, accessExpiresAt)
                .SetProperty(item => item.RefreshLeaseId, (Guid?)null)
                .SetProperty(item => item.RefreshLeaseUntilUtc, (DateTime?)null)
                .SetProperty(item => item.RefreshRetryAtUtc, (DateTime?)null)
                .SetProperty(item => item.Version, item => item.Version + 1), cancellationToken) == 1;
    }

    public async Task ReleaseRefreshLeaseAsync(Guid id, Guid lease, DateTime retryAt, CancellationToken cancellationToken)
    {
        await db.WebAuthSessions.Where(item => item.Id == id && item.RefreshLeaseId == lease && item.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.RefreshLeaseId, (Guid?)null)
                .SetProperty(item => item.RefreshLeaseUntilUtc, (DateTime?)null)
                .SetProperty(item => item.RefreshRetryAtUtc, retryAt)
                .SetProperty(item => item.Version, item => item.Version + 1), cancellationToken);
    }

    public async Task<bool> RevokeFailedRefreshAsync(Guid id, Guid lease, DateTime now, CancellationToken cancellationToken)
    {
        return await db.WebAuthSessions.Where(item => item.Id == id && item.RefreshLeaseId == lease &&
                                                      item.RefreshLeaseUntilUtc > now && item.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.RevokedAtUtc, now)
                .SetProperty(item => item.ProtectedTokens, (string?)null)
                .SetProperty(item => item.AccessTokenExpiresAtUtc, (DateTime?)null)
                .SetProperty(item => item.RefreshLeaseId, (Guid?)null)
                .SetProperty(item => item.RefreshLeaseUntilUtc, (DateTime?)null)
                .SetProperty(item => item.RefreshRetryAtUtc, (DateTime?)null)
                .SetProperty(item => item.Version, item => item.Version + 1), cancellationToken) == 1;
    }

    private IQueryable<WebAuthLogin> Login(string state, string browser, DateTime now) =>
        db.WebAuthLogins.Where(item => item.StateDigest == state && item.BrowserDigest == browser && item.ExpiresAtUtc > now);

    private IQueryable<WebAuthSession> CurrentSession(Guid id, DateTime now)
    {
        var absoluteCutoff = now - WebAuthSessionLimits.AbsoluteLifetime;
        var idleCutoff = now - WebAuthSessionLimits.IdleTimeout;
        return db.WebAuthSessions.Where(item => item.Id == id && item.RevokedAtUtc == null &&
            item.CreatedAtUtc <= now && item.CreatedAtUtc > absoluteCutoff && item.LastActiveAtUtc > idleCutoff);
    }
}
