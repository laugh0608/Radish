using Hangfire.Dashboard;

namespace Radish.Api.Filters;

/// <summary>
/// Hangfire Dashboard 授权过滤器
/// </summary>
/// <remarks>
/// 仅允许本地访问或已认证的管理员访问 Hangfire Dashboard
/// </remarks>
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // 允许本地访问
        if (httpContext.Request.IsLocal())
        {
            return true;
        }

        // 检查是否已认证
        if (!httpContext.User.Identity?.IsAuthenticated ?? false)
        {
            return false;
        }

        // 检查是否为管理员角色
        return httpContext.User.IsInRole("Admin") || httpContext.User.IsInRole("System");
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

        // 如果没有远程 IP，可能是本地请求
        return true;
    }
}
