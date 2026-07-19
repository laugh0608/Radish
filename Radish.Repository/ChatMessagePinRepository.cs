using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using SqlSugar;

namespace Radish.Repository;

/// <summary>Chat 库消息置顶集合的原子状态仓储。</summary>
public sealed class ChatMessagePinRepository : BaseRepository<ChatMessagePin>, IChatMessagePinRepository
{
    private const int MaxActivePinsPerChannel = 20;

    public ChatMessagePinRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public async Task<ChatMessagePinSnapshot?> GetSnapshotAsync(long tenantId, long channelId)
    {
        if (channelId <= 0)
        {
            return null;
        }

        var sqliteLockHeld = await ChatMessagePinTransactionLock.EnterSqliteAsync(DbProtectedClient);
        try
        {
            DbProtectedClient.Ado.BeginTran();
            try
            {
                var channel = await DbProtectedClient.Queryable<Channel>()
                .Where(candidate =>
                    candidate.Id == channelId &&
                    (candidate.TenantId == tenantId || candidate.TenantId == 0) &&
                    candidate.IsEnabled &&
                    !candidate.IsDeleted)
                .Select(candidate => new Channel
                {
                    Id = candidate.Id,
                    TenantId = candidate.TenantId,
                    PinRevision = candidate.PinRevision
                })
                .FirstAsync();
                if (channel == null)
                {
                    DbProtectedClient.Ado.CommitTran();
                    return null;
                }

                await ChatMessagePinTransactionLock.AcquirePostgreSqlAsync(
                    DbProtectedClient,
                    channel.TenantId,
                    channelId);
                channel.PinRevision = await DbProtectedClient.Queryable<Channel>()
                    .Where(candidate =>
                        candidate.Id == channelId &&
                        candidate.TenantId == channel.TenantId &&
                        candidate.IsEnabled &&
                        !candidate.IsDeleted)
                    .Select(candidate => candidate.PinRevision)
                    .FirstAsync();

                var items = await DbProtectedClient.Queryable<ChatMessagePin>()
                .Where(pin =>
                    pin.TenantId == channel.TenantId &&
                    pin.ChannelId == channelId &&
                    !pin.IsDeleted)
                .OrderByDescending(pin => pin.PinnedAt)
                .OrderByDescending(pin => pin.Id)
                .Take(MaxActivePinsPerChannel)
                    .ToListAsync();
                DbProtectedClient.Ado.CommitTran();
                return new ChatMessagePinSnapshot(channel.TenantId, channel.PinRevision, items);
            }
            catch
            {
                DbProtectedClient.Ado.RollbackTran();
                throw;
            }
        }
        finally
        {
            ChatMessagePinTransactionLock.ExitSqlite(sqliteLockHeld);
        }
    }

    public async Task<ChatMessagePinWriteResult> SetAsync(ChatMessagePinSetCommand command)
    {
        var sqliteLockHeld = await ChatMessagePinTransactionLock.EnterSqliteAsync(DbProtectedClient);
        try
        {
            return await SetWithConflictRecoveryAsync(command);
        }
        finally
        {
            ChatMessagePinTransactionLock.ExitSqlite(sqliteLockHeld);
        }
    }

    private async Task<ChatMessagePinWriteResult> SetWithConflictRecoveryAsync(ChatMessagePinSetCommand command)
    {
        try
        {
            return await SetCoreAsync(command);
        }
        catch (Exception exception) when (IsUniqueConstraintConflict(exception))
        {
            try
            {
                return await SetCoreAsync(command);
            }
            catch (Exception retryException)
            {
                throw new ChatMessagePinConcurrentConflictException(retryException);
            }
        }
    }

