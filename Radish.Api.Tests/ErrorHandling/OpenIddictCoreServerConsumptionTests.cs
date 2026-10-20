using System;
using System.Security.Claims;
using System.Threading.Tasks;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Xunit;
using static OpenIddict.Server.OpenIddictServerEvents;
using Handlers = OpenIddict.Server.OpenIddictServerHandlers;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class OpenIddictCoreServerConsumptionTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task Redemption_ShouldKeepAuthorizationCodeRejectionAndRefreshFailureConsumption(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var fixture = new OpenIddictLoggingFixture(capture);
        foreach (var refresh in new[] { false, true })
        foreach (var fault in new[] { "none", "concurrency", "update-cancel", "cache-add", "set-status" })
        {
            var boundary = new OpenIddictCoreMutationFixture(fixture.Factory, fault);
            var principal = Principal(refresh ? OpenIddictConstants.TokenTypeIdentifiers.RefreshToken : OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
            var transaction = fixture.Transaction(new() { GrantType = refresh ? "refresh_token" : "authorization_code" });
            transaction.EndpointType = OpenIddictServerEndpointType.Token;
            var authentication = new ProcessAuthenticationContext(transaction);
            if (refresh) authentication.RefreshTokenPrincipal = principal;
            else authentication.AuthorizationCodePrincipal = principal;
            transaction.SetProperty(typeof(ProcessAuthenticationContext).FullName!, authentication);
            var context = new ProcessSignInContext(transaction) { Principal = principal };
            var handler = new Handlers.RedeemTokenEntry(boundary.TokenManager);
            if (fault == "set-status")
            {
                Assert.Same(boundary.Failure, await Record.ExceptionAsync(async () => await handler.HandleAsync(context)));
                Assert.False(context.IsRejected);
                OpenIddictCoreEvents.AssertEvents(capture);
            }
            else
            {
                await handler.HandleAsync(context);
                Assert.Equal(!refresh && fault != "none", context.IsRejected);
                Assert.Equal(context.IsRejected ? "invalid_token" : null, context.Error);
                OpenIddictCoreEvents.AssertEvents(capture, fault == "none" ? 6168 : fault == "concurrency" ? 6169 : 6170);
            }
            Assert.Same(principal, context.Principal);
            Assert.Equal(fault is "none" or "cache-add" ? 1 : 0, boundary.Writes);
            Assert.Equal(fault == "set-status" ? "valid" : "redeemed", boundary.Entity.Status);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task Revocation_ShouldKeepUnsupportedTokenErrorsAndResponseNormalization(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var fixture = new OpenIddictLoggingFixture(capture);
        foreach (var fault in new[] { "none", "concurrency", "update-cancel", "cache-add", "set-status" })
        {
            var boundary = new OpenIddictCoreMutationFixture(fixture.Factory, fault);
            var transaction = fixture.Transaction();
            transaction.EndpointType = OpenIddictServerEndpointType.Revocation;
            var principal = Principal(OpenIddictConstants.TokenTypeIdentifiers.AccessToken);
            var context = new HandleRevocationRequestContext(transaction) { GenericTokenPrincipal = principal };
            var handler = new Handlers.Revocation.RevokeToken(boundary.TokenManager);
            if (fault == "set-status")
            {
                Assert.Same(boundary.Failure, await Record.ExceptionAsync(async () => await handler.HandleAsync(context)));
                Assert.False(context.IsRejected);
                OpenIddictCoreEvents.AssertEvents(capture);
            }
            else
            {
                await handler.HandleAsync(context);
                Assert.Equal(fault != "none", context.IsRejected);
                Assert.Equal(fault == "none" ? null : "unsupported_token_type", context.Error);
                OpenIddictCoreEvents.AssertEvents(capture, fault == "none" ? 6174 : fault == "concurrency" ? 6175 : 6176);
                transaction.Response = new() { Error = context.Error, ErrorDescription = context.ErrorDescription, ErrorUri = context.ErrorUri };
                await new Handlers.Revocation.NormalizeErrorResponse().HandleAsync(new(transaction));
                Assert.Equal(context.Error, transaction.Response.Error);
                Assert.Equal(context.ErrorDescription, transaction.Response.ErrorDescription);
                Assert.Equal(context.ErrorUri, transaction.Response.ErrorUri);
            }
            Assert.Same(principal, context.GenericTokenPrincipal);
            Assert.Equal(fault is "none" or "cache-add" ? 1 : 0, boundary.Writes);
            capture.Clear();
        }
        var invalid = fixture.Transaction();
        invalid.Response = new() { Error = "invalid_token", ErrorDescription = HttpBoundaryCapture.Secret, ErrorUri = "https://example.invalid/" };
        await new Handlers.Revocation.NormalizeErrorResponse().HandleAsync(new(invalid));
        Assert.Null(invalid.Response.Error);
        Assert.Null(invalid.Response.ErrorDescription);
        Assert.Null(invalid.Response.ErrorUri);
        OpenIddictCoreEvents.AssertEvents(capture);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task ConditionalDeviceAndLogoutHandlers_ShouldKeepIgnoredFalseAndPropagatedExceptions(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var fixture = new OpenIddictLoggingFixture(capture);
        foreach (var kind in new[] { "device", "user-code", "logout" })
        foreach (var fault in new[] { "none", "update-io", "update-cancel", "set-status" })
        {
            var boundary = new OpenIddictCoreMutationFixture(fixture.Factory, fault);
            var transaction = fixture.Transaction();
            transaction.EndpointType = kind == "logout" ? OpenIddictServerEndpointType.EndSession : OpenIddictServerEndpointType.EndUserVerification;
            var principal = Principal(OpenIddictConstants.TokenTypeIdentifiers.Private.UserCode)
                .SetClaim(OpenIddictConstants.Claims.Private.DeviceCodeId, HttpBoundaryCapture.Secret);
            transaction.SetProperty(typeof(ProcessAuthenticationContext).FullName!, new ProcessAuthenticationContext(transaction)
            {
                RequestTokenPrincipal = principal, UserCodePrincipal = principal
            });
            var challenge = new ProcessChallengeContext(transaction);
            var logout = new ProcessSignOutContext(transaction);
            var exception = await Record.ExceptionAsync(async () =>
            {
                if (kind == "device") await new Handlers.RejectDeviceCodeEntry(boundary.TokenManager).HandleAsync(challenge);
                else if (kind == "user-code") await new Handlers.RejectUserCodeEntry(boundary.TokenManager).HandleAsync(challenge);
                else await new Handlers.RedeemLogoutTokenEntry(boundary.TokenManager).HandleAsync(logout);
            });
            if (fault == "set-status") Assert.Same(boundary.Failure, exception);
            else Assert.Null(exception);
            Assert.False(challenge.IsRejected);
            Assert.False(logout.IsRejected);
            Assert.Equal(fault == "none" ? 1 : 0, boundary.Writes);
            OpenIddictCoreEvents.AssertEvents(capture, fault == "set-status" ? [] : [(kind == "logout" ? 6168 : 6171) + (fault == "none" ? 0 : 2)]);
            capture.Clear();
        }
    }

    private static ClaimsPrincipal Principal(string type) => new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", HttpBoundaryCapture.Secret) }))
        .SetTokenId(HttpBoundaryCapture.Secret).SetTokenType(type);
}
