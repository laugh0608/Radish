using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using OpenIddict.Abstractions;
using Radish.Auth.Controllers;
using Radish.Auth.Models;
using Radish.Common.HttpContextTool;
using Xunit;

namespace Radish.Api.Tests.Controllers;

[TestSubject(typeof(AuthorizationController))]
public class AuthorizationControllerTest
{
    private static readonly MethodInfo GetClaimDestinationsMethod =
        typeof(AuthorizationController).GetMethod("GetClaimDestinations", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("GetClaimDestinations method not found.");

    private static readonly MethodInfo CreateConsentRequestParametersMethod =
        typeof(AuthorizationController).GetMethod(
            "CreateConsentRequestParameters",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("CreateConsentRequestParameters method not found.");

    [Fact]
    public void GetClaimDestinations_ShouldSkipLegacyClaims()
    {
        var scopes = ImmutableArray.Create(OpenIddictConstants.Scopes.OpenId, OpenIddictConstants.Scopes.Profile);

        Assert.Empty(InvokeGetClaimDestinations(new Claim(UserClaimTypes.LegacyNameIdentifier, "1"), scopes));
        Assert.Empty(InvokeGetClaimDestinations(new Claim(UserClaimTypes.LegacyName, "test-user"), scopes));
        Assert.Empty(InvokeGetClaimDestinations(new Claim(UserClaimTypes.LegacyRole, "Admin"), scopes));
        Assert.Empty(InvokeGetClaimDestinations(new Claim(UserClaimTypes.LegacyTenantId, "1"), scopes));
        Assert.Empty(InvokeGetClaimDestinations(new Claim(UserClaimTypes.LegacyJti, "legacy-jti"), scopes));
    }

    [Fact]
    public void GetClaimDestinations_ShouldKeepStandardClaims()
    {
        var scopes = ImmutableArray.Create(OpenIddictConstants.Scopes.OpenId, OpenIddictConstants.Scopes.Profile);

        Assert.Equal(
            new[]
            {
                OpenIddictConstants.Destinations.AccessToken,
                OpenIddictConstants.Destinations.IdentityToken
            },
            InvokeGetClaimDestinations(new Claim(OpenIddictConstants.Claims.Subject, "1"), scopes));

        Assert.Equal(
            new[]
            {
                OpenIddictConstants.Destinations.AccessToken,
                OpenIddictConstants.Destinations.IdentityToken
            },
            InvokeGetClaimDestinations(new Claim(OpenIddictConstants.Claims.Role, "Admin"), scopes));

        Assert.Equal(
            new[]
            {
                OpenIddictConstants.Destinations.AccessToken
            },
            InvokeGetClaimDestinations(new Claim(UserClaimTypes.TenantId, "1"), scopes));
    }

    [Fact]
    public void CreateConsentRequestParameters_ShouldPreservePkceAndExtensionParameters()
    {
        var request = new OpenIddictRequest
        {
            ClientId = "radish-console",
            RedirectUri = "https://localhost:5000/console/callback",
            ResponseType = OpenIddictConstants.ResponseTypes.Code,
            Scope = "openid profile offline_access radish-api",
            State = "state-value",
            CodeChallenge = "code-challenge-value",
            CodeChallengeMethod = OpenIddictConstants.CodeChallengeMethods.Sha256
        };
        request.SetParameter(OpenIddictConstants.Parameters.UiLocales, new StringValues("zh"));
        request.SetParameter("decision", new StringValues("accept"));
        request.SetParameter("__RequestVerificationToken", new StringValues("antiforgery-token"));

        var parameters = InvokeCreateConsentRequestParameters(request)
            .ToDictionary(parameter => parameter.Name, parameter => parameter.Value, StringComparer.Ordinal);

        Assert.Equal("radish-console", parameters[OpenIddictConstants.Parameters.ClientId]);
        Assert.Equal("code-challenge-value", parameters[OpenIddictConstants.Parameters.CodeChallenge]);
        Assert.Equal(
            OpenIddictConstants.CodeChallengeMethods.Sha256,
            parameters[OpenIddictConstants.Parameters.CodeChallengeMethod]);
        Assert.Equal("zh", parameters[OpenIddictConstants.Parameters.UiLocales]);
        Assert.DoesNotContain("decision", parameters.Keys);
        Assert.DoesNotContain("__RequestVerificationToken", parameters.Keys);
    }

    private static string[] InvokeGetClaimDestinations(Claim claim, ImmutableArray<string> scopes)
    {
        return ((IEnumerable<string>)GetClaimDestinationsMethod.Invoke(null, new object[] { claim, scopes })!)
            .ToArray();
    }

    private static IReadOnlyList<ConsentRequestParameter> InvokeCreateConsentRequestParameters(
        OpenIddictRequest request)
    {
        return (IReadOnlyList<ConsentRequestParameter>)CreateConsentRequestParametersMethod.Invoke(
            null,
            new object[] { request })!;
    }
}
