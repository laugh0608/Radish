namespace Radish.IService;

public sealed record UserInteractionPolicySnapshot(
    long OtherUserId,
    bool HasInteractionBarrier,
    bool IsBlockedByCurrentUser)
{
    public bool CanInteract => !HasInteractionBarrier;
}

public interface IUserInteractionPolicyService
{
    Task<UserInteractionPolicySnapshot> GetSnapshotAsync(
        long tenantId,
        long currentUserId,
        long otherUserId);

    Task<IReadOnlyDictionary<long, UserInteractionPolicySnapshot>> GetSnapshotsAsync(
        long tenantId,
        long currentUserId,
        IReadOnlyCollection<long> otherUserIds);

    Task<IReadOnlyList<long>> GetBarrierUserIdsAsync(long tenantId, long currentUserId);

    Task<IReadOnlyList<long>> ExcludeInteractionBarriersAsync(
        long tenantId,
        long currentUserId,
        IReadOnlyCollection<long> candidateUserIds);

    Task EnsureCanInteractAsync(long tenantId, long currentUserId, long otherUserId);
}
