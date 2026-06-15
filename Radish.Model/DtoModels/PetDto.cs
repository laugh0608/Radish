using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>领取宠物请求</summary>
public class PetClaimDto
{
    /// <summary>宠物名称</summary>
    [StringLength(40, MinimumLength = 1, ErrorMessage = "宠物名称长度必须为 1-40 个字符")]
    public string? Name { get; set; }
}

/// <summary>更新宠物资料请求</summary>
public class UpdatePetProfileDto
{
    /// <summary>宠物名称</summary>
    [StringLength(40, MinimumLength = 1, ErrorMessage = "宠物名称长度必须为 1-40 个字符")]
    public string? Name { get; set; }

    /// <summary>是否公开展示宠物名片</summary>
    public bool? IsPublic { get; set; }
}

/// <summary>照顾宠物请求</summary>
public class PetCareDto
{
    /// <summary>动作类型：feed/clean/play/rest</summary>
    [Required(ErrorMessage = "actionType 不能为空")]
    [StringLength(20, ErrorMessage = "actionType 长度不能超过 20 个字符")]
    public string ActionType { get; set; } = string.Empty;

    /// <summary>幂等键</summary>
    [StringLength(80, ErrorMessage = "idempotencyKey 长度不能超过 80 个字符")]
    public string? IdempotencyKey { get; set; }
}
