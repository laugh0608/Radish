using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Radish.Auth.Controllers;
using Radish.Auth.OpenIddict;
using Radish.Auth.WebSessions;
using Radish.Common.HttpContextTool;
using Radish.Gateway;
using Xunit;
using Yarp.ReverseProxy.Configuration;

namespace Radish.Api.Tests.Auth;

public sealed class WebSessionControllerTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallbackClearsOnlyItsOwnBindingAndReturnsTheLocalRedirect(bool startedBeforeUpgrade)
    {
        var fixture = new Fixture();
        var request = fixture.Controller.Request;
        var state = WebSessionSecrets.NewSecret();
        var other = WebSessionCookieNames.LoginPrefix + WebSessionSecrets.Digest(WebSessionSecrets.NewSecret());
        var name = startedBeforeUpgrade ? WebSessionCookieNames.Login : WebSessionCookieNames.LoginPrefix + WebSessionSecrets.Digest(state);
        var cookiesIn = $"{other}={WebSessionSecrets.NewSecret()}; {name}={fixture.Cookie}";
        if (!startedBeforeUpgrade) cookiesIn += $"; {WebSessionCookieNames.Login}={WebSessionSecrets.NewSecret()}";
        request.Headers.Cookie = cookiesIn;
        var newCookie = WebSessionSecrets.NewSecret();
        var expiry = new DateTimeOffset(2026, 12, 21, 12, 0, 0, TimeSpan.Zero);
        fixture.Login.Setup(login => login.CompleteAsync(state, fixture.Cookie, "code", CancellationToken.None))
            .ReturnsAsync(new WebLoginResult(WebTokenBackchannelStatus.Success, newCookie, expiry, "/forum"));

        var result = Assert.IsType<LocalRedirectResult>(await fixture.Controller.Callback(state, "code", CancellationToken.None));
        Assert.Equal("/forum", result.Url);
        var cookies = fixture.Controller.Response.Headers.SetCookie;
        Assert.Equal(2, cookies.Count);
        Assert.Contains(cookies, value => value?.StartsWith(name + "=;", StringComparison.Ordinal) == true);
        var session = Assert.Single(cookies, value => value?.StartsWith(WebSessionCookieNames.Session + "=", StringComparison.Ordinal) == true)!;
        Assert.StartsWith(WebSessionCookieNames.Session + "=" + newCookie, session, StringComparison.Ordinal);
        Assert.Contains("secure", session, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", session, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", session, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", session, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", session, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("no-store", fixture.Controller.Response.Headers.CacheControl.ToString());
        Assert.Equal("no-referrer", fixture.Controller.Response.Headers["Referrer-Policy"].ToString());
    }

    [Theory]
    [InlineData("https://sibling.radish.test:35443", true)]
    [InlineData("https://radish.test:35443", false)]
    [InlineData(null, true)]
    public async Task WritesRejectCrossOriginAndUnboundCsrfProof(string? origin, bool validProof)
    {
        var fixture = new Fixture();
        fixture.CarrySession(origin, validProof ? WebSessionSecrets.Proof(fixture.Cookie) : WebSessionSecrets.NewSecret());

        var ct = TestContext.Current.CancellationToken;
        var results = new[]
        {
            await fixture.Controller.Token(ct),
            await fixture.Controller.Activity(ct),
            await fixture.Controller.Logout(ct)
        };
        Assert.All(results, result =>
            Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode));
        Assert.False(fixture.Controller.Response.Headers.ContainsKey("Set-Cookie"));
        fixture.Access.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TemporaryRefreshFailureDoesNotDeleteTheCookie()
    {
        var fixture = new Fixture();
        fixture.CarrySession("https://radish.test:35443", WebSessionSecrets.Proof(fixture.Cookie));
        fixture.Access.Setup(service => service.GetTokenAsync(fixture.Cookie, CancellationToken.None))
            .ReturnsAsync(new WebAccessTokenResult(WebSessionResultStatus.Retry));

        var response = Assert.IsType<StatusCodeResult>(await fixture.Controller.Token(CancellationToken.None));
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, response.StatusCode);
        Assert.Equal(0, fixture.Controller.Response.Headers.SetCookie.Count);
        Assert.Equal("2", fixture.Controller.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public async Task LogoutEmitsTheExactRegisteredReturnAddress()
    {
        var fixture = new Fixture();
        fixture.CarrySession("https://radish.test:35443", WebSessionSecrets.Proof(fixture.Cookie));
        fixture.Access.Setup(service => service.LogoutAsync(fixture.Cookie, CancellationToken.None))
            .ReturnsAsync(WebSessionResultStatus.Success);

        var result = Assert.IsType<OkObjectResult>(await fixture.Controller.Logout(CancellationToken.None));
        var target = new Uri(Assert.IsType<WebSessionLogoutVo>(result.Value).VoEndSessionUrl);
        var query = QueryHelpers.ParseQuery(target.Query);
        var registration = WebAuthClient.CreateRegistration(fixture.Configuration)!;
        var registered = Assert.Single(registration.PostLogoutRedirectUris).OriginalString;
        Assert.Equal("/connect/endsession", target.AbsolutePath);
        Assert.Equal(WebAuthClient.ClientId, Assert.Single(query["client_id"]));
        Assert.Equal(registered, Assert.Single(query["post_logout_redirect_uri"]));
    }

    [Fact]
    public async Task CrossOriginStatusCannotReadTheCsrfProof()
    {
        var fixture = new Fixture();
        fixture.CarrySession(null, null);
        fixture.Controller.Request.Headers["Sec-Fetch-Site"] = "same-site";

        var response = Assert.IsType<StatusCodeResult>(await fixture.Controller.Status(CancellationToken.None));
        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        fixture.Access.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false, 404, "legacy")]
    [InlineData(true, 401, "cookie")]
    public async Task AnonymousStatusDistinguishesOptOutFromMissingCookie(bool enabled, int status, string mode)
    {
        var fixture = new Fixture(enabled);
        var response = Assert.IsAssignableFrom<StatusCodeResult>(await fixture.Controller.Status(CancellationToken.None));
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(mode, fixture.Controller.Response.Headers["X-Radish-Web-Session-Mode"].ToString());
        fixture.Access.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true, "/connect/authorize", false, true)]
    [InlineData(true, "/Account/Login", false, true)]
    [InlineData(true, "/web/session/callback", true, true)]
    [InlineData(true, "/web/session/status", true, false)]
    [InlineData(false, "/web/session/callback", false, false)]
    [InlineData(true, "/connect/token", false, false)]
    [InlineData(false, "/connect/authorize", false, false)]
    [InlineData(false, "/console/", false, false)]
    public void ProxyForwardsOnlyCookiesForTheDestination(bool toAuth, string path, bool allowSession, bool allowLogin)
    {
        const string session = "__Host-Radish-Web-Session=web";
        const string bindings = "__Host-Radish-Web-Login=login; __Host-Radish-Web-Login.A=a; __Host-Radish-Web-Login.B=b";
        const string unrelated = ".Radish.Auth.Session=legacy";
        var expected = new List<string>();
        if (allowSession) expected.Add(session);
        if (allowLogin) expected.Add(bindings);
        expected.Add(unrelated);
        Assert.Equal(string.Join("; ", expected), WebSessionProxy.FilterCookies([session, bindings, unrelated], toAuth, new PathString(path)));
    }

    [Fact]
    public void GatewayLoadsWebSessionRouteAlongsideTheExistingAuthCluster()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddReverseProxy().LoadFromMemory([], [new ClusterConfig { ClusterId = "authCluster" }]).AddWebSessionRouting();
        using var provider = services.BuildServiceProvider();
        var sources = provider.GetServices<IProxyConfigProvider>();
        Assert.Contains(sources, source => source.GetConfig().Routes.Any(route =>
            route is { ClusterId: "authCluster", Match.Path: "/web/session/{**catch-all}" }));
        Assert.Contains(sources, source => source.GetConfig().Routes.Any(route =>
            route is { ClusterId: "apiCluster", Match.Path: "/hub/web/{**catch-all}" }));
    }

    private sealed class Fixture
    {
        public readonly Mock<IWebSessionLoginService> Login = new(MockBehavior.Strict);
        public readonly Mock<IWebSessionAccessService> Access = new(MockBehavior.Strict);
        public readonly string Cookie = WebSessionSecrets.NewSecret();
        public WebSessionController Controller { get; }
        public IConfiguration Configuration { get; }

        public Fixture(bool enabled = true)
        {
            Configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenIddict:WebSession:Enabled"] = enabled.ToString(),
                ["OpenIddict:WebSession:ClientSecret"] = "test-only-confidential-client-secret",
                ["OpenIddict:Server:Issuer"] = "https://radish.test:35443/"
            }).Build();
            var context = new DefaultHttpContext();
            context.Request.Scheme = "https";
            context.Request.Host = new HostString("radish.test", 35443);
            Controller = new(Login.Object, Access.Object, Configuration) { ControllerContext = new() { HttpContext = context } };
            var url = new Mock<IUrlHelper>();
            url.Setup(helper => helper.IsLocalUrl("/forum")).Returns(true);
            Controller.Url = url.Object;
        }

        public void CarrySession(string? origin, string? proof)
        {
            var request = Controller.Request;
            request.Headers.Cookie = $"{WebSessionCookieNames.Session}={Cookie}";
            if (origin is not null) request.Headers.Origin = origin;
            if (proof is not null) request.Headers["X-Radish-Web-Session"] = proof;
        }
    }
}
