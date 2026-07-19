using System.Linq.Expressions;
using Radish.IRepository.Base;
using Radish.Model;
using SqlSugar;

namespace Radish.IRepository;

/// <summary>Chat 消息事务写入结果</summary>
public sealed record ChannelMessageWriteResult(long MessageId, bool PeerWasUnarchived);

/// <summary>陌生私聊请求首条消息已被其他写入占用。</summary>
public sealed class DirectConversationRequestClaimException : Exception
{
    public DirectConversationRequestClaimException()
        : base("陌生私聊请求已存在首条消息。")
    {
    }
}

/// <summary>聊天室消息仓储接口</summary>
public interface IChannelMessageRepository : IBaseRepository<ChannelMessage>
{
    Task<long> AddWithOutboxAsync(ChannelMessage message, ReliableOutboxDraft? outboxDraft);

    /// <summary>在 Chat 单事务内写入消息、多个可靠任务并清除接收方归档。</summary>
    Task<ChannelMessageWriteResult> AddWithEffectsAsync(
        ChannelMessage message,
        IReadOnlyCollection<ReliableOutboxDraft> outboxDrafts,
        long? unarchiveUserId = null,
        long? claimPendingConversationId = null);

    /// <summary>在 Chat 单事务内撤回消息并软删除其全部活跃回应。</summary>
    Task<int> RecallWithReactionsAsync(
        long messageId,
        long operatorId,
        string operatorName,
        DateTime nowUtc);

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
