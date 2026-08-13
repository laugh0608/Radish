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
    [Required(ErrorMessage = "调整原因不能为空")]
    [MaxLength(500, ErrorMessage = "调整原因不能超过500个字符")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>操作者读取到的经验聚合版本。</summary>
    [Range(0, int.MaxValue, ErrorMessage = "经验版本不能小于0")]
    public int ExpectedVersion { get; set; }

    /// <summary>本次非幂等资产写入的客户端幂等键。</summary>
    [Required(ErrorMessage = "幂等键不能为空")]
    [MaxLength(80, ErrorMessage = "幂等键不能超过80个字符")]
    public string IdempotencyKey { get; set; } = string.Empty;
}
