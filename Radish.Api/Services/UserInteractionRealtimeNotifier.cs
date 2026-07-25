using Microsoft.AspNetCore.SignalR;
using Radish.Api.Hubs;
using Radish.IService;
using Radish.Model.ViewModels;

namespace Radish.Api.Services;

/// <summary>只广播关系版本失效，不携带屏蔽方向。</summary>
public sealed class UserInteractionRealtimeNotifier : IUserInteractionRealtimeNotifier
{
    private readonly IHubContext<ChatHub> _chatHubContext;
    private readonly ILogger<UserInteractionRealtimeNotifier> _logger;

    public UserInteractionRealtimeNotifier(
        IHubContext<ChatHub> chatHubContext,
        ILogger<UserInteractionRealtimeNotifier> logger)
    {
        _chatHubContext = chatHubContext;
        _logger = logger;
    }

    public async Task NotifyRelationshipChangedAsync(
        long blockerUserId,
        long blockedUserId,
        long relationshipVersion)
    {
        foreach (var userId in new[] { blockerUserId, blockedUserId }.Where(id => id > 0).Distinct())
        {
            try
            {
                await _chatHubContext.Clients.Group($"user:{userId}")
                    .SendAsync("UserInteractionChanged", new UserInteractionChangedVo
                    {
                        VoRelationshipVersion = relationshipVersion.ToString(
                            System.Globalization.CultureInfo.InvariantCulture)
                    });
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "用户关系失效推送失败，UserId={UserId}, RelationshipVersion={RelationshipVersion}",
                    userId,
                    relationshipVersion);
            }
        }
    }
}
