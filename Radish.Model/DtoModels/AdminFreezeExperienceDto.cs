using System;
using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>
/// 管理员冻结用户经验值请求 DTO
/// </summary>
public class AdminFreezeExperienceDto
{
    /// <summary>用户 ID</summary>
    [Required(ErrorMessage = "用户ID不能为空")]
    [Range(1, long.MaxValue, ErrorMessage = "用户ID必须大于0")]
    public long UserId { get; set; }

    /// <summary>冻结到期时间，留空表示永久冻结</summary>
    public DateTime? FrozenUntil { get; set; }

    /// <summary>冻结原因</summary>
    [Required(ErrorMessage = "冻结原因不能为空")]
    [MaxLength(500, ErrorMessage = "冻结原因不能超过500个字符")]
    public string Reason { get; set; } = string.Empty;
}
