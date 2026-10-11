using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Radish.Api.Security;
using Radish.Common.HttpContextTool;
using Serilog;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

/// <summary>只构造 DI 与内存 HTTP context；没有宿主、监听器、发现请求或持久密钥。</summary>
internal sealed class AuthenticationFixture : IDisposable
{
    internal const string Issuer = "https://issuer.example.invalid";
    internal const string CookieScheme = "Cookies";
    internal readonly SymmetricSecurityKey Key = new(RandomNumberGenerator.GetBytes(32));
    internal readonly ServiceProvider Services;
    private readonly List<IServiceScope> _scopes = [];
    private readonly Serilog.ILogger _previous;
    private readonly HttpBoundaryCapture _capture;

    internal AuthenticationFixture(HttpBoundaryCapture capture, Action<JwtBearerOptions>? jwt = null,
        Action<CookieAuthenticationOptions>? cookie = null, Action<IServiceCollection>? configure = null)
    {
        _capture = capture;
        _previous = Log.Logger;
        Log.Logger = capture.Logger;
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.ClearProviders().AddSerilog(capture.Logger, dispose: false));
        services.AddRouting();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = ApiJwtValidationPolicy.Create(Issuer, Key);
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(new() { Issuer = Issuer });
                options.Events = new ApiJwtBearerEvents();
                // 测试明确保留协议 claim 名；生产身份标准化由既有身份专题验收。
                options.MapInboundClaims = false;
                jwt?.Invoke(options);
            })
            .AddCookie(CookieScheme, options =>
            {
                options.Cookie.Name = "Radish.Test.Session";
                options.LoginPath = "/Account/Login";
                options.AccessDeniedPath = "/Account/Denied";
                cookie?.Invoke(options);
            })
            .AddCookie(HangfireDashboardAuthentication.Scheme, options => HangfireDashboardAuthentication.Configure(options, "/hangfire"));
        services.AddAuthorization(options => options.AddPolicy("admin", policy =>
            policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser().RequireRole("admin")));
        configure?.Invoke(services);
        Services = services.BuildServiceProvider();
    }

    internal DefaultHttpContext Context(string path = "/api/private")
    {
        var scope = Services.CreateScope();
        _scopes.Add(scope);
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Path = path;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("test.example.invalid");
        context.Request.QueryString = new QueryString("?private=" + HttpBoundaryCapture.Secret);
        context.TraceIdentifier = HttpBoundaryCapture.Secret;
        context.Response.Body = new MemoryStream();
        return context;
    }

    internal string Token(string role = "admin", bool expired = false) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = Issuer, Audience = UserScopes.RadishApi,
        Subject = new ClaimsIdentity(new[] { new Claim(UserClaimTypes.Sub, HttpBoundaryCapture.Secret), new Claim(UserClaimTypes.Role, role) }),
        SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256),
        IssuedAt = DateTime.UtcNow.AddMinutes(-10), NotBefore = DateTime.UtcNow.AddMinutes(-10),
        Expires = DateTime.UtcNow.AddMinutes(expired ? -1 : 10)
    });

    internal void AssertCodes(params string[] codes)
    {
        var expected = _capture.Candidate && _capture.Environment == "Production"
            ? codes.Where(c => c is "auth.jwt_failure_observed" or "auth.jwt_challenge_observed" or "auth.jwt_processing_failed")
            : codes;
        Assert.Equal(expected, _capture.Codes);
        AuthenticationFrameworkLoggingTests.AssertSafe(_capture);
    }

    public void Dispose()
    {
        foreach (var scope in _scopes) scope.Dispose();
        Services.Dispose();
        Log.Logger = _previous;
    }
}
