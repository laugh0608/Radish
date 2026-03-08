namespace Radish.Common.HttpContextTool;

[Obsolete("禁止新增使用，请改用 CurrentUser / ICurrentUserAccessor")]
public static class HttpContextUserCompatibilityExtensions
{
    public static bool IsSystemOrAdmin(this IHttpContextUser httpContextUser)
    {
        ArgumentNullException.ThrowIfNull(httpContextUser);
        return httpContextUser.IsInRole(UserRoles.System) || httpContextUser.IsInRole(UserRoles.Admin);
    }
}
