using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using SqlSugar;

namespace Radish.Repository;

/// <summary>Chat 库消息回应原子写入与幂等仓储。</summary>
public sealed class ChatMessageReactionRepository : BaseRepository<ChatMessageReaction>, IChatMessageReactionRepository
{
    private const int MaxUserReactionTypesPerMessage = 10;
    private static readonly SemaphoreSlim SqliteWriteLock = new(1, 1);

    public ChatMessageReactionRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public async Task<IReadOnlyList<ChatMessageReaction>> QueryActiveByMessageIdsAsync(
        long tenantId,
        IReadOnlyCollection<long> messageIds)
    {
        if (messageIds.Count == 0)
        {
            return [];
        }

        var ids = messageIds.Distinct().ToList();
        return await ExecuteDbOperationAsync(() => DbProtectedClient.Queryable<ChatMessageReaction>()
            .Where(reaction =>
                reaction.TenantId == tenantId &&
                ids.Contains(reaction.MessageId) &&
                !reaction.IsDeleted)
            .ToListAsync());
    }

    public async Task<ChatMessageReactionWriteResult> SetAsync(ChatMessageReactionSetCommand command)
    {
        if (DbProtectedClient.CurrentConnectionConfig.DbType != DbType.Sqlite)
        {
            return await SetWithConflictRecoveryAsync(command);
        }

        await SqliteWriteLock.WaitAsync();
        try
        {
            return await SetWithConflictRecoveryAsync(command);
        }
        finally
        {
            SqliteWriteLock.Release();
        }
    }

    private async Task<ChatMessageReactionWriteResult> SetWithConflictRecoveryAsync(
        ChatMessageReactionSetCommand command)
    {
        try
        {
            return await SetCoreAsync(command);
        }
        catch (Exception exception) when (IsUniqueConstraintConflict(exception))
        {
            var replay = await QueryOperationAsync(command);
            if (replay == null)
            {
                throw new ChatMessageReactionConcurrentConflictException(exception);
            }

            EnsureSameFingerprint(replay, command);
            return new ChatMessageReactionWriteResult(replay.ResultRevision, false, true);
        }
    }

    public async Task<int> DeleteExpiredOperationsAsync(DateTime nowUtc, int batchSize)
    {
        if (batchSize <= 0)
        {
            return 0;
        }

        var operationIds = await ExecuteDbOperationAsync(() =>
            DbProtectedClient.Queryable<ChatMessageReactionOperation>()
                .Where(operation => operation.ExpiresAtUtc <= nowUtc)
                .OrderBy(operation => operation.ExpiresAtUtc)
                .OrderBy(operation => operation.Id)
                .Take(batchSize)
                .Select(operation => operation.Id)
                .ToListAsync());
        if (operationIds.Count == 0)
        {
            return 0;
        }

        return await ExecuteDbOperationAsync(() =>
            DbProtectedClient.Deleteable<ChatMessageReactionOperation>()
                .In(operation => operation.Id, operationIds)
                .ExecuteCommandAsync());
    }

