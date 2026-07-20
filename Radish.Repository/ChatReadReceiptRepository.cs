using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using SqlSugar;

namespace Radish.Repository;

/// <summary>Chat 成员游标的原子写入和受限聚合仓储。</summary>
public sealed class ChatReadReceiptRepository : BaseRepository<ChannelMember>, IChatReadReceiptRepository
{
    public ChatReadReceiptRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public Task<AdvanceChatReadStateResult> AdvanceAsync(AdvanceChatReadStateCommand command)
    {
        return ExecuteDbOperationAsync(() => AdvanceWithConflictRecoveryAsync(command));
    }

    public Task<long?> GetMemberReadCursorAsync(long channelId, long userId)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var member = await DbProtectedClient.Queryable<ChannelMember>()
                .Where(candidate =>
                    candidate.ChannelId == channelId &&
                    candidate.UserId == userId &&
                    !candidate.IsDeleted)
                .Select(candidate => new ChannelMember
                {
                    LastReadMessageId = candidate.LastReadMessageId
                })
                .FirstAsync();
            return member?.LastReadMessageId;
        });
    }

    public Task<IReadOnlyList<ChatReadCountAggregate>> GetReadCountsAsync(
        long channelId,
        long senderUserId,
        IReadOnlyCollection<long> messageIds)
    {
        if (messageIds.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<ChatReadCountAggregate>>([]);
        }

        var targetMessageIds = messageIds.Distinct().ToList();
        return ExecuteDbOperationAsync(async () =>
        {
            var rows = await DbProtectedClient.Queryable<ChannelMessage, ChannelMember>(
                    (message, member) => new JoinQueryInfos(
                        JoinType.Inner,
                        message.ChannelId == member.ChannelId &&
                        member.LastReadMessageId != null &&
                        member.LastReadMessageId.Value >= message.Id &&
                        member.JoinedAt <= message.CreateTime))
                .Where((message, member) =>
                    message.ChannelId == channelId &&
                    targetMessageIds.Contains(message.Id) &&
                    message.UserId == senderUserId &&
                    !message.IsDeleted &&
                    member.UserId != senderUserId &&
                    !member.IsDeleted)
                .GroupBy((message, member) => message.Id)
                .Select((message, member) => new ChatReadCountAggregate
                {
                    MessageId = message.Id,
                    ReadCount = SqlFunc.AggregateCount(member.Id)
                })
                .ToListAsync();
            return (IReadOnlyList<ChatReadCountAggregate>)rows;
        });
    }

    public Task<IReadOnlyList<long>> GetReaderUserIdsAsync(
        long channelId,
        long senderUserId,
        long messageId,
        DateTime messageCreateTime,
        long? afterUserId,
        int take)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var query = DbProtectedClient.Queryable<ChannelMember>()
                .Where(member =>
                    member.ChannelId == channelId &&
                    member.UserId != senderUserId &&
                    !member.IsDeleted &&
                    member.JoinedAt <= messageCreateTime &&
                    member.LastReadMessageId != null &&
                    member.LastReadMessageId.Value >= messageId);
            if (afterUserId.HasValue)
            {
                query = query.Where(member => member.UserId > afterUserId.Value);
            }

            var userIds = await query
                .OrderBy(member => member.UserId)
                .Select(member => member.UserId)
                .Take(take)
                .ToListAsync();
            return (IReadOnlyList<long>)userIds;
        });
    }

    private async Task<AdvanceChatReadStateResult> AdvanceWithConflictRecoveryAsync(
        AdvanceChatReadStateCommand command)
    {
        try
        {
            return await AdvanceCoreAsync(command);
        }
        catch (Exception exception) when (command.AllowCreate && IsUniqueConstraintConflict(exception))
        {
            return await AdvanceCoreAsync(command);
        }
    }

    private async Task<AdvanceChatReadStateResult> AdvanceCoreAsync(AdvanceChatReadStateCommand command)
    {
        DbProtectedClient.Ado.BeginTran();
        try
        {
            var affected = await DbProtectedClient.Updateable<ChannelMember>()
                .SetColumns(member => new ChannelMember
                {
                    LastReadMessageId = command.ReadThroughMessageId,
                    ModifyTime = command.NowUtc,
                    ModifyBy = command.OperatorName,
                    ModifyId = command.UserId
                })
                .Where(member =>
                    member.ChannelId == command.ChannelId &&
                    member.UserId == command.UserId &&
                    !member.IsDeleted &&
                    (member.LastReadMessageId == null ||
                     member.LastReadMessageId.Value < command.ReadThroughMessageId))
                .ExecuteCommandAsync();
            if (affected == 1)
            {
                DbProtectedClient.Ado.CommitTran();
                return new AdvanceChatReadStateResult(command.ReadThroughMessageId, true);
            }

            var existing = await DbProtectedClient.Queryable<ChannelMember>()
                .Where(member =>
                    member.ChannelId == command.ChannelId &&
                    member.UserId == command.UserId)
                .Select(member => new ChannelMember
                {
                    LastReadMessageId = member.LastReadMessageId,
                    IsDeleted = member.IsDeleted
                })
                .FirstAsync();
            if (existing is { IsDeleted: false })
            {
                var current = existing.LastReadMessageId.GetValueOrDefault();
                DbProtectedClient.Ado.CommitTran();
                return new AdvanceChatReadStateResult(current, false);
            }

            if (!command.AllowCreate || existing != null)
            {
                throw new ChatReadMemberUnavailableException();
            }

            await DbProtectedClient.Insertable(new ChannelMember
            {
                Id = SnowFlakeSingle.Instance.NextId(),
                ChannelId = command.ChannelId,
                UserId = command.UserId,
                Role = MemberRole.Member,
                LastReadMessageId = command.ReadThroughMessageId,
                JoinedAt = command.NowUtc,
                TenantId = command.MemberTenantId,
                CreateTime = command.NowUtc,
                CreateBy = command.OperatorName,
                CreateId = command.UserId,
                IsDeleted = false
            }).ExecuteCommandAsync();
            DbProtectedClient.Ado.CommitTran();
            return new AdvanceChatReadStateResult(command.ReadThroughMessageId, true);
        }
        catch
        {
            DbProtectedClient.Ado.RollbackTran();
            throw;
        }
    }

    private static bool IsUniqueConstraintConflict(Exception exception)
    {
        var message = exception.ToString();
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("23505", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("idx_channel_member_channel_user", StringComparison.OrdinalIgnoreCase);
    }
}
