using Radish.Model;

namespace Radish.IRepository;

public sealed record ChatMessageReactionSetCommand(
    long TenantId,
    long ChannelId,
    long MessageId,
    long UserId,
    string UserName,
    string EmojiType,
    string EmojiValue,
    long? StickerAttachmentId,
    bool IsActive,
    string ClientOperationId,
    DateTime NowUtc,
    DateTime OperationExpiresAtUtc);

public sealed record ChatMessageReactionWriteResult(long Revision, bool Changed, bool Replayed);

public sealed class ChatMessageReactionIdempotencyConflictException : Exception
{
}

public sealed class ChatMessageReactionTargetUnavailableException : Exception
{
}

public sealed class ChatMessageReactionLimitExceededException : Exception
{
}

public sealed class ChatMessageReactionConcurrentConflictException : Exception
{
    public ChatMessageReactionConcurrentConflictException()
        : base("消息回应状态发生并发冲突。")
    {
    }

    public ChatMessageReactionConcurrentConflictException(Exception innerException)
        : base("消息回应状态发生并发冲突。", innerException)
    {
    }
}

public interface IChatMessageReactionRepository
{
    Task<IReadOnlyList<ChatMessageReaction>> QueryActiveByMessageIdsAsync(
        long tenantId,
        IReadOnlyCollection<long> messageIds);

    Task<ChatMessageReactionWriteResult> SetAsync(ChatMessageReactionSetCommand command);

    Task<int> DeleteExpiredOperationsAsync(DateTime nowUtc, int batchSize);
}
