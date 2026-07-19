namespace Radish.IRepository;

/// <summary>个人频道已读游标推进命令。</summary>
public sealed record AdvanceChatReadStateCommand(
    long ChannelId,
    long UserId,
    long MemberTenantId,
    long ReadThroughMessageId,
    bool AllowCreate,
    string OperatorName,
    DateTime NowUtc);

/// <summary>个人频道已读游标权威写入结果。</summary>
public sealed record AdvanceChatReadStateResult(long LastReadMessageId, bool Changed);

/// <summary>普通 Private 中一条自己所发消息的已读人数。</summary>
public sealed class ChatReadCountAggregate
{
    public long MessageId { get; set; }

    public int ReadCount { get; set; }
}

/// <summary>个人成员行不存在、已退出或在写入期间失效。</summary>
public sealed class ChatReadMemberUnavailableException : Exception
{
}

/// <summary>Chat 成员游标的原子写入和受限聚合仓储。</summary>
public interface IChatReadReceiptRepository
{
    Task<AdvanceChatReadStateResult> AdvanceAsync(AdvanceChatReadStateCommand command);

    Task<long?> GetMemberReadCursorAsync(long channelId, long userId);

    Task<IReadOnlyList<ChatReadCountAggregate>> GetReadCountsAsync(
        long channelId,
        long senderUserId,
        IReadOnlyCollection<long> messageIds);

    Task<IReadOnlyList<long>> GetReaderUserIdsAsync(
        long channelId,
        long senderUserId,
        long messageId,
        DateTime messageCreateTime,
        long? afterUserId,
        int take);
}
