using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Radish.Auth.OpenIddict;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Xunit;

namespace Radish.Api.Tests.Auth;

public sealed class WebAuthSessionTokenHandlerTest
{
    private static readonly Guid RefreshLease = Guid.NewGuid();

    [Theory]
    [InlineData("radish-client")]
    [InlineData("radish-console")]
    public async Task LegacyClients_ShouldNeverEnterWebSessionValidationOrIssuance(string clientId)
    {
        var sessions = new Mock<IWebAuthSessionService>(MockBehavior.Strict);
        var handler = new WebAuthSessionTokenHandler(CreateConfiguration(), sessions.Object, Mock.Of<IUserService>());
        var transaction = CreateTransaction(clientId, OpenIddictConstants.GrantTypes.RefreshToken);
        var principal = BoundPrincipal();
        var validate = new OpenIddictServerEvents.ValidateTokenRequestContext(transaction) { RefreshTokenPrincipal = principal };
        var signIn = new OpenIddictServerEvents.ProcessSignInContext(transaction)
        {
            Principal = principal,
            AccessTokenPrincipal = principal.Clone(static _ => true),
            RefreshTokenPrincipal = principal.Clone(static _ => true)
        };

        await handler.HandleAsync(validate);
        await handler.HandleAsync(signIn);

        Assert.False(validate.IsRejected);
        Assert.False(signIn.IsRejected);
        Assert.Null(signIn.AccessTokenPrincipal.GetExpirationDate());
        Assert.Null(signIn.RefreshTokenPrincipal.GetExpirationDate());
        sessions.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task WebRefresh_ShouldRejectDisabledClientOrMissingBinding(bool enabled, bool hasBinding)
    {
        var sessions = new Mock<IWebAuthSessionService>(MockBehavior.Strict);
        var handler = new WebAuthSessionTokenHandler(CreateConfiguration(enabled), sessions.Object, Mock.Of<IUserService>());
        var context = new OpenIddictServerEvents.ValidateTokenRequestContext(
            CreateTransaction(WebAuthClient.ClientId, OpenIddictConstants.GrantTypes.RefreshToken))
        {
            RefreshTokenPrincipal = hasBinding ? BoundPrincipal() : new ClaimsPrincipal()
        };
        context.Request.SetParameter(IdleSessionPolicy.LastActiveParameter, "9999999999");

        await handler.HandleAsync(context);

        Assert.True(context.IsRejected);
        Assert.Equal(OpenIddictConstants.Errors.InvalidGrant, context.Error);
        sessions.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task WebAuthorization_ShouldRejectAnOrdinaryAuthCookieWithoutServerBinding()
    {
        var transaction = CreateTransaction(WebAuthClient.ClientId, OpenIddictConstants.GrantTypes.AuthorizationCode);
        transaction.EndpointType = OpenIddictServerEndpointType.Authorization;
        var context = new OpenIddictServerEvents.ProcessSignInContext(transaction)
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity("Cookie"))
        };
        context.Request.SetParameter(WebAuthClient.SessionIdClaim, Guid.NewGuid().ToString());

        await new RequireWebAuthSessionAuthorizationHandler(CreateConfiguration()).HandleAsync(context);

        Assert.True(context.IsRejected);
        Assert.Equal("web_session_login_required", context.ErrorDescription);
    }

    [Theory]
    [InlineData(WebAuthSessionGrantStatus.IdleExpired, "session_idle_expired")]
    [InlineData(WebAuthSessionGrantStatus.AbsoluteExpired, "session_absolute_expired")]
    public async Task WebRefresh_ShouldUseOnlyServerSideDeadlineState(WebAuthSessionGrantStatus status, string error)
    {
        var sessions = new Mock<IWebAuthSessionService>();
        sessions.Setup(service => service.EvaluateGrantAsync(
                It.IsAny<WebAuthSessionBinding>(), true, It.IsAny<CancellationToken>(), RefreshLease))
            .ReturnsAsync(new WebAuthSessionGrant(status));
        var handler = new WebAuthSessionTokenHandler(CreateConfiguration(), sessions.Object, Mock.Of<IUserService>());
        var context = new OpenIddictServerEvents.ValidateTokenRequestContext(
            CreateTransaction(WebAuthClient.ClientId, OpenIddictConstants.GrantTypes.RefreshToken))
        {
            RefreshTokenPrincipal = BoundPrincipal()
        };
        context.Request.SetParameter(IdleSessionPolicy.LastActiveParameter, "9999999999");

        await handler.HandleAsync(context);

        Assert.True(context.IsRejected);
        Assert.Equal(error, context.ErrorDescription);
    }

