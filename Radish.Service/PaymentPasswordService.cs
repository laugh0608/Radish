using AutoMapper;
using Microsoft.Extensions.Logging;
using Radish.Common.HelpTool;
using Radish.Common.Utils;
using Radish.Common;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.Models;
using Radish.Model.ViewModels;
using Radish.Shared.Security;
using SqlSugar;
using System.Security.Cryptography;
using System.Text;

namespace Radish.Service;

/// <summary>
/// 支付密码服务实现
/// </summary>
public class PaymentPasswordService : IPaymentPasswordService
{
    private readonly IPaymentPasswordRepository _paymentPasswordRepository;
    private readonly IAuditLogService _auditLogService;
    private readonly IMapper _mapper;
    private readonly ILogger<PaymentPasswordService> _logger;

    // 安全配置常量
    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes = 30;

    public PaymentPasswordService(
        IPaymentPasswordRepository paymentPasswordRepository,
        IAuditLogService auditLogService,
        IMapper mapper,
        ILogger<PaymentPasswordService> logger)
    {
        _paymentPasswordRepository = paymentPasswordRepository;
        _auditLogService = auditLogService;
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
                    VoIsLegacyPasscode = false,
                    VoRequiresPasscodeUpgrade = false,
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
            vo.VoCreatedAtDisplay = paymentPassword.CreateTime.ToString("yyyy-MM-dd HH:mm:ss");
            vo.VoStrengthLevelDisplay = GetStrengthLevelDisplay(paymentPassword.StrengthLevel);
            vo.VoSecurityStatus = GetSecurityStatus(paymentPassword);
            vo.VoSecuritySuggestions = await GenerateSecuritySuggestionsAsync(userId);

            return vo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户支付口令状态失败，用户ID: {UserId}", userId);
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
            EnsureNewPasscodeCanBeSaved(request.NewPassword, "新支付口令不能为空");

            if (request.NewPassword != request.ConfirmPassword)
                throw new ArgumentException("两次输入的支付口令不一致");

            // 检查是否已设置支付密码
            var existingPassword = await _paymentPasswordRepository.GetByUserIdAsync(userId);
            if (existingPassword != null
                && !string.IsNullOrEmpty(existingPassword.PasswordHash)
                && !HasLegacyPasscode(existingPassword))
            {
                throw new InvalidOperationException("用户已设置支付口令，请使用修改口令功能");
            }

            // 当前版本使用 Argon2id 自带盐值编码，旧 Salt 字段保留为空字符串兼容表结构。
            var passwordHash = HashCurrentPassword(request.NewPassword);
            var strengthLevel = CheckPasswordStrength(request.NewPassword);

            if (existingPassword != null)
            {
                // 更新现有记录
                existingPassword.PasswordHash = passwordHash;
                existingPassword.Salt = string.Empty;
                existingPassword.StrengthLevel = strengthLevel;
                existingPassword.PasscodeVersion = PaymentPasscodeRules.CurrentPasscodeVersion;
                existingPassword.IsEnabled = true;
                existingPassword.FailedAttempts = 0;
                existingPassword.LockedUntil = null;
                existingPassword.LastModifiedTime = DateTime.Now;
                existingPassword.ModifyTime = DateTime.Now;
                existingPassword.ModifyBy = "System";
                existingPassword.ModifyId = 0;

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
                    Salt = string.Empty,
                    StrengthLevel = strengthLevel,
                    PasscodeVersion = PaymentPasscodeRules.CurrentPasscodeVersion,
                    IsEnabled = true,
                    FailedAttempts = 0,
                    CreateTime = DateTime.Now,
                    CreateBy = "System",
                    CreateId = 0,
                    LastModifiedTime = DateTime.Now
                };

                await _paymentPasswordRepository.AddAsync(newPaymentPassword);
            }

