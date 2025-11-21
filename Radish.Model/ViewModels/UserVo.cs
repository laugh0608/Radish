namespace Radish.Model.ViewModels;

public class UserVo
{
    public long Uuid { get; set; }
    public string VoLoName { get; set; } = string.Empty;
    public string VoUsName { get; set; } = string.Empty;
    public string VoUsEmail { get; set; } = string.Empty;
    public string VoLoPwd { get; set; } = string.Empty;
    public string VoReNa { get; set; } = string.Empty;
    public int VoSexDo { get; set; } = 0;
    public int VoAgeDo { get; set; } = 18;
    public DateTime? VoBiTh { get; set; }
    public string VoAdRes { get; set; } = string.Empty;
    public int VoStaCo { get; set; } = -1;
    public DateTime VoCreateTime { get; set; } = DateTime.Now;
    public DateTime? VoUpdateTime { get; set; }
    public DateTime? VoCrModifyTime { get; set; }
    public DateTime? VoLaErrTime { get; set; }
    public int VoErrorCount { get; set; } = 0;
    public bool VoIsEnable { get; set; } = false;
    public bool VoIsDeleted { get; set; } = true;
    public long VoDeId { get; set; } = 1000001;
    public string VoDeNa { get; set; } = string.Empty;
    public long VoTenId { get; set; } = 1000001;
    public List<long> VoRoIds { get; set; } = new List<long>();
    public List<string> VoRoNas { get; set; } = new List<string>();
    public List<long> VoDeIds { get; set; } = new List<long>();
    public string VoRemark { get; set; } = string.Empty;
}