using Microsoft.AspNetCore.SignalR;
using Radish.Api.Hubs;
using Radish.IService;
using Serilog;

namespace Radish.Api.Services;

/// <summary>
/// 通知推送服务实现
/// </summary>
public class NotificationPushService : INotificationPushService
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationPushService(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <summary>
    /// 推送未读数变更到指定用户
    /// </summary>
    public async Task PushUnreadCountAsync(long userId, int unreadCount)
    {
        try
        {
            var groupName = $"user:{userId}";

            Log.Debug(
                "[NotificationPushService] 推送未读数 - UserId: {UserId}, UnreadCount: {UnreadCount}",
                userId, unreadCount);

            await _hubContext.Clients.Group(groupName)
                .SendAsync("UnreadCountChanged", new { unreadCount });
        }
        catch (Exception ex)
        {
            Log.Error(ex,
                "[NotificationPushService] 推送未读数失败 - UserId: {UserId}, UnreadCount: {UnreadCount}",
                userId, unreadCount);

            // 推送失败不应阻断业务流程，仅记录日志
            // 客户端可通过轮询降级或重连后主动拉取
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

        try
        {
            var tasks = userUnreadCounts.Select(kvp =>
                PushUnreadCountAsync(kvp.Key, kvp.Value)
            );

            await Task.WhenAll(tasks);

            Log.Debug(
                "[NotificationPushService] 批量推送未读数完成 - Count: {Count}",
                userUnreadCounts.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex,
                "[NotificationPushService] 批量推送未读数失败 - Count: {Count}",
                userUnreadCounts.Count);
        }
    }

    /// <summary>
    /// 推送通知到指定用户（P1 阶段使用）
    /// </summary>
    public async Task PushNotificationAsync(long userId, object notification)
    {
        try
        {
            var groupName = $"user:{userId}";

            Log.Debug(
                "[NotificationPushService] 推送通知 - UserId: {UserId}",
                userId);

            await _hubContext.Clients.Group(groupName)
                .SendAsync("NotificationReceived", notification);
        }
        catch (Exception ex)
        {
            Log.Error(ex,
                "[NotificationPushService] 推送通知失败 - UserId: {UserId}",
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
            var groupName = $"user:{userId}";

            Log.Debug(
                "[NotificationPushService] 推送已读状态 - UserId: {UserId}, Count: {Count}",
                userId, notificationIds.Length);

            await _hubContext.Clients.Group(groupName)
                .SendAsync("NotificationRead", new { notificationIds });
        }
        catch (Exception ex)
        {
            Log.Error(ex,
                "[NotificationPushService] 推送已读状态失败 - UserId: {UserId}",
                userId);
        }
    }
}
