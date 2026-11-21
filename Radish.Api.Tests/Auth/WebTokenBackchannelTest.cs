using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using OpenIddict.Server;
using Radish.Auth.OpenIddict;
using Radish.Auth.WebSessions;
using Xunit;

namespace Radish.Api.Tests.Auth;

public sealed class WebTokenBackchannelTest
{
    private const string Issuer = "https://radish.test:35443/";
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly WebAuthSession Session = new() { Subject = "10001", TenantId = "0" };

    [Theory]
    [InlineData("expected-nonce", WebTokenBackchannelStatus.Success)]
    [InlineData("wrong-nonce", WebTokenBackchannelStatus.Rejected)]
    public async Task CodeExchangeValidatesIdentityAndUsesFixedHttpsBackchannel(string nonce, WebTokenBackchannelStatus expected)
    {
        using var rsa = RSA.Create(2048);
        var credentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
        using var client = new HttpClient(new FakeHttp(async (request, cancellation) =>
        {
            Assert.Equal(new Uri(Issuer + "connect/token"), request.RequestUri);
            var form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellation));
            Assert.Equal("radish-web", form["client_id"].ToString());
            Assert.Equal("test-server-secret", form["client_secret"].ToString());
            Assert.Equal("verifier", form["code_verifier"].ToString());
            Assert.Equal(Issuer + "web/session/callback", form["redirect_uri"].ToString());
            return Json(HttpStatusCode.OK, new
            {
                token_type = "Bearer",
                access_token = Sign("radish-api", credentials, new Claim("client_id", "radish-web"), new Claim("tenant_id", "0")),
                id_token = Sign("radish-web", credentials, new Claim("nonce", nonce)),
                refresh_token = "issued-refresh"
            });
        }));

        var outcome = await Create(client, credentials).ExchangeAsync("code", "verifier", "expected-nonce", Session,
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, outcome.Status);
        Assert.Equal(expected == WebTokenBackchannelStatus.Success ? "issued-refresh" : null, outcome.Tokens?.RefreshToken);
    }

    [Theory]
    [InlineData(400, "invalid_grant", WebTokenBackchannelStatus.Rejected)]
    [InlineData(400, "invalid_client", WebTokenBackchannelStatus.Unavailable)]
    [InlineData(503, "invalid_grant", WebTokenBackchannelStatus.Unavailable)]
    public async Task RefreshDistinguishesProtocolRejectionFromInfrastructureFailure(int httpStatus, string error,
        WebTokenBackchannelStatus expected)
    {
        var lease = Guid.NewGuid();
        using var rsa = RSA.Create(2048);
        var credentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
        using var client = new HttpClient(new FakeHttp(async (request, cancellation) =>
        {
            var form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellation));
            Assert.Equal("refresh_token", form["grant_type"].ToString());
            Assert.Equal(lease.ToString("N"), form[WebAuthClient.RefreshLeaseParameter].ToString());
            return Json((HttpStatusCode)httpStatus, new { error });
        }));

        var outcome = await Create(client, credentials).RefreshAsync("current-refresh", lease, Session, TestContext.Current.CancellationToken);

        Assert.Equal(expected, outcome.Status);
        Assert.Null(outcome.Tokens);
    }

    private static WebTokenBackchannel Create(HttpClient client, SigningCredentials credentials)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenIddict:Server:Issuer"] = Issuer,
            ["OpenIddict:WebSession:ClientSecret"] = "test-server-secret"
        }).Build();
        var options = new OpenIddictServerOptions();
        options.SigningCredentials.Add(credentials);
        var monitor = new Mock<IOptionsMonitor<OpenIddictServerOptions>>();
        monitor.SetupGet(value => value.CurrentValue).Returns(options);
        return new(client, config, monitor.Object, new TestClock());
    }

    private static string Sign(string audience, SigningCredentials credentials, params Claim[] claims)
    {
        var token = new JwtSecurityToken(Issuer, audience, [new Claim("sub", "10001"), .. claims],
            Now.UtcDateTime, Now.AddMinutes(5).UtcDateTime, credentials);
        if (audience == "radish-api") token.Header["typ"] = "at+jwt";
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object content) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json")
    };

    private sealed class TestClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeHttp(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
