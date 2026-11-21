using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Radish.Auth.OpenIddict;
using Xunit;

namespace Radish.Api.Tests.Auth;

public sealed class WebAuthSessionServiceTest
{
    [Fact]
    public async Task RestoringAndRefreshing_ShouldNotExtendIdleDeadline()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await SessionFixture.CreateAsync(cancellationToken);
        var binding = await fixture.CreateActiveSessionAsync(cancellationToken);
        var original = fixture.Clock.Now;

        fixture.Clock.Now = original.AddDays(1);
        var overnight = await fixture.Sessions.EvaluateGrantAsync(binding, isRefresh: true, cancellationToken);
        Assert.Equal(WebAuthSessionGrantStatus.Allowed, overnight.Status);
        Assert.Equal(fixture.Clock.Now.AddMinutes(5), overnight.AccessTokenExpiresAt);
        Assert.Equal(original.AddDays(30), overnight.RefreshTokenExpiresAt);

        fixture.Clock.Now = original.AddDays(7).AddSeconds(-1);
        var finalGrant = await fixture.Sessions.EvaluateGrantAsync(binding, isRefresh: true, cancellationToken);
        Assert.Equal(original.AddDays(7), finalGrant.AccessTokenExpiresAt);

        fixture.Clock.Now = original.AddDays(7);
        Assert.False(await fixture.Sessions.RecordActivityAsync(binding.SessionId, cancellationToken));
        Assert.Equal(WebAuthSessionGrantStatus.IdleExpired,
            (await fixture.Sessions.EvaluateGrantAsync(binding, isRefresh: true, cancellationToken)).Status);
    }

    [Fact]
    public async Task Activity_ShouldExtendIdleDeadlineWithoutMovingAbsoluteDeadline()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await SessionFixture.CreateAsync(cancellationToken);
        var binding = await fixture.CreateActiveSessionAsync(cancellationToken);
        var original = fixture.Clock.Now;

        for (var day = 6; day < 30; day += 6)
        {
            fixture.Clock.Now = original.AddDays(day);
            Assert.True(await fixture.Sessions.RecordActivityAsync(binding.SessionId, cancellationToken));
        }

        fixture.Clock.Now = original.AddDays(30).AddSeconds(-1);
        Assert.True(await fixture.Sessions.RecordActivityAsync(binding.SessionId, cancellationToken));
        var grant = await fixture.Sessions.EvaluateGrantAsync(binding, isRefresh: true, cancellationToken);
        Assert.Equal(WebAuthSessionGrantStatus.Allowed, grant.Status);
        Assert.Equal(original.AddDays(30), grant.AccessTokenExpiresAt);
        Assert.Equal(original.AddDays(30), grant.RefreshTokenExpiresAt);

        fixture.Clock.Now = original.AddDays(30);
        Assert.False(await fixture.Sessions.RecordActivityAsync(binding.SessionId, cancellationToken));
        Assert.Equal(WebAuthSessionGrantStatus.AbsoluteExpired,
            (await fixture.Sessions.EvaluateGrantAsync(binding, isRefresh: true, cancellationToken)).Status);
    }

    [Fact]
    public async Task Grants_ShouldRequireMatchingIdentityAuthorizationAndLifecycle()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await SessionFixture.CreateAsync(cancellationToken);
        var id = await fixture.Sessions.CreatePendingAsync("10001", "0", cancellationToken);
        var binding = new WebAuthSessionBinding(id, "10001", "0", "auth-1");

        Assert.False(await fixture.Sessions.BindAuthorizationAsync(binding with { Subject = "10002" }, cancellationToken));
        Assert.False(await fixture.Sessions.ActivateAsync(binding, cancellationToken));
        Assert.True(await fixture.Sessions.BindAuthorizationAsync(binding, cancellationToken));
        Assert.False(await fixture.Sessions.BindAuthorizationAsync(binding with { AuthorizationId = "auth-2" }, cancellationToken));
        Assert.Equal(WebAuthSessionGrantStatus.Allowed,
            (await fixture.Sessions.EvaluateGrantAsync(binding, isRefresh: false, cancellationToken)).Status);
        Assert.Equal(WebAuthSessionGrantStatus.Invalid,
            (await fixture.Sessions.EvaluateGrantAsync(binding, isRefresh: true, cancellationToken)).Status);
        Assert.True(await fixture.Sessions.ActivateAsync(binding, cancellationToken));
        Assert.Equal(WebAuthSessionGrantStatus.Invalid,
            (await fixture.Sessions.EvaluateGrantAsync(binding with { TenantId = "1" }, isRefresh: true, cancellationToken)).Status);
        Assert.Equal(WebAuthSessionGrantStatus.Invalid,
            (await fixture.Sessions.EvaluateGrantAsync(binding, isRefresh: false, cancellationToken)).Status);
        Assert.True(await fixture.Sessions.RevokeAsync(id, cancellationToken));
        Assert.False(await fixture.Sessions.RecordActivityAsync(id, cancellationToken));
        Assert.Equal(WebAuthSessionGrantStatus.Invalid,
            (await fixture.Sessions.EvaluateGrantAsync(binding, isRefresh: true, cancellationToken)).Status);
    }

    [Fact]
    public async Task Activity_ShouldBeThrottledAndAStaleVersionCannotUndoRevocation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await SessionFixture.CreateAsync(cancellationToken);
        var binding = await fixture.CreateActiveSessionAsync(cancellationToken);
        var stale = await fixture.Repository.FindAsync(binding.SessionId, cancellationToken);
        Assert.NotNull(stale);

        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(59);
        Assert.True(await fixture.Sessions.RecordActivityAsync(binding.SessionId, cancellationToken));
        Assert.Equal(stale.Version, (await fixture.Repository.FindAsync(binding.SessionId, cancellationToken))!.Version);
        Assert.True(await fixture.Sessions.RevokeAsync(binding.SessionId, cancellationToken));
        stale.LastActiveAtUtc = fixture.Clock.Now.UtcDateTime;
        Assert.False(await fixture.Repository.UpdateAsync(stale, cancellationToken));
    }

    private sealed class SessionFixture(SqliteConnection connection, AuthOpenIddictDbContext db) : IAsyncDisposable
    {
        public TestClock Clock { get; } = new();
        public WebAuthSessionRepository Repository { get; } = new(db);
        public WebAuthSessionService Sessions => new(Repository, Clock);

        public static async Task<SessionFixture> CreateAsync(CancellationToken cancellationToken)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            var db = new AuthOpenIddictDbContext(new DbContextOptionsBuilder<AuthOpenIddictDbContext>()
                .UseSqlite(connection).Options);
            var fixture = new SessionFixture(connection, db);
            try
            {
                await connection.OpenAsync(cancellationToken);
                await db.Database.EnsureCreatedAsync(cancellationToken);
                return fixture;
            }
            catch
            {
                await fixture.DisposeAsync();
                throw;
            }
        }

        public async Task<WebAuthSessionBinding> CreateActiveSessionAsync(CancellationToken cancellationToken)
        {
            var id = await Sessions.CreatePendingAsync("10001", "0", cancellationToken);
            var binding = new WebAuthSessionBinding(id, "10001", "0", "auth-1");
            Assert.True(await Sessions.BindAuthorizationAsync(binding, cancellationToken));
            Assert.True(await Sessions.ActivateAsync(binding, cancellationToken));
            return binding;
        }

        public async ValueTask DisposeAsync()
        {
            await db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
