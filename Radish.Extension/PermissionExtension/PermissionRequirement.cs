using Microsoft.AspNetCore.Authorization;

namespace Radish.Extension.PermissionExtension;

/// <summary>
/// 权限管理器
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// 用户或角色或其他凭据实体列表
    /// </summary>
    public List<PermissionItem> PermissionItems { get; set; } = new List<PermissionItem>();
    
    // protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    // {
    //     await Task.CompletedTask;
    //     context.Succeed(requirement); // 直接放行所有授权
    //     return;
    // }
}