            _logger.LogInformation("用户设置支付口令成功，用户ID: {UserId}, 强度等级: {StrengthLevel}", userId, strengthLevel);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置支付口令失败，用户ID: {UserId}", userId);
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
                throw new ArgumentException("当前支付口令不能为空");

            EnsureNewPasscodeCanBeSaved(request.NewPassword, "新支付口令不能为空");

            if (request.NewPassword != request.ConfirmPassword)
                throw new ArgumentException("两次输入的新支付口令不一致");

            // 获取现有支付密码
            var paymentPassword = await _paymentPasswordRepository.GetByUserIdAsync(userId);
            if (paymentPassword == null || string.IsNullOrEmpty(paymentPassword.PasswordHash))
            {
                throw new InvalidOperationException("用户未设置支付口令");
            }

            if (HasLegacyPasscode(paymentPassword))
            {
                throw new InvalidOperationException(PaymentPasscodeRules.UpgradeRequiredErrorMessage);
            }

            // 验证当前密码
            var verifyResult = await VerifyPaymentPasswordAsync(userId, new VerifyPaymentPasswordRequest
            {
                Password = request.CurrentPassword,
                BusinessType = "CHANGE_PASSWORD"
            });

            if (!verifyResult.IsSuccess)
            {
                throw new UnauthorizedAccessException(verifyResult.ErrorMessage ?? "当前支付口令验证失败");
            }

            // 当前版本使用 Argon2id 自带盐值编码，旧 Salt 字段保留为空字符串兼容表结构。
            var passwordHash = HashCurrentPassword(request.NewPassword);
            var strengthLevel = CheckPasswordStrength(request.NewPassword);

            // 更新密码
            paymentPassword.PasswordHash = passwordHash;
            paymentPassword.Salt = string.Empty;
            paymentPassword.StrengthLevel = strengthLevel;
            paymentPassword.PasscodeVersion = PaymentPasscodeRules.CurrentPasscodeVersion;
            paymentPassword.FailedAttempts = 0;
            paymentPassword.LockedUntil = null;
            paymentPassword.LastModifiedTime = DateTime.Now;
            paymentPassword.ModifyTime = DateTime.Now;
            paymentPassword.ModifyBy = "System";
            paymentPassword.ModifyId = 0;

            await _paymentPasswordRepository.UpdateAsync(paymentPassword);

