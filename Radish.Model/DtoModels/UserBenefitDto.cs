using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>管理员撤销用户持续权益请求。</summary>
public sealed class RevokeUserBenefitDto
{
    [Required(ErrorMessage = "撤销原因不能为空")]
    [StringLength(500, MinimumLength = 2, ErrorMessage = "撤销原因长度必须为 2-500 个字符")]
    public string Reason { get; set; } = string.Empty;
}
