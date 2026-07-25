using Radish.Model;

namespace Radish.IRepository;

public sealed record UserBlockMutationCommand(
    long TenantId,
    long ActorUserId,
    long TargetUserId,
    string OperationType,
    string OperationKey,
    string OperatorName,
    DateTime NowUtc);

public sealed record UserBlockWriteResult(
    UserBlock Block,
    long RelationshipVersion,
    bool Changed,
    bool Replayed);

public sealed class UserBlockOperationConflictException : Exception
{
}

public sealed class UserBlockStateConflictException : Exception
{
}

public interface IUserBlockRepository
{
    Task<UserBlockWriteResult> MutateAsync(UserBlockMutationCommand command);

    Task<UserBlock?> QueryPairIncludingDeletedAsync(long tenantId, long blockerUserId, long blockedUserId);

    Task<IReadOnlyList<UserBlock>> QueryActiveBetweenAsync(
        long tenantId,
        long currentUserId,
        IReadOnlyCollection<long> otherUserIds);

    Task<IReadOnlyList<long>> QueryBarrierUserIdsAsync(long tenantId, long currentUserId);

    Task<(IReadOnlyList<UserBlock> Items, int Total)> QueryMineAsync(
        long tenantId,
        long blockerUserId,
        int pageIndex,
        int pageSize);
}
