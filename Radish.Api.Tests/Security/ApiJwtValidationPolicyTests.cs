using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Radish.Api.Security;
using Radish.Common.HttpContextTool;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.Security;

public class ApiJwtValidationPolicyTests
{
    private const string Issuer = "https://radish.test";
    private static readonly SymmetricSecurityKey SigningKey = new(
        Encoding.UTF8.GetBytes("radish-q0-c-test-signing-key-32bytes"));

    [Fact]
    public void ValidateToken_ShouldAcceptRadishApiAudienceAndScope()
    {
        var principal = ValidateToken(CreateToken(UserScopes.RadishApi, UserScopes.RadishApi));

        UserClaimReader.HasScope(principal, UserScopes.RadishApi).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("another-api")]
    public void ValidateToken_ShouldRejectMissingOrWrongAudience(string? audience)
    {
        Should.Throw<SecurityTokenInvalidAudienceException>(() =>
            ValidateToken(CreateToken(audience, UserScopes.RadishApi)));
    }

    [Fact]
    public void ValidAudienceWithoutRadishApiScope_ShouldNotSatisfyClientPolicy()
    {
        var principal = ValidateToken(CreateToken(UserScopes.RadishApi, UserScopes.Profile));

        UserClaimReader.HasScope(principal, UserScopes.RadishApi).ShouldBeFalse();
    }

    private static ClaimsPrincipal ValidateToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        return handler.ValidateToken(token, ApiJwtValidationPolicy.Create(Issuer, SigningKey), out _);
    }

    private static string CreateToken(string? audience, string scope)
    {
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: audience,
            claims:
            [
                new Claim(UserClaimTypes.Sub, "10001"),
                new Claim(UserClaimTypes.Scope, scope)
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
