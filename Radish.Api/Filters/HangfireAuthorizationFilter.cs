using Hangfire.Dashboard;
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
/// 仅允许本地访问或已认证且具备 Hangfire 访问权限的用户访问 Dashboard
/// </remarks>
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        if (httpContext.Request.IsLocal())
        {
            return true;
        }

        var schemes = httpContext.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();
        var defaultAuthenticate = schemes.GetDefaultAuthenticateSchemeAsync().GetAwaiter().GetResult();
        if (defaultAuthenticate == null)
        {
            return false;
        }

        var authenticateResult = httpContext.AuthenticateAsync(defaultAuthenticate.Name).GetAwaiter().GetResult();
        if (authenticateResult?.Principal == null)
        {
            return false;
        }

        httpContext.User = authenticateResult.Principal;

        var currentUser = httpContext.RequestServices.GetRequiredService<ICurrentUserAccessor>().Current;
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

/// <summary>
/// HttpRequest 扩展方法
/// </summary>
public static class HttpRequestExtensions
{
    /// <summary>
    /// 判断请求是否来自本地
    /// </summary>
    public static bool IsLocal(this HttpRequest request)
    {
        var connection = request.HttpContext.Connection;
        if (connection.RemoteIpAddress != null)
        {
            return connection.LocalIpAddress != null
                ? connection.RemoteIpAddress.Equals(connection.LocalIpAddress)
                : System.Net.IPAddress.IsLoopback(connection.RemoteIpAddress);
        }

        return true;
    }
}
