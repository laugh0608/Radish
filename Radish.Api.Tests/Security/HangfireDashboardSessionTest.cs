using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Api.Filters;
using Radish.Api.Security;
using Radish.Api.Services;
using Radish.Common.HttpContextTool;
using Radish.Common.PermissionTool;
using Radish.IService;
using Xunit;

namespace Radish.Api.Tests.Security;

public sealed class HangfireDashboardSessionTest
{
    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("9223372036854775807")]
    [InlineData("-9223372036854775808")]
    public void TokenExpiry_ShouldRejectMissingOrInvalidNumericDate(string? expiration)
    {
        var identity = new ClaimsIdentity(JwtBearerDefaults.AuthenticationScheme);
        if (expiration != null) identity.AddClaim(new Claim("exp", expiration));
        Assert.Null(UserClaimReader.GetTokenExpiresAtUtc(new ClaimsPrincipal(identity)));
    }

    [Fact]
    public async Task Session_ShouldBeProtectedScopedAndNeverAuthenticateOrdinaryApi()
    {
        var clock = new TestClock();
        using var provider = CreateProvider(clock, out _);
        using var scope = provider.CreateScope();
        var context = Context(scope.ServiceProvider);
        context.User = User(clock.GetUtcNow().AddMinutes(30));
        var session = await SessionService(context, clock).CreateAsync();
        Assert.NotNull(session);
        Assert.Equal(clock.GetUtcNow().AddMinutes(5).UtcDateTime, session.VoExpiresAtUtc);
        Assert.Equal("/hangfire", session.VoDashboardPath);
        var header = Assert.Single(context.Response.Headers.SetCookie);
        Assert.Contains("httponly", header!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", header!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", header!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/hangfire", header!, StringComparison.OrdinalIgnoreCase);
        var cookie = header!.Split(';')[0];

        using var dashboardScope = provider.CreateScope();
        var dashboard = Context(dashboardScope.ServiceProvider, cookie);
        Assert.True((await dashboard.AuthenticateAsync(HangfireDashboardAuthentication.Scheme)).Succeeded);
        Assert.True(new HangfireAuthorizationFilter().Authorize(
            new AspNetCoreDashboardContext(Mock.Of<JobStorage>(), new DashboardOptions(), dashboard)));

        using var apiScope = provider.CreateScope();
        var api = Context(apiScope.ServiceProvider, cookie);
        api.Request.Path = "/api/v1/Post/GetList";
        Assert.False((await api.AuthenticateAsync()).Succeeded);

        clock.UtcNow = clock.UtcNow.AddMinutes(6);
        using var expiredScope = provider.CreateScope();
        Assert.False((await Context(expiredScope.ServiceProvider, cookie)
            .AuthenticateAsync(HangfireDashboardAuthentication.Scheme)).Succeeded);
    }

    [Fact]
    public async Task Session_ShouldNotOutliveBearer_AndRejectExpiredOrAnonymousIdentity()
    {
        var clock = new TestClock();
        using var provider = CreateProvider(clock, out _);
        using var scope = provider.CreateScope();
        var context = Context(scope.ServiceProvider);
        var service = SessionService(context, clock);
        Assert.Null(await service.CreateAsync());
        context.User = User(clock.GetUtcNow().AddMinutes(-1));
        Assert.Null(await service.CreateAsync());
        context.User = User(clock.GetUtcNow().AddSeconds(60));
        var session = await service.CreateAsync();
        Assert.Equal(clock.GetUtcNow().AddSeconds(60).UtcDateTime, session!.VoExpiresAtUtc);
    }

    [Fact]
    public async Task Dashboard_ShouldRecheckPermission_AndRejectLocalAnonymousRequests()
    {
        var clock = new TestClock();
        using var provider = CreateProvider(clock, out var authorization);
        using var scope = provider.CreateScope();
        var context = Context(scope.ServiceProvider);
        context.User = User(clock.GetUtcNow().AddMinutes(30));
        await SessionService(context, clock).CreateAsync();
        var cookie = context.Response.Headers.SetCookie[0]!.Split(';')[0];
        authorization.Setup(service => service.GetPermissionKeysByRolesAsync(It.IsAny<IReadOnlyCollection<string>>()))
            .ReturnsAsync(new List<string>());
        using var deniedScope = provider.CreateScope();
        var denied = Context(deniedScope.ServiceProvider, cookie);
        Assert.False(new HangfireAuthorizationFilter().Authorize(
            new AspNetCoreDashboardContext(Mock.Of<JobStorage>(), new DashboardOptions(), denied)));
        using var anonymousScope = provider.CreateScope();
        var anonymous = Context(anonymousScope.ServiceProvider);
        anonymous.Connection.RemoteIpAddress = IPAddress.Loopback;
        anonymous.Connection.LocalIpAddress = IPAddress.Loopback;
        Assert.False(new HangfireAuthorizationFilter().Authorize(
            new AspNetCoreDashboardContext(Mock.Of<JobStorage>(), new DashboardOptions(), anonymous)));
    }

    private static ServiceProvider CreateProvider(TestClock clock, out Mock<IConsoleAuthorizationService> authorization)
    {
        authorization = new Mock<IConsoleAuthorizationService>();
        authorization.Setup(service => service.GetPermissionKeysByRolesAsync(It.IsAny<IReadOnlyCollection<string>>()))
            .ReturnsAsync(new List<string> { ConsolePermissions.HangfireView });
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddSingleton<IClaimsPrincipalNormalizer, ClaimsPrincipalNormalizer>();
        services.AddSingleton(authorization.Object);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer()
            .AddCookie(HangfireDashboardAuthentication.Scheme, options =>
            {
                HangfireDashboardAuthentication.Configure(options, "/hangfire");
                options.TimeProvider = clock;
            });
        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext Context(IServiceProvider services, string? cookie = null)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("radish.example.com");
        context.Request.Path = "/hangfire";
        if (cookie != null) context.Request.Headers.Cookie = cookie;
        return context;
    }

    private static ClaimsPrincipal User(DateTimeOffset expires) => new(new ClaimsIdentity(new[]
    {
        new Claim("sub", "1001"), new Claim("role", "Operator"),
        new Claim("exp", expires.ToUnixTimeSeconds().ToString())
    }, JwtBearerDefaults.AuthenticationScheme));

    private static HangfireDashboardSessionService SessionService(HttpContext context, TimeProvider clock) => new(
        new HttpContextAccessor { HttpContext = context },
        context.RequestServices.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>(), clock);

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.FromUnixTimeSeconds(1789800000);
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
