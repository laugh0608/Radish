using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Api.ErrorHandling;
using Radish.Shared.Constants;

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
            context.Result = ApiErrorResultFactory.Create(
                context.HttpContext,
                StatusCodes.Status401Unauthorized,
                "请先登录后再继续操作",
                ApiErrorCodes.Unauthorized,
                "error.auth.unauthorized");
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
            context.Result = ApiErrorResultFactory.Create(
                context.HttpContext,
                StatusCodes.Status403Forbidden,
                "当前账号无权执行此操作",
                ApiErrorCodes.Forbidden,
                "error.auth.forbidden");
        }
    }
}
