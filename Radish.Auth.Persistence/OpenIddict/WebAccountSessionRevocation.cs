using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using Radish.Common.HttpContextTool;

namespace Radish.Auth.OpenIddict;

public sealed class WebAccountSessionRevocation(AuthOpenIddictDbContext db)
{
    // SQLite's immediate write transaction serializes writers. PostgreSQL uses a transaction-scoped account lock.
    public static async Task<IDbContextTransaction> BeginAccountWriteAsync(AuthOpenIddictDbContext db,
        string tenant, string subject, CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (db.Database.IsNpgsql())
            {
                var key = $"radish-web-account:{tenant}:{subject}";
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
            }
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    public async Task RevokeAsync(string tenant, string subject, DateTime now, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginAccountWriteAsync(db, tenant, subject, cancellationToken);
        var sessions = db.WebAuthSessions.Where(row => row.TenantId == tenant && row.Subject == subject);
        await sessions.Where(row => row.RevokedAtUtc == null || row.ProtectedTokens != null ||
                row.AccessTokenExpiresAtUtc != null || row.RefreshLeaseId != null ||
                row.RefreshLeaseUntilUtc != null || row.RefreshRetryAtUtc != null)
            .ExecuteUpdateAsync(set => set
                .SetProperty(row => row.RevokedAtUtc, row => row.RevokedAtUtc ?? now)
                .SetProperty(row => row.ProtectedTokens, (string?)null)
                .SetProperty(row => row.AccessTokenExpiresAtUtc, (DateTime?)null)
                .SetProperty(row => row.RefreshLeaseId, (Guid?)null)
                .SetProperty(row => row.RefreshLeaseUntilUtc, (DateTime?)null)
                .SetProperty(row => row.RefreshRetryAtUtc, (DateTime?)null)
                .SetProperty(row => row.Version, row => row.Version + 1), cancellationToken);

        var webClientIds = db.Set<OpenIddictEntityFrameworkCoreApplication>()
            .Where(app => app.ClientId == WebSessionProtocol.ClientId).Select(app => app.Id);
        var boundIds = sessions.Select(s => s.AuthorizationId);
        var authorizations = db.Set<OpenIddictEntityFrameworkCoreAuthorization>().Where(a =>
            webClientIds.Contains(EF.Property<string>(a, "ApplicationId")) && a.Subject == subject && boundIds.Contains(a.Id));
        var authorizationIds = authorizations.Select(a => a.Id);
        var version = Guid.NewGuid().ToString();
        await db.Set<OpenIddictEntityFrameworkCoreToken>().Where(t =>
                t.Type == OpenIddictConstants.TokenTypeIdentifiers.RefreshToken &&
                t.Status == OpenIddictConstants.Statuses.Valid && authorizationIds.Contains(EF.Property<string>(t, "AuthorizationId")))
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.Status, OpenIddictConstants.Statuses.Revoked)
                .SetProperty(t => t.ConcurrencyToken, version), cancellationToken);
        await authorizations.Where(a => a.Status != OpenIddictConstants.Statuses.Revoked)
            .ExecuteUpdateAsync(set => set.SetProperty(a => a.Status, OpenIddictConstants.Statuses.Revoked)
                .SetProperty(a => a.ConcurrencyToken, version), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
