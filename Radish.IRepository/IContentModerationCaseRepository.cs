using Radish.Model;

namespace Radish.IRepository;

/// <summary>内容治理案件的事务与并发写入边界。</summary>
public interface IContentModerationCaseRepository
{
    Task<ContentModerationReportWriteResult> SubmitReportAsync(ContentModerationReportWriteCommand command);
    Task<(List<ContentReport> data, int totalCount)> QueryMyReportsAsync(long tenantId, long reporterUserId, int pageIndex, int pageSize);
    Task<ContentReport?> QueryMyReportAsync(long tenantId, long reporterUserId, string reportPublicId);
    Task<ContentModerationCase?> QueryCaseByIdAsync(long tenantId, long caseId);
    Task<IReadOnlyList<ContentModerationCase>> QueryCasesByIdsAsync(long tenantId, IReadOnlyCollection<long> caseIds);
    Task<IReadOnlyDictionary<long, int>> QueryReportCountsByCaseIdsAsync(long tenantId, IReadOnlyCollection<long> caseIds);
    Task<(List<ContentModerationCase> data, int totalCount)> QueryCaseQueueAsync(ContentModerationCaseQueueCommand command);
    Task<ContentModerationCaseAggregate?> QueryCaseAggregateAsync(long tenantId, string casePublicId);
    Task<ContentModerationEvidenceWriteResult> AppendEvidenceAsync(ContentModerationEvidenceWriteCommand command);
    Task<ContentModerationCaseReviewWriteResult> ReviewCaseAsync(ContentModerationCaseReviewWriteCommand command);
    Task<ContentModerationCase> CompleteChatTargetActionAsync(ContentModerationChatActionCompletionCommand command);
    Task<ContentModerationCaseReviewWriteResult> ApplyCorrectiveUserActionAsync(ContentModerationCorrectiveActionCommand command);
    Task<ContentModerationStandaloneUserActionWriteResult> ApplyStandaloneUserActionAsync(ContentModerationStandaloneUserActionCommand command);
    Task<IReadOnlyList<UserModerationState>> QueryUserStatesAsync(long tenantId, long targetUserId);
    Task<(List<ContentModerationDecisionCandidate> data, int totalCount)> QueryMyAppealableDecisionsAsync(
        long tenantId,
        long appellantUserId,
        int pageIndex,
        int pageSize);
    Task<(List<ContentModerationAppeal> data, int totalCount)> QueryMyAppealsAsync(
        long tenantId,
        long appellantUserId,
        int pageIndex,
        int pageSize);
    Task<ContentModerationAppealAggregate?> QueryAppealAggregateAsync(
        long tenantId,
        string appealPublicId,
        long? appellantUserId = null);
    Task<(List<ContentModerationAppeal> data, int totalCount)> QueryAppealQueueAsync(
        ContentModerationAppealQueueCommand command);
    Task<ContentModerationAppealWriteResult> SubmitAppealAsync(ContentModerationAppealSubmitCommand command);
    Task<ContentModerationAppealWriteResult> WithdrawAppealAsync(ContentModerationAppealWithdrawCommand command);
    Task<ContentModerationAppealWriteResult> StartAppealReviewAsync(ContentModerationAppealStartReviewCommand command);
    Task<ContentModerationAppealWriteResult> AppendAppealEvidenceAsync(ContentModerationAppealEvidenceCommand command);
    Task<ContentModerationAppealWriteResult> ReviewAppealAsync(ContentModerationAppealReviewCommand command);
    Task<ContentModerationAppealReliefWriteResult> ExecuteAppealReliefAsync(ContentModerationAppealReliefCommand command);
    Task<ContentModerationAppeal> CompleteChatReliefAsync(ContentModerationChatReliefCompletionCommand command);
}