            _logger.LogInformation("用户修改支付口令成功，用户ID: {UserId}, 新强度等级: {StrengthLevel}", userId, strengthLevel);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "修改支付口令失败，用户ID: {UserId}", userId);
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
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return new PaymentPasswordVerifyResult
                {
                    IsSuccess = false,
                    ErrorMessage = PaymentPasscodeRules.EmptyErrorMessage,
                    RemainingAttempts = MaxFailedAttempts
                };
            }

            var paymentPassword = await _paymentPasswordRepository.GetByUserIdAsync(userId);

            if (paymentPassword == null || string.IsNullOrEmpty(paymentPassword.PasswordHash))
            {
                return new PaymentPasswordVerifyResult
                {
                    IsSuccess = false,
                    ErrorMessage = "用户未设置支付口令",
                    RemainingAttempts = 0
                };
            }

            if (HasLegacyPasscode(paymentPassword))
            {
                return CreateUpgradeRequiredResult();
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
            var isPasswordCorrect = VerifyStoredPassword(request.Password, paymentPassword);

            if (isPasswordCorrect)
            {
                if (PaymentPasscodeRules.CanVerifyAndUpgrade(paymentPassword.PasscodeVersion))
                {
                    await UpgradePaymentPasswordHashAsync(paymentPassword, request.Password);
                }

                // 密码正确，重置失败次数并更新最后使用时间
                await _paymentPasswordRepository.ResetFailedAttemptsAsync(userId);
                await _paymentPasswordRepository.UpdateLastUsedTimeAsync(userId);

                _logger.LogInformation("支付口令验证成功，用户ID: {UserId}, 业务类型: {BusinessType}",
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
                    ? $"支付口令错误次数过多，账户已被锁定{LockoutMinutes}分钟"
                    : $"支付口令错误，还可尝试{remainingAttempts}次";

                _logger.LogWarning("支付口令验证失败，用户ID: {UserId}, 失败次数: {FailedAttempts}, 业务类型: {BusinessType}",
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
            _logger.LogError(ex, "验证支付口令失败，用户ID: {UserId}", userId);
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
                throw new InvalidOperationException("目标用户未设置支付口令");
            }

            // 重置密码（清空密码哈希，用户需要重新设置）
            paymentPassword.PasswordHash = string.Empty;
            paymentPassword.Salt = string.Empty;
            paymentPassword.FailedAttempts = 0;
            paymentPassword.LockedUntil = null;
            paymentPassword.StrengthLevel = 0;
            paymentPassword.PasscodeVersion = null;
            paymentPassword.IsEnabled = false;
            paymentPassword.LastModifiedTime = DateTime.Now;
            paymentPassword.ModifyTime = DateTime.Now;
            paymentPassword.ModifyBy = $"Admin_{adminUserId}";
            paymentPassword.ModifyId = adminUserId;
            paymentPassword.Remark = $"管理员重置 - {request.Reason} - 操作人: {adminUserId}";

            await _paymentPasswordRepository.UpdateAsync(paymentPassword);

            _logger.LogWarning("管理员重置用户支付口令，管理员ID: {AdminUserId}, 目标用户ID: {TargetUserId}, 原因: {Reason}",
                adminUserId, targetUserId, request.Reason);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重置支付口令失败，管理员ID: {AdminUserId}, 目标用户ID: {TargetUserId}",
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
                _logger.LogWarning("管理员解锁用户支付口令，管理员ID: {AdminUserId}, 目标用户ID: {TargetUserId}, 原因: {Reason}",
                    adminUserId, targetUserId, reason);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解锁支付口令失败，管理员ID: {AdminUserId}, 目标用户ID: {TargetUserId}",
                adminUserId, targetUserId);
            throw;
        }
    }

    /// <summary>
    /// 获取支付密码统计信息
    /// </summary>
    /// <returns>统计信息</returns>
    public async Task<PaymentPasswordStatsVo> GetPaymentPasswordStatsAsync()
    {
        try
        {
            var totalUsers = await _paymentPasswordRepository.QueryCountAsync(p => p.IsEnabled);
            var lockedUsers = await _paymentPasswordRepository.GetLockedUsersCountAsync();
            var usersWithPassword = await _paymentPasswordRepository.QueryCountAsync(p => p.IsEnabled && !string.IsNullOrEmpty(p.PasswordHash));

            return new PaymentPasswordStatsVo
            {
                VoTotalUsers = totalUsers,
                VoUsersWithPassword = usersWithPassword,
                VoLockedUsers = lockedUsers,
                VoPasswordSetupRate = totalUsers > 0 ? (double)usersWithPassword / totalUsers * 100 : 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取支付口令统计信息失败");
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
        return PaymentPasscodeRules.GetStrengthLevel(password);
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
                suggestions.Add("建议设置支付口令以保护账户安全");
                suggestions.Add("支付口令必须为 6 位数字，且不能为 6 个相同数字");
                suggestions.Add("建议避免使用连续数字或过于简单的数字组合");
                return suggestions;
            }

            if (HasLegacyPasscode(paymentPassword))
            {
                suggestions.Add(PaymentPasscodeRules.UpgradeRequiredSuggestion);
                suggestions.Add("旧支付口令已不再支持商城购买和萝卜转移，请在安全设置中直接重置为新的6位数字支付口令");
                return suggestions;
            }

            // 口令强度建议
            if (paymentPassword.StrengthLevel < 3)
            {
                suggestions.Add("当前支付口令较弱，建议避开连续数字或过于集中的数字组合");
            }

            // 使用时间建议
            if (paymentPassword.LastModifiedTime.HasValue)
            {
                var daysSinceLastChange = (DateTime.Now - paymentPassword.LastModifiedTime.Value).Days;
                if (daysSinceLastChange > 90)
                {
                    suggestions.Add("建议定期更换支付口令，上次修改已超过90天");
                }
            }

            // 失败次数建议
            if (paymentPassword.FailedAttempts > 0)
            {
                suggestions.Add("检测到口令输入错误记录，请确认是否存在异常操作");
            }

            // 通用安全建议
            if (suggestions.Count == 0)
            {
                suggestions.Add("支付口令安全状态良好");
                suggestions.Add("建议定期更换口令以保持安全");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成安全建议失败，用户ID: {UserId}", userId);
            suggestions.Add("无法获取安全建议，请稍后重试");
        }

        return suggestions;
    }

    /// <summary>
    /// 获取当前用户支付密码安全日志
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>支付密码安全日志分页结果</returns>
    public async Task<PageModel<PaymentPasswordSecurityLogVo>> GetSecurityLogsAsync(long userId, int pageIndex = 1, int pageSize = 20)
    {
        var safePageIndex = Math.Max(1, pageIndex);
        var safePageSize = Math.Clamp(pageSize, 1, 50);

        var auditLogs = await _auditLogService.QueryPageAsync(new AuditLogQueryDto
        {
            UserId = userId,
            RequestPath = "PaymentPassword",
            PageIndex = safePageIndex,
            PageSize = safePageSize,
            OrderBy = "DateTime",
            OrderDirection = "desc"
        });

        return new PageModel<PaymentPasswordSecurityLogVo>
        {
            Page = auditLogs.Page,
            PageSize = auditLogs.PageSize,
            DataCount = auditLogs.DataCount,
            PageCount = auditLogs.PageCount,
            Data = auditLogs.Data
                .Where(log => !IsPaymentPasswordAdminPath(log.VoRequestPath))
                .Select(MapPaymentPasswordSecurityLog)
                .ToList()
        };
    }

    #region 私有方法

    private static void EnsureNewPasscodeCanBeSaved(string? passcode, string emptyMessage)
    {
        if (string.IsNullOrWhiteSpace(passcode))
            throw new ArgumentException(emptyMessage);

        if (!PaymentPasscodeRules.IsFormatValid(passcode))
            throw new ArgumentException(PaymentPasscodeRules.FormatErrorMessage);

        if (PaymentPasscodeRules.IsRepeatedDigits(passcode))
            throw new ArgumentException(PaymentPasscodeRules.RepeatedDigitErrorMessage);
    }

    private static bool HasLegacyPasscode(UserPaymentPassword paymentPassword)
    {
        return !string.IsNullOrEmpty(paymentPassword.PasswordHash)
               && PaymentPasscodeRules.RequiresUpgrade(paymentPassword.PasscodeVersion);
    }

    private static PaymentPasswordVerifyResult CreateUpgradeRequiredResult()
    {
        return new PaymentPasswordVerifyResult
        {
            IsSuccess = false,
            ErrorCode = PaymentPasscodeErrorCodes.UpgradeRequired,
            ErrorMessage = PaymentPasscodeRules.UpgradeRequiredErrorMessage,
            RemainingAttempts = 0,
            RequiresPasscodeUpgrade = true
        };
    }

    private async Task UpgradePaymentPasswordHashAsync(UserPaymentPassword paymentPassword, string password)
    {
        paymentPassword.PasswordHash = HashCurrentPassword(password);
        paymentPassword.Salt = string.Empty;
        paymentPassword.PasscodeVersion = PaymentPasscodeRules.CurrentPasscodeVersion;
        paymentPassword.LastModifiedTime = DateTime.Now;
        paymentPassword.ModifyTime = DateTime.Now;
        paymentPassword.ModifyBy = "System";
        paymentPassword.ModifyId = 0;

        await _paymentPasswordRepository.UpdateAsync(paymentPassword);

        _logger.LogInformation("支付口令哈希已自动升级，用户ID: {UserId}, 版本: {PasscodeVersion}",
            paymentPassword.UserId, PaymentPasscodeRules.CurrentPasscodeVersion);
    }

    /// <summary>
    /// 当前版本哈希密码
    /// </summary>
    /// <param name="password">密码</param>
    /// <returns>哈希值</returns>
    private static string HashCurrentPassword(string password)
    {
        return PasswordHasher.HashPassword(password);
    }

    /// <summary>
    /// 旧版本 SHA256 支付口令哈希
    /// </summary>
    /// <param name="password">密码</param>
    /// <param name="salt">盐值</param>
    /// <returns>哈希值</returns>
    private static string HashLegacySha256Password(string password, string salt)
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
    /// <param name="paymentPassword">支付口令实体</param>
    /// <returns>是否匹配</returns>
    private static bool VerifyStoredPassword(string password, UserPaymentPassword paymentPassword)
    {
        if (PaymentPasscodeRules.UsesCurrentHashVersion(paymentPassword.PasscodeVersion))
            return PasswordHasher.VerifyPassword(password, paymentPassword.PasswordHash);

        if (PaymentPasscodeRules.CanVerifyAndUpgrade(paymentPassword.PasscodeVersion))
        {
            try
            {
                var computedHash = HashLegacySha256Password(password, paymentPassword.Salt);
                var computedBytes = Convert.FromBase64String(computedHash);
                var storedBytes = Convert.FromBase64String(paymentPassword.PasswordHash);
                return computedBytes.Length == storedBytes.Length
                       && CryptographicOperations.FixedTimeEquals(computedBytes, storedBytes);
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                return false;
            }
        }

        return false;
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
            1 => "无效",
            2 => "较弱",
            3 => "一般",
            4 => "稳妥",
            5 => "较强",
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

        if (HasLegacyPasscode(paymentPassword))
            return PaymentPasscodeRules.UpgradeRequiredStatusText;

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

    private static PaymentPasswordSecurityLogVo MapPaymentPasswordSecurityLog(AuditLogVo log)
    {
        var requestPath = log.VoRequestPath ?? string.Empty;

        return new PaymentPasswordSecurityLogVo
        {
            VoId = log.VoId,
            VoType = GetSecurityLogType(requestPath),
            VoAction = GetSecurityLogAction(requestPath),
            VoResult = log.VoIsSuccess ? "success" : "failed",
            VoIpAddress = log.VoIpAddress,
            VoUserAgent = log.VoUserAgent,
            VoCreatedAt = log.VoDateTime
        };
    }

    private static bool IsPaymentPasswordAdminPath(string? requestPath)
    {
        return requestPath?.Contains("/Admin/", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string GetSecurityLogType(string requestPath)
    {
        if (requestPath.Contains("VerifyPassword", StringComparison.OrdinalIgnoreCase))
            return "password_verify";
        if (requestPath.Contains("ChangePassword", StringComparison.OrdinalIgnoreCase))
            return "password_change";
        if (requestPath.Contains("SetPassword", StringComparison.OrdinalIgnoreCase))
            return "password_set";
        if (requestPath.Contains("UnlockPassword", StringComparison.OrdinalIgnoreCase))
            return "account_unlock";

        return "payment_password";
    }

    private static string GetSecurityLogAction(string requestPath)
    {
        if (requestPath.Contains("VerifyPassword", StringComparison.OrdinalIgnoreCase))
            return "支付口令验证";
        if (requestPath.Contains("ChangePassword", StringComparison.OrdinalIgnoreCase))
            return "修改支付口令";
        if (requestPath.Contains("SetPassword", StringComparison.OrdinalIgnoreCase))
            return "设置支付口令";
        if (requestPath.Contains("UnlockPassword", StringComparison.OrdinalIgnoreCase))
            return "解锁支付口令";

        return "支付口令操作";
    }

    #endregion
}
