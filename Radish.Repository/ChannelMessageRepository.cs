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
        if (message.Id <= 0)
        {
            throw new ArgumentException("聊天室消息必须预先分配 ID", nameof(message));
        }

        DbProtectedClient.Ado.BeginTran();
        try
        {
            await DbProtectedClient.Insertable(message).ExecuteCommandAsync();
            if (outboxDraft != null)
            {
                var outboxRepository = _reliableOutboxRepository
                    ?? throw new InvalidOperationException("可靠 Outbox 仓储未注册");
                await outboxRepository.AddAsync(outboxDraft);
            }

            DbProtectedClient.Ado.CommitTran();
            return message.Id;
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
