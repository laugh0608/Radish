using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using Radish.Api.Tests.TestCollections;
using Radish.Auth.OpenIddict;
using Xunit;

namespace Radish.Api.Tests.Auth;

[Collection(PostgreSqlIntegrationCollection.CollectionName)]
public sealed class WebAccountSessionRevocationTest
{
    [Fact]
    public async Task Revoke_ClearsPendingAndActiveSessionsAndRetriesOnlyTheirWebGrants()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(ct);
        await using var db = new AuthOpenIddictDbContext(new DbContextOptionsBuilder<AuthOpenIddictDbContext>()
            .UseSqlite(connection).Options);
        await CheckRevocationAsync(db);
    }

    [Fact]
    [Trait("Database", "PostgreSQL")]
    public Task PostgreSql_Revoke_ClearsOnlyBoundWebGrants() =>
        TemporaryPostgreSqlDatabase.RunAsync(async cs =>
        {
            await using var db = new AuthOpenIddictDbContext(new DbContextOptionsBuilder<AuthOpenIddictDbContext>()
                .UseNpgsql(cs).Options);
            await CheckRevocationAsync(db);
        });

    private static async Task CheckRevocationAsync(AuthOpenIddictDbContext db)
    {
        var ct = TestContext.Current.CancellationToken;
        await db.Database.EnsureCreatedAsync(ct);
        var web = new OpenIddictEntityFrameworkCoreApplication { ClientId = WebAuthClient.ClientId };
        var console = new OpenIddictEntityFrameworkCoreApplication { ClientId = "radish-console" };
        var now = DateTime.UtcNow;
        var active = AddSession(web, "1", "31");
        var prior = AddSession(web, "1", "31");
        prior.RevokedAtUtc = now.AddDays(-1);
        var pending = new WebAuthSession { Id = Guid.NewGuid(), Subject = "31", TenantId = "1", CreatedAtUtc = now };
        db.Add(pending);
        var otherTenant = AddSession(web, "2", "31");
        var otherAccount = AddSession(web, "1", "32");
        var otherClient = AddGrant(console, "31");
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        var revocation = new WebAccountSessionRevocation(db);
        var failGrant = db.Database.IsSqlite()
            ? "CREATE TRIGGER fail_grant BEFORE UPDATE ON OpenIddictAuthorizations BEGIN SELECT RAISE(ABORT, 'fixture failure'); END"
            : """ALTER TABLE "OpenIddictAuthorizations" ADD CONSTRAINT fail_grant CHECK ("Status" <> 'revoked')""";
        await db.Database.ExecuteSqlRawAsync(failGrant, ct);
        await Assert.ThrowsAnyAsync<System.Data.Common.DbException>(() => revocation.RevokeAsync("1", "31", now, ct));
        Assert.Null((await db.WebAuthSessions.AsNoTracking().SingleAsync(s => s.Id == active.Id, ct)).RevokedAtUtc);
        await AssertGrantAsync(active.AuthorizationId!, OpenIddictConstants.Statuses.Valid);
        var restore = db.Database.IsSqlite() ? "DROP TRIGGER fail_grant"
            : """ALTER TABLE "OpenIddictAuthorizations" DROP CONSTRAINT fail_grant""";
        await db.Database.ExecuteSqlRawAsync(restore, ct);

        await revocation.RevokeAsync("1", "31", now, ct);
        await revocation.RevokeAsync("1", "31", now.AddMinutes(1), ct);

        foreach (var id in new[] { active.Id, prior.Id, pending.Id })
        {
            var row = await db.WebAuthSessions.SingleAsync(s => s.Id == id, ct);
            Assert.NotNull(row.RevokedAtUtc);
            Assert.Equal(1, row.Version);
            Assert.Null(row.ProtectedTokens);
            Assert.Null(row.AccessTokenExpiresAtUtc);
            Assert.Null(row.RefreshLeaseId);
            Assert.Null(row.RefreshLeaseUntilUtc);
            Assert.Null(row.RefreshRetryAtUtc);
        }
        Assert.False(await new WebAuthSessionRepository(db).UpdateAsync(active, ct));
        foreach (var id in new[] { active.AuthorizationId, prior.AuthorizationId })
            await AssertGrantAsync(id!, OpenIddictConstants.Statuses.Revoked);
        foreach (var id in new[] { otherTenant.AuthorizationId, otherAccount.AuthorizationId, otherClient.Id })
            await AssertGrantAsync(id!, OpenIddictConstants.Statuses.Valid);
        Assert.Equal(2, await db.WebAuthSessions.CountAsync(s => s.RevokedAtUtc == null, ct));

        OpenIddictEntityFrameworkCoreAuthorization AddGrant(OpenIddictEntityFrameworkCoreApplication client, string subject)
        {
            var authorization = new OpenIddictEntityFrameworkCoreAuthorization { Application = client, Subject = subject,
                Status = OpenIddictConstants.Statuses.Valid };
            db.Add(new OpenIddictEntityFrameworkCoreToken { Application = client, Authorization = authorization,
                Type = OpenIddictConstants.TokenTypeIdentifiers.RefreshToken, Status = OpenIddictConstants.Statuses.Valid });
            return authorization;
        }

        WebAuthSession AddSession(OpenIddictEntityFrameworkCoreApplication client, string tenant, string subject)
        {
            var grant = AddGrant(client, subject);
            var row = new WebAuthSession { Id = Guid.NewGuid(), TenantId = tenant, Subject = subject,
                AuthorizationId = grant.Id, CreatedAtUtc = now, LastActiveAtUtc = now, ActivatedAtUtc = now,
                ProtectedTokens = "test", AccessTokenExpiresAtUtc = now.AddMinutes(5),
                RefreshLeaseId = Guid.NewGuid(), RefreshLeaseUntilUtc = now.AddSeconds(20) };
            db.Add(row);
            return row;
        }

        async Task AssertGrantAsync(string id, string status)
        {
            Assert.Equal(status, await db.Set<OpenIddictEntityFrameworkCoreAuthorization>()
                .Where(a => a.Id == id).Select(a => a.Status).SingleAsync(ct));
            Assert.Equal(status, await db.Set<OpenIddictEntityFrameworkCoreToken>()
                .Where(t => t.Authorization!.Id == id).Select(t => t.Status).SingleAsync(ct));
        }
    }
}
