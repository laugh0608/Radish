using Radish.Model;

namespace Radish.IRepository;

public sealed record ContentRewardWriteCommand(
    long TenantId,
    long SenderUserId,
    string SenderName,
    string TargetType,
    long TargetId,
    string ReasonCode,
    long IdempotencyRecordId,
    string IdempotencyRequestHash,
    DateTime BusinessDayStartUtc,
    DateTime BusinessDayEndUtc,
    int DailyTotalLimit,
    int DailyRecipientLimit,
    bool RecoverExpiredProcessing,
    DateTime NowUtc);

public sealed record ContentRewardWriteResult(
    ContentReward Reward,
    string TransactionNo,
    long SenderAvailableBalance,
    long TotalCount);

public sealed record ContentRewardTargetKey(string TargetType, long TargetId);

public sealed record ContentRewardTargetCount(
    string TargetType,
    long TargetId,
    long TotalCount);

public sealed class ContentRewardTargetUnavailableException : Exception
{
}

public sealed class ContentRewardSelfNotAllowedException : Exception
{
}

public sealed class ContentRewardAlreadyExistsException : Exception
{
}

public sealed class ContentRewardInsufficientBalanceException : Exception
{
}

public sealed class ContentRewardDailyLimitExceededException : Exception
{
}

public sealed class ContentRewardAccountUnavailableException : Exception
{
}

public sealed class ContentRewardInteractionUnavailableException : Exception
{
}

public sealed class ContentRewardIdempotencyStateException : Exception
{
}

public sealed class ContentRewardConcurrentConflictException : Exception
{
}

public sealed class ContentRewardRecoveryUnavailableException : Exception
{
}

public sealed class ContentRewardRelationshipUnavailableException : Exception
{
    public ContentRewardRelationshipUnavailableException(Exception innerException)
        : base("无法确认用户屏蔽关系。", innerException)
    {
    }
}

public interface IContentRewardRepository
{
    Task<ContentRewardWriteResult> CreateAsync(ContentRewardWriteCommand command);

    Task<(IReadOnlyList<ContentReward> Items, int Total)> QueryTargetPageAsync(
        long tenantId,
        string targetType,
        long targetId,
        int pageIndex,
        int pageSize);

    Task<IReadOnlyList<ContentRewardTargetCount>> QueryTargetCountsAsync(
        long tenantId,
        IReadOnlyCollection<ContentRewardTargetKey> targets);

    Task<IReadOnlySet<ContentRewardTargetKey>> QueryRewardedTargetsAsync(
        long tenantId,
        long senderUserId,
        IReadOnlyCollection<ContentRewardTargetKey> targets);
}
