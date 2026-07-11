using Microsoft.IdentityModel.Tokens;
using Radish.Common.HttpContextTool;

namespace Radish.Api.Security;

public static class ApiJwtValidationPolicy
{
    public static TokenValidationParameters Create(string? issuer, SecurityKey? signingKey)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidAudience = UserScopes.RadishApi,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = UserClaimTypes.Sub,
            RoleClaimType = UserClaimTypes.Role,
            ValidIssuers = BuildValidIssuers(issuer),
            IssuerSigningKey = signingKey
        };
    }

    private static string[]? BuildValidIssuers(string? issuer)
    {
        if (string.IsNullOrWhiteSpace(issuer))
        {
            return null;
        }

        var normalizedIssuer = issuer.Trim().TrimEnd('/');
        return [normalizedIssuer, $"{normalizedIssuer}/"];
    }
}
