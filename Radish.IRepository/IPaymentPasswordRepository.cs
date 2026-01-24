using Radish.Model.Models;

namespace Radish.IRepository;

/// <summary>
/// 支付密码仓储接口
/// </summary>
public interface IPaymentPasswordRepository : IBaseRepository<UserPaymentPassword>
{
    /// <summary>
    /// 根据用户ID获取支付密码信息
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>支付密码信息</returns>
    Task<UserPaymentPassword?> GetByUserIdAsync(long userId);

    /// <summary>
    /// 检查用户是否已设置支付密码
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>是否已设置</returns>
    Task<bool> HasPaymentPasswordAsync(long userId);

    /// <summary>
    /// 更新失败尝试次数
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="failedAttempts">失败次数</param>
    /// <param name="lockedUntil">锁定到期时间</param>
    /// <returns>更新结果</returns>
    Task<bool> UpdateFailedAttemptsAsync(long userId, int failedAttempts, DateTime? lockedUntil = null);

    /// <summary>
    /// 重置失败尝试次数
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>重置结果</returns>
    Task<bool> ResetFailedAttemptsAsync(long userId);

    /// <summary>
    /// 更新最后使用时间
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>更新结果</returns>
    Task<bool> UpdateLastUsedTimeAsync(long userId);

    /// <summary>
    /// 获取被锁定的用户数量
    /// </summary>
    /// <returns>被锁定的用户数量</returns>
    Task<int> GetLockedUsersCountAsync();

    /// <summary>
    /// 清理过期的锁定状态
    /// </summary>
    /// <returns>清理的记录数</returns>
    Task<int> ClearExpiredLocksAsync();
}