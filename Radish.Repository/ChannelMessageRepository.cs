using System.Linq.Expressions;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using SqlSugar;

namespace Radish.Repository;

/// <summary>聊天室消息仓储</summary>
public class ChannelMessageRepository : BaseRepository<ChannelMessage>, IChannelMessageRepository
{
    private readonly IReliableOutboxRepository? _reliableOutboxRepository;

    public ChannelMessageRepository(
        IUnitOfWorkManage unitOfWorkManage,
        IReliableOutboxRepository? reliableOutboxRepository = null) : base(unitOfWorkManage)
    {
        _reliableOutboxRepository = reliableOutboxRepository;
    }

    public async Task<long> AddWithOutboxAsync(ChannelMessage message, ReliableOutboxDraft? outboxDraft)
    {
        var result = await AddWithEffectsAsync(
            message,
            outboxDraft == null ? [] : [outboxDraft]);
        return result.MessageId;
    }

    public async Task<ChannelMessageWriteResult> AddWithEffectsAsync(
        ChannelMessage message,
        IReadOnlyCollection<ReliableOutboxDraft> outboxDrafts,
        long? unarchiveUserId = null,
        long? claimPendingConversationId = null)
    {
        if (message.Id <= 0)
        {
            throw new ArgumentException("聊天室消息必须预先分配 ID", nameof(message));
        }

        DbProtectedClient.Ado.BeginTran();
        try
        {
            if (claimPendingConversationId > 0)
            {
                var claimed = await DbProtectedClient.Updateable<DirectConversation>()
                    .SetColumns(conversation => new DirectConversation
                    {
                        RequestMessageId = message.Id,
                        ModifyTime = message.CreateTime,
                        ModifyBy = message.UserName,
                        ModifyId = message.UserId
                    })
                    .Where(conversation =>
                        conversation.Id == claimPendingConversationId.Value &&
                        conversation.RequestStatus == DirectConversationRequestStatus.Pending &&
                        conversation.RequestMessageId == null &&
                        conversation.BlockedByUserId == null &&
                        !conversation.IsDeleted)
                    .ExecuteCommandAsync();
                if (claimed == 0)
                {
                    throw new DirectConversationRequestClaimException();
                }
            }

            await DbProtectedClient.Insertable(message).ExecuteCommandAsync();
            if (outboxDrafts.Count > 0)
            {
                var outboxRepository = _reliableOutboxRepository
                    ?? throw new InvalidOperationException("可靠 Outbox 仓储未注册");
                foreach (var outboxDraft in outboxDrafts)
                {
                    await outboxRepository.AddAsync(outboxDraft);
                }
            }

            var peerWasUnarchived = false;
            if (unarchiveUserId > 0)
            {
                var affected = await DbProtectedClient.Updateable<ChannelMember>()
                    .SetColumns(member => new ChannelMember
                    {
                        ArchivedAt = null,
                        ModifyTime = message.CreateTime,
                        ModifyBy = message.UserName,
                        ModifyId = message.UserId
                    })
                    .Where(member =>
                        member.ChannelId == message.ChannelId &&
                        member.UserId == unarchiveUserId.Value &&
                        member.ArchivedAt != null &&
                        !member.IsDeleted)
                    .ExecuteCommandAsync();
                peerWasUnarchived = affected > 0;
            }

            DbProtectedClient.Ado.CommitTran();
            return new ChannelMessageWriteResult(message.Id, peerWasUnarchived);
        }
        catch
        {
            DbProtectedClient.Ado.RollbackTran();
            throw;
        }
    }

    public async Task<ChannelMessage?> QueryFirstIncludingDeletedAsync(Expression<Func<ChannelMessage, bool>> whereExpression)
    {
        return await ExecuteDbOperationAsync(
            () => CreateTenantQueryableFor<ChannelMessage>(includeDeleted: true).FirstAsync(whereExpression));
    }

    public async Task<(List<ChannelMessage> data, int totalCount)> QueryPageIncludingDeletedAsync(
        Expression<Func<ChannelMessage, bool>>? whereExpression,
        int pageIndex,
        int pageSize,
        Expression<Func<ChannelMessage, object>>? orderByExpression,
        OrderByType orderByType)
    {
        return await ExecuteDbOperationAsync(async () =>
        {
            RefAsync<int> totalCount = 0;
            var query = CreateTenantQueryableFor<ChannelMessage>(includeDeleted: true);
            if (whereExpression != null)
            {
                query = query.Where(whereExpression);
            }

            if (orderByExpression != null)
            {
                query = orderByType == OrderByType.Asc
                    ? query.OrderBy(orderByExpression)
                    : query.OrderByDescending(orderByExpression);
            }

            var data = await query.ToPageListAsync(pageIndex, pageSize, totalCount);
            return (data, totalCount.Value);
        });
    }

    public async Task<List<ChannelMessage>> QueryByIdsIncludingDeletedAsync(List<long> ids)
    {
        if (ids.Count == 0)
        {
            return new List<ChannelMessage>();
        }

        return await ExecuteDbOperationAsync(
            () => CreateTenantQueryableFor<ChannelMessage>(includeDeleted: true)
                .In(ids)
                .ToListAsync());
    }

    public async Task<bool> QueryExistsIncludingDeletedAsync(Expression<Func<ChannelMessage, bool>> whereExpression)
    {
        return await ExecuteDbOperationAsync(
            () => CreateTenantQueryableFor<ChannelMessage>(includeDeleted: true)
                .Where(whereExpression)
                .AnyAsync());
    }
}
