using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model.ViewModels;

namespace Radish.Api.Hubs;

/// <summary>只广播权威收件箱 revision 的通知 Hub。</summary>
[Authorize(Policy = AuthorizationPolicies.Client)]
public sealed class NotificationHub : Hub
{
    private readonly INotificationPushService _notificationPushService;
    private readonly IClaimsPrincipalNormalizer _claimsPrincipalNormalizer;
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(
        INotificationPushService notificationPushService,
        IClaimsPrincipalNormalizer claimsPrincipalNormalizer,
        ILogger<NotificationHub> logger)
    {
        _notificationPushService = notificationPushService;
        _claimsPrincipalNormalizer = claimsPrincipalNormalizer;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var current = GetCurrentUser();
        if (current.UserId <= 0)
        {
            throw new HubException("无法获取用户 ID");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{current.UserId}");
        var summary = await _notificationPushService.GetInboxSummaryAsync(current.TenantId, current.UserId);
        await Clients.Caller.SendAsync("NotificationInboxChanged", new NotificationInboxChangedVo
        {
            VoRevision = summary.VoRevision,
            VoUnreadGroupCount = summary.VoUnreadGroupCount,
            VoUnreadOccurrenceCount = summary.VoUnreadOccurrenceCount,
            VoReason = "Connected",
            VoRealtimePreviewAllowed = false
        });

        // F4-B-C 前保留旧前端连接初始化事件。
        await Clients.Caller.SendAsync("UnreadCountChanged", new
        {
            unreadCount = summary.VoUnreadGroupCount
        });
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var current = GetCurrentUser();
        if (current.UserId > 0)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{current.UserId}");
        }

        if (exception != null)
        {
            _logger.LogWarning(
                exception,
                "通知 Hub 连接异常断开，UserId={UserId}, ConnectionId={ConnectionId}",
                current.UserId,
                Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private CurrentUser GetCurrentUser()
    {
        if (Context.User == null)
        {
            throw new HubException("用户未认证");
        }

        return _claimsPrincipalNormalizer.Normalize(Context.User, GetAccessToken());
    }

    private string? GetAccessToken()
    {
        var httpContext = Context.GetHttpContext();
        var accessToken = httpContext?.Request.Query["access_token"].ToString();
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            return accessToken;
        }

        var authorization = httpContext?.Request.Headers.Authorization.ToString();
        return authorization?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
            ? authorization["Bearer ".Length..].Trim()
            : authorization;
    }
}
