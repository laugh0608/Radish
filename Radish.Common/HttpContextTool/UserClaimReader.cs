using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Radish.Common.HttpContextTool;

public static class UserClaimReader
{
    public static string GetUserName(ClaimsPrincipal? principal, string? token = null, string defaultValue = "")
    {
        var name = GetFirstClaimValue(principal, UserClaimTypes.Name)
            ?? GetFirstClaimValue(principal, UserClaimTypes.PreferredUsername)
            ?? GetFirstClaimValue(principal, UserClaimTypes.LegacyName)
            ?? principal?.Identity?.Name
            ?? GetFirstClaimValueFromToken(token, UserClaimTypes.Name)
            ?? GetFirstClaimValueFromToken(token, UserClaimTypes.PreferredUsername)
            ?? GetFirstClaimValueFromToken(token, UserClaimTypes.LegacyName);

        return string.IsNullOrWhiteSpace(name) ? defaultValue : name;
    }

    public static long GetUserId(ClaimsPrincipal? principal, string? token = null)
    {
        return TryParsePositiveLong(GetFirstClaimValue(principal, UserClaimTypes.Sub))
               ?? TryParsePositiveLong(GetFirstClaimValue(principal, UserClaimTypes.LegacyNameIdentifier))
               ?? TryParsePositiveLong(GetFirstClaimValue(principal, UserClaimTypes.LegacyJti))
               ?? TryParsePositiveLong(GetFirstClaimValueFromToken(token, UserClaimTypes.Sub))
               ?? TryParsePositiveLong(GetFirstClaimValueFromToken(token, UserClaimTypes.LegacyNameIdentifier))
               ?? TryParsePositiveLong(GetFirstClaimValueFromToken(token, UserClaimTypes.LegacyJti))
               ?? 0;
    }

    public static long GetTenantId(ClaimsPrincipal? principal, string? token = null)
    {
        return TryParseNonNegativeLong(GetFirstClaimValue(principal, UserClaimTypes.TenantId))
               ?? TryParseNonNegativeLong(GetFirstClaimValue(principal, UserClaimTypes.LegacyTenantId))
               ?? TryParseNonNegativeLong(GetFirstClaimValueFromToken(token, UserClaimTypes.TenantId))
               ?? TryParseNonNegativeLong(GetFirstClaimValueFromToken(token, UserClaimTypes.LegacyTenantId))
               ?? 0;
    }

    public static List<string> GetRoles(ClaimsPrincipal? principal, string? token = null)
    {
        return GetClaimValues(principal, UserClaimTypes.Role)
            .Concat(GetClaimValues(principal, UserClaimTypes.LegacyRole))
            .Concat(GetClaimValuesFromToken(token, UserClaimTypes.Role))
            .Concat(GetClaimValuesFromToken(token, UserClaimTypes.LegacyRole))
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<string> GetScopes(ClaimsPrincipal? principal, string? token = null)
    {
        return GetClaimValues(principal, UserClaimTypes.Scope)
            .Concat(GetClaimValuesFromToken(token, UserClaimTypes.Scope))
            .SelectMany(SplitSpaceSeparatedValues)
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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

    private static IEnumerable<string> SplitSpaceSeparatedValues(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
