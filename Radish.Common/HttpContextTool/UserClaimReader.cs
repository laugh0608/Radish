using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Radish.Common.HttpContextTool;

public static class UserClaimReader
{
    public static string GetUserName(ClaimsPrincipal? principal, string? token = null, string defaultValue = "")
    {
        var name = GetFirstClaimValue(principal, "name")
            ?? GetFirstClaimValue(principal, ClaimTypes.Name)
            ?? principal?.Identity?.Name
            ?? GetFirstClaimValueFromToken(token, "name")
            ?? GetFirstClaimValueFromToken(token, ClaimTypes.Name);

        return string.IsNullOrWhiteSpace(name) ? defaultValue : name;
    }

    public static long GetUserId(ClaimsPrincipal? principal, string? token = null)
    {
        return TryParsePositiveLong(GetFirstClaimValue(principal, "sub"))
               ?? TryParsePositiveLong(GetFirstClaimValue(principal, ClaimTypes.NameIdentifier))
               ?? TryParsePositiveLong(GetFirstClaimValue(principal, "jti"))
               ?? TryParsePositiveLong(GetFirstClaimValueFromToken(token, "sub"))
               ?? TryParsePositiveLong(GetFirstClaimValueFromToken(token, ClaimTypes.NameIdentifier))
               ?? TryParsePositiveLong(GetFirstClaimValueFromToken(token, "jti"))
               ?? 0;
    }

    public static long GetTenantId(ClaimsPrincipal? principal, string? token = null)
    {
        return TryParseNonNegativeLong(GetFirstClaimValue(principal, "tenant_id"))
               ?? TryParseNonNegativeLong(GetFirstClaimValue(principal, "TenantId"))
               ?? TryParseNonNegativeLong(GetFirstClaimValueFromToken(token, "tenant_id"))
               ?? TryParseNonNegativeLong(GetFirstClaimValueFromToken(token, "TenantId"))
               ?? 0;
    }

    public static List<string> GetRoles(ClaimsPrincipal? principal, string? token = null)
    {
        var roles = GetClaimValues(principal, "role")
            .Concat(GetClaimValues(principal, ClaimTypes.Role))
            .Concat(GetClaimValuesFromToken(token, "role"))
            .Concat(GetClaimValuesFromToken(token, ClaimTypes.Role))
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return roles;
    }

    private static string? GetFirstClaimValue(ClaimsPrincipal? principal, string claimType)
    {
        return principal?.FindFirst(claimType)?.Value;
    }

    private static IEnumerable<string> GetClaimValues(ClaimsPrincipal? principal, string claimType)
    {
        if (principal == null)
        {
            return Array.Empty<string>();
        }

        return principal.FindAll(claimType).Select(claim => claim.Value);
    }

    private static string? GetFirstClaimValueFromToken(string? token, string claimType)
    {
        return GetClaimValuesFromToken(token, claimType).FirstOrDefault();
    }

    private static IEnumerable<string> GetClaimValuesFromToken(string? token, string claimType)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Array.Empty<string>();
        }

        var jwtHandler = new JwtSecurityTokenHandler();
        if (!jwtHandler.CanReadToken(token))
        {
            return Array.Empty<string>();
        }

        var jwtToken = jwtHandler.ReadJwtToken(token);
        return jwtToken.Claims
            .Where(claim => claim.Type == claimType)
            .Select(claim => claim.Value)
            .ToList();
    }

    private static long? TryParsePositiveLong(string? value)
    {
        return long.TryParse(value, out var result) && result > 0 ? result : null;
    }

    private static long? TryParseNonNegativeLong(string? value)
    {
        return long.TryParse(value, out var result) && result >= 0 ? result : null;
    }
}
