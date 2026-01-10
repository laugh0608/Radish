using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Radish.Api.Hubs;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;

namespace Radish.Api.Services;

/// <summary>
/// 通知推送服务实现
/// </summary>
/// <remarks>
/// P0 阶段：实现未读数推送
/// P1 阶段：实现完整通知内容推送
/// 注意：此服务在 API 层实现，因为需要直接依赖 SignalR Hub
/// </remarks>
public class NotificationPushService : INotificationPushService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IBaseRepository<UserNotification> _userNotificationRepository;
    private readonly ILogger<NotificationPushService> _logger;

    public NotificationPushService(
        IHubContext<NotificationHub> hubContext,
        IBaseRepository<UserNotification> userNotificationRepository,
        ILogger<NotificationPushService> logger)
    {
        _hubContext = hubContext;
        _userNotificationRepository = userNotificationRepository;
        _logger = logger;
    }

    /// <summary>
    /// 推送未读数变更到指定用户
    /// </summary>
    public async Task PushUnreadCountAsync(long userId, int unreadCount)
    {
        try
        {
            _logger.LogDebug(
                "[NotificationPushService] 推送未读数到用户 {UserId}，未读数：{UnreadCount}",
                userId, unreadCount);

            // 通过 SignalR 推送未读数到指定用户组
            await _hubContext.Clients
                .Group($"user:{userId}")
                .SendAsync("UnreadCountChanged", new { unreadCount });

            _logger.LogDebug(
                "[NotificationPushService] 成功推送未读数到用户 {UserId}",
                userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[NotificationPushService] 推送未读数失败，UserId: {UserId}",
                userId);
            // 推送失败不抛出异常，避免影响主流程
        }
    }

    /// <summary>
    /// 批量推送未读数变更
    /// </summary>
    public async Task PushUnreadCountBatchAsync(Dictionary<long, int> userUnreadCounts)
    {
        if (userUnreadCounts == null || userUnreadCounts.Count == 0)
        {
            return;
        }

        _logger.LogDebug(
            "[NotificationPushService] 批量推送未读数，用户数：{Count}",
            userUnreadCounts.Count);

        var pushTasks = userUnreadCounts.Select(kvp =>
            PushUnreadCountAsync(kvp.Key, kvp.Value));

        try
        {
            await Task.WhenAll(pushTasks);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[NotificationPushService] 批量推送未读数时部分失败");
            // 部分失败不影响整体
        }
    }

    /// <summary>
    /// 推送通知到指定用户（P1 阶段实现）
    /// </summary>
    public async Task PushNotificationAsync(long userId, object notification)
    {
        try
        {
            _logger.LogDebug(
                "[NotificationPushService] 推送通知到用户 {UserId}",
                userId);

            // P1 阶段：推送完整的通知对象
            await _hubContext.Clients
                .Group($"user:{userId}")
                .SendAsync("NotificationReceived", notification);

            _logger.LogDebug(
                "[NotificationPushService] 成功推送通知到用户 {UserId}",
                userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[NotificationPushService] 推送通知失败，UserId: {UserId}",
                userId);
        }
    }

    /// <summary>
    /// 推送已读状态变更到指定用户的其他端（多端同步）
    /// </summary>
    public async Task PushNotificationReadAsync(long userId, long[] notificationIds)
    {
        try
        {
            _logger.LogDebug(
                "[NotificationPushService] 推送已读状态到用户 {UserId}，通知数：{Count}",
                userId, notificationIds.Length);

            // 推送到该用户的所有端（不包括当前连接）
            await _hubContext.Clients
                .Group($"user:{userId}")
                .SendAsync("NotificationRead", new { notificationIds });

            _logger.LogDebug(
                "[NotificationPushService] 成功推送已读状态到用户 {UserId}",
                userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[NotificationPushService] 推送已读状态失败，UserId: {UserId}",
                userId);
        }
    }

    /// <summary>
    /// 查询用户的未读数量（辅助方法，用于 Hub）
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>未读数量</returns>
    public async Task<int> GetUnreadCountAsync(long userId)
    {
        try
        {
            // 从数据库查询未读数量
            // P0 阶段：通过 Repository 查询数据库
            // P1 阶段：优先从缓存读取
            var count = await _userNotificationRepository.QueryCountAsync(
                n => n.UserId == userId && !n.IsRead && !n.IsDeleted);

            _logger.LogDebug(
                "[NotificationPushService] 用户 {UserId} 未读数：{Count}",
                userId, count);

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[NotificationPushService] 查询未读数失败，UserId: {UserId}",
                userId);
            return 0;
        }
    }
}
