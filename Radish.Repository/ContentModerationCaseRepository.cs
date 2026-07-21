using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Radish.Common;
using Radish.Common.CoreTool;
using Radish.IRepository;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Repository;

/// <summary>内容治理案件仓储。</summary>
public sealed partial class ContentModerationCaseRepository
    : BaseRepository<ContentModerationCase>, IContentModerationCaseRepository
{
    private readonly IUnitOfWorkManage _unitOfWorkManage;

    public ContentModerationCaseRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
        _unitOfWorkManage = unitOfWorkManage;
    }

    public Task<ContentModerationReportWriteResult> SubmitReportAsync(ContentModerationReportWriteCommand command)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            DbProtectedClient.Ado.BeginTran();
            try
            {
                var openTargetKey = BuildOpenTargetKey(command.TargetType, command.TargetContentId);
                await AcquireTransactionLockAsync($"moderation-target:{command.TenantId}:{openTargetKey}");

                var moderationCase = await DbProtectedClient.Queryable<ContentModerationCase>()
                    .Where(item => item.TenantId == command.TenantId && item.OpenTargetKey == openTargetKey && !item.IsDeleted)
                    .FirstAsync();
                var caseCreated = false;
                if (moderationCase == null)
                {
                    moderationCase = CreateCase(command, openTargetKey);
                    await _unitOfWorkManage.ExecuteInSavepointAsync(() =>
                        DbProtectedClient.Insertable(moderationCase).ExecuteCommandAsync());
                    caseCreated = true;
                }

                var existingReport = await DbProtectedClient.Queryable<ContentReport>()
                    .Where(report =>
                        report.TenantId == command.TenantId &&
                        report.CaseId == moderationCase.Id &&
                        report.ReporterUserId == command.ReporterUserId &&
                        !report.IsDeleted)
                    .FirstAsync();
                if (existingReport != null)
                {
                    DbProtectedClient.Ado.CommitTran();
                    return new ContentModerationReportWriteResult(moderationCase, existingReport, true);
                }

                var report = CreateReport(command, moderationCase.Id);
                await DbProtectedClient.Insertable(report).ExecuteCommandAsync();

                var nextEvidenceSequence = await GetNextEvidenceSequenceAsync(moderationCase.Id);
                var evidence = CreateReportEvidence(command, moderationCase.Id, report.Id, nextEvidenceSequence);
                await DbProtectedClient.Insertable(evidence).ExecuteCommandAsync();

                var nextEventSequence = await GetNextEventSequenceAsync(moderationCase.Id);
                if (caseCreated)
                {
                    await DbProtectedClient.Insertable(CreateEvent(
                        moderationCase,
                        nextEventSequence++,
                        "Opened",
                        command.ReporterUserId,
                        command.ReporterUserName,
                        relatedReportId: report.Id)).ExecuteCommandAsync();
                }

                await DbProtectedClient.Insertable(CreateEvent(
                    moderationCase,
                    nextEventSequence,
                    "ReportAttached",
                    command.ReporterUserId,
                    command.ReporterUserName,
                    relatedReportId: report.Id)).ExecuteCommandAsync();

                DbProtectedClient.Ado.CommitTran();
                return new ContentModerationReportWriteResult(moderationCase, report, false);
            }
            catch
            {
                DbProtectedClient.Ado.RollbackTran();
                throw;
            }
        });
    }

    public async Task<(List<ContentReport> data, int totalCount)> QueryMyReportsAsync(
        long tenantId,
        long reporterUserId,
        int pageIndex,
        int pageSize)
    {
        return await ExecuteDbOperationAsync(async () =>
        {
            RefAsync<int> totalCount = 0;
            var data = await DbProtectedClient.Queryable<ContentReport>()
                .Where(report =>
                    report.TenantId == tenantId &&
                    report.ReporterUserId == reporterUserId &&
                    !report.IsDeleted)
                .OrderByDescending(report => report.CreateTime)
                .OrderByDescending(report => report.Id)
                .ToPageListAsync(pageIndex, pageSize, totalCount);
            return (data, totalCount.Value);
        });
    }

    public Task<ContentReport?> QueryMyReportAsync(long tenantId, long reporterUserId, string reportPublicId)
    {
        return ExecuteDbOperationAsync(async () => (ContentReport?)await DbProtectedClient.Queryable<ContentReport>()
            .Where(report =>
                report.TenantId == tenantId &&
                report.ReporterUserId == reporterUserId &&
                report.PublicId == reportPublicId &&
                !report.IsDeleted)
            .FirstAsync());
    }

    public Task<ContentModerationCase?> QueryCaseByIdAsync(long tenantId, long caseId)
    {
        return ExecuteDbOperationAsync(async () => (ContentModerationCase?)await DbProtectedClient
            .Queryable<ContentModerationCase>()
            .Where(item => item.TenantId == tenantId && item.Id == caseId && !item.IsDeleted)
            .FirstAsync());
    }

    public Task<IReadOnlyList<ContentModerationCase>> QueryCasesByIdsAsync(
        long tenantId,
        IReadOnlyCollection<long> caseIds)
    {
        if (caseIds.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<ContentModerationCase>>([]);
        }

        var ids = caseIds.Distinct().ToArray();
        return ExecuteDbOperationAsync(async () =>
            (IReadOnlyList<ContentModerationCase>)await DbProtectedClient.Queryable<ContentModerationCase>()
                .Where(item => item.TenantId == tenantId && ids.Contains(item.Id) && !item.IsDeleted)
                .ToListAsync());
    }

    public Task<IReadOnlyDictionary<long, int>> QueryReportCountsByCaseIdsAsync(
        long tenantId,
        IReadOnlyCollection<long> caseIds)
    {
        if (caseIds.Count == 0)
        {
            return Task.FromResult<IReadOnlyDictionary<long, int>>(new Dictionary<long, int>());
        }

        var ids = caseIds.Distinct().ToArray();
        return ExecuteDbOperationAsync(async () =>
        {
            var reports = await DbProtectedClient.Queryable<ContentReport>()
                .Where(report =>
                    report.TenantId == tenantId &&
                    report.CaseId.HasValue &&
                    ids.Contains(report.CaseId.Value) &&
                    !report.IsDeleted)
                .Select(report => new ContentReport { CaseId = report.CaseId })
                .ToListAsync();
            return (IReadOnlyDictionary<long, int>)reports
                .GroupBy(report => report.CaseId!.Value)
                .ToDictionary(group => group.Key, group => group.Count());
        });
    }

    public async Task<(List<ContentModerationCase> data, int totalCount)> QueryCaseQueueAsync(
        ContentModerationCaseQueueCommand command)
    {
        return await ExecuteDbOperationAsync(async () =>
        {
            RefAsync<int> totalCount = 0;
            var query = DbProtectedClient.Queryable<ContentModerationCase>()
                .Where(item => item.TenantId == command.TenantId && !item.IsDeleted);
            if (command.Status.HasValue)
            {
                query = query.Where(item => item.Status == command.Status.Value);
            }

            if (command.TargetType.HasValue)
            {
                query = query.Where(item => item.TargetType == command.TargetType.Value);
            }

            if (!string.IsNullOrWhiteSpace(command.Keyword))
            {
                var keyword = command.Keyword;
                if (long.TryParse(keyword, out var longId))
                {
                    query = query.Where(item =>
                        item.TargetContentId == longId ||
                        item.TargetUserId == longId ||
                        item.PublicId.Contains(keyword));
                }
                else
                {
                    query = query.Where(item =>
                        item.PublicId.Contains(keyword) ||
                        (item.InternalRemark != null && item.InternalRemark.Contains(keyword)));
                }
            }

            var data = await query
                .OrderByDescending(item => item.ModifyTime ?? item.OpenedAt)
                .OrderByDescending(item => item.Id)
                .ToPageListAsync(command.PageIndex, command.PageSize, totalCount);
            return (data, totalCount.Value);
        });
    }

    public Task<ContentModerationCaseAggregate?> QueryCaseAggregateAsync(long tenantId, string casePublicId)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var moderationCase = await DbProtectedClient.Queryable<ContentModerationCase>()
                .Where(item => item.TenantId == tenantId && item.PublicId == casePublicId && !item.IsDeleted)
                .FirstAsync();
            if (moderationCase == null)
            {
                return null;
            }

            var reports = await DbProtectedClient.Queryable<ContentReport>()
                .Where(report => report.TenantId == tenantId && report.CaseId == moderationCase.Id && !report.IsDeleted)
                .OrderBy(report => report.CreateTime)
                .OrderBy(report => report.Id)
                .Take(100)
                .ToListAsync();
            var evidence = await DbProtectedClient.Queryable<ContentModerationEvidence>()
                .Where(item => item.TenantId == tenantId && item.CaseId == moderationCase.Id)
                .OrderBy(item => item.EvidenceSequence)
                .Take(100)
                .ToListAsync();
            var events = await DbProtectedClient.Queryable<ContentModerationCaseEvent>()
                .Where(item => item.TenantId == tenantId && item.CaseId == moderationCase.Id)
                .OrderBy(item => item.EventSequence)
                .Take(200)
                .ToListAsync();
            var states = await DbProtectedClient.Queryable<UserModerationState>()
                .Where(item => item.TenantId == tenantId && item.TargetUserId == moderationCase.TargetUserId)
                .OrderBy(item => item.PolicyType)
                .ToListAsync();
            return new ContentModerationCaseAggregate(moderationCase, reports, evidence, events, states);
        });
    }

    public Task<ContentModerationEvidenceWriteResult> AppendEvidenceAsync(ContentModerationEvidenceWriteCommand command)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            DbProtectedClient.Ado.BeginTran();
            try
            {
                await AcquireTransactionLockAsync($"moderation-case:{command.TenantId}:{command.CasePublicId}");
                var moderationCase = await QueryCaseForWriteAsync(command.TenantId, command.CasePublicId);
                if (moderationCase.Version != command.ExpectedCaseVersion ||
                    moderationCase.Status == (int)ContentModerationCaseStatus.Resolved)
                {
                    throw new ContentModerationConcurrencyException();
                }

                var nextVersion = moderationCase.Version + 1;
                var nextStatus = (int)ContentModerationCaseStatus.Reviewing;
                var affected = await DbProtectedClient.Updateable<ContentModerationCase>()
                    .SetColumns(item => new ContentModerationCase
                    {
                        Status = nextStatus,
                        Version = nextVersion,
                        ModifyTime = command.NowUtc,
                        ModifyBy = command.OperatorName,
                        ModifyId = command.OperatorUserId
                    })
                    .Where(item =>
                        item.Id == moderationCase.Id &&
                        item.TenantId == command.TenantId &&
                        item.Version == command.ExpectedCaseVersion &&
                        item.Status != (int)ContentModerationCaseStatus.Resolved &&
                        !item.IsDeleted)
                    .ExecuteCommandAsync();
                EnsureSingleRow(affected);

                var evidence = new ContentModerationEvidence
                {
                    Id = SnowFlakeSingle.Instance.NextId(),
                    TenantId = command.TenantId,
                    CaseId = moderationCase.Id,
                    EvidenceSequence = await GetNextEvidenceSequenceAsync(moderationCase.Id),
                    EvidenceType = command.EvidenceType,
                    TargetState = command.TargetState,
                    SnapshotTitle = command.SnapshotTitle,
                    SnapshotSummary = command.SnapshotSummary,
                    TargetUserId = command.TargetUserId,
                    TargetUserName = command.TargetUserName,
                    TargetPostId = command.TargetPostId,
                    TargetCommentId = command.TargetCommentId,
                    TargetChannelId = command.TargetChannelId,
                    TargetMessageId = command.TargetMessageId,
                    ContentRevision = command.ContentRevision,
                    TargetModifiedAt = command.TargetModifiedAt,
                    SnapshotHash = command.SnapshotHash,
                    CapturedAt = command.NowUtc,
                    CapturedById = command.OperatorUserId,
                    CapturedByName = command.OperatorName,
                    CreateTime = command.NowUtc,
                    CreateBy = command.OperatorName,
                    CreateId = command.OperatorUserId
                };
                await DbProtectedClient.Insertable(evidence).ExecuteCommandAsync();

                var eventType = moderationCase.Status == (int)ContentModerationCaseStatus.Open
                    ? "ReviewStarted"
                    : "EvidenceCaptured";
                await DbProtectedClient.Insertable(CreateEvent(
                    moderationCase,
                    await GetNextEventSequenceAsync(moderationCase.Id),
                    eventType,
                    command.OperatorUserId,
                    command.OperatorName,
                    fromStatus: moderationCase.Status,
                    toStatus: nextStatus,
                    resultVersion: nextVersion)).ExecuteCommandAsync();

                moderationCase.Status = nextStatus;
                moderationCase.Version = nextVersion;
                moderationCase.ModifyTime = command.NowUtc;
                moderationCase.ModifyBy = command.OperatorName;
                moderationCase.ModifyId = command.OperatorUserId;
                DbProtectedClient.Ado.CommitTran();
                return new ContentModerationEvidenceWriteResult(moderationCase, evidence);
            }
            catch
            {
                DbProtectedClient.Ado.RollbackTran();
                throw;
            }
        });
    }

    public Task<ContentModerationCaseReviewWriteResult> ReviewCaseAsync(ContentModerationCaseReviewWriteCommand command)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            DbProtectedClient.Ado.BeginTran();
            try
            {
                await AcquireTransactionLockAsync($"moderation-case:{command.TenantId}:{command.CasePublicId}");
                await AcquireTransactionLockAsync($"moderation-operation:{command.TenantId}:{command.OperationKey}");
                var moderationCase = await QueryCaseForWriteAsync(command.TenantId, command.CasePublicId);
                var existingAction = await DbProtectedClient.Queryable<UserModerationAction>()
                    .Where(action => action.TenantId == command.TenantId && action.OperationKey == command.OperationKey)
                    .FirstAsync();
                if (moderationCase.DecisionOperationKey == command.OperationKey)
                {
                    if (!IsSameDecisionOperation(moderationCase, command) ||
                        (existingAction != null && !IsSameOperation(existingAction, moderationCase.Id, command)) ||
                        (command.UserAction != null && existingAction == null))
                    {
                        throw new ContentModerationIdempotencyConflictException();
                    }

                    var existingState = existingAction == null ? null : await QueryActionStateAsync(existingAction);
                    DbProtectedClient.Ado.CommitTran();
                    return new ContentModerationCaseReviewWriteResult(moderationCase, existingAction, existingState, true);
                }

                if (existingAction != null || await DbProtectedClient.Queryable<ContentModerationCase>()
                        .AnyAsync(item =>
                            item.TenantId == command.TenantId &&
                            item.DecisionOperationKey == command.OperationKey &&
                            item.Id != moderationCase.Id))
                {
                    throw new ContentModerationIdempotencyConflictException();
                }

                if (moderationCase.Version != command.ExpectedCaseVersion ||
                    moderationCase.Status == (int)ContentModerationCaseStatus.Resolved)
                {
                    throw new ContentModerationConcurrencyException();
                }

                UserModerationAction? action = null;
                UserModerationState? state = null;
                if (command.UserAction != null)
                {
                    (action, state) = await ApplyUserActionAsync(moderationCase, command);
                }

                var targetActionResult = "Kept";
                if (command.TargetDisposition == (int)ContentModerationTargetDisposition.Restricted)
                {
                    if (moderationCase.TargetType == (int)ContentReportTargetTypeEnum.ChatMessage)
                    {
                        await EnqueueChatRecallAsync(moderationCase, command);
                        var pendingCase = await SavePendingChatDecisionAsync(moderationCase, command, action);
                        if (action != null)
                        {
                            await EnqueueUserActionNotificationAsync(action, command.OperationKey, command.NowUtc);
                        }
                        DbProtectedClient.Ado.CommitTran();
                        return new ContentModerationCaseReviewWriteResult(pendingCase, action, state, false);
                    }

                    targetActionResult = await RestrictMainTargetAsync(moderationCase, command);
                }

                var nextVersion = moderationCase.Version + 1;
                var affected = await DbProtectedClient.Updateable<ContentModerationCase>()
                    .SetColumns(item => new ContentModerationCase
                    {
                        Status = (int)ContentModerationCaseStatus.Resolved,
                        Decision = command.Decision,
                        TargetDisposition = command.TargetDisposition,
                        Version = nextVersion,
                        DecisionOperationKey = command.OperationKey,
                        OpenTargetKey = null,
                        PublicResultCode = command.PublicResultCode,
                        InternalRemark = command.InternalRemark,
                        ResolvedAt = command.NowUtc,
                        ResolvedById = command.OperatorUserId,
                        ResolvedByName = command.OperatorName,
                        ModifyTime = command.NowUtc,
                        ModifyBy = command.OperatorName,
                        ModifyId = command.OperatorUserId
                    })
                    .Where(item =>
                        item.Id == moderationCase.Id &&
                        item.TenantId == command.TenantId &&
                        item.Version == command.ExpectedCaseVersion &&
                        item.Status != (int)ContentModerationCaseStatus.Resolved &&
                        !item.IsDeleted)
                    .ExecuteCommandAsync();
                EnsureSingleRow(affected);

                await DbProtectedClient.Updateable<ContentReport>()
                    .SetColumns(report => new ContentReport
                    {
                        ReporterState = "Resolved",
                        ModifyTime = command.NowUtc,
                        ModifyBy = command.OperatorName,
                        ModifyId = command.OperatorUserId
                    })
                    .Where(report => report.TenantId == command.TenantId && report.CaseId == moderationCase.Id && !report.IsDeleted)
                    .ExecuteCommandAsync();

                var eventSequence = await GetNextEventSequenceAsync(moderationCase.Id);
                await DbProtectedClient.Insertable(CreateEvent(
                    moderationCase,
                    eventSequence++,
                    "DecisionRecorded",
                    command.OperatorUserId,
                    command.OperatorName,
                    relatedActionId: action?.Id,
                    fromStatus: moderationCase.Status,
                    toStatus: (int)ContentModerationCaseStatus.Resolved,
                    resultCode: targetActionResult,
                    remark: command.InternalRemark,
                    resultVersion: nextVersion)).ExecuteCommandAsync();
                if (action != null)
                {
                    await DbProtectedClient.Insertable(CreateEvent(
                        moderationCase,
                        eventSequence,
                        "ActionSucceeded",
                        command.OperatorUserId,
                        command.OperatorName,
                        relatedActionId: action.Id,
                        resultCode: action.ResultCode,
                        resultVersion: nextVersion)).ExecuteCommandAsync();
                    await EnqueueUserActionNotificationAsync(action, command.OperationKey, command.NowUtc);
                }
                await EnqueueReportResultNotificationsAsync(moderationCase, command.PublicResultCode, command.OperationKey, command.NowUtc);

                moderationCase.Status = (int)ContentModerationCaseStatus.Resolved;
                moderationCase.Decision = command.Decision;
                moderationCase.TargetDisposition = command.TargetDisposition;
                moderationCase.Version = nextVersion;
                moderationCase.DecisionOperationKey = command.OperationKey;
                moderationCase.OpenTargetKey = null;
                moderationCase.PublicResultCode = command.PublicResultCode;
                moderationCase.InternalRemark = command.InternalRemark;
                moderationCase.ResolvedAt = command.NowUtc;
                moderationCase.ResolvedById = command.OperatorUserId;
                moderationCase.ResolvedByName = command.OperatorName;
                DbProtectedClient.Ado.CommitTran();
                return new ContentModerationCaseReviewWriteResult(moderationCase, action, state, false);
            }
            catch
            {
                DbProtectedClient.Ado.RollbackTran();
                throw;
            }
        });
    }

    public Task<ContentModerationCase> CompleteChatTargetActionAsync(ContentModerationChatActionCompletionCommand command)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            DbProtectedClient.Ado.BeginTran();
            try
            {
                await AcquireTransactionLockAsync($"moderation-case-id:{command.TenantId}:{command.CaseId}");
                var moderationCase = await DbProtectedClient.Queryable<ContentModerationCase>()
                    .Where(item => item.TenantId == command.TenantId && item.Id == command.CaseId && !item.IsDeleted)
                    .FirstAsync();
                if (moderationCase == null)
                {
                    throw new ContentModerationCaseNotFoundException();
                }

                if (moderationCase.Status == (int)ContentModerationCaseStatus.Resolved)
                {
                    DbProtectedClient.Ado.CommitTran();
                    return moderationCase;
                }

                var nextVersion = moderationCase.Version + 1;
                var finalDisposition = command.Succeeded
                    ? command.ResultCode == "TargetUnavailable"
                        ? (int)ContentModerationTargetDisposition.Unavailable
                        : (int)ContentModerationTargetDisposition.Restricted
                    : (int)ContentModerationTargetDisposition.ActionFailed;
                var affected = await DbProtectedClient.Updateable<ContentModerationCase>()
                    .SetColumns(item => new ContentModerationCase
                    {
                        Status = command.Succeeded
                            ? (int)ContentModerationCaseStatus.Resolved
                            : (int)ContentModerationCaseStatus.Reviewing,
                        TargetDisposition = finalDisposition,
                        Version = nextVersion,
                        OpenTargetKey = command.Succeeded ? null : item.OpenTargetKey,
                        ResolvedAt = command.Succeeded ? command.NowUtc : null,
                        ResolvedById = command.Succeeded ? command.OperatorUserId : null,
                        ResolvedByName = command.Succeeded ? command.OperatorName : null,
                        ModifyTime = command.NowUtc,
                        ModifyBy = command.OperatorName,
                        ModifyId = command.OperatorUserId
                    })
                    .Where(item =>
                        item.Id == command.CaseId &&
                        item.TenantId == command.TenantId &&
                        item.Version == moderationCase.Version &&
                        item.Status != (int)ContentModerationCaseStatus.Resolved &&
                        !item.IsDeleted)
                    .ExecuteCommandAsync();
                EnsureSingleRow(affected);
                if (command.Succeeded)
                {
                    await DbProtectedClient.Updateable<ContentReport>()
                        .SetColumns(report => new ContentReport
                        {
                            ReporterState = "Resolved",
                            ModifyTime = command.NowUtc,
                            ModifyBy = command.OperatorName,
                            ModifyId = command.OperatorUserId
                        })
                        .Where(report => report.TenantId == command.TenantId && report.CaseId == command.CaseId && !report.IsDeleted)
                        .ExecuteCommandAsync();
                    await EnqueueReportResultNotificationsAsync(
                        moderationCase,
                        moderationCase.PublicResultCode ?? command.ResultCode,
                        command.OperationKey,
                        command.NowUtc);
                }

                await DbProtectedClient.Insertable(CreateEvent(
                    moderationCase,
                    await GetNextEventSequenceAsync(moderationCase.Id),
                    command.Succeeded ? "ActionSucceeded" : "ActionFailed",
                    command.OperatorUserId,
                    command.OperatorName,
                    resultCode: command.ResultCode,
                    fromStatus: moderationCase.Status,
                    toStatus: command.Succeeded
                        ? (int)ContentModerationCaseStatus.Resolved
                        : (int)ContentModerationCaseStatus.Reviewing,
                    resultVersion: nextVersion)).ExecuteCommandAsync();

                moderationCase.Status = command.Succeeded
                    ? (int)ContentModerationCaseStatus.Resolved
                    : (int)ContentModerationCaseStatus.Reviewing;
                moderationCase.TargetDisposition = finalDisposition;
                moderationCase.Version = nextVersion;
                moderationCase.OpenTargetKey = command.Succeeded ? null : moderationCase.OpenTargetKey;
                moderationCase.ResolvedAt = command.Succeeded ? command.NowUtc : null;
                DbProtectedClient.Ado.CommitTran();
                return moderationCase;
            }
            catch
            {
                DbProtectedClient.Ado.RollbackTran();
                throw;
            }
        });
    }

    public async Task<IReadOnlyList<UserModerationState>> QueryUserStatesAsync(long tenantId, long targetUserId)
    {
        var effectiveTenantId = tenantId >= 0 ? tenantId : Math.Max(App.CurrentUser.TenantId, 0);
        return await ExecuteDbOperationAsync(() => DbProtectedClient.Queryable<UserModerationState>()
            .Where(item => item.TenantId == effectiveTenantId && item.TargetUserId == targetUserId)
            .OrderBy(item => item.PolicyType)
            .ToListAsync());
    }

    public Task<ContentModerationCaseReviewWriteResult> ApplyCorrectiveUserActionAsync(
        ContentModerationCorrectiveActionCommand command)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            DbProtectedClient.Ado.BeginTran();
            try
            {
                await AcquireTransactionLockAsync($"moderation-case:{command.TenantId}:{command.CasePublicId}");
                await AcquireTransactionLockAsync($"moderation-operation:{command.TenantId}:{command.OperationKey}");
                var moderationCase = await QueryCaseForWriteAsync(command.TenantId, command.CasePublicId);
                var reviewCommand = new ContentModerationCaseReviewWriteCommand(
                    command.TenantId,
                    command.CasePublicId,
                    command.ExpectedCaseVersion,
                    moderationCase.Decision,
                    moderationCase.TargetDisposition,
                    null,
                    moderationCase.PublicResultCode ?? string.Empty,
                    moderationCase.InternalRemark,
                    command.UserAction,
                    command.OperationKey,
                    command.OperatorUserId,
                    command.OperatorName,
                    command.NowUtc);
                var existingAction = await DbProtectedClient.Queryable<UserModerationAction>()
                    .Where(action => action.TenantId == command.TenantId && action.OperationKey == command.OperationKey)
                    .FirstAsync();
                if (existingAction != null)
                {
                    if (!IsSameOperation(existingAction, moderationCase.Id, reviewCommand))
                    {
                        throw new ContentModerationIdempotencyConflictException();
                    }

                    var existingState = await QueryActionStateAsync(existingAction);
                    DbProtectedClient.Ado.CommitTran();
                    return new ContentModerationCaseReviewWriteResult(moderationCase, existingAction, existingState, true);
                }

                if (moderationCase.Status != (int)ContentModerationCaseStatus.Resolved ||
                    moderationCase.Version != command.ExpectedCaseVersion)
                {
                    throw new ContentModerationConcurrencyException();
                }

                var (action, state) = await ApplyUserActionAsync(moderationCase, reviewCommand);
                var nextVersion = moderationCase.Version + 1;
                var affected = await DbProtectedClient.Updateable<ContentModerationCase>()
                    .SetColumns(item => new ContentModerationCase
                    {
                        Version = nextVersion,
                        ModifyTime = command.NowUtc,
                        ModifyBy = command.OperatorName,
                        ModifyId = command.OperatorUserId
                    })
                    .Where(item =>
                        item.Id == moderationCase.Id &&
                        item.TenantId == command.TenantId &&
                        item.Version == command.ExpectedCaseVersion &&
                        item.Status == (int)ContentModerationCaseStatus.Resolved &&
                        !item.IsDeleted)
                    .ExecuteCommandAsync();
                EnsureSingleRow(affected);
                await DbProtectedClient.Insertable(CreateEvent(
                    moderationCase,
                    await GetNextEventSequenceAsync(moderationCase.Id),
                    "CorrectiveAction",
                    command.OperatorUserId,
                    command.OperatorName,
                    relatedActionId: action.Id,
                    resultCode: action.ResultCode,
                    remark: command.Remark,
                    resultVersion: nextVersion)).ExecuteCommandAsync();
                await EnqueueUserActionNotificationAsync(action, command.OperationKey, command.NowUtc);

                moderationCase.Version = nextVersion;
                moderationCase.ModifyTime = command.NowUtc;
                moderationCase.ModifyBy = command.OperatorName;
                moderationCase.ModifyId = command.OperatorUserId;
                DbProtectedClient.Ado.CommitTran();
                return new ContentModerationCaseReviewWriteResult(moderationCase, action, state, false);
            }
            catch
            {
                DbProtectedClient.Ado.RollbackTran();
                throw;
            }
        });
    }

    public Task<ContentModerationStandaloneUserActionWriteResult> ApplyStandaloneUserActionAsync(
        ContentModerationStandaloneUserActionCommand command)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var ownsTransaction = DbProtectedClient.Ado.Transaction == null;
            if (ownsTransaction)
            {
                DbProtectedClient.Ado.BeginTran();
            }

            try
            {
                await AcquireTransactionLockAsync($"moderation-operation:{command.TenantId}:{command.OperationKey}");
                var existingAction = await DbProtectedClient.Queryable<UserModerationAction>()
                    .Where(action => action.TenantId == command.TenantId && action.OperationKey == command.OperationKey)
                    .FirstAsync();
                if (existingAction != null)
                {
                    if (!IsSameStandaloneOperation(existingAction, command))
                    {
                        throw new ContentModerationIdempotencyConflictException();
                    }

                    var existingState = await QueryActionStateAsync(existingAction)
                        ?? throw new ContentModerationConcurrencyException();
                    if (ownsTransaction)
                    {
                        DbProtectedClient.Ado.CommitTran();
                    }

                    return new ContentModerationStandaloneUserActionWriteResult(existingAction, existingState, true);
                }

                var policyType = GetPolicyType(command.ActionType);
                await AcquireTransactionLockAsync($"moderation-user:{command.TenantId}:{command.TargetUserId}:{policyType}");
                var state = await DbProtectedClient.Queryable<UserModerationState>()
                    .Where(item =>
                        item.TenantId == command.TenantId &&
                        item.TargetUserId == command.TargetUserId &&
                        item.PolicyType == policyType)
                    .FirstAsync();
                var (action, updatedState) = await WriteUserActionStateAsync(
                    state,
                    caseId: null,
                    sourceReportId: command.SourceReportId,
                    command.TenantId,
                    command.TargetUserId,
                    command.TargetUserName,
                    command.ActionType,
                    command.DurationHours,
                    command.Reason,
                    command.OperationKey,
                    command.OperatorUserId,
                    command.OperatorName,
                    command.NowUtc);

                if (command.ActionType == (int)ModerationActionTypeEnum.Ban)
                {
                    await DeactivateMuteForBanAsync(
                        caseId: null,
                        sourceReportId: command.SourceReportId,
                        command.TenantId,
                        command.TargetUserId,
                        command.TargetUserName,
                        command.OperationKey,
                        command.OperatorUserId,
                        command.OperatorName,
                        command.NowUtc);
                }

                await EnqueueUserActionNotificationAsync(action, command.OperationKey, command.NowUtc);

                if (ownsTransaction)
                {
                    DbProtectedClient.Ado.CommitTran();
                }

                return new ContentModerationStandaloneUserActionWriteResult(action, updatedState, false);
            }
            catch
            {
                if (ownsTransaction)
                {
                    DbProtectedClient.Ado.RollbackTran();
                }

                throw;
            }
        });
    }

    private async Task<(UserModerationAction action, UserModerationState state)> ApplyUserActionAsync(
        ContentModerationCase moderationCase,
        ContentModerationCaseReviewWriteCommand command)
    {
        var requested = command.UserAction!;
        var policyType = GetPolicyType(requested.ActionType);
        await AcquireTransactionLockAsync($"moderation-user:{command.TenantId}:{requested.TargetUserId}:{policyType}");
        var state = await DbProtectedClient.Queryable<UserModerationState>()
            .Where(item =>
                item.TenantId == command.TenantId &&
                item.TargetUserId == requested.TargetUserId &&
                item.PolicyType == policyType)
            .FirstAsync();
        if ((state?.Version ?? 0) != requested.ExpectedStateVersion)
        {
            throw new ContentModerationConcurrencyException();
        }

        var (action, updatedState) = await WriteUserActionStateAsync(
            state,
            moderationCase.Id,
            sourceReportId: null,
            command.TenantId,
            requested.TargetUserId,
            requested.TargetUserName,
            requested.ActionType,
            requested.DurationHours,
            requested.Reason,
            command.OperationKey,
            command.OperatorUserId,
            command.OperatorName,
            command.NowUtc);

        if (requested.ActionType == (int)ModerationActionTypeEnum.Ban)
        {
            await DeactivateMuteForBanAsync(
                moderationCase.Id,
                sourceReportId: null,
                command.TenantId,
                requested.TargetUserId,
                requested.TargetUserName,
                command.OperationKey,
                command.OperatorUserId,
                command.OperatorName,
                command.NowUtc);
        }

        return (action, updatedState);
    }

    private async Task<string> RestrictMainTargetAsync(
        ContentModerationCase moderationCase,
        ContentModerationCaseReviewWriteCommand command)
    {
        return (ContentReportTargetTypeEnum)moderationCase.TargetType switch
        {
            ContentReportTargetTypeEnum.Post => await RestrictPostAsync(moderationCase, command),
            ContentReportTargetTypeEnum.Comment => await RestrictCommentAsync(moderationCase, command),
            ContentReportTargetTypeEnum.PostQuickReply => await RestrictQuickReplyAsync(moderationCase, command),
            ContentReportTargetTypeEnum.Product => await RestrictProductAsync(moderationCase, command),
            _ => throw new ContentModerationTargetActionException("Unsupported")
        };
    }

    private async Task<string> RestrictPostAsync(
        ContentModerationCase moderationCase,
        ContentModerationCaseReviewWriteCommand command)
    {
        var post = await DbProtectedClient.Queryable<Post>()
            .Where(item => item.Id == moderationCase.TargetContentId && item.TenantId == command.TenantId)
            .FirstAsync();
        if (post == null)
        {
            throw new ContentModerationTargetActionException("TargetUnavailable");
        }

        if (post.IsDeleted)
        {
            return "AlreadyRestricted";
        }

        if (!command.ExpectedTargetVersion.HasValue || post.EditCount != command.ExpectedTargetVersion.Value)
        {
            throw new ContentModerationTargetActionException("VersionConflict");
        }

        var affected = await DbProtectedClient.Updateable<Post>()
            .SetColumns(item => new Post
            {
                IsDeleted = true,
                DeletedAt = command.NowUtc,
                DeletedBy = command.OperatorName,
                ModifyTime = command.NowUtc,
                ModifyBy = command.OperatorName,
                ModifyId = command.OperatorUserId
            })
            .Where(item =>
                item.Id == moderationCase.TargetContentId &&
                item.TenantId == command.TenantId &&
                item.EditCount == command.ExpectedTargetVersion.Value &&
                !item.IsDeleted)
            .ExecuteCommandAsync();
        if (affected != 1)
        {
            throw new ContentModerationTargetActionException("VersionConflict");
        }

        return "Restricted";
    }

    private async Task<string> RestrictCommentAsync(
        ContentModerationCase moderationCase,
        ContentModerationCaseReviewWriteCommand command)
    {
        var comment = await DbProtectedClient.Queryable<Comment>()
            .Where(item => item.Id == moderationCase.TargetContentId && item.TenantId == command.TenantId)
            .FirstAsync();
        if (comment == null)
        {
            throw new ContentModerationTargetActionException("TargetUnavailable");
        }

        if (comment.IsDeleted)
        {
            return "AlreadyRestricted";
        }

        if (!command.ExpectedTargetVersion.HasValue || comment.EditCount != command.ExpectedTargetVersion.Value)
        {
            throw new ContentModerationTargetActionException("VersionConflict");
        }

        var affected = await DbProtectedClient.Updateable<Comment>()
            .SetColumns(item => new Comment
            {
                IsDeleted = true,
                ModifyTime = command.NowUtc,
                ModifyBy = command.OperatorName,
                ModifyId = command.OperatorUserId
            })
            .Where(item =>
                item.Id == moderationCase.TargetContentId &&
                item.TenantId == command.TenantId &&
                item.EditCount == command.ExpectedTargetVersion.Value &&
                !item.IsDeleted)
            .ExecuteCommandAsync();
        if (affected != 1)
        {
            throw new ContentModerationTargetActionException("VersionConflict");
        }

        return "Restricted";
    }

    private async Task<string> RestrictQuickReplyAsync(
        ContentModerationCase moderationCase,
        ContentModerationCaseReviewWriteCommand command)
    {
        var quickReply = await DbProtectedClient.Queryable<PostQuickReply>()
            .Where(item => item.Id == moderationCase.TargetContentId && item.TenantId == command.TenantId)
            .FirstAsync();
        if (quickReply == null)
        {
            throw new ContentModerationTargetActionException("TargetUnavailable");
        }

        if (quickReply.IsDeleted)
        {
            return "AlreadyRestricted";
        }

        var affected = await DbProtectedClient.Updateable<PostQuickReply>()
            .SetColumns(item => new PostQuickReply
            {
                IsDeleted = true,
                DeletedAt = command.NowUtc,
                DeletedBy = command.OperatorName,
                ModifyTime = command.NowUtc,
                ModifyBy = command.OperatorName,
                ModifyId = command.OperatorUserId
            })
            .Where(item =>
                item.Id == moderationCase.TargetContentId &&
                item.TenantId == command.TenantId &&
                !item.IsDeleted)
            .ExecuteCommandAsync();
        if (affected != 1)
        {
            throw new ContentModerationTargetActionException("VersionConflict");
        }

        return "Restricted";
    }

    private async Task<string> RestrictProductAsync(
        ContentModerationCase moderationCase,
        ContentModerationCaseReviewWriteCommand command)
    {
        var product = await DbProtectedClient.Queryable<Product>()
            .Where(item => item.Id == moderationCase.TargetContentId && item.TenantId == command.TenantId && !item.IsDeleted)
            .FirstAsync();
        if (product == null)
        {
            throw new ContentModerationTargetActionException("TargetUnavailable");
        }

        if (!product.IsOnSale)
        {
            return "AlreadyRestricted";
        }

        if (!command.ExpectedTargetVersion.HasValue || product.Version != command.ExpectedTargetVersion.Value)
        {
            throw new ContentModerationTargetActionException("VersionConflict");
        }

        var affected = await DbProtectedClient.Updateable<Product>()
            .SetColumns(item => new Product
            {
                IsOnSale = false,
                OffSaleTime = command.NowUtc,
                Version = item.Version + 1,
                ModifyTime = command.NowUtc,
                ModifyBy = command.OperatorName,
                ModifyId = command.OperatorUserId
            })
            .Where(item =>
                item.Id == moderationCase.TargetContentId &&
                item.TenantId == command.TenantId &&
                item.Version == command.ExpectedTargetVersion.Value &&
                item.IsOnSale &&
                !item.IsDeleted)
            .ExecuteCommandAsync();
        if (affected != 1)
        {
            throw new ContentModerationTargetActionException("VersionConflict");
        }

        return "Restricted";
    }

    private async Task EnqueueChatRecallAsync(
        ContentModerationCase moderationCase,
        ContentModerationCaseReviewWriteCommand command)
    {
        var payload = new ContentModerationChatRecallTaskPayload(
            command.TenantId,
            moderationCase.Id,
            moderationCase.TargetContentId,
            command.OperationKey,
            command.OperatorUserId,
            command.OperatorName);
        await DbProtectedClient.Insertable(new ReliableOutboxMessage
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            TenantId = command.TenantId,
            TaskType = ReliableTaskTypes.ContentModerationChatRecall,
            SchemaVersion = 1,
            IdempotencyKey = $"moderation-chat-recall:{command.OperationKey}",
            AggregateType = "ContentModerationCase",
            AggregateId = moderationCase.Id.ToString(),
            PayloadJson = JsonSerializer.Serialize(payload),
            Status = ReliableOutboxStatuses.Pending,
            MaxAttempts = 6,
            OccurredAtUtc = command.NowUtc,
            AvailableAtUtc = command.NowUtc,
            CreateTime = command.NowUtc
        }).ExecuteCommandAsync();
    }

    private async Task EnqueueReportResultNotificationsAsync(
        ContentModerationCase moderationCase,
        string resultCode,
        string operationKey,
        DateTime nowUtc)
    {
        var operationFingerprint = BuildOperationFingerprint(operationKey);
        var reports = await DbProtectedClient.Queryable<ContentReport>()
            .Where(report =>
                report.TenantId == moderationCase.TenantId &&
                report.CaseId == moderationCase.Id &&
                !report.IsDeleted)
            .Select(report => new { report.Id, report.ReporterUserId })
            .ToListAsync();
        foreach (var report in reports.Where(item => item.ReporterUserId > 0))
        {
            var notification = new CreateNotificationDto
            {
                NotificationId = SnowFlakeSingle.Instance.NextId(),
                BusinessKey = $"notification:moderation-report:{report.Id}:result:{operationFingerprint}",
                Type = NotificationType.ContentReportResolved,
                Title = "举报处理结果",
                Content = resultCode,
                Priority = (int)NotificationPriority.Normal,
                BusinessType = BusinessType.System,
                BusinessId = report.Id,
                ReceiverUserIds = [report.ReporterUserId],
                TenantId = moderationCase.TenantId,
                TemplateArguments = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["resultCode"] = resultCode
                },
                TargetKind = NotificationTargetKind.None,
                OccurredAtUtc = nowUtc
            };
            await EnqueueNotificationAsync(
                moderationCase.TenantId,
                $"moderation-report:{report.Id}:result:{operationFingerprint}",
                "ContentReport",
                report.Id,
                notification,
                nowUtc);
        }
    }

    private Task EnqueueUserActionNotificationAsync(
        UserModerationAction action,
        string operationKey,
        DateTime nowUtc)
    {
        if (action.TargetUserId <= 0)
        {
            return Task.CompletedTask;
        }

        var actionType = ((ModerationActionTypeEnum)action.ActionType).ToString();
        var operationFingerprint = BuildOperationFingerprint(operationKey);
        var notification = new CreateNotificationDto
        {
            NotificationId = SnowFlakeSingle.Instance.NextId(),
            BusinessKey = $"notification:moderation-action:{operationFingerprint}:receiver:{action.TargetUserId}",
            Type = NotificationType.UserModerationChanged,
            Title = "账号治理状态更新",
            Content = actionType,
            Priority = (int)NotificationPriority.High,
            BusinessType = BusinessType.System,
            BusinessId = action.Id,
            ReceiverUserIds = [action.TargetUserId],
            TenantId = action.TenantId,
            TemplateArguments = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["actionType"] = actionType
            },
            TargetKind = NotificationTargetKind.None,
            OccurredAtUtc = nowUtc
        };
        return EnqueueNotificationAsync(
            action.TenantId,
            $"moderation-action:{operationFingerprint}:receiver:{action.TargetUserId}",
            "UserModerationAction",
            action.Id,
            notification,
            nowUtc);
    }

    private async Task EnqueueNotificationAsync(
        long tenantId,
        string idempotencyKey,
        string aggregateType,
        long aggregateId,
        CreateNotificationDto notification,
        DateTime nowUtc)
    {
        await DbProtectedClient.Insertable(new ReliableOutboxMessage
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            TenantId = tenantId,
            TaskType = ReliableTaskTypes.NotificationRequested,
            SchemaVersion = 1,
            IdempotencyKey = $"task:notification:{idempotencyKey}",
            AggregateType = aggregateType,
            AggregateId = aggregateId.ToString(),
            PayloadJson = JsonSerializer.Serialize(new NotificationRequestedTaskPayload(notification)),
            Status = ReliableOutboxStatuses.Pending,
            MaxAttempts = 6,
            OccurredAtUtc = nowUtc,
            AvailableAtUtc = nowUtc,
            CreateTime = nowUtc
        }).ExecuteCommandAsync();
    }

    private async Task<ContentModerationCase> SavePendingChatDecisionAsync(
        ContentModerationCase moderationCase,
        ContentModerationCaseReviewWriteCommand command,
        UserModerationAction? userAction)
    {
        var nextVersion = moderationCase.Version + 1;
        var affected = await DbProtectedClient.Updateable<ContentModerationCase>()
            .SetColumns(item => new ContentModerationCase
            {
                Status = (int)ContentModerationCaseStatus.Reviewing,
                Decision = command.Decision,
                TargetDisposition = (int)ContentModerationTargetDisposition.ActionPending,
                Version = nextVersion,
                DecisionOperationKey = command.OperationKey,
                PublicResultCode = command.PublicResultCode,
                InternalRemark = command.InternalRemark,
                ModifyTime = command.NowUtc,
                ModifyBy = command.OperatorName,
                ModifyId = command.OperatorUserId
            })
            .Where(item =>
                item.Id == moderationCase.Id &&
                item.TenantId == command.TenantId &&
                item.Version == command.ExpectedCaseVersion &&
                item.Status != (int)ContentModerationCaseStatus.Resolved &&
                !item.IsDeleted)
            .ExecuteCommandAsync();
        EnsureSingleRow(affected);
        var sequence = await GetNextEventSequenceAsync(moderationCase.Id);
        await DbProtectedClient.Insertable(CreateEvent(
            moderationCase,
            sequence++,
            "DecisionRecorded",
            command.OperatorUserId,
            command.OperatorName,
            relatedActionId: userAction?.Id,
            fromStatus: moderationCase.Status,
            toStatus: (int)ContentModerationCaseStatus.Reviewing,
            resultCode: command.PublicResultCode,
            remark: command.InternalRemark,
            resultVersion: nextVersion)).ExecuteCommandAsync();
        await DbProtectedClient.Insertable(CreateEvent(
            moderationCase,
            sequence,
            "ActionRequested",
            command.OperatorUserId,
            command.OperatorName,
            resultCode: "ChatRecallQueued",
            resultVersion: nextVersion)).ExecuteCommandAsync();

        moderationCase.Status = (int)ContentModerationCaseStatus.Reviewing;
        moderationCase.Decision = command.Decision;
        moderationCase.TargetDisposition = (int)ContentModerationTargetDisposition.ActionPending;
        moderationCase.Version = nextVersion;
        moderationCase.DecisionOperationKey = command.OperationKey;
        moderationCase.PublicResultCode = command.PublicResultCode;
        moderationCase.InternalRemark = command.InternalRemark;
        return moderationCase;
    }

    private async Task<(UserModerationAction action, UserModerationState state)> WriteUserActionStateAsync(
        UserModerationState? state,
        long? caseId,
        long? sourceReportId,
        long tenantId,
        long targetUserId,
        string? targetUserName,
        int actionType,
        int? durationHours,
        string reason,
        string operationKey,
        long operatorUserId,
        string operatorName,
        DateTime nowUtc)
    {
        var policyType = GetPolicyType(actionType);
        var currentVersion = state?.Version ?? 0;
        var activating = actionType is
            (int)ModerationActionTypeEnum.Mute or (int)ModerationActionTypeEnum.Ban;
        var nextVersion = currentVersion + 1;
        DateTime? endTime = activating && durationHours.HasValue
            ? nowUtc.AddHours(durationHours.Value)
            : null;
        var action = new UserModerationAction
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            TenantId = tenantId,
            CaseId = caseId,
            SourceReportId = sourceReportId,
            OperationKey = operationKey,
            PreviousStateVersion = currentVersion,
            ResultStateVersion = nextVersion,
            SupersedesActionId = state?.CurrentActionId,
            ResultCode = activating ? "StateActivated" : "StateDeactivated",
            TargetUserId = targetUserId,
            TargetUserName = targetUserName,
            ActionType = actionType,
            Reason = reason,
            DurationHours = activating ? durationHours : null,
            StartTime = nowUtc,
            EndTime = endTime,
            IsActive = activating,
            CreateTime = nowUtc,
            CreateBy = operatorName,
            CreateId = operatorUserId
        };
        await DbProtectedClient.Insertable(action).ExecuteCommandAsync();

        if (state == null)
        {
            state = new UserModerationState
            {
                Id = SnowFlakeSingle.Instance.NextId(),
                TenantId = tenantId,
                TargetUserId = targetUserId,
                PolicyType = policyType,
                CreateTime = nowUtc,
                CreateBy = operatorName,
                CreateId = operatorUserId
            };
        }

        state.State = activating ? (int)UserModerationStateValue.Active : (int)UserModerationStateValue.Inactive;
        state.EffectiveFrom = activating ? nowUtc : null;
        state.EffectiveUntil = endTime;
        state.Version = nextVersion;
        state.CurrentActionId = action.Id;
        state.SourceCaseId = caseId;
        state.ModifyTime = nowUtc;
        state.ModifyBy = operatorName;
        state.ModifyId = operatorUserId;
        if (currentVersion == 0)
        {
            await DbProtectedClient.Insertable(state).ExecuteCommandAsync();
        }
        else
        {
            var affected = await DbProtectedClient.Updateable<UserModerationState>()
                .SetColumns(item => new UserModerationState
                {
                    State = state.State,
                    EffectiveFrom = state.EffectiveFrom,
                    EffectiveUntil = state.EffectiveUntil,
                    Version = state.Version,
                    CurrentActionId = state.CurrentActionId,
                    SourceCaseId = state.SourceCaseId,
                    ModifyTime = state.ModifyTime,
                    ModifyBy = state.ModifyBy,
                    ModifyId = state.ModifyId
                })
                .Where(item => item.Id == state.Id && item.Version == currentVersion)
                .ExecuteCommandAsync();
            EnsureSingleRow(affected);
        }

        return (action, state);
    }

    private async Task DeactivateMuteForBanAsync(
        long? caseId,
        long? sourceReportId,
        long tenantId,
        long targetUserId,
        string? targetUserName,
        string operationKey,
        long operatorUserId,
        string operatorName,
        DateTime nowUtc)
    {
        var mutePolicy = (int)UserModerationPolicyType.Mute;
        await AcquireTransactionLockAsync($"moderation-user:{tenantId}:{targetUserId}:{mutePolicy}");
        var muteState = await DbProtectedClient.Queryable<UserModerationState>()
            .Where(item =>
                item.TenantId == tenantId &&
                item.TargetUserId == targetUserId &&
                item.PolicyType == mutePolicy)
            .FirstAsync();
        if (muteState == null || muteState.State != (int)UserModerationStateValue.Active)
        {
            return;
        }

        var action = new UserModerationAction
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            TenantId = tenantId,
            CaseId = caseId,
            SourceReportId = sourceReportId,
            OperationKey = $"{operationKey}:ban-stops-mute",
            PreviousStateVersion = muteState.Version,
            ResultStateVersion = muteState.Version + 1,
            SupersedesActionId = muteState.CurrentActionId,
            ResultCode = "SupersededByBan",
            TargetUserId = targetUserId,
            TargetUserName = targetUserName,
            ActionType = (int)ModerationActionTypeEnum.Unmute,
            Reason = "封禁生效时停止当前禁言状态",
            StartTime = nowUtc,
            IsActive = false,
            CreateTime = nowUtc,
            CreateBy = operatorName,
            CreateId = operatorUserId
        };
        await DbProtectedClient.Insertable(action).ExecuteCommandAsync();
        var affected = await DbProtectedClient.Updateable<UserModerationState>()
            .SetColumns(item => new UserModerationState
            {
                State = (int)UserModerationStateValue.Inactive,
                EffectiveFrom = null,
                EffectiveUntil = null,
                Version = muteState.Version + 1,
                CurrentActionId = action.Id,
                SourceCaseId = caseId,
                ModifyTime = nowUtc,
                ModifyBy = operatorName,
                ModifyId = operatorUserId
            })
            .Where(item => item.Id == muteState.Id && item.Version == muteState.Version)
            .ExecuteCommandAsync();
        EnsureSingleRow(affected);
    }

    private static int GetPolicyType(int actionType)
    {
        return actionType is (int)ModerationActionTypeEnum.Mute or (int)ModerationActionTypeEnum.Unmute
            ? (int)UserModerationPolicyType.Mute
            : (int)UserModerationPolicyType.Ban;
    }

    private async Task<UserModerationState?> QueryActionStateAsync(UserModerationAction action)
    {
        var policyType = action.ActionType is
            (int)ModerationActionTypeEnum.Mute or (int)ModerationActionTypeEnum.Unmute
                ? (int)UserModerationPolicyType.Mute
                : (int)UserModerationPolicyType.Ban;
        return await DbProtectedClient.Queryable<UserModerationState>()
            .Where(item =>
                item.TenantId == action.TenantId &&
                item.TargetUserId == action.TargetUserId &&
                item.PolicyType == policyType)
            .FirstAsync();
    }

    private async Task<ContentModerationCase> QueryCaseForWriteAsync(long tenantId, string casePublicId)
    {
        var moderationCase = await DbProtectedClient.Queryable<ContentModerationCase>()
            .Where(item => item.TenantId == tenantId && item.PublicId == casePublicId && !item.IsDeleted)
            .FirstAsync();
        return moderationCase ?? throw new ContentModerationCaseNotFoundException();
    }

    private async Task<int> GetNextEvidenceSequenceAsync(long caseId)
    {
        var current = await DbProtectedClient.Queryable<ContentModerationEvidence>()
            .Where(item => item.CaseId == caseId)
            .MaxAsync(item => (int?)item.EvidenceSequence);
        return (current ?? 0) + 1;
    }

    private async Task<int> GetNextEventSequenceAsync(long caseId)
    {
        var current = await DbProtectedClient.Queryable<ContentModerationCaseEvent>()
            .Where(item => item.CaseId == caseId)
            .MaxAsync(item => (int?)item.EventSequence);
        return (current ?? 0) + 1;
    }

    private async Task AcquireTransactionLockAsync(string identity)
    {
        if (DbProtectedClient.CurrentConnectionConfig.DbType != DbType.PostgreSQL)
        {
            return;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        var lockKey = BinaryPrimitives.ReadInt64BigEndian(hash);
        await DbProtectedClient.Ado.ExecuteCommandAsync(
            "SELECT pg_advisory_xact_lock(@LockKey)",
            new SugarParameter("@LockKey", lockKey));
    }

}
