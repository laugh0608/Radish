namespace Radish.Common.HttpContextTool;

public static class UserRoleHelper
{
    public static bool ContainsRole(IEnumerable<string>? roles, string? role)
    {
        if (roles == null || string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        return roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    public static bool HasAnyRole(IEnumerable<string>? roles, params string[] expectedRoles)
    {
        if (roles == null || expectedRoles.Length == 0)
        {
            return false;
        }

        return expectedRoles.Any(role => ContainsRole(roles, role));
    }

    public static bool IsSystemOrAdmin(IEnumerable<string>? roles)
    {
        return HasAnyRole(roles, UserRoles.System, UserRoles.Admin);
    }
}
