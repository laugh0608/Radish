using System.Text.Json;
using Radish.Common;
using Radish.IRepository;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Repository;

public sealed partial class ContentModerationCaseRepository
{
    private static readonly TimeSpan AppealWindow = TimeSpan.FromDays(30);

    public async Task<(List<ContentModerationDecisionCandidate> data, int totalCount)>
        QueryMyAppealableDecisionsAsync(
            long tenantId,
            long appellantUserId,
            int pageIndex,
            int pageSize)
    {
        return await ExecuteDbOperationAsync(async () =>
        {
            RefAsync<int> totalCount = 0;
            var cases = await DbProtectedClient.Queryable<ContentModerationCase>()
                .Where(item =>
                    item.TenantId == tenantId &&
                    item.TargetUserId == appellantUserId &&
                    item.Status == (int)ContentModerationCaseStatus.Resolved &&
                    item.Decision == (int)ContentModerationDecision.Violation &&
                    item.ResolvedAt != null &&
                    !item.IsDeleted)
                .Where(item =>
                    SqlFunc.Subqueryable<ContentModerationTargetAction>()
                        .Where(action =>
                            action.TenantId == tenantId &&
                            action.CaseId == item.Id &&
                            action.ActionType == (int)ContentModerationTargetActionType.Restrict &&
                            action.ChangedTargetState)
                        .Any() ||
                    SqlFunc.Subqueryable<UserModerationAction>()
                        .Where(action =>
                            action.TenantId == tenantId &&
                            action.CaseId == item.Id &&
                            (action.ActionType == (int)ModerationActionTypeEnum.Mute ||
                             action.ActionType == (int)ModerationActionTypeEnum.Ban))
                        .Any())
                .OrderByDescending(item => item.ResolvedAt)
                .OrderByDescending(item => item.Id)
                .ToPageListAsync(pageIndex, pageSize, totalCount);
            if (cases.Count == 0)
            {
                return ([], totalCount.Value);
            }

            var caseIds = cases.Select(item => item.Id).ToArray();
            var appeals = await DbProtectedClient.Queryable<ContentModerationAppeal>()
                .Where(item =>
                    item.TenantId == tenantId &&
                    item.AppellantUserId == appellantUserId &&
                    caseIds.Contains(item.CaseId))
                .ToListAsync();
            var evidence = await DbProtectedClient.Queryable<ContentModerationEvidence>()
                .Where(item =>
                    item.TenantId == tenantId &&
                    caseIds.Contains(item.CaseId) &&
                    item.AppealId == null &&
                    (item.EvidenceType == (int)ContentModerationEvidenceType.ReportSnapshot ||
                     item.EvidenceType == (int)ContentModerationEvidenceType.CurrentTargetSnapshot))
                .OrderBy(item => item.EvidenceSequence)
                .ToListAsync();
            var targetActions = await DbProtectedClient.Queryable<ContentModerationTargetAction>()
                .Where(item => item.TenantId == tenantId && caseIds.Contains(item.CaseId))
                .OrderBy(item => item.CreateTime)
                .ToListAsync();
            var userActions = await DbProtectedClient.Queryable<UserModerationAction>()
                .Where(item => item.TenantId == tenantId && item.CaseId != null && caseIds.Contains(item.CaseId.Value))
                .OrderBy(item => item.CreateTime)
                .ToListAsync();
            var appealByCaseId = appeals.ToDictionary(item => item.CaseId);
            var data = cases.Select(item => new ContentModerationDecisionCandidate(
                item,
                appealByCaseId.GetValueOrDefault(item.Id),
                evidence.Where(snapshot => snapshot.CaseId == item.Id).ToList(),
                targetActions.Where(action => action.CaseId == item.Id).ToList(),
                userActions.Where(action => action.CaseId == item.Id).ToList())).ToList();
            return (data, totalCount.Value);
        });
    }

    public async Task<(List<ContentModerationAppeal> data, int totalCount)> QueryMyAppealsAsync(
        long tenantId,
        long appellantUserId,
        int pageIndex,
        int pageSize)
    {
        return await ExecuteDbOperationAsync(async () =>
        {
            RefAsync<int> totalCount = 0;
            var data = await DbProtectedClient.Queryable<ContentModerationAppeal>()
                .Where(item => item.TenantId == tenantId && item.AppellantUserId == appellantUserId)
                .OrderByDescending(item => item.SubmittedAt)
                .OrderByDescending(item => item.Id)
                .ToPageListAsync(pageIndex, pageSize, totalCount);
            return (data, totalCount.Value);
        });
    }

