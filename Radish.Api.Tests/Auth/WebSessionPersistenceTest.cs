using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using OpenIddict.Abstractions;
using Radish.Auth.Controllers;
using Radish.Auth.OpenIddict;
using Radish.Auth.WebSessions;
using Radish.Common.HttpContextTool;
using Xunit;

namespace Radish.Api.Tests.Auth;

public sealed partial class WebSessionPersistenceTest
{
    private const string InitialRefresh = "initial-test-refresh";

    [Fact]
    public async Task LoginRequiresFreshBoundBrowserAndConsumesTheAuthorizationCallbackOnce()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellation);
        var start = await fixture.Login.StartAsync("/forum", cancellation);
        var parameters = QueryHelpers.ParseQuery(start.AuthorizeUri.Query);
        var state = parameters["state"].ToString();
        Assert.Equal("login", parameters["prompt"].ToString());
        Assert.Null(await fixture.Login.FindAuthenticatedSessionAsync(state, start.BrowserSecret, "10001", "0", cancellation));
        Assert.False(await fixture.Login.CompleteInteractiveAsync(state, WebSessionSecrets.NewSecret(), "10001", "0", cancellation));
        Assert.True(await fixture.Login.CompleteInteractiveAsync(state, start.BrowserSecret, "10001", "0", cancellation));
        Assert.False(await fixture.Login.CompleteInteractiveAsync(state, start.BrowserSecret, "10002", "0", cancellation));
        Assert.Null(await fixture.Login.FindAuthenticatedSessionAsync(state, start.BrowserSecret, "10002", "0", cancellation));

        var id = await fixture.Login.FindAuthenticatedSessionAsync(state, start.BrowserSecret, "10001", "0", cancellation);
        Assert.NotNull(id);
        Assert.True(await fixture.Sessions.BindAuthorizationAsync(new(id.Value, "10001", "0", "auth-web"), cancellation));
        var nonce = parameters["nonce"].ToString();
        var challenge = parameters["code_challenge"].ToString();
        fixture.Backchannel.Setup(client => client.ExchangeAsync("auth-code", It.IsAny<string>(), nonce,
                It.Is<WebAuthSession>(session => session.Id == id), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string verifier, string _, WebAuthSession _, CancellationToken _) =>
            {
                Assert.Equal(challenge, WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))));
                return fixture.Success("initial-access", InitialRefresh);
            });

        var result = await fixture.Login.CompleteAsync(state, start.BrowserSecret, "auth-code", cancellation);
        Assert.Equal(WebTokenBackchannelStatus.Success, result.Status);
        Assert.Equal("/forum", result.ReturnPath);
        Assert.Equal(fixture.Clock.Now.AddDays(30), result.ExpiresAt);
        Assert.NotNull(result.Cookie);
        var saved = await fixture.Store.FindByCookieAsync(WebSessionSecrets.Digest(result.Cookie), cancellation);
        Assert.NotNull(saved);
        Assert.NotEqual(result.Cookie, saved.CookieDigest);
        Assert.NotNull(saved.ProtectedTokens);
        Assert.DoesNotContain(InitialRefresh, saved.ProtectedTokens, StringComparison.Ordinal);
        Assert.Equal(InitialRefresh, fixture.Protection.ReadTokens<WebSessionTokens>(id.Value, saved.ProtectedTokens).RefreshToken);
        Assert.Equal(WebTokenBackchannelStatus.Rejected,
            (await fixture.Login.CompleteAsync(state, start.BrowserSecret, "auth-code", cancellation)).Status);
        fixture.Backchannel.Verify(client => client.ExchangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<WebAuthSession>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TemporaryFailureKeepsSessionAndRetryPersistsRotationBeforeReturningBearer()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellation);
        var browser = await fixture.CreateSessionAsync(cancellation);
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(10);
        fixture.Backchannel.SetupSequence(client => client.RefreshAsync(InitialRefresh, It.IsAny<Guid>(),
                It.IsAny<WebAuthSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebTokenBackchannelResult(WebTokenBackchannelStatus.Unavailable))
            .ReturnsAsync(fixture.Success("rotated-access", "rotated-refresh"));

        Assert.Equal(WebSessionResultStatus.Retry, (await fixture.Access.GetTokenAsync(browser.Cookie, cancellation)).Status);
        Assert.True(await fixture.Access.IsAuthenticatedAsync(browser.Cookie, cancellation));
        Assert.Equal(WebSessionResultStatus.Retry, (await fixture.Access.GetTokenAsync(browser.Cookie, cancellation)).Status);
        fixture.Backchannel.Verify(client => client.RefreshAsync(InitialRefresh, It.IsAny<Guid>(), It.IsAny<WebAuthSession>(),
            It.IsAny<CancellationToken>()), Times.Once);

        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(20);
        Assert.Equal("rotated-access", (await fixture.Access.GetTokenAsync(browser.Cookie, cancellation)).AccessToken);
        var restartedAccess = fixture.CreateAccess();
        Assert.Equal("rotated-access", (await restartedAccess.GetTokenAsync(browser.Cookie, cancellation)).AccessToken);
        fixture.Backchannel.Verify(client => client.RefreshAsync(InitialRefresh, It.IsAny<Guid>(), It.IsAny<WebAuthSession>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));

        var saved = await fixture.Repository.FindAsync(browser.Id, cancellation);
        Assert.NotNull(saved);
        Assert.Equal("rotated-refresh", fixture.Protection.ReadTokens<WebSessionTokens>(browser.Id, saved.ProtectedTokens!).RefreshToken);
        Assert.Equal(fixture.Clock.Now.AddMinutes(-10).AddSeconds(-20).UtcDateTime, saved.LastActiveAtUtc);
    }

    [Fact]
    public async Task InFlightRefreshRejectsContendersAndCannotUndoLogout()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellation);
        var browser = await fixture.CreateSessionAsync(cancellation);
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(10);
        var received = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<WebTokenBackchannelResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Backchannel.Setup(client => client.RefreshAsync(InitialRefresh, It.IsAny<Guid>(), It.IsAny<WebAuthSession>(),
                CancellationToken.None))
            .Returns((string _, Guid lease, WebAuthSession _, CancellationToken _) =>
            {
                received.SetResult(lease);
                return release.Task;
            });
        var refresh = fixture.Access.GetTokenAsync(browser.Cookie, cancellation);
        var currentLease = await received.Task.WaitAsync(cancellation);
        try
        {
            var binding = new WebAuthSessionBinding(browser.Id, "10001", "0", "auth-web");
            Assert.Equal(WebAuthSessionGrantStatus.Allowed,
                (await fixture.Sessions.EvaluateGrantAsync(binding, true, cancellation, currentLease)).Status);
            Assert.Equal(WebAuthSessionGrantStatus.Invalid,
                (await fixture.Sessions.EvaluateGrantAsync(binding, true, cancellation, Guid.NewGuid())).Status);
            Assert.Equal(WebSessionResultStatus.Retry, (await fixture.CreateAccess().GetTokenAsync(browser.Cookie, cancellation)).Status);
            Assert.Equal(WebSessionResultStatus.Success, await fixture.Access.LogoutAsync(browser.Cookie, cancellation));
        }
        finally
        {
            release.TrySetResult(fixture.Success("late-access", "late-refresh"));
        }

        Assert.Equal(WebSessionResultStatus.SignedOut, (await refresh).Status);
        var revoked = await fixture.Repository.FindAsync(browser.Id, cancellation);
        Assert.NotNull(revoked);
        Assert.NotNull(revoked.RevokedAtUtc);
        Assert.Null(revoked.ProtectedTokens);
        Assert.Null(revoked.RefreshLeaseId);
        fixture.Authorizations.Verify(manager => manager.TryRevokeAsync(fixture.Authorization, cancellation), Times.Once);
    }

    [Fact]
    public async Task StaleLeaseCannotPublishAfterReacquisitionAndRejectedGrantClearsCredentials()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellation);
        var browser = await fixture.CreateSessionAsync(cancellation);
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(10);
        var old = Guid.NewGuid();
        var snapshot = (await fixture.Repository.FindAsync(browser.Id, cancellation))!;
        var now = fixture.Clock.Now.UtcDateTime;
        Assert.True(await fixture.Store.AcquireRefreshLeaseAsync(snapshot, old, now, now.AddSeconds(20), cancellation));
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(20);
        Assert.False(await fixture.Store.CompleteRefreshAsync(browser.Id, old, "stale-tokens", now.AddMinutes(5), fixture.Clock.Now.UtcDateTime, cancellation));

        fixture.Backchannel.Setup(client => client.RefreshAsync(InitialRefresh, It.Is<Guid>(lease => lease != old),
                It.IsAny<WebAuthSession>(), CancellationToken.None))
            .ReturnsAsync(new WebTokenBackchannelResult(WebTokenBackchannelStatus.Rejected));
        Assert.Equal(WebSessionResultStatus.SignedOut, (await fixture.Access.GetTokenAsync(browser.Cookie, cancellation)).Status);
        Assert.False(await fixture.Access.IsAuthenticatedAsync(browser.Cookie, cancellation));
        Assert.Null((await fixture.Repository.FindAsync(browser.Id, cancellation))!.ProtectedTokens);
    }

    [Fact]
    public async Task UnavailableProtectionKeyPreservesTheSessionForRecovery()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellation);
        var browser = await fixture.CreateSessionAsync(cancellation);
        var wrongKeys = new WebSessionProtection(() => new EphemeralDataProtectionProvider());

        Assert.Equal(WebSessionResultStatus.Retry, (await fixture.CreateAccess(wrongKeys).GetTokenAsync(browser.Cookie, cancellation)).Status);
        Assert.True(await fixture.Access.IsAuthenticatedAsync(browser.Cookie, cancellation));
        Assert.Equal("initial-access", (await fixture.Access.GetTokenAsync(browser.Cookie, cancellation)).AccessToken);
    }

    [Fact]
    public async Task AuthManagerFailureDuringLogoutKeepsRevocationEffectiveAndAllowsRetry()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellation);
        var browser = await fixture.CreateSessionAsync(cancellation);
        fixture.Authorizations.SetupSequence(manager => manager.TryRevokeAsync(fixture.Authorization, cancellation))
            .ReturnsAsync(false).ReturnsAsync(true);

        Assert.Equal(WebSessionResultStatus.Retry, await fixture.Access.LogoutAsync(browser.Cookie, cancellation));
        Assert.Equal(WebSessionResultStatus.SignedOut, (await fixture.Access.GetTokenAsync(browser.Cookie, cancellation)).Status);
        Assert.Equal(WebSessionResultStatus.Success, await fixture.CreateAccess().LogoutAsync(browser.Cookie, cancellation));
        Assert.Null((await fixture.Repository.FindAsync(browser.Id, cancellation))!.ProtectedTokens);
    }

    [Fact]
    public async Task BrowserDisconnectDoesNotDiscardAnInFlightRotation()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellation);
        var browser = await fixture.CreateSessionAsync(cancellation);
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(10);
        using var browserRequest = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        fixture.Backchannel.Setup(client => client.RefreshAsync(InitialRefresh, It.IsAny<Guid>(),
            It.IsAny<WebAuthSession>(), CancellationToken.None)).ReturnsAsync(() =>
        {
            browserRequest.Cancel();
            return fixture.Success("reopened-access", "reopened-refresh");
        });

        await fixture.Access.GetTokenAsync(browser.Cookie, browserRequest.Token);

        Assert.Equal("reopened-access", (await fixture.CreateAccess().GetTokenAsync(browser.Cookie, cancellation)).AccessToken);
        fixture.Backchannel.Verify(client => client.RefreshAsync(InitialRefresh, It.IsAny<Guid>(),
            It.IsAny<WebAuthSession>(), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task FailedRotationWriteKeepsOldCredentialsAndLetsTheNextOwnerRecover()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellation);
        var browser = await fixture.CreateSessionAsync(cancellation);
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(10);
        fixture.Backchannel.SetupSequence(client => client.RefreshAsync(InitialRefresh, It.IsAny<Guid>(),
                It.IsAny<WebAuthSession>(), CancellationToken.None))
            .ReturnsAsync(fixture.Success("not-persisted", "not-persisted-refresh"))
            .ReturnsAsync(fixture.Success("recovered-access", "recovered-refresh"));
        var digest = WebSessionSecrets.Digest(browser.Cookie);
        var failingStore = new Mock<IWebSessionStore>(MockBehavior.Strict);
        failingStore.Setup(store => store.FindByCookieAsync(digest, cancellation))
            .Returns(() => fixture.Store.FindByCookieAsync(digest, cancellation));
        failingStore.Setup(store => store.AcquireRefreshLeaseAsync(It.IsAny<WebAuthSession>(), It.IsAny<Guid>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), cancellation))
            .Returns((WebAuthSession session, Guid lease, DateTime now, DateTime until, CancellationToken token) =>
                fixture.Store.AcquireRefreshLeaseAsync(session, lease, now, until, token));
        failingStore.Setup(store => store.CompleteRefreshAsync(browser.Id, It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), CancellationToken.None))
            .ThrowsAsync(new DbUpdateException("Test write failure."));

        var interrupted = await fixture.CreateAccess(store: failingStore.Object).GetTokenAsync(browser.Cookie, cancellation);

        Assert.Equal(WebSessionResultStatus.Retry, interrupted.Status);
        Assert.Null(interrupted.AccessToken);
        Assert.Equal(WebSessionResultStatus.Retry, (await fixture.Access.GetTokenAsync(browser.Cookie, cancellation)).Status);
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(20);
        Assert.Equal("recovered-access", (await fixture.Access.GetTokenAsync(browser.Cookie, cancellation)).AccessToken);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly AuthOpenIddictDbContext _db;
        private readonly IConfiguration _configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenIddict:WebSession:Enabled"] = "true",
            ["OpenIddict:Server:Issuer"] = "https://radish.test:35443/"
        }).Build();

        private Fixture(SqliteConnection connection)
        {
            _connection = connection;
            _db = new AuthOpenIddictDbContext(new DbContextOptionsBuilder<AuthOpenIddictDbContext>().UseSqlite(connection).Options);
            Store = new(_db);
            Repository = new(_db);
            Sessions = new(Repository, Clock);
            Login = new(Store, Repository, Backchannel.Object, Protection, _configuration, Clock);
            Authorizations.Setup(manager => manager.FindByIdAsync("auth-web", It.IsAny<CancellationToken>())).ReturnsAsync(Authorization);
            Authorizations.Setup(manager => manager.TryRevokeAsync(Authorization, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        }

        public TestClock Clock { get; } = new();
        public WebSessionStore Store { get; }
        public WebAuthSessionRepository Repository { get; }
        public WebAuthSessionService Sessions { get; }
        public WebSessionLoginService Login { get; }
        public Mock<IWebTokenBackchannel> Backchannel { get; } = new(MockBehavior.Strict);
        public WebSessionProtection Protection { get; } = new(() => new EphemeralDataProtectionProvider());
        public Mock<IOpenIddictAuthorizationManager> Authorizations { get; } = new();
        public object Authorization { get; } = new();
        public WebSessionAccessService Access => CreateAccess();

        public static async Task<Fixture> CreateAsync(CancellationToken cancellationToken)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            var fixture = new Fixture(connection);
            try
            {
                await connection.OpenAsync(cancellationToken);
                await fixture._db.Database.EnsureCreatedAsync(cancellationToken);
                return fixture;
            }
            catch
            {
                await fixture.DisposeAsync();
                throw;
            }
        }

        public WebSessionAccessService CreateAccess(WebSessionProtection? protection = null, IWebSessionStore? store = null) =>
            new(store ?? Store, Sessions, Backchannel.Object, protection ?? Protection, Authorizations.Object, _configuration, Clock);

        public WebSessionController CreateController(string cookie)
        {
            var http = new DefaultHttpContext();
            var issuer = WebAuthClient.GetIssuer(_configuration);
            http.Request.Scheme = issuer.Scheme;
            http.Request.Host = new HostString(issuer.Host, issuer.Port);
            http.Request.Headers.Origin = issuer.GetLeftPart(UriPartial.Authority);
            http.Request.Headers.Cookie = $"{WebSessionCookieNames.Session}={cookie}";
            http.Request.Headers["X-Radish-Web-Session"] = WebSessionSecrets.Proof(cookie);
            return new(Login, Access, _configuration) { ControllerContext = new() { HttpContext = http } };
        }

        public WebTokenBackchannelResult Success(string bearer, string refresh) =>
            new(WebTokenBackchannelStatus.Success, new(bearer, refresh, Clock.Now.AddMinutes(5)));

        public async Task<(Guid Id, string Cookie)> CreateSessionAsync(CancellationToken cancellationToken)
        {
            var id = await Sessions.CreatePendingAsync("10001", "0", cancellationToken);
            Assert.True(await Sessions.BindAuthorizationAsync(new(id, "10001", "0", "auth-web"), cancellationToken));
            var session = (await Repository.FindAsync(id, cancellationToken))!;
            var cookie = WebSessionSecrets.NewSecret();
            var issued = Success("initial-access", InitialRefresh).Tokens!;
            Assert.True(await Store.ActivateAsync(session, WebSessionSecrets.Digest(cookie), Protection.ProtectTokens(id, issued),
                issued.AccessExpiresAt.UtcDateTime, Clock.Now.UtcDateTime, cancellationToken));
            return (id, cookie);
        }

        public async ValueTask DisposeAsync()
        {
            await _db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
