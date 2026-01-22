namespace Radish.Model.ViewModels;

public class RoleVo
{
    public long VoId { get; set; }
    public string VoRoleName { get; set; } = string.Empty;
    public string VoRoleDescription { get; set; } = string.Empty;
    public int VoOrderSort { get; set; }
    public string VoDepartmentIds { get; set; } = string.Empty;
    public int VoAuthorityScope { get; set; }
    public bool VoIsEnabled { get; set; }
    public bool VoIsDeleted { get; set; }
    public long VoCreateId { get; set; }
    public string VoCreateBy { get; set; } = string.Empty;
    public DateTime VoCreateTime { get; set; }
    public long? VoModifyId { get; set; }
    public string? VoModifyBy { get; set; }
    public DateTime? VoModifyTime { get; set; }
}