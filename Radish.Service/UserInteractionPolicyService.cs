using Microsoft.AspNetCore.Http;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IService;

namespace Radish.Service;

/// <summary>基于 Main UserBlock 唯一真相的统一用户交互策略。</summary>
public sealed class UserInteractionPolicyService : IUserInteractionPolicyService
{
    private readonly IUserBlockRepository _userBlockRepository;

    public UserInteractionPolicyService(IUserBlockRepository userBlockRepository)
    {
        _userBlockRepository = userBlockRepository;
    }

    public async Task<UserInteractionPolicySnapshot> GetSnapshotAsync(
        long tenantId,
        long currentUserId,
        long otherUserId)
    {
        var snapshots = await GetSnapshotsAsync(tenantId, currentUserId, [otherUserId]);
        return snapshots.TryGetValue(otherUserId, out var snapshot)
            ? snapshot
            : new UserInteractionPolicySnapshot(otherUserId, false, false);
    }

    public async Task<IReadOnlyDictionary<long, UserInteractionPolicySnapshot>> GetSnapshotsAsync(
        long tenantId,
        long currentUserId,
        IReadOnlyCollection<long> otherUserIds)
    {
        var normalizedIds = otherUserIds
            .Where(id => id > 0 && id != currentUserId)
            .Distinct()
            .ToList();
        if (currentUserId <= 0 || normalizedIds.Count == 0)
        {
            return new Dictionary<long, UserInteractionPolicySnapshot>();
        }

        IReadOnlyList<Radish.Model.UserBlock> relations;
        try
        {
            relations = await _userBlockRepository.QueryActiveBetweenAsync(
                Math.Max(0, tenantId),
                currentUserId,
                normalizedIds);
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw RelationshipTemporarilyUnavailable(exception);
        }
        var outboundIds = relations
            .Where(item => item.BlockerUserId == currentUserId)
            .Select(item => item.BlockedUserId)
            .ToHashSet();
        var barrierIds = relations
            .Select(item => item.BlockerUserId == currentUserId ? item.BlockedUserId : item.BlockerUserId)
            .ToHashSet();
        return normalizedIds.ToDictionary(
            id => id,
            id => new UserInteractionPolicySnapshot(
                id,
                barrierIds.Contains(id),
                outboundIds.Contains(id)));
    }

    public Task<IReadOnlyList<long>> GetBarrierUserIdsAsync(long tenantId, long currentUserId)
    {
        if (currentUserId <= 0)
        {
            return Task.FromResult<IReadOnlyList<long>>([]);
        }

        return QueryBarrierUserIdsCoreAsync(tenantId, currentUserId);
    }

    public async Task<IReadOnlyList<long>> ExcludeInteractionBarriersAsync(
        long tenantId,
        long currentUserId,
        IReadOnlyCollection<long> candidateUserIds)
    {
        var normalizedIds = candidateUserIds.Where(id => id > 0).Distinct().ToList();
        if (currentUserId <= 0 || normalizedIds.Count == 0)
        {
            return normalizedIds;
        }

        var snapshots = await GetSnapshotsAsync(tenantId, currentUserId, normalizedIds);
        return normalizedIds
            .Where(id => !snapshots.TryGetValue(id, out var snapshot) || !snapshot.HasInteractionBarrier)
            .ToList();
    }

    public async Task EnsureCanInteractAsync(long tenantId, long currentUserId, long otherUserId)
    {
        if (currentUserId <= 0 || otherUserId <= 0 || currentUserId == otherUserId)
        {
            throw InteractionUnavailable();
        }

        var snapshot = await GetSnapshotAsync(tenantId, currentUserId, otherUserId);
        if (snapshot.HasInteractionBarrier)
        {
            throw InteractionUnavailable();
        }
    }

    private static BusinessException InteractionUnavailable() => new(
        "当前无法与该用户互动",
        StatusCodes.Status409Conflict,
        "UserBlock.InteractionUnavailable",
        "error.user_block.interaction_unavailable");

    private async Task<IReadOnlyList<long>> QueryBarrierUserIdsCoreAsync(long tenantId, long currentUserId)
    {
        try
        {
            return await _userBlockRepository.QueryBarrierUserIdsAsync(Math.Max(0, tenantId), currentUserId);
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw RelationshipTemporarilyUnavailable(exception);
        }
    }

    private static BusinessException RelationshipTemporarilyUnavailable(Exception innerException) => new(
        "用户关系服务暂时不可用，请稍后重试",
        innerException,
        StatusCodes.Status503ServiceUnavailable,
        "UserBlock.RelationshipTemporarilyUnavailable",
        "error.user_block.relationship_temporarily_unavailable");
}
