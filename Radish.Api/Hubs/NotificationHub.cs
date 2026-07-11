using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Serilog;

namespace Radish.Api.Hubs;

/// <summary>
/// 通知 SignalR Hub
/// </summary>
[Authorize(Policy = AuthorizationPolicies.Client)]
public class NotificationHub : Hub
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

    /// <summary>
    /// 连接建立时
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var connectionId = Context.ConnectionId;
        var clientIp = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        _logger.LogInformation(
            "[NotificationHub] 连接建立 - UserId: {UserId}, ConnectionId: {ConnectionId}, IP: {ClientIp}",
            userId, connectionId, clientIp);

        // 将连接加入用户组（支持多端同时在线）
        await Groups.AddToGroupAsync(connectionId, $"user:{userId}");

        // 获取并推送真实的未读数
        var unreadCount = await _notificationPushService.GetUnreadCountAsync(userId);
        await Clients.Caller.SendAsync("UnreadCountChanged", new { unreadCount });

        _logger.LogInformation(
            "[NotificationHub] 已推送未读数到用户 {UserId}，未读数：{UnreadCount}",
            userId, unreadCount);

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// 连接断开时
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        var connectionId = Context.ConnectionId;

        if (exception != null)
        {
            _logger.LogWarning(exception,
                "[NotificationHub] 连接异常断开 - UserId: {UserId}, ConnectionId: {ConnectionId}",
                userId, connectionId);
        }
        else
        {
            _logger.LogInformation(
                "[NotificationHub] 连接正常断开 - UserId: {UserId}, ConnectionId: {ConnectionId}",
                userId, connectionId);
        }

        // 从用户组移除
        await Groups.RemoveFromGroupAsync(connectionId, $"user:{userId}");

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// 客户端标记通知已读（用于多端同步）
    /// </summary>
    /// <param name="notificationId">通知 ID</param>
    public async Task MarkAsRead(long notificationId)
    {
        var userId = GetUserId();

        _logger.LogDebug(
            "[NotificationHub] 客户端标记已读 - UserId: {UserId}, NotificationId: {NotificationId}",
            userId, notificationId);

        // 通知该用户的其他端（多端同步）
        await Clients.OthersInGroup($"user:{userId}")
            .SendAsync("NotificationRead", new { notificationIds = new[] { notificationId } });
    }

    /// <summary>
    /// 客户端标记全部已读（用于多端同步）
    /// </summary>
    public async Task MarkAllAsRead()
    {
        var userId = GetUserId();

        _logger.LogDebug("[NotificationHub] 客户端标记全部已读 - UserId: {UserId}", userId);

        // 通知该用户的其他端
        await Clients.OthersInGroup($"user:{userId}")
            .SendAsync("AllNotificationsRead", new { });
    }

    /// <summary>
    /// 获取当前用户 ID
    /// </summary>
    private long GetUserId()
    {
        if (Context.User == null)
        {
            _logger.LogWarning(
                "[NotificationHub] 连接缺少认证用户 - ConnectionId: {ConnectionId}",
                Context.ConnectionId);
            throw new HubException("用户未认证");
        }

        var userId = GetCurrentUser().UserId;
        if (userId > 0)
        {
            return userId;
        }

        _logger.LogWarning(
            "[NotificationHub] 连接无法解析用户标识 - ConnectionId: {ConnectionId}",
            Context.ConnectionId);
        throw new HubException("无法获取用户 ID");
    }

    private CurrentUser GetCurrentUser()
    {
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
        if (string.IsNullOrWhiteSpace(authorization))
        {
            return null;
        }

        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization["Bearer ".Length..].Trim()
            : authorization;
    }
}
