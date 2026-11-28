using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Radish.Api.Tests.TestCollections;
using Radish.Auth.OpenIddict;
using Xunit;

namespace Radish.Api.Tests.Auth;

[Collection(PostgreSqlIntegrationCollection.CollectionName)]
public sealed class WebAccountSessionConcurrencyTest
{
    [Fact]
    public async Task Sqlite_RevocationOrdersPendingLoginsAcrossConnections()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-account-lock-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AuthOpenIddictDbContext>()
            .UseSqlite($"Data Source={path};Pooling=False").Options;
        try
        {
            await using var db = new AuthOpenIddictDbContext(options);
            await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            await CheckConcurrentAttachAsync(options);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    [Trait("Database", "PostgreSQL")]
    public Task PostgreSql_RevocationOrdersPendingLoginsAcrossConnections() =>
        TemporaryPostgreSqlDatabase.RunAsync(async cs =>
        {
            var options = new DbContextOptionsBuilder<AuthOpenIddictDbContext>().UseNpgsql(cs).Options;
            await using var db = new AuthOpenIddictDbContext(options);
            await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            await CheckConcurrentAttachAsync(options);
        });

    private static async Task CheckConcurrentAttachAsync(DbContextOptions<AuthOpenIddictDbContext> options)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        var ct = timeout.Token;
        await using var first = new AuthOpenIddictDbContext(options);
        await using var second = new AuthOpenIddictDbContext(options);
        var store = new WebSessionStore(first);
        var now = DateTime.UtcNow;
        var login = new WebAuthLogin { StateDigest = "login", BrowserDigest = "browser", ProtectedRequest = "fixture",
            ExpiresAtUtc = now.AddMinutes(10) };
        await store.AddLoginAsync(login, now, ct);
        var session = new WebAuthSession { Id = Guid.NewGuid(), Subject = "101", TenantId = "7", CreatedAtUtc = now,
            LastActiveAtUtc = now };
        var checkingAccount = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowAttach = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var attaching = store.AttachAuthenticatedSessionAsync(login, session, now, () =>
        {
            checkingAccount.SetResult();
            return allowAttach.Task.WaitAsync(ct);
        }, ct);
        await checkingAccount.Task.WaitAsync(ct);
        var revokeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var revoking = Task.Run(async () =>
        {
            revokeStarted.SetResult();
            await new WebAccountSessionRevocation(second).RevokeAsync("7", "101", now, ct);
        }, ct);
        await revokeStarted.Task.WaitAsync(ct);
        try
        {
            Assert.NotSame(revoking, await Task.WhenAny(revoking, Task.Delay(100, ct)));
        }
        finally
        {
            allowAttach.TrySetResult(true);
        }
        Assert.True(await attaching);
        await revoking;
        Assert.NotNull((await first.WebAuthSessions.AsNoTracking().SingleAsync(ct)).RevokedAtUtc);

        var laterLogin = new WebAuthLogin { StateDigest = "later", BrowserDigest = "other", ProtectedRequest = "fixture",
            ExpiresAtUtc = now.AddMinutes(10) };
        await store.AddLoginAsync(laterLogin, now, ct);
        Assert.False(await store.AttachAuthenticatedSessionAsync(laterLogin, session, now, () => Task.FromResult(false), ct));
        Assert.Null((await first.WebAuthLogins.AsNoTracking().SingleAsync(row => row.StateDigest == "later", ct)).SessionId);
    }
}