    private async Task<ChatMessageReactionWriteResult> SetCoreAsync(ChatMessageReactionSetCommand command)
    {
        DbProtectedClient.Ado.BeginTran();
        try
        {
            var operation = await QueryOperationAsync(command);
            if (operation != null)
            {
                EnsureSameFingerprint(operation, command);
                DbProtectedClient.Ado.CommitTran();
                return new ChatMessageReactionWriteResult(operation.ResultRevision, false, true);
            }

            var message = await DbProtectedClient.Queryable<ChannelMessage>()
                .Where(candidate =>
                    candidate.Id == command.MessageId &&
                    candidate.ChannelId == command.ChannelId &&
                    candidate.TenantId == command.TenantId &&
                    !candidate.IsDeleted)
                .FirstAsync();
            if (message == null)
            {
                throw new ChatMessageReactionTargetUnavailableException();
            }

            var reaction = await DbProtectedClient.Queryable<ChatMessageReaction>()
                .Where(candidate =>
                    candidate.TenantId == command.TenantId &&
                    candidate.MessageId == command.MessageId &&
                    candidate.UserId == command.UserId &&
                    candidate.EmojiType == command.EmojiType &&
                    candidate.EmojiValue == command.EmojiValue)
                .FirstAsync();

            var changed = command.IsActive ? reaction == null || reaction.IsDeleted : reaction is { IsDeleted: false };
            if (changed && command.IsActive)
            {
                await EnsureReactionLimitAsync(command);
                if (reaction == null)
                {
                    await DbProtectedClient.Insertable(new ChatMessageReaction
                    {
                        Id = SnowFlakeSingle.Instance.NextId(),
                        TenantId = command.TenantId,
                        ChannelId = command.ChannelId,
                        MessageId = command.MessageId,
                        UserId = command.UserId,
                        UserName = command.UserName,
                        EmojiType = command.EmojiType,
                        EmojiValue = command.EmojiValue,
                        StickerAttachmentId = command.StickerAttachmentId,
                        CreateTime = command.NowUtc,
                        CreateBy = command.UserName,
                        CreateId = command.UserId
                    }).ExecuteCommandAsync();
                }
                else
                {
                    await DbProtectedClient.Updateable<ChatMessageReaction>()
                        .SetColumns(candidate => new ChatMessageReaction
                        {
                            IsDeleted = false,
                            DeletedAt = null,
                            DeletedBy = null,
                            UserName = command.UserName,
                            StickerAttachmentId = command.StickerAttachmentId,
                            ModifyTime = command.NowUtc,
                            ModifyBy = command.UserName,
                            ModifyId = command.UserId
                        })
                        .Where(candidate => candidate.Id == reaction.Id && candidate.IsDeleted)
                        .ExecuteCommandAsync();
                }
            }
            else if (changed)
            {
                await DbProtectedClient.Updateable<ChatMessageReaction>()
                    .SetColumns(candidate => new ChatMessageReaction
                    {
                        IsDeleted = true,
                        DeletedAt = command.NowUtc,
                        DeletedBy = command.UserName,
                        ModifyTime = command.NowUtc,
                        ModifyBy = command.UserName,
                        ModifyId = command.UserId
                    })
                    .Where(candidate => candidate.Id == reaction!.Id && !candidate.IsDeleted)
                    .ExecuteCommandAsync();
            }

            var revision = message.ReactionRevision;
            if (changed)
            {
                var affected = await DbProtectedClient.Updateable<ChannelMessage>()
                    .SetColumns(candidate => candidate.ReactionRevision == candidate.ReactionRevision + 1)
                    .Where(candidate =>
                        candidate.Id == command.MessageId &&
                        candidate.ChannelId == command.ChannelId &&
                        candidate.TenantId == command.TenantId &&
                        !candidate.IsDeleted)
                    .ExecuteCommandAsync();
                if (affected != 1)
                {
                    throw new ChatMessageReactionTargetUnavailableException();
                }

                revision = await DbProtectedClient.Queryable<ChannelMessage>()
                    .Where(candidate => candidate.Id == command.MessageId)
                    .Select(candidate => candidate.ReactionRevision)
                    .FirstAsync();
            }

            await DbProtectedClient.Insertable(new ChatMessageReactionOperation
            {
                Id = SnowFlakeSingle.Instance.NextId(),
                TenantId = command.TenantId,
                UserId = command.UserId,
                ClientOperationId = command.ClientOperationId,
                ChannelId = command.ChannelId,
                MessageId = command.MessageId,
                EmojiType = command.EmojiType,
                EmojiValue = command.EmojiValue,
                IsActive = command.IsActive,
                ResultRevision = revision,
                ExpiresAtUtc = command.OperationExpiresAtUtc,
                CreateTime = command.NowUtc,
                CreateBy = command.UserName,
                CreateId = command.UserId
            }).ExecuteCommandAsync();

            DbProtectedClient.Ado.CommitTran();
            return new ChatMessageReactionWriteResult(revision, changed, false);
        }
        catch
        {
            DbProtectedClient.Ado.RollbackTran();
            throw;
        }
    }

    private async Task EnsureReactionLimitAsync(ChatMessageReactionSetCommand command)
    {
        var activeCount = await DbProtectedClient.Queryable<ChatMessageReaction>()
            .Where(candidate =>
                candidate.TenantId == command.TenantId &&
                candidate.MessageId == command.MessageId &&
                candidate.UserId == command.UserId &&
                !candidate.IsDeleted)
            .CountAsync();
        if (activeCount >= MaxUserReactionTypesPerMessage)
        {
            throw new ChatMessageReactionLimitExceededException();
        }
    }

    private async Task<ChatMessageReactionOperation?> QueryOperationAsync(ChatMessageReactionSetCommand command)
    {
        return await DbProtectedClient.Queryable<ChatMessageReactionOperation>()
            .Where(candidate =>
                candidate.TenantId == command.TenantId &&
                candidate.UserId == command.UserId &&
                candidate.ClientOperationId == command.ClientOperationId)
            .FirstAsync();
    }

    private static void EnsureSameFingerprint(
        ChatMessageReactionOperation operation,
        ChatMessageReactionSetCommand command)
    {
        if (operation.ChannelId != command.ChannelId ||
            operation.MessageId != command.MessageId ||
            !string.Equals(operation.EmojiType, command.EmojiType, StringComparison.Ordinal) ||
            !string.Equals(operation.EmojiValue, command.EmojiValue, StringComparison.Ordinal) ||
            operation.IsActive != command.IsActive)
        {
            throw new ChatMessageReactionIdempotencyConflictException();
        }
    }

    private static bool IsUniqueConstraintConflict(Exception exception)
    {
        var message = exception.ToString();
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("23505", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("idx_chat_reaction_", StringComparison.OrdinalIgnoreCase);
    }
}
