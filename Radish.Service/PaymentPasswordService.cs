using AutoMapper;
using Microsoft.Extensions.Logging;
using Radish.Common.Utils;
using Radish.IRepository;
using Radish.IService;
using Radish.Model.Models;
using Radish.Model.ViewModels;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Radish.Service;

/// <summary>
/// 支付密码服务实现
/// </summary>
public class PaymentPasswordService : IPaymentPasswordService
{
    private readonly IPaymentPasswordRepository _paymentPasswordRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<PaymentPasswordService> _logger;

    // 安全配置常量
    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes = 30;
    private const int MinPasswordLength = 6;
    private const int MaxPasswordLength = 20;

    public PaymentPasswordService(
        IPaymentPasswordRepository paymentPasswordRepository,
        IMapper mapper,
        ILogger<PaymentPasswordService> logger)
    {
        _paymentPasswordRepository = paymentPasswordRepository;
        _mapper = mapper;
        _logger = logger;
    }

    /// <summary>
    /// 获取用户支付密码状态
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>支付密码状态</returns>
    public async Task<UserPaymentPasswordVo?> GetPaymentPasswordStatusAsync(long userId)
    {
        try
        {
            var paymentPassword = await _paymentPasswordRepository.GetByUserIdAsync(userId);

            if (paymentPassword == null)
            {
                // 用户未设置支付密码，返回默认状态
                return new UserPaymentPasswordVo
                {
                    VoUserId = userId,
                    VoHasPaymentPassword = false,
                    VoIsEnabled = false,
                    VoFailedAttempts = 0,
                    VoIsLocked = false,
                    VoStrengthLevel = 0,
                    VoSecurityStatus = "未设置",
                    VoSecuritySuggestions = await GenerateSecuritySuggestionsAsync(userId)
                };
            }

            var vo = _mapper.Map<UserPaymentPasswordVo>(paymentPassword);

            // 设置显示字段
            vo.VoLastUsedTimeDisplay = paymentPassword.LastUsedTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "从未使用";
            vo.VoLastModifiedTimeDisplay = paymentPassword.LastModifiedTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "未知";
            vo.VoCreatedAtDisplay = paymentPassword.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            vo.VoStrengthLevelDisplay = GetStrengthLevelDisplay(paymentPassword.StrengthLevel);
            vo.VoSecurityStatus = GetSecurityStatus(paymentPassword);
            vo.VoSecuritySuggestions = await GenerateSecuritySuggestionsAsync(userId);

            return vo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户支付密码状态失败，用户ID: {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// 设置支付密码
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="request">设置请求</param>
    /// <returns>设置结果</returns>
    public async Task<bool> SetPaymentPasswordAsync(long userId, SetPaymentPasswordRequest request)
    {
        try
        {
            // 验证请求参数
            if (string.IsNullOrWhiteSpace(request.NewPassword))
                throw new ArgumentException("新密码不能为空");

            if (request.NewPassword != request.ConfirmPassword)
                throw new ArgumentException("两次输入的密码不一致");

            if (!IsValidPassword(request.NewPassword))
                throw new ArgumentException($"密码格式不正确，长度应为{MinPasswordLength}-{MaxPasswordLength}位");

            // 检查是否已设置支付密码
            var existingPassword = await _paymentPasswordRepository.GetByUserIdAsync(userId);
            if (existingPassword != null && !string.IsNullOrEmpty(existingPassword.PasswordHash))
            {
                throw new InvalidOperationException("用户已设置支付密码，请使用修改密码功能");
            }

            // 生成盐值和哈希
            var salt = GenerateSalt();
            var passwordHash = HashPassword(request.NewPassword, salt);
            var strengthLevel = CheckPasswordStrength(request.NewPassword);

            if (existingPassword != null)
            {
                // 更新现有记录
                existingPassword.PasswordHash = passwordHash;
                existingPassword.Salt = salt;
                existingPassword.StrengthLevel = strengthLevel;
                existingPassword.IsEnabled = true;
                existingPassword.FailedAttempts = 0;
                existingPassword.LockedUntil = null;
                existingPassword.LastModifiedTime = DateTime.Now;
                existingPassword.ModifiedAt = DateTime.Now;

                await _paymentPasswordRepository.UpdateAsync(existingPassword);
            }
            else
            {
                // 创建新记录
                var newPaymentPassword = new UserPaymentPassword
                {
                    Id = SnowFlakeSingle.Instance.NextId(),
                    UserId = userId,
                    PasswordHash = passwordHash,
                    Salt = salt,
                    StrengthLevel = strengthLevel,
                    IsEnabled = true,
                    FailedAttempts = 0,
                    CreatedAt = DateTime.Now,
                    ModifiedAt = DateTime.Now,
                    LastModifiedTime = DateTime.Now
                };

                await _paymentPasswordRepository.AddAsync(newPaymentPassword);
            }

            _logger.LogInformation("用户设置支付密码成功，用户ID: {UserId}, 强度等级: {StrengthLevel}", userId, strengthLevel);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置支付密码失败，用户ID: {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// 修改支付密码
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="request">修改请求</param>
    /// <returns>修改结果</returns>
    public async Task<bool> ChangePaymentPasswordAsync(long userId, ChangePaymentPasswordRequest request)
    {
        try
        {
            // 验证请求参数
            if (string.IsNullOrWhiteSpace(request.CurrentPassword))
                throw new ArgumentException("当前密码不能为空");

            if (string.IsNullOrWhiteSpace(request.NewPassword))
                throw new ArgumentException("新密码不能为空");

            if (request.NewPassword != request.ConfirmPassword)
                throw new ArgumentException("两次输入的新密码不一致");

            if (!IsValidPassword(request.NewPassword))
                throw new ArgumentException($"新密码格式不正确，长度应为{MinPasswordLength}-{MaxPasswordLength}位");

            // 获取现有支付密码
            var paymentPassword = await _paymentPasswordRepository.GetByUserIdAsync(userId);
            if (paymentPassword == null || string.IsNullOrEmpty(paymentPassword.PasswordHash))
            {
                throw new InvalidOperationException("用户未设置支付密码");
            }

            // 验证当前密码
            var verifyResult = await VerifyPaymentPasswordAsync(userId, new VerifyPaymentPasswordRequest
            {
                Password = request.CurrentPassword,
                BusinessType = "CHANGE_PASSWORD"
            });

            if (!verifyResult.IsSuccess)
            {
                throw new UnauthorizedAccessException(verifyResult.ErrorMessage ?? "当前密码验证失败");
            }

            // 生成新的盐值和哈希
            var salt = GenerateSalt();
            var passwordHash = HashPassword(request.NewPassword, salt);
            var strengthLevel = CheckPasswordStrength(request.NewPassword);

            // 更新密码
            paymentPassword.PasswordHash = passwordHash;
            paymentPassword.Salt = salt;
            paymentPassword.StrengthLevel = strengthLevel;
            paymentPassword.FailedAttempts = 0;
            paymentPassword.LockedUntil = null;
            paymentPassword.LastModifiedTime = DateTime.Now;
            paymentPassword.ModifiedAt = DateTime.Now;

            await _paymentPasswordRepository.UpdateAsync(paymentPassword);

            _logger.LogInformation("用户修改支付密码成功，用户ID: {UserId}, 新强度等级: {StrengthLevel}", userId, strengthLevel);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "修改支付密码失败，用户ID: {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// 验证支付密码
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="request">验证请求</param>
    /// <returns>验证结果</returns>
    public async Task<PaymentPasswordVerifyResult> VerifyPaymentPasswordAsync(long userId, VerifyPaymentPasswordRequest request)
    {
        try
        {
            var paymentPassword = await _paymentPasswordRepository.GetByUserIdAsync(userId);

            if (paymentPassword == null || string.IsNullOrEmpty(paymentPassword.PasswordHash))
            {
                return new PaymentPasswordVerifyResult
                {
                    IsSuccess = false,
                    ErrorMessage = "用户未设置支付密码",
                    RemainingAttempts = 0
                };
            }

            // 检查是否被锁定
            if (paymentPassword.LockedUntil.HasValue && paymentPassword.LockedUntil.Value > DateTime.Now)
            {
                var remainingMinutes = (int)(paymentPassword.LockedUntil.Value - DateTime.Now).TotalMinutes;
                return new PaymentPasswordVerifyResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"账户已被锁定，请{remainingMinutes}分钟后重试",
                    IsLocked = true,
                    LockedRemainingMinutes = remainingMinutes,
                    RemainingAttempts = 0
                };
            }

            // 验证密码
            var isPasswordCorrect = VerifyPassword(request.Password, paymentPassword.PasswordHash, paymentPassword.Salt);

            if (isPasswordCorrect)
            {
                // 密码正确，重置失败次数并更新最后使用时间
                await _paymentPasswordRepository.ResetFailedAttemptsAsync(userId);
                await _paymentPasswordRepository.UpdateLastUsedTimeAsync(userId);

                _logger.LogInformation("支付密码验证成功，用户ID: {UserId}, 业务类型: {BusinessType}",
                    userId, request.BusinessType ?? "UNKNOWN");

                return new PaymentPasswordVerifyResult
                {
                    IsSuccess = true,
                    RemainingAttempts = MaxFailedAttempts
                };
            }
            else
            {
                // 密码错误，增加失败次数
                var newFailedAttempts = paymentPassword.FailedAttempts + 1;
                DateTime? lockedUntil = null;

                if (newFailedAttempts >= MaxFailedAttempts)
                {
                    lockedUntil = DateTime.Now.AddMinutes(LockoutMinutes);
                }

                await _paymentPasswordRepository.UpdateFailedAttemptsAsync(userId, newFailedAttempts, lockedUntil);

                var remainingAttempts = Math.Max(0, MaxFailedAttempts - newFailedAttempts);
                var errorMessage = lockedUntil.HasValue
                    ? $"密码错误次数过多，账户已被锁定{LockoutMinutes}分钟"
                    : $"支付密码错误，还可尝试{remainingAttempts}次";

                _logger.LogWarning("支付密码验证失败，用户ID: {UserId}, 失败次数: {FailedAttempts}, 业务类型: {BusinessType}",
                    userId, newFailedAttempts, request.BusinessType ?? "UNKNOWN");

                return new PaymentPasswordVerifyResult
                {
                    IsSuccess = false,
                    ErrorMessage = errorMessage,
                    RemainingAttempts = remainingAttempts,
                    IsLocked = lockedUntil.HasValue,
                    LockedRemainingMinutes = lockedUntil.HasValue ? LockoutMinutes : 0
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证支付密码失败，用户ID: {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// 重置支付密码（管理员操作）
    /// </summary>
    /// <param name="adminUserId">管理员用户ID</param>
    /// <param name="request">重置请求</param>
    /// <returns>重置结果</returns>
    public async Task<bool> ResetPaymentPasswordAsync(long adminUserId, ResetPaymentPasswordRequest request)
    {
        try
        {
            var targetUserId = request.UserId ?? throw new ArgumentException("目标用户ID不能为空");

            var paymentPassword = await _paymentPasswordRepository.GetByUserIdAsync(targetUserId);
            if (paymentPassword == null)
            {
                throw new InvalidOperationException("目标用户未设置支付密码");
            }

            // 重置密码（清空密码哈希，用户需要重新设置）
            paymentPassword.PasswordHash = string.Empty;
            paymentPassword.Salt = string.Empty;
            paymentPassword.FailedAttempts = 0;
            paymentPassword.LockedUntil = null;
            paymentPassword.StrengthLevel = 0;
            paymentPassword.IsEnabled = false;
            paymentPassword.LastModifiedTime = DateTime.Now;
            paymentPassword.ModifiedAt = DateTime.Now;
            paymentPassword.Remark = $"管理员重置 - {request.Reason} - 操作人: {adminUserId}";

            await _paymentPasswordRepository.UpdateAsync(paymentPassword);

            _logger.LogWarning("管理员重置用户支付密码，管理员ID: {AdminUserId}, 目标用户ID: {TargetUserId}, 原因: {Reason}",
                adminUserId, targetUserId, request.Reason);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重置支付密码失败，管理员ID: {AdminUserId}, 目标用户ID: {TargetUserId}",
                adminUserId, request.UserId);
            throw;
        }
    }

    /// <summary>
    /// 解锁用户支付密码
    /// </summary>
    /// <param name="adminUserId">管理员用户ID</param>
    /// <param name="targetUserId">目标用户ID</param>
    /// <param name="reason">解锁原因</param>
    /// <returns>解锁结果</returns>
    public async Task<bool> UnlockPaymentPasswordAsync(long adminUserId, long targetUserId, string reason)
    {
        try
        {
            var result = await _paymentPasswordRepository.ResetFailedAttemptsAsync(targetUserId);

            if (result)
            {
                _logger.LogWarning("管理员解锁用户支付密码，管理员ID: {AdminUserId}, 目标用户ID: {TargetUserId}, 原因: {Reason}",
                    adminUserId, targetUserId, reason);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解锁支付密码失败，管理员ID: {AdminUserId}, 目标用户ID: {TargetUserId}",
                adminUserId, targetUserId);
            throw;
        }
    }

    /// <summary>
    /// 获取支付密码统计信息
    /// </summary>
    /// <returns>统计信息</returns>
    public async Task<object> GetPaymentPasswordStatsAsync()
    {
        try
        {
            var totalUsers = await _paymentPasswordRepository.QueryCountAsync(p => p.IsEnabled);
            var lockedUsers = await _paymentPasswordRepository.GetLockedUsersCountAsync();
            var usersWithPassword = await _paymentPasswordRepository.QueryCountAsync(p => p.IsEnabled && !string.IsNullOrEmpty(p.PasswordHash));

            return new
            {
                TotalUsers = totalUsers,
                UsersWithPassword = usersWithPassword,
                LockedUsers = lockedUsers,
                PasswordSetupRate = totalUsers > 0 ? (double)usersWithPassword / totalUsers * 100 : 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取支付密码统计信息失败");
            throw;
        }
    }

    /// <summary>
    /// 清理过期的锁定状态
    /// </summary>
    /// <returns>清理的记录数</returns>
    public async Task<int> ClearExpiredLocksAsync()
    {
        try
        {
            var clearedCount = await _paymentPasswordRepository.ClearExpiredLocksAsync();

            if (clearedCount > 0)
            {
                _logger.LogInformation("清理过期锁定状态完成，清理记录数: {ClearedCount}", clearedCount);
            }

            return clearedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理过期锁定状态失败");
            throw;
        }
    }

    /// <summary>
    /// 检查密码强度
    /// </summary>
    /// <param name="password">密码</param>
    /// <returns>强度等级 (1-5)</returns>
    public int CheckPasswordStrength(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return 0;

        var score = 0;

        // 长度检查
        if (password.Length >= 8) score++;
        if (password.Length >= 12) score++;

        // 字符类型检查
        if (Regex.IsMatch(password, @"[a-z]")) score++; // 小写字母
        if (Regex.IsMatch(password, @"[A-Z]")) score++; // 大写字母
        if (Regex.IsMatch(password, @"\d")) score++;    // 数字
        if (Regex.IsMatch(password, @"[!@#$%^&*(),.?""':;|<>]")) score++; // 特殊字符

        // 复杂度检查
        if (password.Length >= 10 && Regex.IsMatch(password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*])"))
            score++;

        return Math.Min(5, Math.Max(1, score));
    }

    /// <summary>
    /// 生成安全建议
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>安全建议列表</returns>
    public async Task<List<string>> GenerateSecuritySuggestionsAsync(long userId)
    {
        var suggestions = new List<string>();

        try
        {
            var paymentPassword = await _paymentPasswordRepository.GetByUserIdAsync(userId);

            if (paymentPassword == null || string.IsNullOrEmpty(paymentPassword.PasswordHash))
            {
                suggestions.Add("建议设置支付密码以保护账户安全");
                suggestions.Add("支付密码应包含数字、字母和特殊字符");
                suggestions.Add("密码长度建议8位以上");
                return suggestions;
            }

            // 密码强度建议
            if (paymentPassword.StrengthLevel < 3)
            {
                suggestions.Add("当前密码强度较低，建议使用更复杂的密码");
            }

            // 使用时间建议
            if (paymentPassword.LastModifiedTime.HasValue)
            {
                var daysSinceLastChange = (DateTime.Now - paymentPassword.LastModifiedTime.Value).Days;
                if (daysSinceLastChange > 90)
                {
                    suggestions.Add("建议定期更换支付密码，上次修改已超过90天");
                }
            }

            // 失败次数建议
            if (paymentPassword.FailedAttempts > 0)
            {
                suggestions.Add("检测到密码输入错误记录，请确保密码安全");
            }

            // 通用安全建议
            if (suggestions.Count == 0)
            {
                suggestions.Add("支付密码安全状态良好");
                suggestions.Add("建议定期更换密码以保持安全");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成安全建议失败，用户ID: {UserId}", userId);
            suggestions.Add("无法获取安全建议，请稍后重试");
        }

        return suggestions;
    }

    #region 私有方法

    /// <summary>
    /// 验证密码格式
    /// </summary>
    /// <param name="password">密码</param>
    /// <returns>是否有效</returns>
    private static bool IsValidPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return false;

        if (password.Length < MinPasswordLength || password.Length > MaxPasswordLength)
            return false;

        // 至少包含数字
        if (!Regex.IsMatch(password, @"\d"))
            return false;

        return true;
    }

    /// <summary>
    /// 生成盐值
    /// </summary>
    /// <returns>盐值</returns>
    private static string GenerateSalt()
    {
        var saltBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(saltBytes);
        return Convert.ToBase64String(saltBytes);
    }

    /// <summary>
    /// 哈希密码
    /// </summary>
    /// <param name="password">密码</param>
    /// <param name="salt">盐值</param>
    /// <returns>哈希值</returns>
    private static string HashPassword(string password, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var passwordBytes = Encoding.UTF8.GetBytes(password);

        var combinedBytes = new byte[saltBytes.Length + passwordBytes.Length];
        Buffer.BlockCopy(saltBytes, 0, combinedBytes, 0, saltBytes.Length);
        Buffer.BlockCopy(passwordBytes, 0, combinedBytes, saltBytes.Length, passwordBytes.Length);

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(combinedBytes);
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// 验证密码
    /// </summary>
    /// <param name="password">输入的密码</param>
    /// <param name="storedHash">存储的哈希值</param>
    /// <param name="salt">盐值</param>
    /// <returns>是否匹配</returns>
    private static bool VerifyPassword(string password, string storedHash, string salt)
    {
        var computedHash = HashPassword(password, salt);
        return computedHash == storedHash;
    }

    /// <summary>
    /// 获取强度等级显示
    /// </summary>
    /// <param name="level">强度等级</param>
    /// <returns>显示文本</returns>
    private static string GetStrengthLevelDisplay(int level)
    {
        return level switch
        {
            0 => "未设置",
            1 => "很弱",
            2 => "弱",
            3 => "中等",
            4 => "强",
            5 => "很强",
            _ => "未知"
        };
    }

    /// <summary>
    /// 获取安全状态
    /// </summary>
    /// <param name="paymentPassword">支付密码实体</param>
    /// <returns>安全状态</returns>
    private static string GetSecurityStatus(UserPaymentPassword paymentPassword)
    {
        if (string.IsNullOrEmpty(paymentPassword.PasswordHash))
            return "未设置";

        if (paymentPassword.LockedUntil.HasValue && paymentPassword.LockedUntil.Value > DateTime.Now)
            return "已锁定";

        if (paymentPassword.FailedAttempts > 0)
            return "有风险";

        if (paymentPassword.StrengthLevel >= 4)
            return "安全";

        if (paymentPassword.StrengthLevel >= 2)
            return "一般";

        return "较弱";
    }

    #endregion
}