    [Theory]
    [InlineData(OpenIddictConstants.GrantTypes.AuthorizationCode, false, true)]
    [InlineData(OpenIddictConstants.GrantTypes.RefreshToken, true, true)]
    [InlineData(OpenIddictConstants.GrantTypes.RefreshToken, true, false)]
    public async Task WebIssuance_ShouldEnforceAccountStatusAndExactExpiry(string grantType, bool isRefresh, bool accountEnabled)
    {
        var principal = BoundPrincipal();
        var binding = new WebAuthSessionBinding(Guid.Parse(principal.GetClaim(WebAuthClient.SessionIdClaim)!), "10001", "0", "auth-1");
        var expiry = new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
        var users = new Mock<IUserService>();
        users.Setup(service => service.QueryExistsAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync(accountEnabled);
        var sessions = new Mock<IWebAuthSessionService>(MockBehavior.Strict);
        sessions.Setup(service => service.EvaluateGrantAsync(binding, isRefresh, It.IsAny<CancellationToken>(), isRefresh ? RefreshLease : null))
            .ReturnsAsync(new WebAuthSessionGrant(WebAuthSessionGrantStatus.Allowed, expiry, expiry.AddDays(10)));
        var handler = new WebAuthSessionTokenHandler(CreateConfiguration(), sessions.Object, users.Object);
        var context = new OpenIddictServerEvents.ProcessSignInContext(CreateTransaction(WebAuthClient.ClientId, grantType))
        {
            Principal = principal,
            AccessTokenPrincipal = principal.Clone(static _ => true),
            RefreshTokenPrincipal = principal.Clone(static _ => true)
        };

        await handler.HandleAsync(context);

        Assert.Equal(!accountEnabled, context.IsRejected);
        Assert.Equal(accountEnabled ? expiry : null, context.AccessTokenPrincipal.GetExpirationDate());
        Assert.Equal(accountEnabled ? expiry.AddDays(10) : null, context.RefreshTokenPrincipal.GetExpirationDate());
        sessions.Verify(service => service.EvaluateGrantAsync(binding, isRefresh, It.IsAny<CancellationToken>(), isRefresh ? RefreshLease : null),
            accountEnabled ? Times.Once() : Times.Never());
    }

    private static IConfiguration CreateConfiguration(bool enabled = true)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenIddict:WebSession:Enabled"] = enabled.ToString()
        }).Build();
    }

    private static OpenIddictServerTransaction CreateTransaction(string clientId, string grantType)
    {
        var request = new OpenIddictRequest { ClientId = clientId, GrantType = grantType };
        if (clientId == WebAuthClient.ClientId && request.IsRefreshTokenGrantType())
        {
            request.SetParameter(WebAuthClient.RefreshLeaseParameter, RefreshLease.ToString("N"));
        }

        return new OpenIddictServerTransaction
        {
            EndpointType = OpenIddictServerEndpointType.Token,
            Options = new OpenIddictServerOptions(),
            Logger = NullLogger.Instance,
            Request = request
        };
    }

    private static ClaimsPrincipal BoundPrincipal()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity("OIDC"));
        principal.SetClaim(OpenIddictConstants.Claims.Subject, "10001");
        principal.SetClaim(UserClaimTypes.TenantId, "0");
        principal.SetClaim(WebAuthClient.SessionIdClaim, Guid.NewGuid().ToString());
        principal.SetAuthorizationId("auth-1");
        return principal;
    }
}
