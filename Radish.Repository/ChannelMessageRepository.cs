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
    public ChannelMessageRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public async Task<ChannelMessage?> QueryFirstIncludingDeletedAsync(Expression<Func<ChannelMessage, bool>> whereExpression)
    {
        return await CreateTenantQueryableFor<ChannelMessage>(includeDeleted: true)
            .FirstAsync(whereExpression);
    }

    public async Task<(List<ChannelMessage> data, int totalCount)> QueryPageIncludingDeletedAsync(
        Expression<Func<ChannelMessage, bool>>? whereExpression,
        int pageIndex,
        int pageSize,
        Expression<Func<ChannelMessage, object>>? orderByExpression,
        OrderByType orderByType)
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
        return (data, totalCount);
    }

    public async Task<List<ChannelMessage>> QueryByIdsIncludingDeletedAsync(List<long> ids)
    {
        if (ids.Count == 0)
        {
            return new List<ChannelMessage>();
        }

        return await CreateTenantQueryableFor<ChannelMessage>(includeDeleted: true)
            .In(ids)
            .ToListAsync();
    }

    public async Task<bool> QueryExistsIncludingDeletedAsync(Expression<Func<ChannelMessage, bool>> whereExpression)
    {
        return await CreateTenantQueryableFor<ChannelMessage>(includeDeleted: true)
            .Where(whereExpression)
            .AnyAsync();
    }
}
