using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using JetBrains.Annotations;
using OpenIddict.Abstractions;
using Radish.Auth.Controllers;
using Radish.Common.HttpContextTool;
using Xunit;

namespace Radish.Api.Tests.Controllers;

[TestSubject(typeof(AuthorizationController))]
public class AuthorizationControllerTest
{
    private static readonly MethodInfo GetClaimDestinationsMethod =
        typeof(AuthorizationController).GetMethod("GetClaimDestinations", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("GetClaimDestinations method not found.");

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

    private static string[] InvokeGetClaimDestinations(Claim claim, ImmutableArray<string> scopes)
    {
        return ((IEnumerable<string>)GetClaimDestinationsMethod.Invoke(null, new object[] { claim, scopes })!)
            .ToArray();
    }
}