public sealed record ContentModerationDecisionCandidate(
    ContentModerationCase Case,
    ContentModerationAppeal? Appeal,
    IReadOnlyList<ContentModerationEvidence> Evidence,
    IReadOnlyList<ContentModerationTargetAction> TargetActions,
    IReadOnlyList<UserModerationAction> UserActions);

public sealed record ContentModerationAppealAggregate(
    ContentModerationAppeal Appeal,
    ContentModerationCase Case,
    IReadOnlyList<ContentModerationEvidence> Evidence,
    IReadOnlyList<ContentModerationAppealEvent> Events,
    IReadOnlyList<ContentModerationTargetAction> TargetActions,
    IReadOnlyList<UserModerationAction> UserActions,
    IReadOnlyList<UserModerationState> UserStates);

public sealed record ContentModerationAppealQueueCommand(
    long TenantId,
    int? Status,
    int? TargetType,
    string? Keyword,
    int PageIndex,
    int PageSize);

public sealed record ContentModerationAppealSubmitCommand(
    long TenantId,
    string CasePublicId,
    long AppellantUserId,
    string AppellantName,
    string Statement,
    string OperationKey,
    DateTime NowUtc);

public sealed record ContentModerationAppealWithdrawCommand(
    long TenantId,
    string AppealPublicId,
    long AppellantUserId,
    int ExpectedAppealVersion,
    string OperationKey,
    DateTime NowUtc);

public sealed record ContentModerationAppealStartReviewCommand(
    long TenantId,
    string AppealPublicId,
    int ExpectedAppealVersion,
    string OperationKey,
    long OperatorUserId,
    string OperatorName,
    DateTime NowUtc);

public sealed record ContentModerationAppealEvidenceCommand(
    long TenantId,
    string AppealPublicId,
    int ExpectedAppealVersion,
    int TargetState,
    string? SnapshotTitle,
    string? SnapshotSummary,
    long? TargetPostId,
    long? TargetCommentId,
    long? TargetChannelId,
    long? TargetMessageId,
    string SnapshotHash,
    string OperationKey,
    long OperatorUserId,
    string OperatorName,
    DateTime NowUtc);

public sealed record ContentModerationAppealReviewCommand(
    long TenantId,
    string AppealPublicId,
    int ExpectedAppealVersion,
    int Outcome,
    int GrantedScope,
    string PublicResultCode,
    string PublicResultSummary,
    string? InternalRemark,
    string OperationKey,
    long OperatorUserId,
    string OperatorName,
    DateTime NowUtc);

public sealed record ContentModerationAppealReliefCommand(
    long TenantId,
    string AppealPublicId,
    int ExpectedAppealVersion,
    string OperationKey,
    long OperatorUserId,
    string OperatorName,
    DateTime NowUtc);

public sealed record ContentModerationAppealWriteResult(
    ContentModerationAppeal Appeal,
    bool IsIdempotentReplay);

public sealed record ContentModerationAppealReliefWriteResult(
    ContentModerationAppeal Appeal,
    IReadOnlyList<ContentModerationTargetAction> TargetActions,
    IReadOnlyList<UserModerationAction> UserActions,
    bool IsIdempotentReplay);

public sealed record ContentModerationChatReliefCompletionCommand(
    long TenantId,
    long AppealId,
    long TargetActionId,
    string OperationKey,
    bool Succeeded,
    string ResultCode,
    long OperatorUserId,
    string OperatorName,
    DateTime NowUtc);

public sealed record ContentModerationReportWriteCommand(
    long TenantId,
    int TargetType,
    long TargetContentId,
    long TargetUserId,
    string? TargetUserName,
    long? TargetPostId,
    long? TargetChannelId,
    string? SnapshotTitle,
    string? SnapshotSummary,
    string SnapshotHash,
    long ReporterUserId,
    string ReporterUserName,
    string ReasonType,
    string? ReasonDetail,
    DateTime NowUtc);

public sealed record ContentModerationReportWriteResult(
    ContentModerationCase Case,
    ContentReport Report,
    bool IsDuplicate);

