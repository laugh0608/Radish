using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Serilog;

namespace Radish.Api.Hubs;

/// <summary>
/// 通知 SignalR Hub
/// </summary>
[Authorize(Policy = "Client")]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 连接建立时
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        // 【调试】输出所有 claims，用于诊断 UserId 获取失败问题
        var allClaims = Context.User?.Claims
            .Select(c => $"{c.Type}={c.Value}")
            .ToArray() ?? Array.Empty<string>();
        _logger.LogInformation("[NotificationHub] 所有 Claims: {Claims}", string.Join(", ", allClaims));

        var userId = GetUserId();
        var connectionId = Context.ConnectionId;
        var clientIp = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        _logger.LogInformation(
            "[NotificationHub] 连接建立 - UserId: {UserId}, ConnectionId: {ConnectionId}, IP: {ClientIp}",
            userId, connectionId, clientIp);

        // 将连接加入用户组（支持多端同时在线）
        await Groups.AddToGroupAsync(connectionId, $"user:{userId}");

        // 推送当前未读数（P0 阶段先返回 0，后续从缓存/数据库获取）
        await Clients.Caller.SendAsync("UnreadCountChanged", new { unreadCount = 0 });

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
        try
        {
            // 【调试】检查 Context.User 是否为 null
            if (Context.User == null)
            {
                _logger.LogError("[NotificationHub] Context.User 为 null");
                throw new HubException("用户未认证");
            }

            var allClaims = Context.User.Claims.Select(c => $"{c.Type}={c.Value}").ToArray();
            _logger.LogInformation("[NotificationHub.GetUserId] 所有 Claims: {Claims}", string.Join(", ", allClaims));

            // 优先从 OIDC 标准 claim 获取用户 ID
            // 由于我们禁用了 claim type mapping，现在 "sub" 应该保持原样
            var userIdClaim = Context.User.FindFirst("sub")?.Value
                ?? Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? Context.User.FindFirst("userId")?.Value
                ?? Context.User.FindFirst("UserId")?.Value
                ?? Context.User.FindFirst("uid")?.Value
                ?? Context.User.FindFirst("userid")?.Value;

            _logger.LogInformation("[NotificationHub.GetUserId] 提取到的 userIdClaim: {UserIdClaim}", userIdClaim ?? "null");

            if (!string.IsNullOrWhiteSpace(userIdClaim) && long.TryParse(userIdClaim, out var userId))
            {
                _logger.LogInformation("[NotificationHub.GetUserId] 成功解析 userId: {UserId}", userId);
                return userId;
            }

            // 如果无法获取用户 ID，抛出异常（Hub 要求已认证）
            _logger.LogError("[NotificationHub] 无法获取用户 ID，Claims: {Claims}",
                string.Join(", ", allClaims));

            throw new HubException("无法获取用户 ID");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NotificationHub] GetUserId 发生异常");
            throw;
        }
    }
}
