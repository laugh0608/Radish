using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>
/// 支付密码服务接口
/// </summary>
public interface IPaymentPasswordService
{
    /// <summary>
    /// 获取用户支付密码状态
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>支付密码状态</returns>
    Task<UserPaymentPasswordVo?> GetPaymentPasswordStatusAsync(long userId);

    /// <summary>
    /// 设置支付密码
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="request">设置请求</param>
    /// <returns>设置结果</returns>
    Task<bool> SetPaymentPasswordAsync(long userId, SetPaymentPasswordRequest request);

    /// <summary>
    /// 修改支付密码
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="request">修改请求</param>
    /// <returns>修改结果</returns>
    Task<bool> ChangePaymentPasswordAsync(long userId, ChangePaymentPasswordRequest request);

    /// <summary>
    /// 验证支付密码
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="request">验证请求</param>
    /// <returns>验证结果</returns>
    Task<PaymentPasswordVerifyResult> VerifyPaymentPasswordAsync(long userId, VerifyPaymentPasswordRequest request);

    /// <summary>
    /// 重置支付密码（管理员操作）
    /// </summary>
    /// <param name="adminUserId">管理员用户ID</param>
    /// <param name="request">重置请求</param>
    /// <returns>重置结果</returns>
    Task<bool> ResetPaymentPasswordAsync(long adminUserId, ResetPaymentPasswordRequest request);

    /// <summary>
    /// 解锁用户支付密码
    /// </summary>
    /// <param name="adminUserId">管理员用户ID</param>
    /// <param name="targetUserId">目标用户ID</param>
    /// <param name="reason">解锁原因</param>
    /// <returns>解锁结果</returns>
    Task<bool> UnlockPaymentPasswordAsync(long adminUserId, long targetUserId, string reason);

    /// <summary>
    /// 获取支付密码统计信息
    /// </summary>
    /// <returns>统计信息</returns>
    Task<PaymentPasswordStatsVo> GetPaymentPasswordStatsAsync();

    /// <summary>
    /// 清理过期的锁定状态
    /// </summary>
    /// <returns>清理的记录数</returns>
    Task<int> ClearExpiredLocksAsync();

    /// <summary>
    /// 检查密码强度
    /// </summary>
    /// <param name="password">密码</param>
    /// <returns>强度等级 (1-5)</returns>
    int CheckPasswordStrength(string password);

    /// <summary>
    /// 生成安全建议
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>安全建议列表</returns>
    Task<List<string>> GenerateSecuritySuggestionsAsync(long userId);
}