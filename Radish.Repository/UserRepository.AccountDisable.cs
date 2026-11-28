using System.Globalization;
using System.Text.Json;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Model;
using SqlSugar;

namespace Radish.Repository;

public partial class UserRepository
{
    private static string DisableKey(long userId) => FormattableString.Invariant($"account-disable:{userId}");

    public Task<ReliableOutboxMessage?> FindDisableTaskAsync(long tenantId, long userId) =>
        ExecuteDbOperationAsync(() => ReadDisableTaskAsync(tenantId, userId));

    private async Task<ReliableOutboxMessage?> ReadDisableTaskAsync(long tenantId, long userId)
    {
        var key = DisableKey(userId);
        return await DbProtectedClient.Queryable<ReliableOutboxMessage>()
            .Where(row => row.TenantId == tenantId && row.IdempotencyKey == key).FirstAsync();
    }

    public Task<(ReliableOutboxMessage Task, bool Created)> DisableAccountAsync(long tenantId, long userId,
        long operatorId, string reason, DateTime now) => ExecuteDbOperationAsync(async () =>
    {
        var db = DbProtectedClient;
        db.Ado.BeginTran();
        try
        {
            await UserInteractionTransactionLock.AcquirePostgreSqlAsync(db, tenantId, userId, userId);
            var target = await db.Queryable<User>().Where(u => u.Id == userId && u.TenantId == tenantId && !u.IsDeleted)
                .FirstAsync();
            if (target == null)
                throw new BusinessException("用户不存在", 404, "User.NotFound", "error.user.not_found");

            var roleNames = await db.Queryable<UserRole, Role>((ur, r) => new JoinQueryInfos(JoinType.Inner, ur.RoleId == r.Id))
                .Where((ur, r) => ur.UserId == userId && !ur.IsDeleted && r.IsEnabled && !r.IsDeleted)
                .Select((ur, r) => r.RoleName.Trim()).ToListAsync();
            if (userId == operatorId || (tenantId == 0 && userId is 20000 or 20001) || UserRoleHelper.IsSystemOrAdmin(roleNames))
                throw new BusinessException("此账号受到保护，无法停用", 403, "User.DisableForbidden", "error.user.disable_forbidden");

            var existing = await ReadDisableTaskAsync(tenantId, userId);
            if (existing != null)
            {
                db.Ado.CommitTran();
                return (existing, false);
            }

            if (target.IsEnable)
            {
                var changed = await db.Updateable<User>().SetColumns(u => new User
                    { IsEnable = false, UpdateTime = now, CriticalModifyTime = now })
                    .Where(u => u.Id == userId && u.TenantId == tenantId && !u.IsDeleted && u.IsEnable)
                    .ExecuteCommandAsync();
                if (changed != 1)
                    throw new BusinessException("账号状态已变化，请刷新详情", 409, "User.DisableConflict", "error.user.disable_conflict");
            }

            // The immutable payload and envelope retain the disable audit even after retries or manual replay.
            var task = new ReliableOutboxMessage
            {
                Id = SnowFlakeSingle.Instance.NextId(), TenantId = tenantId, TaskType = ReliableTaskTypes.AccountDisabled,
                IdempotencyKey = DisableKey(userId), AggregateType = nameof(User),
                AggregateId = userId.ToString(CultureInfo.InvariantCulture),
                PayloadJson = JsonSerializer.Serialize(new AccountDisableAudit(operatorId, reason)),
                Status = ReliableOutboxStatuses.Processing, LockedBy = "account-disable", LockedAtUtc = now,
                OccurredAtUtc = now, AvailableAtUtc = now, CreateTime = now
            };
            await db.Insertable(task).ExecuteCommandAsync();
            db.Ado.CommitTran();
            return (task, true);
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }
    });
}
