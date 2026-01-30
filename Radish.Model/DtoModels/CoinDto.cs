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

/// <summary>
/// 转账请求对象
/// </summary>
public class TransferDto
{
    /// <summary>
    /// 收款人用户 ID
    /// </summary>
    [Required(ErrorMessage = "收款人ID不能为空")]
    public long ToUserId { get; set; }

    /// <summary>
    /// 转账金额（胡萝卜）
    /// </summary>
    [Required(ErrorMessage = "转账金额不能为空")]
    [Range(1, long.MaxValue, ErrorMessage = "转账金额必须大于0")]
    public long Amount { get; set; }

    /// <summary>
    /// 备注信息
    /// </summary>
    [StringLength(200, ErrorMessage = "备注不能超过200个字符")]
    public string? Remark { get; set; }

    /// <summary>
    /// 支付密码
    /// </summary>
    [Required(ErrorMessage = "支付密码不能为空")]
    [StringLength(100, ErrorMessage = "支付密码格式错误")]
    public string PaymentPassword { get; set; } = string.Empty;
}