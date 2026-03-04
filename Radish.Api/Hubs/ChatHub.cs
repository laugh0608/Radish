using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Radish.IService;

namespace Radish.Api.Hubs;

/// <summary>聊天室 SignalR Hub</summary>
[Authorize(Policy = "Client")]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly IChatPresenceService _chatPresenceService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IChatService chatService,
        IChatPresenceService chatPresenceService,
        ILogger<ChatHub> logger)
    {
        _chatService = chatService;
        _chatPresenceService = chatPresenceService;
        _logger = logger;
    }

    /// <summary>构建频道组名</summary>
    public static string BuildChannelGroup(long tenantId, long channelId)
    {
        return $"channel:{tenantId}:{channelId}";
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

        _logger.LogInformation("[ChatHub] 连接建立，UserId: {UserId}, ConnectionId: {ConnectionId}", userId, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        _chatPresenceService.RemoveConnection(Context.ConnectionId, userId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");

        if (exception != null)
        {
            _logger.LogWarning(exception, "[ChatHub] 连接异常断开，UserId: {UserId}, ConnectionId: {ConnectionId}", userId, Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>加入频道</summary>
    public async Task JoinChannel(long channelId)
    {
        if (channelId <= 0)
        {
            throw new HubException("频道 Id 无效");
        }

        var userId = GetUserId();
        var tenantId = GetTenantId();
        var userName = GetUserName();

        await _chatService.JoinChannelAsync(tenantId, userId, channelId, userName);

        var channelGroup = BuildChannelGroup(tenantId, channelId);
        await Groups.AddToGroupAsync(Context.ConnectionId, channelGroup);
        _chatPresenceService.JoinChannel(Context.ConnectionId, tenantId, channelId, userId);

        _logger.LogDebug("[ChatHub] 用户加入频道，UserId: {UserId}, ChannelId: {ChannelId}", userId, channelId);
    }

    /// <summary>离开频道</summary>
    public async Task LeaveChannel(long channelId)
    {
        if (channelId <= 0)
        {
            return;
        }

        var userId = GetUserId();
        var tenantId = GetTenantId();

        await _chatService.LeaveChannelAsync(tenantId, userId, channelId);

        var channelGroup = BuildChannelGroup(tenantId, channelId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, channelGroup);
        _chatPresenceService.LeaveChannel(Context.ConnectionId, tenantId, channelId, userId);

        _logger.LogDebug("[ChatHub] 用户离开频道，UserId: {UserId}, ChannelId: {ChannelId}", userId, channelId);
    }

    /// <summary>输入中状态</summary>
    public async Task StartTyping(long channelId)
    {
        if (channelId <= 0)
        {
            return;
        }

        var tenantId = GetTenantId();
        var userId = GetUserId();
        var userName = GetUserName();

        var channelGroup = BuildChannelGroup(tenantId, channelId);
        await Clients.OthersInGroup(channelGroup)
            .SendAsync("UserTyping", new
            {
                channelId,
                userId,
                userName
            });
    }

    /// <summary>标记频道已读</summary>
    public async Task MarkChannelAsRead(long channelId)
    {
        if (channelId <= 0)
        {
            return;
        }

        var tenantId = GetTenantId();
        var userId = GetUserId();
        var userName = GetUserName();

        var unreadState = await _chatService.MarkChannelAsReadAsync(tenantId, userId, channelId, userName);

        await Clients.Group($"user:{userId}")
            .SendAsync("ChannelUnreadChanged", new
            {
                channelId = unreadState.VoChannelId,
                unreadCount = unreadState.VoUnreadCount,
                hasMention = unreadState.VoHasMention
            });
    }

    private long GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst("sub")?.Value
            ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrWhiteSpace(userIdClaim) && long.TryParse(userIdClaim, out var userId) && userId > 0)
        {
            return userId;
        }

        throw new HubException("无法获取用户 Id");
    }

    private long GetTenantId()
    {
        var tenantIdClaim = Context.User?.FindFirst("tenant_id")?.Value
            ?? Context.User?.FindFirst("TenantId")?.Value;

        return !string.IsNullOrWhiteSpace(tenantIdClaim) && long.TryParse(tenantIdClaim, out var tenantId)
            ? tenantId
            : 0;
    }

    private string GetUserName()
    {
        var userName = Context.User?.FindFirst("name")?.Value
            ?? Context.User?.FindFirst(ClaimTypes.Name)?.Value;

        return string.IsNullOrWhiteSpace(userName) ? "Unknown" : userName;
    }
}
