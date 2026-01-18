using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>
/// 管理员调整经验值请求DTO
/// </summary>
public class AdminAdjustExpDto
{
    /// <summary>用户 ID</summary>
    [Required(ErrorMessage = "用户ID不能为空")]
    [Range(1, long.MaxValue, ErrorMessage = "用户ID必须大于0")]
    public long UserId { get; set; }

    /// <summary>经验值变动量（正数=增加，负数=减少）</summary>
    [Required(ErrorMessage = "经验值变动量不能为空")]
    public int DeltaExp { get; set; }

    /// <summary>调整原因</summary>
    [MaxLength(500, ErrorMessage = "调整原因不能超过500个字符")]
    public string? Reason { get; set; }
}