using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Radish.Auth.Controllers;
using Radish.Auth.OpenIddict;
using Radish.Auth.WebSessions;
using Radish.Common.HttpContextTool;
using Xunit;

namespace Radish.Api.Tests.Auth;

public sealed partial class WebSessionPersistenceTest
{
    [Theory]
    [InlineData(2, 9, WebAuthSessionGrantStatus.Allowed)]
    [InlineData(24, 30, WebAuthSessionGrantStatus.AbsoluteExpired)]
    public async Task OneSecondBeforeDeadline_AllowsRefreshWithoutCountingItAsActivity(
        int lastActivityDay, int deadlineDay, WebAuthSessionGrantStatus afterActivity)
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellation);
        var created = fixture.Clock.Now;
        var deadline = created.AddDays(deadlineDay);
        var browser = await CreateSessionWithActivityAsync(fixture, lastActivityDay, cancellation);
        var binding = new WebAuthSessionBinding(browser.Id, "10001", "0", "auth-web");
        fixture.Clock.Now = deadline.AddSeconds(-1);
        fixture.Backchannel.Setup(client => client.RefreshAsync(InitialRefresh, It.IsAny<Guid>(), It.IsAny<WebAuthSession>(),
                CancellationToken.None))
            .Returns(async (string _, Guid lease, WebAuthSession _, CancellationToken token) =>
            {
                var grant = await fixture.Sessions.EvaluateGrantAsync(binding, true, token, lease);
                Assert.Equal(WebAuthSessionGrantStatus.Allowed, grant.Status);
                Assert.Equal(deadline, grant.AccessTokenExpiresAt);
                Assert.Equal(created.AddDays(30), grant.RefreshTokenExpiresAt);
                return new WebTokenBackchannelResult(WebTokenBackchannelStatus.Success,
                    new("renewed-access", "renewed-refresh", grant.AccessTokenExpiresAt));
            });

        Assert.IsType<OkObjectResult>(await fixture.CreateController(browser.Cookie).Status(cancellation));
        var result = Assert.IsType<OkObjectResult>(await fixture.CreateController(browser.Cookie).Token(cancellation));
        Assert.Equal(deadline, Assert.IsType<WebSessionTokenVo>(result.Value).VoExpiresAt);
        var passive = await fixture.Repository.FindAsync(browser.Id, cancellation);
        Assert.NotNull(passive);
        Assert.Equal(created.AddDays(lastActivityDay).UtcDateTime, passive.LastActiveAtUtc);
        Assert.Equal(deadline.UtcDateTime, passive.AccessTokenExpiresAtUtc);

        Assert.IsType<NoContentResult>(await fixture.CreateController(browser.Cookie).Activity(cancellation));
        var active = await fixture.Repository.FindAsync(browser.Id, cancellation);
        Assert.NotNull(active);
        Assert.Equal(fixture.Clock.Now.UtcDateTime, active.LastActiveAtUtc);
        Assert.Equal(created.UtcDateTime, active.CreatedAtUtc);

        fixture.Clock.Now = deadline;
        Assert.Equal(afterActivity, (await fixture.Sessions.EvaluateGrantAsync(binding, true, cancellation)).Status);
    }

    [Theory]
    [InlineData(2, 9, 0, WebAuthSessionGrantStatus.IdleExpired)]
    [InlineData(2, 9, 1, WebAuthSessionGrantStatus.IdleExpired)]
    [InlineData(24, 30, 0, WebAuthSessionGrantStatus.AbsoluteExpired)]
    [InlineData(24, 30, 1, WebAuthSessionGrantStatus.AbsoluteExpired)]
    public async Task AtOrAfterDeadline_RejectsStatusTokenActivityAndOutstandingGrant(
        int lastActivityDay, int deadlineDay, int secondsAfter, WebAuthSessionGrantStatus expected)
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellation);
        var deadline = fixture.Clock.Now.AddDays(deadlineDay);
        var browser = await CreateSessionWithActivityAsync(fixture, lastActivityDay, cancellation);
        var binding = new WebAuthSessionBinding(browser.Id, "10001", "0", "auth-web");
        var snapshot = await fixture.Repository.FindAsync(browser.Id, cancellation);
        Assert.NotNull(snapshot);

        // A refresh that already holds a lease must still obey the session deadline.
        var lease = Guid.NewGuid();
        fixture.Clock.Now = deadline.AddSeconds(-1);
        Assert.True(await fixture.Store.AcquireRefreshLeaseAsync(snapshot, lease, fixture.Clock.Now.UtcDateTime,
            fixture.Clock.Now.AddSeconds(20).UtcDateTime, cancellation));
        fixture.Clock.Now = deadline.AddSeconds(secondsAfter);
        var grant = await fixture.Sessions.EvaluateGrantAsync(binding, true, cancellation, lease);
        Assert.Equal(expected, grant.Status);

        Assert.IsType<UnauthorizedResult>(await fixture.CreateController(browser.Cookie).Status(cancellation));
        Assert.IsType<UnauthorizedResult>(await fixture.CreateController(browser.Cookie).Activity(cancellation));
        var tokenRequest = fixture.CreateController(browser.Cookie);
        Assert.IsType<UnauthorizedResult>(await tokenRequest.Token(cancellation));
        Assert.StartsWith(WebSessionCookieNames.Session + "=;", Assert.Single(tokenRequest.Response.Headers.SetCookie),
            StringComparison.Ordinal);
        fixture.Backchannel.VerifyNoOtherCalls();
        var saved = await fixture.Repository.FindAsync(browser.Id, cancellation);
        Assert.NotNull(saved);
        Assert.Equal(snapshot.CreatedAtUtc, saved.CreatedAtUtc);
        Assert.Equal(snapshot.LastActiveAtUtc, saved.LastActiveAtUtc);
        Assert.Equal(snapshot.ProtectedTokens, saved.ProtectedTokens);
    }

    [Theory]
    [InlineData(2, 9, WebTokenBackchannelStatus.Success)]
    [InlineData(2, 9, WebTokenBackchannelStatus.Unavailable)]
    [InlineData(24, 30, WebTokenBackchannelStatus.Success)]
    [InlineData(24, 30, WebTokenBackchannelStatus.Unavailable)]
    public async Task RefreshReplyArrivingAtDeadlineImmediatelySignsOut(
        int lastActivityDay, int deadlineDay, WebTokenBackchannelStatus reply)
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellation);
        var deadline = fixture.Clock.Now.AddDays(deadlineDay);
        var browser = await CreateSessionWithActivityAsync(fixture, lastActivityDay, cancellation);
        var before = await fixture.Repository.FindAsync(browser.Id, cancellation);
        Assert.NotNull(before);
        fixture.Clock.Now = deadline.AddSeconds(-1);
        fixture.Backchannel.Setup(client => client.RefreshAsync(InitialRefresh, It.IsAny<Guid>(), It.IsAny<WebAuthSession>(),
                CancellationToken.None))
            .ReturnsAsync(() =>
            {
                fixture.Clock.Now = deadline;
                return reply == WebTokenBackchannelStatus.Success
                    ? new WebTokenBackchannelResult(reply, new("late-access", "late-refresh", deadline))
                    : new WebTokenBackchannelResult(reply);
            });

        var request = fixture.CreateController(browser.Cookie);
        Assert.IsType<UnauthorizedResult>(await request.Token(cancellation));
        Assert.StartsWith(WebSessionCookieNames.Session + "=;", Assert.Single(request.Response.Headers.SetCookie),
            StringComparison.Ordinal);
        Assert.Equal(0, request.Response.Headers.RetryAfter.Count);
        Assert.IsType<UnauthorizedResult>(await fixture.CreateController(browser.Cookie).Token(cancellation));
        var after = await fixture.Repository.FindAsync(browser.Id, cancellation);
        Assert.NotNull(after);
        Assert.Equal(before.ProtectedTokens, after.ProtectedTokens);
        Assert.Equal(before.AccessTokenExpiresAtUtc, after.AccessTokenExpiresAtUtc);
        Assert.Equal(before.LastActiveAtUtc, after.LastActiveAtUtc);
        fixture.Backchannel.Verify(client => client.RefreshAsync(InitialRefresh, It.IsAny<Guid>(), It.IsAny<WebAuthSession>(),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ActivityBeforeIdleDeadlineKeepsTemporaryRefreshFailureRetryable()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellation);
        var deadline = fixture.Clock.Now.AddDays(7);
        var browser = await fixture.CreateSessionAsync(cancellation);
        fixture.Clock.Now = deadline.AddSeconds(-2);
        fixture.Backchannel.Setup(client => client.RefreshAsync(InitialRefresh, It.IsAny<Guid>(), It.IsAny<WebAuthSession>(),
                CancellationToken.None))
            .Returns(async () =>
            {
                fixture.Clock.Now = deadline.AddSeconds(-1);
                Assert.IsType<NoContentResult>(await fixture.CreateController(browser.Cookie).Activity(cancellation));
                fixture.Clock.Now = deadline;
                return new WebTokenBackchannelResult(WebTokenBackchannelStatus.Unavailable);
            });

        var request = fixture.CreateController(browser.Cookie);
        var result = Assert.IsType<StatusCodeResult>(await request.Token(cancellation));
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("2", request.Response.Headers.RetryAfter.ToString());
        Assert.Equal(0, request.Response.Headers.SetCookie.Count);
        Assert.IsType<OkObjectResult>(await fixture.CreateController(browser.Cookie).Status(cancellation));
    }

    private static async Task<(Guid Id, string Cookie)> CreateSessionWithActivityAsync(
        Fixture fixture, int lastActivityDay, CancellationToken cancellation)
    {
        var created = fixture.Clock.Now;
        var browser = await fixture.CreateSessionAsync(cancellation);
        for (var day = 1; day <= lastActivityDay; day++)
        {
            fixture.Clock.Now = created.AddDays(day);
            Assert.Equal(WebSessionResultStatus.Success, await fixture.Access.RecordActivityAsync(browser.Cookie, cancellation));
        }

        return browser;
    }
}
