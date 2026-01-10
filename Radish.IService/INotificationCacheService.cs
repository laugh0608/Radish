namespace Radish.IService;

/// <summary>
/// 通知缓存服务接口
/// </summary>
/// <remarks>
/// 负责管理通知相关的缓存数据，减少数据库查询压力
/// 主要功能：
/// - 未读数量缓存
/// - 缓存失效管理
/// </remarks>
public interface INotificationCacheService
{
    /// <summary>
    /// 获取用户的未读通知数量（优先从缓存读取）
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>未读数量</returns>
    Task<long> GetUnreadCountAsync(long userId);

    /// <summary>
    /// 设置用户的未读通知数量到缓存
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="count">未读数量</param>
    Task SetUnreadCountAsync(long userId, long count);

    /// <summary>
    /// 增量更新用户的未读数量（+1）
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>更新后的未读数量</returns>
    Task<long> IncrementUnreadCountAsync(long userId);

    /// <summary>
    /// 减量更新用户的未读数量（-delta）
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="delta">减少的数量</param>
    /// <returns>更新后的未读数量</returns>
    Task<long> DecrementUnreadCountAsync(long userId, long delta);

    /// <summary>
    /// 清除用户的未读数量缓存
    /// </summary>
    /// <param name="userId">用户 ID</param>
    Task ClearUnreadCountAsync(long userId);

    /// <summary>
    /// 刷新用户的未读数量缓存（从数据库重新加载）
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>最新的未读数量</returns>
    Task<long> RefreshUnreadCountAsync(long userId);
}
