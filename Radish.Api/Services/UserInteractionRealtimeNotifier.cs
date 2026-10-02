using Microsoft.AspNetCore.SignalR;
using Radish.Api.Hubs;
using Radish.Common.LogTool;
using Radish.IService;
using Radish.Model.ViewModels;

namespace Radish.Api.Services;

/// <summary>只广播关系版本失效，不携带屏蔽方向。</summary>
public sealed class UserInteractionRealtimeNotifier : IUserInteractionRealtimeNotifier
{
    private readonly IHubContext<ChatHub> _chatHubContext;
    private readonly IHubContext<NotificationHub> _notificationHubContext;
    private readonly ILogger<UserInteractionRealtimeNotifier> _logger;

    public UserInteractionRealtimeNotifier(
        IHubContext<ChatHub> chatHubContext,
        IHubContext<NotificationHub> notificationHubContext,
        ILogger<UserInteractionRealtimeNotifier> logger)
    {
        _chatHubContext = chatHubContext;
        _notificationHubContext = notificationHubContext;
        _logger = logger;
    }

    public async Task NotifyRelationshipChangedAsync(
        long blockerUserId,
        long blockedUserId,
        long relationshipVersion)
    {
        foreach (var userId in new[] { blockerUserId, blockedUserId }.Where(id => id > 0).Distinct())
        {
            var change = new UserInteractionChangedVo
            {
                VoRelationshipVersion = relationshipVersion.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)
            };

            try
            {
                await _chatHubContext.Clients.Group($"user:{userId}")
                    .SendAsync("UserInteractionChanged", change);
            }
            catch (Exception exception)
            {
                using var scope = _logger.BeginScope(new Dictionary<string, object>
                {
                    ["EventCode"] = "user_interaction.chat_push_failed", ["SourceCategory"] = "business"
                });
                _logger.LogWarning("Relationship invalidation chat push unavailable; kind={failureKind}", RuntimeFailureSummary.Classify(exception));
            }

            try
            {
                await _notificationHubContext.Clients.Group($"user:{userId}")
                    .SendAsync("UserInteractionChanged", change);
            }
            catch (Exception exception)
            {
                using var scope = _logger.BeginScope(new Dictionary<string, object>
                {
                    ["EventCode"] = "user_interaction.notification_push_failed", ["SourceCategory"] = "business"
                });
                _logger.LogWarning("Relationship invalidation notification push unavailable; kind={failureKind}", RuntimeFailureSummary.Classify(exception));
            }
        }
    }
}
