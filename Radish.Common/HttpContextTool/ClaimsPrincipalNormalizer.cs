using System.Security.Claims;

namespace Radish.Common.HttpContextTool;

public sealed class ClaimsPrincipalNormalizer : IClaimsPrincipalNormalizer
{
    public CurrentUser Normalize(ClaimsPrincipal? principal, string? token = null)
    {
        var roles = UserClaimReader.GetRoles(principal, token);
        var scopes = UserClaimReader.GetScopes(principal, token);

        return new CurrentUser
        {
            IsAuthenticated = principal?.Identity?.IsAuthenticated ?? false,
            UserId = UserClaimReader.GetUserId(principal, token),
            UserName = UserClaimReader.GetUserName(principal, token, string.Empty),
            TenantId = UserClaimReader.GetTenantId(principal, token),
            Roles = roles,
            Scopes = scopes
        };
    }
}
