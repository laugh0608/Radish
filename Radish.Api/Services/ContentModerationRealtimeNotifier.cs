using Microsoft.AspNetCore.SignalR;
using Radish.Api.Hubs;
using Radish.IService;

namespace Radish.Api.Services;

public sealed class ContentModerationRealtimeNotifier(IHubContext<ChatHub> hubContext)
    : IContentModerationRealtimeNotifier
{
    public Task NotifyChatMessageRecalledAsync(
        long tenantId,
        long channelId,
        long messageId,
        CancellationToken cancellationToken = default)
    {
        return hubContext.Clients.Group(ChatHub.BuildChannelGroup(tenantId, channelId))
            .SendAsync("MessageRecalled", new { channelId, messageId }, cancellationToken);
    }
}
