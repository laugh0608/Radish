using Microsoft.Extensions.Logging;
using Radish.Common.CacheTool;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;

namespace Radish.Service;

/// <summary>
/// 通知缓存服务实现
/// </summary>
/// <remarks>
/// 使用缓存减少数据库查询压力，提升通知系统性能
/// 缓存策略：
/// - 未读数：30 分钟过期，增量更新
/// - 缓存失效：标记已读/删除时触发
/// </remarks>
public class NotificationCacheService : INotificationCacheService
{
    private readonly ICaching _caching;
    private readonly IBaseRepository<UserNotification> _userNotificationRepository;
    private readonly ILogger<NotificationCacheService> _logger;

    // 缓存键前缀
    private const string UnreadCountKeyPrefix = "notification:unread_count:";

    // 缓存过期时间（30 分钟）
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(30);

    public NotificationCacheService(
        ICaching caching,
        IBaseRepository<UserNotification> userNotificationRepository,
        ILogger<NotificationCacheService> logger)
    {
        _caching = caching;
        _userNotificationRepository = userNotificationRepository;
        _logger = logger;
    }

    /// <summary>
    /// 获取用户的未读通知数量（优先从缓存读取）
    /// </summary>
    public async Task<long> GetUnreadCountAsync(long userId)
    {
        try
        {
            var cacheKey = GetUnreadCountKey(userId);

            // 1. 尝试从缓存读取
            var cachedValue = await _caching.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedValue) && long.TryParse(cachedValue, out var count))
            {
                _logger.LogDebug(
                    "[NotificationCache] 命中缓存，UserId: {UserId}, UnreadCount: {Count}",
                    userId, count);
                return count;
            }

            // 2. 缓存未命中，从数据库查询
            _logger.LogDebug(
                "[NotificationCache] 缓存未命中，从数据库查询，UserId: {UserId}",
                userId);

            var dbCount = await _userNotificationRepository.QueryCountAsync(
                un => un.UserId == userId && !un.IsRead && !un.IsDeleted);

            // 3. 写入缓存
            await _caching.SetStringAsync(cacheKey, dbCount.ToString(), CacheExpiration);

            _logger.LogInformation(
                "[NotificationCache] 设置缓存，UserId: {UserId}, UnreadCount: {Count}",
                userId, dbCount);

            return dbCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[NotificationCache] 获取未读数失败，UserId: {UserId}",
                userId);
            return 0;
        }
    }

    /// <summary>
    /// 设置用户的未读通知数量到缓存
    /// </summary>
    public async Task SetUnreadCountAsync(long userId, long count)
    {
        try
        {
            var cacheKey = GetUnreadCountKey(userId);
            await _caching.SetStringAsync(cacheKey, count.ToString(), CacheExpiration);

            _logger.LogDebug(
                "[NotificationCache] 设置缓存，UserId: {UserId}, UnreadCount: {Count}",
                userId, count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[NotificationCache] 设置缓存失败，UserId: {UserId}, Count: {Count}",
                userId, count);
        }
    }

    /// <summary>
    /// 增量更新用户的未读数量（+1）
    /// </summary>
    public async Task<long> IncrementUnreadCountAsync(long userId)
    {
        try
        {
            // 先获取当前值（优先从缓存）
            var currentCount = await GetUnreadCountAsync(userId);
            var newCount = currentCount + 1;

            // 更新缓存
            await SetUnreadCountAsync(userId, newCount);

            _logger.LogDebug(
                "[NotificationCache] 增量更新，UserId: {UserId}, {OldCount} -> {NewCount}",
                userId, currentCount, newCount);

            return newCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[NotificationCache] 增量更新失败，UserId: {UserId}",
                userId);

            // 失败时刷新缓存
            return await RefreshUnreadCountAsync(userId);
        }
    }

    /// <summary>
    /// 减量更新用户的未读数量（-delta）
    /// </summary>
    public async Task<long> DecrementUnreadCountAsync(long userId, long delta)
    {
        try
        {
            // 先获取当前值（优先从缓存）
            var currentCount = await GetUnreadCountAsync(userId);
            var newCount = Math.Max(0, currentCount - delta);

            // 更新缓存
            await SetUnreadCountAsync(userId, newCount);

            _logger.LogDebug(
                "[NotificationCache] 减量更新，UserId: {UserId}, {OldCount} -> {NewCount} (delta: {Delta})",
                userId, currentCount, newCount, delta);

            return newCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[NotificationCache] 减量更新失败，UserId: {UserId}, Delta: {Delta}",
                userId, delta);

            // 失败时刷新缓存
            return await RefreshUnreadCountAsync(userId);
        }
    }

    /// <summary>
    /// 清除用户的未读数量缓存
    /// </summary>
    public async Task ClearUnreadCountAsync(long userId)
    {
        try
        {
            var cacheKey = GetUnreadCountKey(userId);
            await _caching.RemoveAsync(cacheKey);

            _logger.LogDebug(
                "[NotificationCache] 清除缓存，UserId: {UserId}",
                userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[NotificationCache] 清除缓存失败，UserId: {UserId}",
                userId);
        }
    }

    /// <summary>
    /// 刷新用户的未读数量缓存（从数据库重新加载）
    /// </summary>
    public async Task<long> RefreshUnreadCountAsync(long userId)
    {
        try
        {
            // 1. 清除旧缓存
            await ClearUnreadCountAsync(userId);

            // 2. 从数据库查询
            var dbCount = await _userNotificationRepository.QueryCountAsync(
                un => un.UserId == userId && !un.IsRead && !un.IsDeleted);

            // 3. 写入新缓存
            await SetUnreadCountAsync(userId, dbCount);

            _logger.LogInformation(
                "[NotificationCache] 刷新缓存，UserId: {UserId}, UnreadCount: {Count}",
                userId, dbCount);

            return dbCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[NotificationCache] 刷新缓存失败，UserId: {UserId}",
                userId);
            return 0;
        }
    }

    /// <summary>
    /// 生成未读数缓存键
    /// </summary>
    private static string GetUnreadCountKey(long userId)
    {
        return $"{UnreadCountKeyPrefix}{userId}";
    }
}
