namespace Radish.IRepository;

public sealed record ContentRewardAuditEntry(
    long UserId,
    long ChangeAmount,
    long BalanceBefore,
    long BalanceAfter,
    string ChangeType,
    string SourceEventKey);

public sealed record ContentRewardAuditProjectionCommand(
    long TenantId,
    long ContentRewardId,
    long CoinTransactionId,
    DateTime OccurredAtUtc,
    string OperatorName,
    long OperatorId,
    ContentRewardAuditEntry SenderEntry,
    ContentRewardAuditEntry RecipientEntry);

public sealed class ContentRewardAuditProjectionConflictException : Exception
{
}

public interface IContentRewardAuditProjectionRepository
{
    Task ProjectAsync(ContentRewardAuditProjectionCommand command);
}