public sealed record ContentModerationCaseQueueCommand(
    long TenantId,
    int? Status,
    int? TargetType,
    string? Keyword,
    int PageIndex,
    int PageSize);

public sealed record ContentModerationCaseAggregate(
    ContentModerationCase Case,
    IReadOnlyList<ContentReport> Reports,
    IReadOnlyList<ContentModerationEvidence> Evidence,
    IReadOnlyList<ContentModerationCaseEvent> Events,
    IReadOnlyList<UserModerationState> UserStates);

public sealed record ContentModerationEvidenceWriteCommand(
    long TenantId,
    string CasePublicId,
    int ExpectedCaseVersion,
    int EvidenceType,
    int TargetState,
    string? SnapshotTitle,
    string? SnapshotSummary,
    long TargetUserId,
    string? TargetUserName,
    long? TargetPostId,
    long? TargetCommentId,
    long? TargetChannelId,
    long? TargetMessageId,
    int? ContentRevision,
    DateTime? TargetModifiedAt,
    string SnapshotHash,
    long OperatorUserId,
    string OperatorName,
    DateTime NowUtc);

public sealed record ContentModerationEvidenceWriteResult(
    ContentModerationCase Case,
    ContentModerationEvidence Evidence);

public sealed record ContentModerationCaseReviewWriteCommand(
    long TenantId,
    string CasePublicId,
    int ExpectedCaseVersion,
    int Decision,
    int TargetDisposition,
    int? ExpectedTargetVersion,
    string PublicResultCode,
    string? InternalRemark,
    ContentModerationUserActionWriteCommand? UserAction,
    string OperationKey,
    long OperatorUserId,
    string OperatorName,
    DateTime NowUtc);

public sealed record ContentModerationUserActionWriteCommand(
    long TargetUserId,
    string? TargetUserName,
    int ActionType,
    int ExpectedStateVersion,
    int? DurationHours,
    string Reason);

public sealed record ContentModerationCaseReviewWriteResult(
    ContentModerationCase Case,
    UserModerationAction? UserAction,
    UserModerationState? UserState,
    ContentModerationTargetAction? TargetAction,
    bool IsIdempotentReplay);

public sealed record ContentModerationChatActionCompletionCommand(
    long TenantId,
    long CaseId,
    long TargetActionId,
    string OperationKey,
    bool Succeeded,
    string ResultCode,
    long OperatorUserId,
    string OperatorName,
    DateTime NowUtc);

public sealed record ContentModerationCorrectiveActionCommand(
    long TenantId,
    string CasePublicId,
    int ExpectedCaseVersion,
    ContentModerationUserActionWriteCommand UserAction,
    string OperationKey,
    string Remark,
    long OperatorUserId,
    string OperatorName,
    DateTime NowUtc);

public sealed record ContentModerationStandaloneUserActionCommand(
    long TenantId,
    long TargetUserId,
    string? TargetUserName,
    int ActionType,
    int? DurationHours,
    string Reason,
    long? SourceReportId,
    string OperationKey,
    long OperatorUserId,
    string OperatorName,
    DateTime NowUtc);

public sealed record ContentModerationStandaloneUserActionWriteResult(
    UserModerationAction Action,
    UserModerationState State,
    bool IsIdempotentReplay);

public sealed class ContentModerationCaseNotFoundException : Exception;

public sealed class ContentModerationConcurrencyException : Exception;

public sealed class ContentModerationIdempotencyConflictException : Exception;

public sealed class ContentModerationTargetActionException(string resultCode) : Exception
{
    public string ResultCode { get; } = resultCode;
}

public sealed class ContentModerationAppealNotFoundException : Exception;

public sealed class ContentModerationAppealAlreadyExistsException : Exception;

public sealed class ContentModerationAppealEligibilityException(string resultCode) : Exception
{
    public string ResultCode { get; } = resultCode;
}
