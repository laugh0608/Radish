using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>
/// Console 授权服务
/// </summary>
public interface IConsoleAuthorizationService
{
    /// <summary>
    /// 根据角色名列表获取 Console 权限键集合
    /// </summary>
    Task<List<string>> GetPermissionKeysByRolesAsync(IReadOnlyCollection<string> roleNames);

    /// <summary>
    /// 获取 Console 资源树
    /// </summary>
    Task<List<ConsoleResourceTreeNodeVo>> GetResourceTreeAsync();

    /// <summary>
    /// 获取角色授权快照
    /// </summary>
    Task<RoleAuthorizationSnapshotVo?> GetRoleAuthorizationAsync(long roleId);

    /// <summary>
    /// 获取角色权限预览接口
    /// </summary>
    Task<List<ResourceApiBindingVo>> GetRolePermissionPreviewAsync(long roleId);

    /// <summary>
    /// 保存角色授权
    /// </summary>
    Task<bool> SaveRoleAuthorizationAsync(SaveRoleAuthorizationDto dto, long operatorId, string operatorName);
}
