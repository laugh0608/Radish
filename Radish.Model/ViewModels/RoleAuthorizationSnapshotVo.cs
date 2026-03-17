namespace Radish.Model.ViewModels;

/// <summary>
/// 角色授权快照
/// </summary>
public class RoleAuthorizationSnapshotVo
{
    public long VoRoleId { get; set; }
    public string VoRoleName { get; set; } = string.Empty;
    public string VoRoleDescription { get; set; } = string.Empty;
    public bool VoRoleIsEnabled { get; set; }
    public DateTime? VoLastModifyTime { get; set; }
    public List<long> VoGrantedResourceIds { get; set; } = new();
    public List<string> VoGrantedPermissionKeys { get; set; } = new();
    public List<ResourceApiBindingVo> VoDerivedApiModules { get; set; } = new();
}
