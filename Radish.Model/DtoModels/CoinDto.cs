using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>
/// 管理员调整余额请求对象
/// </summary>
public class AdminAdjustBalanceDto
{
    /// <summary>
    /// 用户 ID
    /// </summary>
    [Required(ErrorMessage = "用户ID不能为空")]
    public long UserId { get; set; }

    /// <summary>
    /// 变动金额（胡萝卜）
    /// </summary>
    /// <remarks>正数表示增加，负数表示减少</remarks>
    [Required(ErrorMessage = "变动金额不能为空")]
    public long DeltaAmount { get; set; }

    /// <summary>
    /// 调整原因
    /// </summary>
    [Required(ErrorMessage = "调整原因不能为空")]
    [StringLength(200, ErrorMessage = "调整原因不能超过200个字符")]
    public string Reason { get; set; } = string.Empty;
}