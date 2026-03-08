using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using JetBrains.Annotations;
using Radish.Common.HttpContextTool;
using Xunit;

namespace Radish.Api.Tests;

[TestSubject(typeof(ClaimsPrincipalNormalizer))]
public class ClaimsPrincipalNormalizerTests
{
    private static string CreateJwtToken(IEnumerable<Claim> claims)
    {
        var token = new JwtSecurityToken(claims: claims);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public void Should_Normalize_Oidc_Claims_To_CurrentUser()
    {
        var claims = new List<Claim>
        {
            new(UserClaimTypes.Sub, "20002"),
            new(UserClaimTypes.Name, "test-user"),
            new(UserClaimTypes.TenantId, "30000"),
            new(UserClaimTypes.Role, "System"),
            new(UserClaimTypes.Scope, "openid profile radish-api")
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var normalizer = new ClaimsPrincipalNormalizer();

        var currentUser = normalizer.Normalize(principal);

        Assert.True(currentUser.IsAuthenticated);
        Assert.Equal(20002, currentUser.UserId);
        Assert.Equal("test-user", currentUser.UserName);
        Assert.Equal(30000, currentUser.TenantId);
        Assert.Contains("System", currentUser.Roles);
        Assert.Contains("radish-api", currentUser.Scopes);
        Assert.True(currentUser.IsInRole("system"));
        Assert.True(currentUser.HasScope("radish-api"));
    }

    [Fact]
    public void Should_Fallback_To_Legacy_Claims_When_Oidc_Claims_Missing()
    {
        var claims = new List<Claim>
        {
            new(UserClaimTypes.LegacyJti, "123"),
            new(UserClaimTypes.LegacyTenantId, "456"),
            new(UserClaimTypes.LegacyName, "legacy-user"),
            new(UserClaimTypes.LegacyRole, "Admin")
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var normalizer = new ClaimsPrincipalNormalizer();

        var currentUser = normalizer.Normalize(principal);

        Assert.Equal(123, currentUser.UserId);
        Assert.Equal("legacy-user", currentUser.UserName);
        Assert.Equal(456, currentUser.TenantId);
        Assert.Contains("Admin", currentUser.Roles);
        Assert.True(currentUser.IsInRole("admin"));
    }

    [Fact]
    public void Should_Read_Scopes_And_Claims_From_Token_Fallback()
    {
        var token = CreateJwtToken(new List<Claim>
        {
            new(UserClaimTypes.Sub, "20003"),
            new(UserClaimTypes.TenantId, "30003"),
            new(UserClaimTypes.Role, "Admin"),
            new(UserClaimTypes.Scope, "openid radish-api")
        });

        var normalizer = new ClaimsPrincipalNormalizer();
        var currentUser = normalizer.Normalize(null, token);

        Assert.False(currentUser.IsAuthenticated);
        Assert.Equal(20003, currentUser.UserId);
        Assert.Equal(30003, currentUser.TenantId);
        Assert.Contains("Admin", currentUser.Roles);
        Assert.Contains("radish-api", currentUser.Scopes);
    }

    [Fact]
    public void UserClaimReader_Should_Detect_Scope_From_Space_Separated_Claim()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>
        {
            new(UserClaimTypes.Scope, $"{UserScopes.OpenId} {UserScopes.Profile} {UserScopes.RadishApi}")
        }, "TestAuth"));

        Assert.True(UserClaimReader.HasScope(principal, UserScopes.RadishApi));
        Assert.False(UserClaimReader.HasScope(principal, UserScopes.Email));
    }
}
