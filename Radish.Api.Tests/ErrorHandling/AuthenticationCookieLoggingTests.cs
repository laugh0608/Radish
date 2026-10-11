using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Radish.Api.Security;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class AuthenticationCookieLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task CookieTicket_ShouldKeepSignInAuthenticationAndSignOut(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var scope = capture.SensitiveScope();
        using var fixture = new AuthenticationFixture(capture);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, HttpBoundaryCapture.Secret) }, "Cookies"));
        var signIn = fixture.Context("/Account/Login");
        await signIn.SignInAsync(AuthenticationFixture.CookieScheme, principal, new AuthenticationProperties { IsPersistent = true });
        var cookie = Assert.Single(signIn.Response.Headers.SetCookie);
        Assert.Contains("httponly", cookie!);
        fixture.AssertCodes("auth.cookie_signed_in");
        capture.Clear();

        var authenticated = fixture.Context("/Account/Profile");
        authenticated.Request.Headers.Cookie = cookie!.Split(';')[0];
        var ticket = await authenticated.AuthenticateAsync(AuthenticationFixture.CookieScheme);
        Assert.True(ticket.Succeeded);
        Assert.Equal(HttpBoundaryCapture.Secret, ticket.Principal!.Identity!.Name);
        Assert.True(ticket.Properties!.IsPersistent);
        fixture.AssertCodes("auth.cookie_authenticated");
        capture.Clear();

        await authenticated.SignOutAsync(AuthenticationFixture.CookieScheme);
        Assert.Contains("expires=Thu, 01 Jan 1970", authenticated.Response.Headers.SetCookie.ToString());
        fixture.AssertCodes("auth.cookie_signed_out");
        capture.Clear();

        var absent = await fixture.Context().AuthenticateAsync(AuthenticationFixture.CookieScheme);
        Assert.True(absent.None);
        fixture.AssertCodes("auth.cookie_not_authenticated");
        capture.Clear();
        var invalid = fixture.Context();
        invalid.Request.Headers.Cookie = "Radish.Test.Session=" + HttpBoundaryCapture.Secret;
        Assert.NotNull((await invalid.AuthenticateAsync(AuthenticationFixture.CookieScheme)).Failure);
        fixture.AssertCodes("auth.cookie_authentication_failed");
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task CookieRejection_ShouldPreserveRedirectAndHangfireStatus(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var scope = capture.SensitiveScope();
        using var fixture = new AuthenticationFixture(capture);
        foreach (var hangfire in new[] { false, true })
        {
            var scheme = hangfire ? HangfireDashboardAuthentication.Scheme : AuthenticationFixture.CookieScheme;
            var challenge = fixture.Context("/private/" + HttpBoundaryCapture.Secret);
            await challenge.ChallengeAsync(scheme);
            Assert.Equal(hangfire ? 401 : 302, challenge.Response.StatusCode);
            if (hangfire) Assert.Empty(challenge.Response.Headers.Location.ToString());
            else
            {
                Assert.Contains("/Account/Login?ReturnUrl=", challenge.Response.Headers.Location.ToString());
                Assert.Contains(HttpBoundaryCapture.Secret, challenge.Response.Headers.Location.ToString());
            }
            fixture.AssertCodes("auth.cookie_challenged");
            capture.Clear();
            var forbidden = fixture.Context();
            await forbidden.ForbidAsync(scheme);
            Assert.Equal(hangfire ? 403 : 302, forbidden.Response.StatusCode);
            if (!hangfire) Assert.Contains("/Account/Denied?ReturnUrl=", forbidden.Response.Headers.Location.ToString());
            fixture.AssertCodes("auth.cookie_forbidden");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task CookieSignInCallbackFailure_ShouldPropagateWithoutSignedInEvent(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var scope = capture.SensitiveScope();
        var failure = new IOException(HttpBoundaryCapture.Secret);
        using var fixture = new AuthenticationFixture(capture, cookie: options => options.Events.OnSigningIn = _ => throw failure);
        var context = fixture.Context();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, HttpBoundaryCapture.Secret) }, "Cookies"));
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => context.SignInAsync(AuthenticationFixture.CookieScheme, principal)));
        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
        fixture.AssertCodes();
    }
}