    private async Task<ChatMessagePinWriteResult> SetCoreAsync(ChatMessagePinSetCommand command)
    {
        DbProtectedClient.Ado.BeginTran();
        try
        {
            await ChatMessagePinTransactionLock.AcquirePostgreSqlAsync(
                DbProtectedClient,
                command.TenantId,
                command.ChannelId);
            var channel = await DbProtectedClient.Queryable<Channel>()
                .Where(candidate =>
                    candidate.Id == command.ChannelId &&
                    candidate.TenantId == command.TenantId &&
                    candidate.IsEnabled &&
                    !candidate.IsDeleted)
                .Select(candidate => new Channel
                {
                    Id = candidate.Id,
                    PinRevision = candidate.PinRevision
                })
                .FirstAsync();
            if (channel == null)
            {
                throw new ChatMessagePinTargetUnavailableException();
            }

            var messageExists = await DbProtectedClient.Queryable<ChannelMessage>()
                .Where(message =>
                    message.Id == command.MessageId &&
                    message.ChannelId == command.ChannelId &&
                    message.TenantId == command.TenantId &&
                    !message.IsDeleted)
                .AnyAsync();
            if (!messageExists)
            {
                throw new ChatMessagePinTargetUnavailableException();
            }

            var pin = await DbProtectedClient.Queryable<ChatMessagePin>()
                .Where(candidate =>
                    candidate.TenantId == command.TenantId &&
                    candidate.ChannelId == command.ChannelId &&
                    candidate.MessageId == command.MessageId)
                .FirstAsync();
            var changed = command.IsPinned ? pin == null || pin.IsDeleted : pin is { IsDeleted: false };

            if (changed && command.IsPinned)
            {
                var activeCount = await DbProtectedClient.Queryable<ChatMessagePin>()
                    .Where(candidate =>
                        candidate.TenantId == command.TenantId &&
                        candidate.ChannelId == command.ChannelId &&
                        !candidate.IsDeleted)
                    .CountAsync();
                if (activeCount >= MaxActivePinsPerChannel)
                {
                    throw new ChatMessagePinLimitExceededException();
                }

                if (pin == null)
                {
                    await DbProtectedClient.Insertable(new ChatMessagePin
                    {
                        Id = SnowFlakeSingle.Instance.NextId(),
                        TenantId = command.TenantId,
                        ChannelId = command.ChannelId,
                        MessageId = command.MessageId,
                        PinnedByUserId = command.UserId,
                        PinnedByName = command.UserName,
                        PinnedAt = command.NowUtc,
                        CreateTime = command.NowUtc,
                        CreateBy = command.UserName,
                        CreateId = command.UserId
                    }).ExecuteCommandAsync();
                }
                else
                {
                    var affected = await DbProtectedClient.Updateable<ChatMessagePin>()
                        .SetColumns(candidate => new ChatMessagePin
                        {
                            PinnedByUserId = command.UserId,
                            PinnedByName = command.UserName,
                            PinnedAt = command.NowUtc,
                            IsDeleted = false,
                            DeletedAt = null,
                            DeletedBy = null,
                            DeletedByUserId = null,
                            ModifyTime = command.NowUtc,
                            ModifyBy = command.UserName,
                            ModifyId = command.UserId
                        })
                        .Where(candidate => candidate.Id == pin.Id && candidate.IsDeleted)
                        .ExecuteCommandAsync();
                    EnsureSingleRowAffected(affected, "恢复消息置顶");
                }
            }
            else if (changed)
            {
                var affected = await DbProtectedClient.Updateable<ChatMessagePin>()
                    .SetColumns(candidate => new ChatMessagePin
                    {
                        IsDeleted = true,
                        DeletedAt = command.NowUtc,
                        DeletedBy = command.UserName,
                        DeletedByUserId = command.UserId,
                        ModifyTime = command.NowUtc,
                        ModifyBy = command.UserName,
                        ModifyId = command.UserId
                    })
                    .Where(candidate => candidate.Id == pin!.Id && !candidate.IsDeleted)
                    .ExecuteCommandAsync();
                EnsureSingleRowAffected(affected, "取消消息置顶");
            }

            var revision = channel.PinRevision;
            if (changed)
            {
                var affected = await DbProtectedClient.Updateable<Channel>()
                    .SetColumns(candidate => candidate.PinRevision == candidate.PinRevision + 1)
                    .Where(candidate =>
                        candidate.Id == command.ChannelId &&
                        candidate.TenantId == command.TenantId &&
                        candidate.IsEnabled &&
                        !candidate.IsDeleted)
                    .ExecuteCommandAsync();
                EnsureSingleRowAffected(affected, "递增消息置顶 revision");
                revision = await DbProtectedClient.Queryable<Channel>()
                    .Where(candidate => candidate.Id == command.ChannelId)
                    .Select(candidate => candidate.PinRevision)
                    .FirstAsync();
            }

            DbProtectedClient.Ado.CommitTran();
            return new ChatMessagePinWriteResult(revision, changed);
        }
        catch
        {
            DbProtectedClient.Ado.RollbackTran();
            throw;
        }
    }

    private static void EnsureSingleRowAffected(int affectedRows, string operation)
    {
        if (affectedRows != 1)
        {
            throw new InvalidOperationException($"{operation}失败，数据库影响行数为 {affectedRows}。");
        }
    }

    private static bool IsUniqueConstraintConflict(Exception exception)
    {
        var message = exception.ToString();
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("23505", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("idx_chat_message_pin_unique", StringComparison.OrdinalIgnoreCase);
    }
}
