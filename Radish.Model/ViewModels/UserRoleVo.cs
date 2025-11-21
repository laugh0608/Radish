namespace Radish.Model.ViewModels;

public class UserRoleVo
{
    public long VoUserId { get; set; }
    public long VoRoleId { get; set; }
    public bool VoIsDeleted { get; set; } = true;
    public long VoCreateId { get; set; } = 1000001;
    public string VoCreateBy { get; set; } = "System";
    public DateTime VoCreateTime { get; set; } = DateTime.Now;
    public int? VoModifyId { get; set; }
    public string? VoModifyBy { get; set; }
    public DateTime? VoModifyTime { get; set; }
}