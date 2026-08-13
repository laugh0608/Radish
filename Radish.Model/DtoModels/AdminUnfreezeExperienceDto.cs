using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>
/// 管理员解冻用户经验值请求 DTO
/// </summary>
public class AdminUnfreezeExperienceDto
{
    /// <summary>用户 ID</summary>
    [Required(ErrorMessage = "用户ID不能为空")]
    [Range(1, long.MaxValue, ErrorMessage = "用户ID必须大于0")]
    public long UserId { get; set; }

    /// <summary>解冻原因。</summary>
    [Required(ErrorMessage = "解冻原因不能为空")]
    [MaxLength(500, ErrorMessage = "解冻原因不能超过500个字符")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>操作者读取到的经验聚合版本。</summary>
    [Range(0, int.MaxValue, ErrorMessage = "经验版本不能小于0")]
    public int ExpectedVersion { get; set; }
}
