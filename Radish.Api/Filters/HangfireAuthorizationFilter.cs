using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Radish.Api.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Radish.Common.HttpContextTool;
using Radish.Common.PermissionTool;
using Radish.IService;

namespace Radish.Api.Filters;

/// <summary>
/// Hangfire Dashboard 授权过滤器
/// </summary>
/// <remarks>
/// 仅允许已认证且具备 Hangfire 访问权限的用户访问 Dashboard
/// </remarks>
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // iframe 不携带 Bearer 头，只在此看板入口接受独立短期 Cookie。
        // API 默认认证仍为 Bearer，不接受这个 Cookie。
        var scheme = httpContext.Request.Headers.Authorization.Count > 0
            ? JwtBearerDefaults.AuthenticationScheme
            : HangfireDashboardAuthentication.Scheme;
        var authenticateResult = httpContext.AuthenticateAsync(scheme).GetAwaiter().GetResult();
        if (authenticateResult?.Principal == null)
        {
            return false;
        }

        httpContext.User = authenticateResult.Principal;

        var currentUser = httpContext.RequestServices.GetRequiredService<IClaimsPrincipalNormalizer>()
            .Normalize(authenticateResult.Principal);
        if (!currentUser.IsAuthenticated)
        {
            return false;
        }

        if (currentUser.IsSystemOrAdmin())
        {
            return true;
        }

        if (currentUser.Roles.Count <= 0)
        {
            return false;
        }

        var consoleAuthorizationService = httpContext.RequestServices.GetRequiredService<IConsoleAuthorizationService>();
        var permissionKeys = consoleAuthorizationService
            .GetPermissionKeysByRolesAsync(currentUser.Roles)
            .GetAwaiter()
            .GetResult();

        return permissionKeys.Contains(ConsolePermissions.HangfireView, StringComparer.OrdinalIgnoreCase);
    }
}
