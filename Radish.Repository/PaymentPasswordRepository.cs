using Radish.IRepository;
using Radish.Model.Models;
using Radish.Repository.Base;

namespace Radish.Repository;

/// <summary>
/// 支付密码仓储实现
/// </summary>
public class PaymentPasswordRepository : BaseRepository<UserPaymentPassword>, IPaymentPasswordRepository
{
    public PaymentPasswordRepository(ISqlSugarClient db) : base(db)
    {
    }

    /// <summary>
    /// 根据用户ID获取支付密码信息
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>支付密码信息</returns>
    public async Task<UserPaymentPassword?> GetByUserIdAsync(long userId)
    {
        return await QueryByClauseAsync(p => p.UserId == userId && p.IsEnabled);
    }

    /// <summary>
    /// 检查用户是否已设置支付密码
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>是否已设置</returns>
    public async Task<bool> HasPaymentPasswordAsync(long userId)
    {
        return await QueryCountAsync(p => p.UserId == userId && p.IsEnabled && !string.IsNullOrEmpty(p.PasswordHash)) > 0;
    }

    /// <summary>
    /// 更新失败尝试次数
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="failedAttempts">失败次数</param>
    /// <param name="lockedUntil">锁定到期时间</param>
    /// <returns>更新结果</returns>
    public async Task<bool> UpdateFailedAttemptsAsync(long userId, int failedAttempts, DateTime? lockedUntil = null)
    {
        var updateColumns = new Dictionary<string, object>
        {
            { nameof(UserPaymentPassword.FailedAttempts), failedAttempts },
            { nameof(UserPaymentPassword.ModifiedAt), DateTime.Now }
        };

        if (lockedUntil.HasValue)
        {
            updateColumns.Add(nameof(UserPaymentPassword.LockedUntil), lockedUntil.Value);
        }

        var result = await Db.Updateable<UserPaymentPassword>()
            .SetColumns(updateColumns)
            .Where(p => p.UserId == userId && p.IsEnabled)
            .ExecuteCommandAsync();

        return result > 0;
    }

    /// <summary>
    /// 重置失败尝试次数
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>重置结果</returns>
    public async Task<bool> ResetFailedAttemptsAsync(long userId)
    {
        var result = await Db.Updateable<UserPaymentPassword>()
            .SetColumns(p => new UserPaymentPassword
            {
                FailedAttempts = 0,
                LockedUntil = null,
                ModifiedAt = DateTime.Now
            })
            .Where(p => p.UserId == userId && p.IsEnabled)
            .ExecuteCommandAsync();

        return result > 0;
    }

    /// <summary>
    /// 更新最后使用时间
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>更新结果</returns>
    public async Task<bool> UpdateLastUsedTimeAsync(long userId)
    {
        var result = await Db.Updateable<UserPaymentPassword>()
            .SetColumns(p => new UserPaymentPassword
            {
                LastUsedTime = DateTime.Now,
                ModifiedAt = DateTime.Now
            })
            .Where(p => p.UserId == userId && p.IsEnabled)
            .ExecuteCommandAsync();

        return result > 0;
    }

    /// <summary>
    /// 获取被锁定的用户数量
    /// </summary>
    /// <returns>被锁定的用户数量</returns>
    public async Task<int> GetLockedUsersCountAsync()
    {
        return await QueryCountAsync(p => p.IsEnabled &&
                                         p.LockedUntil.HasValue &&
                                         p.LockedUntil.Value > DateTime.Now);
    }

    /// <summary>
    /// 清理过期的锁定状态
    /// </summary>
    /// <returns>清理的记录数</returns>
    public async Task<int> ClearExpiredLocksAsync()
    {
        var result = await Db.Updateable<UserPaymentPassword>()
            .SetColumns(p => new UserPaymentPassword
            {
                LockedUntil = null,
                ModifiedAt = DateTime.Now
            })
            .Where(p => p.IsEnabled &&
                       p.LockedUntil.HasValue &&
                       p.LockedUntil.Value <= DateTime.Now)
            .ExecuteCommandAsync();

        return result;
    }
}