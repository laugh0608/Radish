namespace Radish.Model.ViewModels;

public class UserVo
{
    public long Uuid { get; set; }
    public string VoLoginName { get; set; } = string.Empty;
    public string VoUserName { get; set; } = string.Empty;
    public string VoUserEmail { get; set; } = string.Empty;
    public string VoLoginPassword { get; set; } = string.Empty;
    public string VoUserRealName { get; set; } = string.Empty;
    public int VoUserSex { get; set; } = 0;
    public int VoUserAge { get; set; } = 18;
    public DateTime? VoUserBirth { get; set; }
    public string VoUserAddress { get; set; } = string.Empty;
    public int VoStatusCode { get; set; } = -1;
    public DateTime VoCreateTime { get; set; } = DateTime.Now;
    public DateTime? VoUpdateTime { get; set; }
    public DateTime? VoCriticalModifyTime { get; set; }
    public DateTime? VoLastErrorTime { get; set; }
    public int VoErrorCount { get; set; } = 0;
    public bool VoIsEnable { get; set; } = false;
    public bool VoIsDeleted { get; set; } = true;
    public long VoDepartmentId { get; set; } = 0;
    public string VoDepartmentName { get; set; } = string.Empty;
    public long VoTenantId { get; set; } = 0;
    public List<long> VoRoleIds { get; set; } = new List<long>();
    public List<string> VoRoleNames { get; set; } = new List<string>();
    public List<long> VoDepartmentIds { get; set; } = new List<long>();
    public string VoRemark { get; set; } = string.Empty;
}