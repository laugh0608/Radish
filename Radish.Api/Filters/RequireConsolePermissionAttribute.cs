using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Radish.Common.HttpContextTool;
using Radish.IService;

namespace Radish.Api.Filters;

/// <summary>
/// Console 权限校验特性
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireConsolePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string[] _permissions;

    public RequireConsolePermissionAttribute(params string[] permissions)
    {
        _permissions = permissions ?? Array.Empty<string>();
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (_permissions.Length <= 0)
        {
            return;
        }

        var currentUserAccessor = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserAccessor>();
        var currentUser = currentUserAccessor.Current;
        if (!currentUser.IsAuthenticated)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (currentUser.IsSystemOrAdmin())
        {
            return;
        }

        var consoleAuthorizationService = context.HttpContext.RequestServices.GetRequiredService<IConsoleAuthorizationService>();
        var permissionKeys = await consoleAuthorizationService.GetPermissionKeysByRolesAsync(currentUser.Roles);

        var hasPermission = _permissions.Any(requiredPermission =>
            permissionKeys.Contains(requiredPermission, StringComparer.OrdinalIgnoreCase));

        if (!hasPermission)
        {
            context.Result = new ForbidResult();
        }
    }
}
