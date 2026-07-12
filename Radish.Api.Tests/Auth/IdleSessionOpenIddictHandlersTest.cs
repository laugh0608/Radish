using System;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Radish.Auth.Models;
using Radish.Auth.OpenIddict;
using Xunit;

namespace Radish.Api.Tests.Auth;

public class IdleSessionOpenIddictHandlersTest
{
    [Fact]
    public async Task ValidateHandler_ShouldRejectExpiredRefreshToken()
    {
        var options = new IdleSessionOptions { Enable = true, IdleTimeoutDays = 7 };
        var handler = new ValidateIdleSessionTokenRequestHandler(
            CreateOptionsMonitor(options),
            NullLogger<ValidateIdleSessionTokenRequestHandler>.Instance);
        var transaction = CreateTransaction(new OpenIddictRequest
        {
            GrantType = OpenIddictConstants.GrantTypes.RefreshToken
        });
        var context = new OpenIddictServerEvents.ValidateTokenRequestContext(transaction)
        {
            RefreshTokenPrincipal = CreateRefreshTokenPrincipal(DateTimeOffset.UtcNow.AddDays(-8).ToUnixTimeSeconds())
        };

        await handler.HandleAsync(context);

        Assert.True(context.IsRejected);
        Assert.Equal(OpenIddictConstants.Errors.InvalidGrant, context.Error);
        Assert.Equal(IdleSessionPolicy.ExpiredErrorDescription, context.ErrorDescription);
    }

    [Fact]
    public async Task AttachHandler_ShouldKeepNewestClientActivityOnRefreshToken()
    {
        var options = new IdleSessionOptions { Enable = true, IdleTimeoutDays = 7 };
        var handler = new AttachIdleSessionRefreshTokenHandler(CreateOptionsMonitor(options));
        var clientLastActiveAt = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();
        var request = new OpenIddictRequest
        {
            GrantType = OpenIddictConstants.GrantTypes.RefreshToken
        };
        request.SetParameter(
            IdleSessionPolicy.LastActiveParameter,
            clientLastActiveAt.ToString(CultureInfo.InvariantCulture));
        var context = new OpenIddictServerEvents.ProcessSignInContext(CreateTransaction(request))
        {
            RefreshTokenPrincipal = CreateRefreshTokenPrincipal(clientLastActiveAt - 3600)
        };

        await handler.HandleAsync(context);

        Assert.Equal(
            clientLastActiveAt.ToString(CultureInfo.InvariantCulture),
            context.RefreshTokenPrincipal.GetClaim(IdleSessionPolicy.LastActiveClaim));
    }

    private static OpenIddictServerTransaction CreateTransaction(OpenIddictRequest request)
    {
        return new OpenIddictServerTransaction
        {
            Logger = NullLogger.Instance,
            Options = new OpenIddictServerOptions(),
            Request = request
        };
    }

    private static ClaimsPrincipal CreateRefreshTokenPrincipal(long lastActiveAt)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity("RefreshToken"));
        principal.SetClaim(OpenIddictConstants.Claims.Subject, "10001");
        principal.SetClaim(
            IdleSessionPolicy.LastActiveClaim,
            lastActiveAt.ToString(CultureInfo.InvariantCulture));
        return principal;
    }

    private static IOptionsMonitor<IdleSessionOptions> CreateOptionsMonitor(IdleSessionOptions options)
    {
        var monitor = new Mock<IOptionsMonitor<IdleSessionOptions>>();
        monitor.SetupGet(item => item.CurrentValue).Returns(options);
        return monitor.Object;
    }
}
