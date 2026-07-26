using Radish.IRepository.Base;
using Radish.Model;

namespace Radish.IRepository;

/// <summary>用户关注关系仓储接口</summary>
public interface IUserFollowRepository : IBaseRepository<UserFollow>
{
    /// <summary>按关注双方查询关系，包含已软删除记录。</summary>
    Task<UserFollow?> QueryPairIncludingDeletedAsync(long followerUserId, long followingUserId, long tenantId);

    Task<bool> FollowAsync(
        long tenantId,
        long followerUserId,
        long followingUserId,
        string operatorName,
        DateTime nowUtc);
}

public sealed class UserFollowInteractionBlockedException : Exception
{
}
