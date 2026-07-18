using Microsoft.AspNetCore.SignalR;
using Radish.Api.Hubs;
using Radish.IRepository;
using Radish.IService;
using Radish.Model.ViewModels;

namespace Radish.Api.Services;

/// <summary>通知 revision 变化的 best-effort SignalR 加速器。</summary>
public sealed class NotificationPushService : INotificationPushService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly INotificationInboxRepository _inboxRepository;
    private readonly ILogger<NotificationPushService> _logger;

    public NotificationPushService(
        IHubContext<NotificationHub> hubContext,
        INotificationInboxRepository inboxRepository,
        ILogger<NotificationPushService> logger)
    {
        _hubContext = hubContext;
        _inboxRepository = inboxRepository;
        _logger = logger;
    }

    public async Task PushInboxChangedAsync(long userId, NotificationInboxChangedVo change)
    {
        try
        {
            var clients = _hubContext.Clients.Group($"user:{userId}");
            await clients.SendAsync("NotificationInboxChanged", change);

            // F4-B-C 前保留旧前端角标事件，但数值已经来自同一权威摘要。
            await clients.SendAsync("UnreadCountChanged", new
            {
                unreadCount = change.VoUnreadGroupCount
            });
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "通知收件箱变化推送失败，UserId={UserId}, Revision={Revision}",
                userId,
                change.VoRevision);
        }
    }

    public async Task<NotificationInboxSummaryVo> GetInboxSummaryAsync(long tenantId, long userId)
    {
        var summary = await _inboxRepository.GetSummaryAsync(tenantId, userId);
        return new NotificationInboxSummaryVo
        {
            VoRevision = summary.Revision,
            VoUnreadGroupCount = summary.UnreadGroupCount,
            VoUnreadOccurrenceCount = summary.UnreadOccurrenceCount,
            VoUnreadGroupCountByCategory = summary.UnreadGroupCountByCategory
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
            VoLastChangedAtUtc = summary.LastChangedAtUtc
        };
    }
}
