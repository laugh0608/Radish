namespace Radish.Common.HttpContextTool;

public static class HttpContextUserCompatibilityExtensions
{
    public static bool IsSystemOrAdmin(this IHttpContextUser httpContextUser)
    {
        ArgumentNullException.ThrowIfNull(httpContextUser);
        return httpContextUser.IsInRole(UserRoles.System) || httpContextUser.IsInRole(UserRoles.Admin);
    }
}
