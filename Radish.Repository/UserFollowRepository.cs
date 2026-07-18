using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;

namespace Radish.Repository;

/// <summary>用户关注关系仓储</summary>
public class UserFollowRepository : BaseRepository<UserFollow>, IUserFollowRepository
{
    public UserFollowRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public async Task<UserFollow?> QueryPairIncludingDeletedAsync(
        long followerUserId,
        long followingUserId,
        long tenantId)
    {
        var normalizedTenantId = tenantId > 0 ? tenantId : 0;
        return await ExecuteDbOperationAsync(
            () => DbProtectedClient.Queryable<UserFollow>()
                .FirstAsync(follow =>
                    follow.TenantId == normalizedTenantId &&
                    follow.FollowerUserId == followerUserId &&
                    follow.FollowingUserId == followingUserId));
    }
}
