namespace Radish.Model.DtoModels;

/// <summary>
/// Console 角色创建 / 更新命令。
/// </summary>
/// <remarks>
/// 保留既有 vo 前缀 JSON 字段，避免改变 Console HTTP 契约；不承载实体审计、删除或主键字段。
/// </remarks>
public sealed class RoleMutationDto
{
    public string VoRoleName { get; set; } = string.Empty;

    public string? VoRoleDescription { get; set; }

    public int VoOrderSort { get; set; }

    public string? VoDepartmentIds { get; set; }

    public int VoAuthorityScope { get; set; }

    public bool VoIsEnabled { get; set; } = true;
}
