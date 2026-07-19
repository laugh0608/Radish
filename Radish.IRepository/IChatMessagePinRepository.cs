using Radish.Model;

namespace Radish.IRepository;

public sealed record ChatMessagePinSetCommand(
    long TenantId,
    long ChannelId,
    long MessageId,
    long UserId,
    string UserName,
    bool IsPinned,
    DateTime NowUtc);

public sealed record ChatMessagePinWriteResult(long Revision, bool Changed);

public sealed record ChatMessagePinSnapshot(long TenantId, long Revision, IReadOnlyList<ChatMessagePin> Items);

public sealed class ChatMessagePinTargetUnavailableException : Exception
{
}

public sealed class ChatMessagePinLimitExceededException : Exception
{
}

public sealed class ChatMessagePinConcurrentConflictException : Exception
{
    public ChatMessagePinConcurrentConflictException(Exception? innerException = null)
        : base("消息置顶状态发生并发冲突。", innerException)
    {
    }
}

public interface IChatMessagePinRepository
{
    Task<ChatMessagePinSnapshot?> GetSnapshotAsync(long tenantId, long channelId);

    Task<ChatMessagePinWriteResult> SetAsync(ChatMessagePinSetCommand command);
}
