using Radish.Common;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using SqlSugar;

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

    public async Task<bool> FollowAsync(
        long tenantId,
        long followerUserId,
        long followingUserId,
        string operatorName,
        DateTime nowUtc)
    {
        var sqliteLock = await UserInteractionTransactionLock.EnterSqliteAsync(
            DbProtectedClient,
            tenantId,
            followerUserId,
            followingUserId);
        try
        {
            DbProtectedClient.Ado.BeginTran();
            try
            {
                await UserInteractionTransactionLock.AcquirePostgreSqlAsync(
                    DbProtectedClient,
                    tenantId,
                    followerUserId,
                    followingUserId);
                var blocked = await DbProtectedClient.Queryable<UserBlock>()
                    .Where(item =>
                        item.TenantId == tenantId &&
                        !item.IsDeleted &&
                        ((item.BlockerUserId == followerUserId && item.BlockedUserId == followingUserId) ||
                         (item.BlockerUserId == followingUserId && item.BlockedUserId == followerUserId)))
                    .AnyAsync();
                if (blocked)
                {
                    throw new UserFollowInteractionBlockedException();
                }

                var existing = await DbProtectedClient.Queryable<UserFollow>()
                    .Where(item =>
                        item.TenantId == tenantId &&
                        item.FollowerUserId == followerUserId &&
                        item.FollowingUserId == followingUserId)
                    .FirstAsync();
                if (existing is { IsDeleted: false })
                {
                    DbProtectedClient.Ado.CommitTran();
                    return false;
                }

                if (existing == null)
                {
                    await DbProtectedClient.Insertable(new UserFollow
                    {
                        Id = SnowFlakeSingle.Instance.NextId(),
                        TenantId = tenantId,
                        FollowerUserId = followerUserId,
                        FollowingUserId = followingUserId,
                        FollowTime = nowUtc,
                        CreateTime = nowUtc,
                        CreateBy = operatorName,
                        CreateId = followerUserId
                    }).ExecuteCommandAsync();
                }
                else
                {
                    existing.IsDeleted = false;
                    existing.DeletedAt = null;
                    existing.DeletedBy = null;
                    existing.FollowTime = nowUtc;
                    existing.ModifyTime = nowUtc;
                    existing.ModifyBy = operatorName;
                    existing.ModifyId = followerUserId;
                    await DbProtectedClient.Updateable(existing).ExecuteCommandAsync();
                }

                DbProtectedClient.Ado.CommitTran();
                return true;
            }
            catch
            {
                DbProtectedClient.Ado.RollbackTran();
                throw;
            }
        }
        finally
        {
            UserInteractionTransactionLock.ExitSqlite(sqliteLock);
        }
    }
}
