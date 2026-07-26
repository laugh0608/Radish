using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Radish.Common;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using SqlSugar;

namespace Radish.Repository;

/// <summary>Main 库用户屏蔽、关注解除、操作回放和可靠任务的原子事务边界。</summary>
public sealed class UserBlockRepository : BaseRepository<UserBlock>, IUserBlockRepository
{
    public UserBlockRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public Task<UserBlockWriteResult> MutateAsync(UserBlockMutationCommand command)
    {
        ValidateCommand(command);
        return MutateWithLockAsync(command);
    }

    public async Task<UserBlock?> QueryPairIncludingDeletedAsync(
        long tenantId,
        long blockerUserId,
        long blockedUserId)
    {
        return await ExecuteDbOperationAsync(() => DbProtectedClient.Queryable<UserBlock>()
            .Where(item =>
                item.TenantId == tenantId &&
                item.BlockerUserId == blockerUserId &&
                item.BlockedUserId == blockedUserId)
            .FirstAsync());
    }

    public async Task<IReadOnlyList<UserBlock>> QueryActiveBetweenAsync(
        long tenantId,
        long currentUserId,
        IReadOnlyCollection<long> otherUserIds)
    {
        var normalizedIds = otherUserIds.Where(id => id > 0 && id != currentUserId).Distinct().ToList();
        if (normalizedIds.Count == 0)
        {
            return [];
        }

        return await ExecuteDbOperationAsync(() => DbProtectedClient.Queryable<UserBlock>()
            .Where(item =>
                item.TenantId == tenantId &&
                !item.IsDeleted &&
                ((item.BlockerUserId == currentUserId && normalizedIds.Contains(item.BlockedUserId)) ||
                 (item.BlockedUserId == currentUserId && normalizedIds.Contains(item.BlockerUserId))))
            .ToListAsync());
    }

