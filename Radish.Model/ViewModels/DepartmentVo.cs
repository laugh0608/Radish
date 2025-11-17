namespace Radish.Model.ViewModels;

///<summary>部门表视图模型</summary>
public class DepartmentVo
{
    public string VoCodeRelationship { get; set; } = string.Empty;
    public string VoDeName { get; set; } = "Radish";
    public string VoDeLeader { get; set; } = "System";
    public int VoOrderSort { get; set; } = 0;
    public bool VoIsDeleted { get; set; } = false;
    public string VoCreateBy { get; set; } = "System";
    public DateTime? VoCreateTime { get; set; }
}