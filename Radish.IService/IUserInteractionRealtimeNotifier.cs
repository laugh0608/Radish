namespace Radish.IService;

public interface IUserInteractionRealtimeNotifier
{
    Task NotifyRelationshipChangedAsync(
        long blockerUserId,
        long blockedUserId,
        long relationshipVersion);
}
