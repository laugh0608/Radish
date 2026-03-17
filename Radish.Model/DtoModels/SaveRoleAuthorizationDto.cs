namespace Radish.Model.DtoModels;

/// <summary>
/// 保存角色授权请求
/// </summary>
public class SaveRoleAuthorizationDto
{
    public long RoleId { get; set; }
    public List<long> ResourceIds { get; set; } = new();
    public DateTime? ExpectedModifyTime { get; set; }
}