    public Task<ContentModerationAppealAggregate?> QueryAppealAggregateAsync(
        long tenantId,
        string appealPublicId,
        long? appellantUserId = null)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var query = DbProtectedClient.Queryable<ContentModerationAppeal>()
                .Where(item => item.TenantId == tenantId && item.PublicId == appealPublicId);
            if (appellantUserId.HasValue)
            {
                query = query.Where(item => item.AppellantUserId == appellantUserId.Value);
            }

            var appeal = await query.FirstAsync();
            if (appeal == null)
            {
                return null;
            }

            var moderationCase = await DbProtectedClient.Queryable<ContentModerationCase>()
                .Where(item => item.TenantId == tenantId && item.Id == appeal.CaseId && !item.IsDeleted)
                .FirstAsync();
            if (moderationCase == null)
            {
                return null;
            }

            var evidence = await DbProtectedClient.Queryable<ContentModerationEvidence>()
                .Where(item => item.TenantId == tenantId && item.CaseId == appeal.CaseId)
                .OrderBy(item => item.EvidenceSequence)
                .Take(100)
                .ToListAsync();
            var events = await DbProtectedClient.Queryable<ContentModerationAppealEvent>()
                .Where(item => item.TenantId == tenantId && item.AppealId == appeal.Id)
                .OrderBy(item => item.EventSequence)
                .Take(200)
                .ToListAsync();
            var targetActions = await DbProtectedClient.Queryable<ContentModerationTargetAction>()
                .Where(item => item.TenantId == tenantId && item.CaseId == appeal.CaseId)
                .OrderBy(item => item.CreateTime)
                .Take(20)
                .ToListAsync();
            var userActions = await DbProtectedClient.Queryable<UserModerationAction>()
                .Where(item => item.TenantId == tenantId && item.CaseId == appeal.CaseId)
                .OrderBy(item => item.CreateTime)
                .Take(20)
                .ToListAsync();
            var states = await DbProtectedClient.Queryable<UserModerationState>()
                .Where(item => item.TenantId == tenantId && item.TargetUserId == appeal.AppellantUserId)
                .OrderBy(item => item.PolicyType)
                .ToListAsync();
            return new ContentModerationAppealAggregate(
                appeal,
                moderationCase,
                evidence,
                events,
                targetActions,
                userActions,
                states);
        });
    }

    public async Task<(List<ContentModerationAppeal> data, int totalCount)> QueryAppealQueueAsync(
        ContentModerationAppealQueueCommand command)
    {
        return await ExecuteDbOperationAsync(async () =>
        {
            RefAsync<int> totalCount = 0;
            var query = DbProtectedClient.Queryable<ContentModerationAppeal>()
                .Where(item => item.TenantId == command.TenantId);
            if (command.Status.HasValue)
            {
                query = query.Where(item => item.Status == command.Status.Value);
            }

            if (command.TargetType.HasValue)
            {
                query = query.Where(item =>
                    SqlFunc.Subqueryable<ContentModerationCase>()
                        .Where(moderationCase =>
                            moderationCase.Id == item.CaseId &&
                            moderationCase.TenantId == command.TenantId &&
                            moderationCase.TargetType == command.TargetType.Value)
                        .Any());
            }

            if (!string.IsNullOrWhiteSpace(command.Keyword))
            {
                var keyword = command.Keyword;
                query = query.Where(item =>
                    item.PublicId.Contains(keyword) ||
                    item.Statement.Contains(keyword) ||
                    (item.InternalRemark != null && item.InternalRemark.Contains(keyword)));
            }

            var data = await query
                .OrderByDescending(item => item.ModifyTime ?? item.SubmittedAt)
                .OrderByDescending(item => item.Id)
                .ToPageListAsync(command.PageIndex, command.PageSize, totalCount);
            return (data, totalCount.Value);
        });
    }

    public Task<ContentModerationAppealWriteResult> SubmitAppealAsync(
        ContentModerationAppealSubmitCommand command)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            DbProtectedClient.Ado.BeginTran();
            try
            {
                await AcquireTransactionLockAsync(
                    $"moderation-appeal-case:{command.TenantId}:{command.CasePublicId}:{command.AppellantUserId}");
                await AcquireTransactionLockAsync(
                    $"moderation-appeal-operation:{command.TenantId}:{command.OperationKey}");
                var moderationCase = await QueryCaseForWriteAsync(command.TenantId, command.CasePublicId);
                var existingByOperation = await DbProtectedClient.Queryable<ContentModerationAppeal>()
                    .Where(item =>
                        item.TenantId == command.TenantId &&
                        item.SubmissionOperationKey == command.OperationKey)
                    .FirstAsync();
                if (existingByOperation != null)
                {
                    if (existingByOperation.CaseId != moderationCase.Id ||
                        existingByOperation.AppellantUserId != command.AppellantUserId ||
                        existingByOperation.Statement != command.Statement)
                    {
                        throw new ContentModerationIdempotencyConflictException();
                    }

                    DbProtectedClient.Ado.CommitTran();
                    return new ContentModerationAppealWriteResult(existingByOperation, true);
                }

                var existingAppeal = await DbProtectedClient.Queryable<ContentModerationAppeal>()
                    .Where(item =>
                        item.TenantId == command.TenantId &&
                        item.CaseId == moderationCase.Id &&
                        item.AppellantUserId == command.AppellantUserId)
                    .FirstAsync();
                if (existingAppeal != null)
                {
                    throw new ContentModerationAppealAlreadyExistsException();
                }

                var eligibleUntilUtc = moderationCase.ResolvedAt?.Add(AppealWindow)
                    ?? throw new ContentModerationAppealEligibilityException("CaseNotResolved");
                if (moderationCase.Status != (int)ContentModerationCaseStatus.Resolved ||
                    moderationCase.Decision != (int)ContentModerationDecision.Violation ||
                    moderationCase.TargetUserId != command.AppellantUserId ||
                    command.NowUtc > eligibleUntilUtc)
                {
                    throw new ContentModerationAppealEligibilityException(
                        command.NowUtc > eligibleUntilUtc ? "Expired" : "NotEligible");
                }

                var eligibleScope = await ResolveEligibleScopeAsync(moderationCase);
                if (eligibleScope == ContentModerationReliefScope.None)
                {
                    throw new ContentModerationAppealEligibilityException("NoEnforcement");
                }

                var appeal = new ContentModerationAppeal
                {
                    Id = SnowFlakeSingle.Instance.NextId(),
                    TenantId = command.TenantId,
                    CaseId = moderationCase.Id,
                    AppellantUserId = command.AppellantUserId,
                    EligibleScopeSnapshot = (int)eligibleScope,
                    Statement = command.Statement,
                    SubmissionOperationKey = command.OperationKey,
                    EligibleUntilUtc = eligibleUntilUtc,
                    SubmittedAt = command.NowUtc,
                    CreateTime = command.NowUtc,
                    CreateBy = command.AppellantName,
                    CreateId = command.AppellantUserId
                };
                await DbProtectedClient.Insertable(appeal).ExecuteCommandAsync();
                await DbProtectedClient.Insertable(CreateAppealEvent(
                    appeal,
                    1,
                    "Submitted",
                    command.OperationKey,
                    command.AppellantUserId,
                    command.AppellantName,
                    fromStatus: null,
                    toStatus: (int)ContentModerationAppealStatus.Submitted,
                    resultVersion: 1,
                    expectedVersion: 0)).ExecuteCommandAsync();
                await DbProtectedClient.Insertable(CreateEvent(
                    moderationCase,
                    await GetNextEventSequenceAsync(moderationCase.Id),
                    "AppealSubmitted",
                    command.AppellantUserId,
                    command.AppellantName,
                    relatedAppealId: appeal.Id,
                    resultCode: appeal.PublicId)).ExecuteCommandAsync();

                DbProtectedClient.Ado.CommitTran();
                return new ContentModerationAppealWriteResult(appeal, false);
            }
            catch
            {
                DbProtectedClient.Ado.RollbackTran();
                throw;
            }
        });
    }

    public Task<ContentModerationAppealWriteResult> WithdrawAppealAsync(
        ContentModerationAppealWithdrawCommand command)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            DbProtectedClient.Ado.BeginTran();
            try
            {
                await AcquireTransactionLockAsync(
                    $"moderation-appeal:{command.TenantId}:{command.AppealPublicId}");
                var appeal = await QueryAppealForWriteAsync(
                    command.TenantId,
                    command.AppealPublicId,
                    command.AppellantUserId);
                if (appeal.Status == (int)ContentModerationAppealStatus.Withdrawn &&
                    appeal.WithdrawalOperationKey == command.OperationKey)
                {
                    DbProtectedClient.Ado.CommitTran();
                    return new ContentModerationAppealWriteResult(appeal, true);
                }

                if (appeal.Version != command.ExpectedAppealVersion ||
                    appeal.Status is not
                        (int)ContentModerationAppealStatus.Submitted and not
                        (int)ContentModerationAppealStatus.Reviewing)
                {
                    throw new ContentModerationConcurrencyException();
                }

                await EnsureAppealOperationAvailableAsync(command.TenantId, command.OperationKey, appeal.Id);
                var previousStatus = appeal.Status;
                var nextVersion = appeal.Version + 1;
                var affected = await DbProtectedClient.Updateable<ContentModerationAppeal>()
                    .SetColumns(item => new ContentModerationAppeal
                    {
                        Status = (int)ContentModerationAppealStatus.Withdrawn,
                        Version = nextVersion,
                        WithdrawalOperationKey = command.OperationKey,
                        WithdrawnAt = command.NowUtc,
                        ResolvedAt = command.NowUtc,
                        ModifyTime = command.NowUtc,
                        ModifyBy = item.CreateBy,
                        ModifyId = command.AppellantUserId
                    })
                    .Where(item =>
                        item.Id == appeal.Id &&
                        item.Version == command.ExpectedAppealVersion &&
                        (item.Status == (int)ContentModerationAppealStatus.Submitted ||
                         item.Status == (int)ContentModerationAppealStatus.Reviewing))
                    .ExecuteCommandAsync();
                EnsureSingleRow(affected);
                appeal.Status = (int)ContentModerationAppealStatus.Withdrawn;
                appeal.Version = nextVersion;
                appeal.WithdrawalOperationKey = command.OperationKey;
                appeal.WithdrawnAt = command.NowUtc;
                appeal.ResolvedAt = command.NowUtc;
                appeal.ModifyTime = command.NowUtc;
                appeal.ModifyBy = appeal.CreateBy;
                appeal.ModifyId = command.AppellantUserId;
                await DbProtectedClient.Insertable(CreateAppealEvent(
                    appeal,
                    await GetNextAppealEventSequenceAsync(appeal.Id),
                    "Withdrawn",
                    command.OperationKey,
                    command.AppellantUserId,
                    appeal.CreateBy,
                    fromStatus: previousStatus,
                    toStatus: appeal.Status,
                    resultCode: "Withdrawn",
                    resultVersion: nextVersion,
                    expectedVersion: command.ExpectedAppealVersion)).ExecuteCommandAsync();
                await EnqueueAppealUpdatedNotificationAsync(appeal, command.OperationKey, command.NowUtc);
                DbProtectedClient.Ado.CommitTran();
                return new ContentModerationAppealWriteResult(appeal, false);
            }
            catch
            {
                DbProtectedClient.Ado.RollbackTran();
                throw;
            }
        });
    }

    public Task<ContentModerationAppealWriteResult> StartAppealReviewAsync(
        ContentModerationAppealStartReviewCommand command)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            DbProtectedClient.Ado.BeginTran();
            try
            {
                await AcquireTransactionLockAsync(
                    $"moderation-appeal:{command.TenantId}:{command.AppealPublicId}");
                var appeal = await QueryAppealForWriteAsync(command.TenantId, command.AppealPublicId);
                var existingEvent = await QueryAppealEventByOperationAsync(command.TenantId, command.OperationKey);
                if (existingEvent != null)
                {
                    if (existingEvent.AppealId != appeal.Id || existingEvent.EventType != "ReviewStarted")
                    {
                        throw new ContentModerationIdempotencyConflictException();
                    }

                    DbProtectedClient.Ado.CommitTran();
                    return new ContentModerationAppealWriteResult(appeal, true);
                }

                if (appeal.Version != command.ExpectedAppealVersion ||
                    appeal.Status != (int)ContentModerationAppealStatus.Submitted)
                {
                    throw new ContentModerationConcurrencyException();
                }

                var nextVersion = appeal.Version + 1;
                var affected = await DbProtectedClient.Updateable<ContentModerationAppeal>()
                    .SetColumns(item => new ContentModerationAppeal
                    {
                        Status = (int)ContentModerationAppealStatus.Reviewing,
                        Version = nextVersion,
                        ModifyTime = command.NowUtc,
                        ModifyBy = command.OperatorName,
                        ModifyId = command.OperatorUserId
                    })
                    .Where(item =>
                        item.Id == appeal.Id &&
                        item.Version == command.ExpectedAppealVersion &&
                        item.Status == (int)ContentModerationAppealStatus.Submitted)
                    .ExecuteCommandAsync();
                EnsureSingleRow(affected);
                var previousStatus = appeal.Status;
                appeal.Status = (int)ContentModerationAppealStatus.Reviewing;
                appeal.Version = nextVersion;
                appeal.ModifyTime = command.NowUtc;
                appeal.ModifyBy = command.OperatorName;
                appeal.ModifyId = command.OperatorUserId;
                await DbProtectedClient.Insertable(CreateAppealEvent(
                    appeal,
                    await GetNextAppealEventSequenceAsync(appeal.Id),
                    "ReviewStarted",
                    command.OperationKey,
                    command.OperatorUserId,
                    command.OperatorName,
                    fromStatus: previousStatus,
                    toStatus: appeal.Status,
                    resultVersion: nextVersion,
                    expectedVersion: command.ExpectedAppealVersion)).ExecuteCommandAsync();
                DbProtectedClient.Ado.CommitTran();
                return new ContentModerationAppealWriteResult(appeal, false);
            }
            catch
            {
                DbProtectedClient.Ado.RollbackTran();
                throw;
            }
        });
    }

    public Task<ContentModerationAppealWriteResult> AppendAppealEvidenceAsync(
        ContentModerationAppealEvidenceCommand command)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            DbProtectedClient.Ado.BeginTran();
            try
            {
                await AcquireTransactionLockAsync(
                    $"moderation-appeal:{command.TenantId}:{command.AppealPublicId}");
                var appeal = await QueryAppealForWriteAsync(command.TenantId, command.AppealPublicId);
                var existingEvent = await QueryAppealEventByOperationAsync(command.TenantId, command.OperationKey);
                if (existingEvent != null)
                {
                    if (existingEvent.AppealId != appeal.Id || existingEvent.EventType != "EvidenceCaptured")
                    {
                        throw new ContentModerationIdempotencyConflictException();
                    }

                    DbProtectedClient.Ado.CommitTran();
                    return new ContentModerationAppealWriteResult(appeal, true);
                }

                if (appeal.Version != command.ExpectedAppealVersion ||
                    appeal.Status is not
                        (int)ContentModerationAppealStatus.Submitted and not
                        (int)ContentModerationAppealStatus.Reviewing)
                {
                    throw new ContentModerationConcurrencyException();
                }

                var moderationCase = await DbProtectedClient.Queryable<ContentModerationCase>()
                    .Where(item => item.TenantId == command.TenantId && item.Id == appeal.CaseId && !item.IsDeleted)
                    .FirstAsync() ?? throw new ContentModerationCaseNotFoundException();
                await AcquireTransactionLockAsync($"moderation-case-id:{command.TenantId}:{appeal.CaseId}");
                var evidence = new ContentModerationEvidence
                {
                    Id = SnowFlakeSingle.Instance.NextId(),
                    TenantId = command.TenantId,
                    CaseId = appeal.CaseId,
                    AppealId = appeal.Id,
                    EvidenceSequence = await GetNextEvidenceSequenceAsync(appeal.CaseId),
                    EvidenceType = (int)ContentModerationEvidenceType.CurrentTargetSnapshot,
                    TargetState = command.TargetState,
                    SnapshotTitle = command.SnapshotTitle,
                    SnapshotSummary = command.SnapshotSummary,
                    TargetUserId = moderationCase.TargetUserId,
                    TargetPostId = command.TargetPostId,
                    TargetCommentId = command.TargetCommentId,
                    TargetChannelId = command.TargetChannelId,
                    TargetMessageId = command.TargetMessageId,
                    SnapshotHash = command.SnapshotHash,
                    CapturedAt = command.NowUtc,
                    CapturedById = command.OperatorUserId,
                    CapturedByName = command.OperatorName,
                    CreateTime = command.NowUtc,
                    CreateBy = command.OperatorName,
                    CreateId = command.OperatorUserId
                };
                await DbProtectedClient.Insertable(evidence).ExecuteCommandAsync();
                var previousStatus = appeal.Status;
                var nextVersion = appeal.Version + 1;
                var affected = await DbProtectedClient.Updateable<ContentModerationAppeal>()
                    .SetColumns(item => new ContentModerationAppeal
                    {
                        Status = (int)ContentModerationAppealStatus.Reviewing,
                        Version = nextVersion,
                        ModifyTime = command.NowUtc,
                        ModifyBy = command.OperatorName,
                        ModifyId = command.OperatorUserId
                    })
                    .Where(item => item.Id == appeal.Id && item.Version == command.ExpectedAppealVersion)
                    .ExecuteCommandAsync();
                EnsureSingleRow(affected);
                appeal.Status = (int)ContentModerationAppealStatus.Reviewing;
                appeal.Version = nextVersion;
                appeal.ModifyTime = command.NowUtc;
                appeal.ModifyBy = command.OperatorName;
                appeal.ModifyId = command.OperatorUserId;
                await DbProtectedClient.Insertable(CreateAppealEvent(
                    appeal,
                    await GetNextAppealEventSequenceAsync(appeal.Id),
                    "EvidenceCaptured",
                    command.OperationKey,
                    command.OperatorUserId,
                    command.OperatorName,
                    relatedEvidenceId: evidence.Id,
                    fromStatus: previousStatus,
                    toStatus: appeal.Status,
                    resultVersion: nextVersion,
                    expectedVersion: command.ExpectedAppealVersion)).ExecuteCommandAsync();
                DbProtectedClient.Ado.CommitTran();
                return new ContentModerationAppealWriteResult(appeal, false);
            }
            catch
            {
                DbProtectedClient.Ado.RollbackTran();
                throw;
            }
        });
    }

    public Task<ContentModerationAppealWriteResult> ReviewAppealAsync(
        ContentModerationAppealReviewCommand command)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            DbProtectedClient.Ado.BeginTran();
            try
            {
                await AcquireTransactionLockAsync(
                    $"moderation-appeal:{command.TenantId}:{command.AppealPublicId}");
                await AcquireTransactionLockAsync(
                    $"moderation-appeal-operation:{command.TenantId}:{command.OperationKey}");
                var appeal = await QueryAppealForWriteAsync(command.TenantId, command.AppealPublicId);
                if (appeal.DecisionOperationKey == command.OperationKey)
                {
                    if (appeal.Outcome != command.Outcome ||
                        appeal.GrantedScope != command.GrantedScope ||
                        appeal.PublicResultCode != command.PublicResultCode ||
                        appeal.PublicResultSummary != command.PublicResultSummary ||
                        appeal.InternalRemark != command.InternalRemark)
                    {
                        throw new ContentModerationIdempotencyConflictException();
                    }

                    DbProtectedClient.Ado.CommitTran();
                    return new ContentModerationAppealWriteResult(appeal, true);
                }

                await EnsureAppealOperationAvailableAsync(command.TenantId, command.OperationKey, appeal.Id);
                if (appeal.Version != command.ExpectedAppealVersion ||
                    appeal.Status is not
                        (int)ContentModerationAppealStatus.Submitted and not
                        (int)ContentModerationAppealStatus.Reviewing)
                {
                    throw new ContentModerationConcurrencyException();
                }

                var nextStatus = command.Outcome == (int)ContentModerationAppealOutcome.Upheld
                    ? (int)ContentModerationAppealStatus.Resolved
                    : (int)ContentModerationAppealStatus.ReliefPending;
                var nextVersion = appeal.Version + 1;
                var previousStatus = appeal.Status;
                var affected = await DbProtectedClient.Updateable<ContentModerationAppeal>()
                    .SetColumns(item => new ContentModerationAppeal
                    {
                        Status = nextStatus,
                        Outcome = command.Outcome,
                        GrantedScope = command.GrantedScope,
                        Version = nextVersion,
                        PublicResultCode = command.PublicResultCode,
                        PublicResultSummary = command.PublicResultSummary,
                        InternalRemark = command.InternalRemark,
                        DecisionOperationKey = command.OperationKey,
                        ReviewedAt = command.NowUtc,
                        ResolvedAt = nextStatus == (int)ContentModerationAppealStatus.Resolved
                            ? command.NowUtc
                            : null,
                        ReviewedById = command.OperatorUserId,
                        ReviewedByName = command.OperatorName,
                        ModifyTime = command.NowUtc,
                        ModifyBy = command.OperatorName,
                        ModifyId = command.OperatorUserId
                    })
                    .Where(item =>
                        item.Id == appeal.Id &&
                        item.Version == command.ExpectedAppealVersion &&
                        (item.Status == (int)ContentModerationAppealStatus.Submitted ||
                         item.Status == (int)ContentModerationAppealStatus.Reviewing))
                    .ExecuteCommandAsync();
                EnsureSingleRow(affected);
                appeal.Status = nextStatus;
                appeal.Outcome = command.Outcome;
                appeal.GrantedScope = command.GrantedScope;
                appeal.Version = nextVersion;
                appeal.PublicResultCode = command.PublicResultCode;
                appeal.PublicResultSummary = command.PublicResultSummary;
                appeal.InternalRemark = command.InternalRemark;
                appeal.DecisionOperationKey = command.OperationKey;
                appeal.ReviewedAt = command.NowUtc;
                appeal.ResolvedAt = nextStatus == (int)ContentModerationAppealStatus.Resolved
                    ? command.NowUtc
                    : null;
                appeal.ReviewedById = command.OperatorUserId;
                appeal.ReviewedByName = command.OperatorName;
                appeal.ModifyTime = command.NowUtc;
                appeal.ModifyBy = command.OperatorName;
                appeal.ModifyId = command.OperatorUserId;
                await DbProtectedClient.Insertable(CreateAppealEvent(
                    appeal,
                    await GetNextAppealEventSequenceAsync(appeal.Id),
                    "DecisionRecorded",
                    command.OperationKey,
                    command.OperatorUserId,
                    command.OperatorName,
                    fromStatus: previousStatus,
                    toStatus: nextStatus,
                    resultCode: command.PublicResultCode,
                    remark: command.InternalRemark,
                    resultVersion: nextVersion,
                    expectedVersion: command.ExpectedAppealVersion)).ExecuteCommandAsync();
                if (nextStatus == (int)ContentModerationAppealStatus.Resolved)
                {
                    await AppendCaseAppealResolvedEventAsync(appeal, command.OperatorUserId, command.OperatorName);
                }

                await EnqueueAppealUpdatedNotificationAsync(appeal, command.OperationKey, command.NowUtc);
                DbProtectedClient.Ado.CommitTran();
                return new ContentModerationAppealWriteResult(appeal, false);
            }
            catch
            {
                DbProtectedClient.Ado.RollbackTran();
                throw;
            }
        });
    }

    private async Task<ContentModerationReliefScope> ResolveEligibleScopeAsync(
        ContentModerationCase moderationCase)
    {
        var scope = ContentModerationReliefScope.None;
        if (await DbProtectedClient.Queryable<ContentModerationTargetAction>()
            .AnyAsync(item =>
                item.TenantId == moderationCase.TenantId &&
                item.CaseId == moderationCase.Id &&
                item.ActionType == (int)ContentModerationTargetActionType.Restrict &&
                item.ChangedTargetState))
        {
            scope |= ContentModerationReliefScope.TargetContent;
        }

        var actions = await DbProtectedClient.Queryable<UserModerationAction>()
            .Where(item => item.TenantId == moderationCase.TenantId && item.CaseId == moderationCase.Id)
            .Select(item => item.ActionType)
            .ToListAsync();
        if (actions.Contains((int)ModerationActionTypeEnum.Mute))
        {
            scope |= ContentModerationReliefScope.Mute;
        }

        if (actions.Contains((int)ModerationActionTypeEnum.Ban))
        {
            scope |= ContentModerationReliefScope.Ban;
        }

        return scope;
    }

    private async Task<ContentModerationAppeal> QueryAppealForWriteAsync(
        long tenantId,
        string appealPublicId,
        long? appellantUserId = null)
    {
        var query = DbProtectedClient.Queryable<ContentModerationAppeal>()
            .Where(item => item.TenantId == tenantId && item.PublicId == appealPublicId);
        if (appellantUserId.HasValue)
        {
            query = query.Where(item => item.AppellantUserId == appellantUserId.Value);
        }

        return await query.FirstAsync() ?? throw new ContentModerationAppealNotFoundException();
    }

    private Task<ContentModerationAppealEvent?> QueryAppealEventByOperationAsync(
        long tenantId,
        string operationKey)
    {
        return ExecuteDbOperationAsync(async () => (ContentModerationAppealEvent?)await DbProtectedClient
            .Queryable<ContentModerationAppealEvent>()
            .Where(item => item.TenantId == tenantId && item.OperationKey == operationKey)
            .FirstAsync());
    }

    private async Task EnsureAppealOperationAvailableAsync(
        long tenantId,
        string operationKey,
        long appealId)
    {
        var existing = await DbProtectedClient.Queryable<ContentModerationAppealEvent>()
            .Where(item => item.TenantId == tenantId && item.OperationKey == operationKey)
            .FirstAsync();
        if (existing != null)
        {
            throw new ContentModerationIdempotencyConflictException();
        }
    }

    private async Task<int> GetNextAppealEventSequenceAsync(long appealId)
    {
        var current = await DbProtectedClient.Queryable<ContentModerationAppealEvent>()
            .Where(item => item.AppealId == appealId)
            .MaxAsync(item => (int?)item.EventSequence);
        return (current ?? 0) + 1;
    }

    private static ContentModerationAppealEvent CreateAppealEvent(
        ContentModerationAppeal appeal,
        int sequence,
        string eventType,
        string? operationKey,
        long actorUserId,
        string actorName,
        long? relatedEvidenceId = null,
        long? relatedTargetActionId = null,
        long? relatedUserActionId = null,
        int? fromStatus = null,
        int? toStatus = null,
        string? resultCode = null,
        string? remark = null,
        int? resultVersion = null,
        int? expectedVersion = null)
    {
        return new ContentModerationAppealEvent
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            TenantId = appeal.TenantId,
            AppealId = appeal.Id,
            EventSequence = sequence,
            EventType = eventType,
            OperationKey = operationKey,
            ExpectedAppealVersion = expectedVersion ?? appeal.Version,
            ResultAppealVersion = resultVersion ?? appeal.Version,
            RelatedEvidenceId = relatedEvidenceId,
            RelatedTargetActionId = relatedTargetActionId,
            RelatedUserActionId = relatedUserActionId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ResultCode = resultCode,
            Remark = remark,
            ActorUserId = actorUserId,
            ActorName = actorName,
            CreateTime = appeal.ModifyTime ?? appeal.SubmittedAt
        };
    }

    private async Task AppendCaseAppealResolvedEventAsync(
        ContentModerationAppeal appeal,
        long actorUserId,
        string actorName)
    {
        var moderationCase = await DbProtectedClient.Queryable<ContentModerationCase>()
            .Where(item => item.TenantId == appeal.TenantId && item.Id == appeal.CaseId && !item.IsDeleted)
            .FirstAsync() ?? throw new ContentModerationCaseNotFoundException();
        await AcquireTransactionLockAsync($"moderation-case-id:{appeal.TenantId}:{appeal.CaseId}");
        await DbProtectedClient.Insertable(CreateEvent(
            moderationCase,
            await GetNextEventSequenceAsync(moderationCase.Id),
            "AppealResolved",
            actorUserId,
            actorName,
            relatedAppealId: appeal.Id,
            resultCode: appeal.PublicResultCode)).ExecuteCommandAsync();
    }

    private Task EnqueueAppealUpdatedNotificationAsync(
        ContentModerationAppeal appeal,
        string operationKey,
        DateTime nowUtc)
    {
        var status = ((ContentModerationAppealStatus)appeal.Status).ToString();
        var resultCode = appeal.PublicResultCode ?? status;
        var notification = new CreateNotificationDto
        {
            NotificationId = SnowFlakeSingle.Instance.NextId(),
            BusinessKey = $"notification:moderation-appeal:{appeal.Id}:version:{appeal.Version}",
            Type = NotificationType.ContentModerationAppealUpdated,
            Title = "治理申诉状态更新",
            Content = resultCode,
            Priority = (int)NotificationPriority.Normal,
            BusinessType = BusinessType.System,
            BusinessId = appeal.Id,
            ReceiverUserIds = [appeal.AppellantUserId],
            TenantId = appeal.TenantId,
            TemplateArguments = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["appealStatus"] = status,
                ["resultCode"] = resultCode
            },
            TargetKind = NotificationTargetKind.GovernanceAppeal,
            Target = new NotificationTargetData
            {
                GovernanceAppealPublicId = appeal.PublicId
            },
            OccurredAtUtc = nowUtc
        };
        return EnqueueNotificationAsync(
            appeal.TenantId,
            $"moderation-appeal:{appeal.Id}:version:{appeal.Version}:{BuildOperationFingerprint(operationKey)}",
            "ContentModerationAppeal",
            appeal.Id,
            notification,
            nowUtc);
    }
}