    public async Task<IReadOnlyList<long>> QueryBarrierUserIdsAsync(long tenantId, long currentUserId)
    {
        var relations = await ExecuteDbOperationAsync(() => DbProtectedClient.Queryable<UserBlock>()
            .Where(item =>
                item.TenantId == tenantId &&
                !item.IsDeleted &&
                (item.BlockerUserId == currentUserId || item.BlockedUserId == currentUserId))
            .Select(item => new UserBlock
            {
                BlockerUserId = item.BlockerUserId,
                BlockedUserId = item.BlockedUserId
            })
            .ToListAsync());
        return relations
            .Select(item => item.BlockerUserId == currentUserId ? item.BlockedUserId : item.BlockerUserId)
            .Where(id => id > 0 && id != currentUserId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
    }

    public async Task<(IReadOnlyList<UserBlock> Items, int Total)> QueryMineAsync(
        long tenantId,
        long blockerUserId,
        int pageIndex,
        int pageSize)
    {
        var safePageIndex = Math.Max(1, pageIndex);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        return await ExecuteDbOperationAsync(async () =>
        {
            var query = DbProtectedClient.Queryable<UserBlock>()
                .Where(item =>
                    item.TenantId == tenantId &&
                    item.BlockerUserId == blockerUserId &&
                    !item.IsDeleted);
            var total = await query.CountAsync();
            var items = await query
                .OrderBy(item => item.CreateTime, OrderByType.Desc)
                .OrderBy(item => item.Id, OrderByType.Desc)
                .Skip((safePageIndex - 1) * safePageSize)
                .Take(safePageSize)
                .ToListAsync();
            return ((IReadOnlyList<UserBlock>)items, total);
        });
    }

    private async Task<UserBlockWriteResult> MutateCoreAsync(UserBlockMutationCommand command)
    {
        DbProtectedClient.Ado.BeginTran();
        try
        {
            await UserInteractionTransactionLock.AcquirePostgreSqlAsync(
                DbProtectedClient,
                command.TenantId,
                command.ActorUserId,
                command.TargetUserId);
            await AcquirePostgreSqlLockAsync(
                $"user-block-operation:{command.TenantId}:{command.ActorUserId}:{command.OperationKey}");

            var replay = await DbProtectedClient.Queryable<UserBlockOperation>()
                .Where(item =>
                    item.TenantId == command.TenantId &&
                    item.ActorUserId == command.ActorUserId &&
                    item.OperationKey == command.OperationKey)
                .FirstAsync();
            if (replay != null)
            {
                EnsureSameOperation(replay, command);
                var replayBlock = await DbProtectedClient.Queryable<UserBlock>()
                    .Where(item => item.Id == replay.UserBlockId && item.TenantId == command.TenantId)
                    .FirstAsync();
                if (replayBlock == null)
                {
                    throw new InvalidOperationException("UserBlock operation 引用了不存在的关系记录。");
                }

                DbProtectedClient.Ado.CommitTran();
                return new UserBlockWriteResult(
                    replayBlock,
                    replay.ResultRelationshipVersion,
                    replay.ResultChanged,
                    true);
            }

            var block = await DbProtectedClient.Queryable<UserBlock>()
                .Where(item =>
                    item.TenantId == command.TenantId &&
                    item.BlockerUserId == command.ActorUserId &&
                    item.BlockedUserId == command.TargetUserId)
                .FirstAsync();
            var changed = false;
            if (command.OperationType == UserBlockOperationTypes.Block)
            {
                (block, changed) = await ApplyBlockAsync(command, block);
                await DeleteFollowsAsync(command);
            }
            else
            {
                if (block is not { IsDeleted: false })
                {
                    throw new UserBlockStateConflictException();
                }

                block.IsDeleted = true;
                block.DeletedAt = command.NowUtc;
                block.DeletedBy = command.OperatorName;
                block.RelationshipVersion++;
                block.ModifyTime = command.NowUtc;
                block.ModifyBy = command.OperatorName;
                block.ModifyId = command.ActorUserId;
                EnsureSingleRowAffected(
                    await DbProtectedClient.Updateable(block).ExecuteCommandAsync(),
                    "解除用户屏蔽");
                changed = true;
            }

            if (changed)
            {
                await AddOutboxAsync(command, block);
            }

            await DbProtectedClient.Insertable(new UserBlockOperation
            {
                Id = SnowFlakeSingle.Instance.NextId(),
                TenantId = command.TenantId,
                ActorUserId = command.ActorUserId,
                OperationKey = command.OperationKey,
                OperationType = command.OperationType,
                TargetUserId = command.TargetUserId,
                UserBlockId = block.Id,
                ResultRelationshipVersion = block.RelationshipVersion,
                ResultChanged = changed,
                CreateTime = command.NowUtc,
                CreateBy = command.OperatorName,
                CreateId = command.ActorUserId
            }).ExecuteCommandAsync();

            DbProtectedClient.Ado.CommitTran();
            return new UserBlockWriteResult(block, block.RelationshipVersion, changed, false);
        }
        catch
        {
            DbProtectedClient.Ado.RollbackTran();
            throw;
        }
    }

    private async Task<UserBlockWriteResult> MutateWithLockAsync(UserBlockMutationCommand command)
    {
        var sqliteLock = await UserInteractionTransactionLock.EnterSqliteAsync(
            DbProtectedClient,
            command.TenantId,
            command.ActorUserId,
            command.TargetUserId);
        string? sqliteOperationLock = null;
        try
        {
            sqliteOperationLock = await UserInteractionTransactionLock.EnterSqliteOperationAsync(
                DbProtectedClient,
                command.TenantId,
                command.ActorUserId,
                command.OperationKey);
            return await MutateCoreAsync(command);
        }
        finally
        {
            UserInteractionTransactionLock.ExitSqlite(sqliteOperationLock);
            UserInteractionTransactionLock.ExitSqlite(sqliteLock);
        }
    }

    private async Task<(UserBlock Block, bool Changed)> ApplyBlockAsync(
        UserBlockMutationCommand command,
        UserBlock? block)
    {
        if (block == null)
        {
            block = new UserBlock
            {
                Id = SnowFlakeSingle.Instance.NextId(),
                TenantId = command.TenantId,
                BlockerUserId = command.ActorUserId,
                BlockedUserId = command.TargetUserId,
                RelationshipVersion = 1,
                CreateTime = command.NowUtc,
                CreateBy = command.OperatorName,
                CreateId = command.ActorUserId
            };
            EnsureSingleRowAffected(
                await DbProtectedClient.Insertable(block).ExecuteCommandAsync(),
                "创建用户屏蔽");
            return (block, true);
        }

        if (!block.IsDeleted)
        {
            return (block, false);
        }

        block.IsDeleted = false;
        block.DeletedAt = null;
        block.DeletedBy = null;
        block.RelationshipVersion = Math.Max(0, block.RelationshipVersion) + 1;
        block.ModifyTime = command.NowUtc;
        block.ModifyBy = command.OperatorName;
        block.ModifyId = command.ActorUserId;
        EnsureSingleRowAffected(
            await DbProtectedClient.Updateable(block).ExecuteCommandAsync(),
            "恢复用户屏蔽");
        return (block, true);
    }

    private async Task DeleteFollowsAsync(UserBlockMutationCommand command)
    {
        await DbProtectedClient.Updateable<UserFollow>()
            .SetColumns(item => new UserFollow
            {
                IsDeleted = true,
                DeletedAt = command.NowUtc,
                DeletedBy = command.OperatorName,
                ModifyTime = command.NowUtc,
                ModifyBy = command.OperatorName,
                ModifyId = command.ActorUserId
            })
            .Where(item =>
                item.TenantId == command.TenantId &&
                !item.IsDeleted &&
                ((item.FollowerUserId == command.ActorUserId && item.FollowingUserId == command.TargetUserId) ||
                 (item.FollowerUserId == command.TargetUserId && item.FollowingUserId == command.ActorUserId)))
            .ExecuteCommandAsync();
    }

    private async Task AddOutboxAsync(UserBlockMutationCommand command, UserBlock block)
    {
        var eventType = command.OperationType == UserBlockOperationTypes.Block
            ? UserBlockRelationshipEventTypes.Blocked
            : UserBlockRelationshipEventTypes.Unblocked;
        var payload = new UserBlockRelationshipChangedTaskPayload(
            command.TenantId,
            block.Id,
            eventType,
            command.ActorUserId,
            command.TargetUserId,
            block.RelationshipVersion,
            command.NowUtc);
        await DbProtectedClient.Insertable(new ReliableOutboxMessage
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            TenantId = command.TenantId,
            TaskType = ReliableTaskTypes.UserBlockRelationshipChanged,
            SchemaVersion = 1,
            IdempotencyKey = $"task:user-block:{block.Id}:version:{block.RelationshipVersion}",
            AggregateType = "UserBlock",
            AggregateId = block.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            PayloadJson = JsonSerializer.Serialize(payload),
            Status = ReliableOutboxStatuses.Pending,
            MaxAttempts = 6,
            OccurredAtUtc = command.NowUtc,
            AvailableAtUtc = command.NowUtc,
            CreateTime = command.NowUtc
        }).ExecuteCommandAsync();
    }

