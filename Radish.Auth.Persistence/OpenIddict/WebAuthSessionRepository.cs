using Microsoft.EntityFrameworkCore;

namespace Radish.Auth.OpenIddict;

public sealed class WebAuthSessionRepository(AuthOpenIddictDbContext db) : IWebAuthSessionRepository
{
    public Task<WebAuthSession?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        return db.WebAuthSessions.AsNoTracking().SingleOrDefaultAsync(session => session.Id == id, cancellationToken);
    }

    public async Task AddAsync(WebAuthSession session, CancellationToken cancellationToken)
    {
        var entry = db.WebAuthSessions.Add(session);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            entry.State = EntityState.Detached;
        }
    }

    public async Task<bool> UpdateAsync(WebAuthSession session, CancellationToken cancellationToken)
    {
        var affected = await db.WebAuthSessions
            .Where(current => current.Id == session.Id && current.Version == session.Version)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(current => current.AuthorizationId, session.AuthorizationId)
                .SetProperty(current => current.LastActiveAtUtc, session.LastActiveAtUtc)
                .SetProperty(current => current.ActivatedAtUtc, session.ActivatedAtUtc)
                .SetProperty(current => current.RevokedAtUtc, session.RevokedAtUtc)
                .SetProperty(current => current.ProtectedTokens, current => session.RevokedAtUtc.HasValue ? null : current.ProtectedTokens)
                .SetProperty(current => current.AccessTokenExpiresAtUtc, current => session.RevokedAtUtc.HasValue ? null : current.AccessTokenExpiresAtUtc)
                .SetProperty(current => current.RefreshLeaseId, current => session.RevokedAtUtc.HasValue ? null : current.RefreshLeaseId)
                .SetProperty(current => current.RefreshLeaseUntilUtc, current => session.RevokedAtUtc.HasValue ? null : current.RefreshLeaseUntilUtc)
                .SetProperty(current => current.RefreshRetryAtUtc, current => session.RevokedAtUtc.HasValue ? null : current.RefreshRetryAtUtc)
                .SetProperty(current => current.Version, current => current.Version + 1), cancellationToken);

        return affected == 1;
    }
}
