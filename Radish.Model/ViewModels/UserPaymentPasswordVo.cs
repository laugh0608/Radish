namespace Radish.Model.ViewModels;

/// <summary>
/// 用户支付密码视图模型
/// </summary>
public class UserPaymentPasswordVo
{
    /// <summary>
    /// 主键ID
    /// </summary>
    public long VoId { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    public long VoUserId { get; set; }

    /// <summary>
    /// 失败尝试次数
    /// </summary>
    public int VoFailedAttempts { get; set; }

    /// <summary>
    /// 是否被锁定
    /// </summary>
    public bool VoIsLocked { get; set; }

    /// <summary>
    /// 锁定到期时间
    /// </summary>
    public DateTime? VoLockedUntil { get; set; }

    /// <summary>
    /// 锁定剩余时间（分钟）
    /// </summary>
    public int VoLockedRemainingMinutes { get; set; }

    /// <summary>
    /// 最后使用时间
    /// </summary>
    public DateTime? VoLastUsedTime { get; set; }

    /// <summary>
    /// 最后使用时间显示
    /// </summary>
    public string VoLastUsedTimeDisplay { get; set; } = string.Empty;

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTime? VoLastModifiedTime { get; set; }

    /// <summary>
    /// 最后修改时间显示
    /// </summary>
    public string VoLastModifiedTimeDisplay { get; set; } = string.Empty;

    /// <summary>
    /// 密码强度等级
    /// </summary>
    public int VoStrengthLevel { get; set; }

    /// <summary>
    /// 密码强度等级显示
    /// </summary>
    public string VoStrengthLevelDisplay { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool VoIsEnabled { get; set; }

    /// <summary>
    /// 是否已设置支付密码
    /// </summary>
    public bool VoHasPaymentPassword { get; set; }

    /// <summary>
    /// 安全状态
    /// </summary>
    public string VoSecurityStatus { get; set; } = string.Empty;

    /// <summary>
    /// 安全建议
    /// </summary>
    public List<string> VoSecuritySuggestions { get; set; } = new();

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime VoCreatedAt { get; set; }

    /// <summary>
    /// 创建时间显示
    /// </summary>
    public string VoCreatedAtDisplay { get; set; } = string.Empty;
}

/// <summary>
/// 支付密码设置请求模型
/// </summary>
public class SetPaymentPasswordRequest
{
    /// <summary>
    /// 新密码
    /// </summary>
    public string NewPassword { get; set; } = string.Empty;

    /// <summary>
    /// 确认密码
    /// </summary>
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>
/// 支付密码修改请求模型
/// </summary>
public class ChangePaymentPasswordRequest
{
    /// <summary>
    /// 当前密码
    /// </summary>
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>
    /// 新密码
    /// </summary>
    public string NewPassword { get; set; } = string.Empty;

    /// <summary>
    /// 确认密码
    /// </summary>
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>
/// 支付密码验证请求模型
/// </summary>
public class VerifyPaymentPasswordRequest
{
    /// <summary>
    /// 支付密码
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 业务类型（可选，用于审计）
    /// </summary>
    public string? BusinessType { get; set; }

    /// <summary>
    /// 业务ID（可选，用于审计）
    /// </summary>
    public string? BusinessId { get; set; }
}

/// <summary>
/// 支付密码验证结果
/// </summary>
public class PaymentPasswordVerifyResult
{
    /// <summary>
    /// 验证是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 剩余尝试次数
    /// </summary>
    public int RemainingAttempts { get; set; }

    /// <summary>
    /// 是否被锁定
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// 锁定剩余时间（分钟）
    /// </summary>
    public int LockedRemainingMinutes { get; set; }
}

/// <summary>
/// 支付密码重置请求模型
/// </summary>
public class ResetPaymentPasswordRequest
{
    /// <summary>
    /// 用户ID（管理员操作时使用）
    /// </summary>
    public long? UserId { get; set; }

    /// <summary>
    /// 重置原因
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// 密码强度检查结果视图模型
/// </summary>
public class PasswordStrengthVo
{
    /// <summary>
    /// 强度等级（0-5）
    /// </summary>
    public int VoLevel { get; set; }

    /// <summary>
    /// 强度等级显示文本
    /// </summary>
    public string VoDisplay { get; set; } = string.Empty;

    /// <summary>
    /// 密码是否有效
    /// </summary>
    public bool VoIsValid { get; set; }
}

/// <summary>
/// 支付密码统计信息视图模型
/// </summary>
public class PaymentPasswordStatsVo
{
    /// <summary>
    /// 总用户数
    /// </summary>
    public int VoTotalUsers { get; set; }

    /// <summary>
    /// 已设置密码的用户数
    /// </summary>
    public int VoUsersWithPassword { get; set; }

    /// <summary>
    /// 被锁定的用户数
    /// </summary>
    public int VoLockedUsers { get; set; }

    /// <summary>
    /// 密码设置率（百分比）
    /// </summary>
    public double VoPasswordSetupRate { get; set; }
}