    private async Task AcquirePostgreSqlLockAsync(string identity)
    {
        if (DbProtectedClient.CurrentConnectionConfig.DbType != DbType.PostgreSQL)
        {
            return;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        var lockKey = BinaryPrimitives.ReadInt64BigEndian(hash);
        await DbProtectedClient.Ado.ExecuteCommandAsync(
            "SELECT pg_advisory_xact_lock(@LockKey)",
            new SugarParameter("@LockKey", lockKey));
    }

    private static void ValidateCommand(UserBlockMutationCommand command)
    {
        if (command.ActorUserId <= 0 || command.TargetUserId <= 0 || command.ActorUserId == command.TargetUserId)
        {
            throw new ArgumentException("用户屏蔽关系参数无效。", nameof(command));
        }

        if (command.OperationType is not (UserBlockOperationTypes.Block or UserBlockOperationTypes.Unblock))
        {
            throw new ArgumentException("用户屏蔽操作类型无效。", nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command.OperationKey) || command.OperationKey.Length > 100)
        {
            throw new ArgumentException("operationKey 无效。", nameof(command));
        }
    }

    private static void EnsureSameOperation(UserBlockOperation operation, UserBlockMutationCommand command)
    {
        if (operation.TargetUserId != command.TargetUserId ||
            !string.Equals(operation.OperationType, command.OperationType, StringComparison.Ordinal))
        {
            throw new UserBlockOperationConflictException();
        }
    }

    private static void EnsureSingleRowAffected(int affectedRows, string operation)
    {
        if (affectedRows != 1)
        {
            throw new InvalidOperationException($"{operation}失败，数据库影响行数为 {affectedRows}。");
        }
    }
}
