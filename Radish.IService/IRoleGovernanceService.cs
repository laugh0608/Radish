using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>
/// Console 角色治理服务。
/// </summary>
public interface IRoleGovernanceService
{
    Task<List<RoleVo>> GetRolesAsync();

    Task<RoleVo?> GetRoleAsync(long roleId);

    Task<RoleVo> CreateRoleAsync(RoleMutationDto request, long operatorId, string operatorName);

    Task<RoleVo> UpdateRoleAsync(long roleId, RoleMutationDto request, long operatorId, string operatorName);

    Task<RoleVo> ToggleRoleAsync(long roleId, bool enabled, long operatorId, string operatorName);

    Task DeleteRoleAsync(long roleId, long operatorId, string operatorName);
}
