namespace Radish.IService;

/// <summary>治理动作完成后的实时事件边界。</summary>
public interface IContentModerationRealtimeNotifier
{
    Task NotifyChatMessageRecalledAsync(long tenantId, long channelId, long messageId, CancellationToken cancellationToken = default);
}
