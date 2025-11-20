using Microsoft.AspNetCore.Authorization;

namespace Radish.Extension;

/// <summary>
/// 权限管理器
/// </summary>
public class PermissionRequirement : AuthorizationHandler<PermissionRequirement>, IAuthorizationRequirement
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        await Task.CompletedTask;
        context.Succeed(requirement); // 直接放行所有授权
        return;
    }
}
