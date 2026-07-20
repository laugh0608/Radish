using Radish.IRepository;
using Radish.Model.Models;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;

namespace Radish.Repository;

/// <summary>
/// 分片上传会话仓储。
/// </summary>
public sealed class UploadSessionRepository : BaseRepository<UploadSession>, IUploadSessionRepository
{
    public UploadSessionRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public async Task<List<UploadSession>> QueryExpiredAcrossTenantsAsync(DateTime expiredBeforeUtc)
    {
        return await ExecuteDbOperationAsync(async () => await DbProtectedClient
            .Queryable<UploadSession>()
            .ClearFilter()
            .Where(session =>
                session.ExpiresAt < expiredBeforeUtc &&
                (session.Status == "Uploading" || session.Status == "Failed"))
            .ToListAsync());
    }

    public async Task<List<UploadSession>> QueryBySessionIdsAcrossTenantsAsync(List<string> sessionIds)
    {
        if (sessionIds.Count == 0)
        {
            return [];
        }

        return await ExecuteDbOperationAsync(async () => await DbProtectedClient
            .Queryable<UploadSession>()
            .ClearFilter()
            .Where(session => sessionIds.Contains(session.SessionId))
            .ToListAsync());
    }

    public async Task<List<UploadSession>> QueryTerminalForSettlementAcrossTenantsAsync(
        DateTime modifiedAfterUtc,
        int maxCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);
        return await ExecuteDbOperationAsync(async () => await DbProtectedClient
            .Queryable<UploadSession>()
            .ClearFilter()
            .Where(session =>
                (session.Status == "Completed" ||
                 session.Status == "Cancelled" ||
                 session.Status == "Expired" ||
                 session.Status == "Failed") &&
                ((session.ModifyTime != null && session.ModifyTime >= modifiedAfterUtc) ||
                 (session.ModifyTime == null && session.CreateTime >= modifiedAfterUtc)))
            .OrderBy(session => session.ModifyTime ?? session.CreateTime, SqlSugar.OrderByType.Desc)
            .Take(maxCount)
            .ToListAsync());
    }

    public async Task<bool> TryMarkExpiredAcrossTenantsAsync(
        string sessionId,
        long tenantId,
        long userId,
        DateTime expiredBeforeUtc,
        DateTime modifiedAtUtc)
    {
        var affectedRows = await ExecuteDbOperationAsync(async () => await DbProtectedClient
            .Updateable<UploadSession>()
            .SetColumns(session => session.Status == "Expired")
            .SetColumns(session => session.ErrorMessage == "上传会话已过期")
            .SetColumns(session => session.ModifyTime == modifiedAtUtc)
            .Where(session =>
                session.SessionId == sessionId &&
                session.TenantId == tenantId &&
                session.UserId == userId &&
                session.ExpiresAt < expiredBeforeUtc &&
                (session.Status == "Uploading" || session.Status == "Failed"))
            .ExecuteCommandAsync());

        return affectedRows == 1;
    }
}
