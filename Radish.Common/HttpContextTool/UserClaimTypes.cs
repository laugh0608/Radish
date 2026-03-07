using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace Radish.Common.HttpContextTool;

public static class UserClaimTypes
{
    public const string Sub = "sub";
    public const string Name = "name";
    public const string PreferredUsername = "preferred_username";
    public const string Role = "role";
    public const string Scope = "scope";
    public const string TenantId = "tenant_id";
    public const string LegacyTenantId = "TenantId";
    public const string LegacyName = ClaimTypes.Name;
    public const string LegacyNameIdentifier = ClaimTypes.NameIdentifier;
    public const string LegacyRole = ClaimTypes.Role;
    public const string LegacyJti = JwtRegisteredClaimNames.Jti;
}
