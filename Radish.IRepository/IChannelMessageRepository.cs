using System.Linq.Expressions;
using Radish.IRepository.Base;
using Radish.Model;
using SqlSugar;

namespace Radish.IRepository;

/// <summary>聊天室消息仓储接口</summary>
public interface IChannelMessageRepository : IBaseRepository<ChannelMessage>
{
    /// <summary>查询单条消息（包含已撤回消息）</summary>
    Task<ChannelMessage?> QueryFirstIncludingDeletedAsync(Expression<Func<ChannelMessage, bool>> whereExpression);

    /// <summary>分页查询消息（包含已撤回消息）</summary>
    Task<(List<ChannelMessage> data, int totalCount)> QueryPageIncludingDeletedAsync(
        Expression<Func<ChannelMessage, bool>>? whereExpression,
        int pageIndex,
        int pageSize,
        Expression<Func<ChannelMessage, object>>? orderByExpression,
        OrderByType orderByType);

    /// <summary>批量查询消息（包含已撤回消息）</summary>
    Task<List<ChannelMessage>> QueryByIdsIncludingDeletedAsync(List<long> ids);

    /// <summary>检查消息是否存在（包含已撤回消息）</summary>
    Task<bool> QueryExistsIncludingDeletedAsync(Expression<Func<ChannelMessage, bool>> whereExpression);
}
