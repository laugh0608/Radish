using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;

namespace Radish.Service;

public partial class ContentModerationService
{
    public async Task<VoPagedResult<ContentModerationDecisionNoticeVo>> GetMyAppealableDecisionsAsync(
        ContentModerationAppealQueryDto query,
        long appellantUserId,
        long tenantId)
    {
        ArgumentNullException.ThrowIfNull(query);
        var pageIndex = NormalizePageIndex(query.PageIndex);
        var pageSize = NormalizePageSize(query.PageSize);
        var (items, total) = await RequireCaseRepository().QueryMyAppealableDecisionsAsync(
            Math.Max(tenantId, 0), appellantUserId, pageIndex, pageSize, DateTime.UtcNow);
        return new VoPagedResult<ContentModerationDecisionNoticeVo>
        {
            VoItems = items.Select(MapDecisionNotice).ToList(),
            VoTotal = total,
            VoPageIndex = pageIndex,
            VoPageSize = pageSize
        };
    }

    public async Task<VoPagedResult<ContentModerationAppealVo>> GetMyAppealsAsync(
        ContentModerationAppealQueryDto query,
        long appellantUserId,
        long tenantId)
    {
        ArgumentNullException.ThrowIfNull(query);
        var pageIndex = NormalizePageIndex(query.PageIndex);
        var pageSize = NormalizePageSize(query.PageSize);
        var repository = RequireCaseRepository();
        var (items, total) = await repository.QueryMyAppealsAsync(
            Math.Max(tenantId, 0), appellantUserId, pageIndex, pageSize);
        var mapped = new List<ContentModerationAppealVo>(items.Count);
        foreach (var item in items)
        {
            var aggregate = await repository.QueryAppealAggregateAsync(
                Math.Max(tenantId, 0), item.PublicId, appellantUserId);
            if (aggregate != null)
            {
                mapped.Add(MapAppeal(aggregate, includeInternal: false));
            }
        }

        return new VoPagedResult<ContentModerationAppealVo>
        {
            VoItems = mapped,
            VoTotal = total,
            VoPageIndex = pageIndex,
            VoPageSize = pageSize
        };
    }

    public async Task<ContentModerationAppealVo> GetAppealAsync(
        string appealPublicId,
        long tenantId,
        long? appellantUserId = null)
    {
        if (string.IsNullOrWhiteSpace(appealPublicId))
        {
            throw new ArgumentException("申诉公开标识不能为空");
        }

        var aggregate = await RequireCaseRepository().QueryAppealAggregateAsync(
            Math.Max(tenantId, 0), appealPublicId.Trim(), appellantUserId);
        return aggregate == null
            ? throw AppealNotFound()
            : MapAppeal(aggregate, includeInternal: !appellantUserId.HasValue);
    }

    public async Task<VoPagedResult<ContentModerationAppealVo>> GetAppealQueueAsync(
        ContentModerationAppealQueryDto query,
        long tenantId)
    {
        ArgumentNullException.ThrowIfNull(query);
        var pageIndex = NormalizePageIndex(query.PageIndex);
        var pageSize = NormalizePageSize(query.PageSize);
        var targetType = string.IsNullOrWhiteSpace(query.TargetType)
            ? null
            : (int?)ParseTargetType(query.TargetType);
        var repository = RequireCaseRepository();
        var (items, total) = await repository.QueryAppealQueueAsync(new ContentModerationAppealQueueCommand(
            Math.Max(tenantId, 0), query.Status, targetType, NormalizeQueueKeyword(query.Keyword), pageIndex, pageSize));
        var mapped = new List<ContentModerationAppealVo>(items.Count);
        foreach (var item in items)
        {
            var aggregate = await repository.QueryAppealAggregateAsync(item.TenantId, item.PublicId);
            if (aggregate != null)
            {
                mapped.Add(MapAppeal(aggregate, includeInternal: true));
            }
        }

        return new VoPagedResult<ContentModerationAppealVo>
        {
            VoItems = mapped,
            VoTotal = total,
            VoPageIndex = pageIndex,
            VoPageSize = pageSize
        };
    }

    public async Task<ContentModerationAppealVo> SubmitAppealAsync(
        SubmitContentModerationAppealDto dto,
        long appellantUserId,
        string appellantName,
        long tenantId)
    {
        ArgumentNullException.ThrowIfNull(dto);
        try
        {
            var result = await RequireCaseRepository().SubmitAppealAsync(new ContentModerationAppealSubmitCommand(
                Math.Max(tenantId, 0), dto.CasePublicId.Trim(), appellantUserId,
                NormalizeActorName(appellantName, appellantUserId), dto.Statement.Trim(),
                dto.OperationKey.Trim(), DateTime.UtcNow));
            var mapped = await GetAppealAsync(result.Appeal.PublicId, tenantId, appellantUserId);
            mapped.VoIsIdempotentReplay = result.IsIdempotentReplay;
            return mapped;
        }
        catch (ContentModerationAppealAlreadyExistsException)
        {
            throw new BusinessException("该案件已经提交过申诉", 409,
                "Moderation.AppealAlreadyExists", "error.moderation.appeal_already_exists");
        }
        catch (ContentModerationAppealEligibilityException ex)
        {
            throw new BusinessException("该案件当前不符合申诉条件", 409,
                $"Moderation.Appeal{ex.ResultCode}", "error.moderation.appeal_not_eligible");
        }
        catch (ContentModerationCaseNotFoundException)
        {
            throw AppealNotFound();
        }
    }

    public Task<ContentModerationAppealVo> WithdrawAppealAsync(
        ContentModerationAppealVersionedOperationDto dto,
        long appellantUserId,
        long tenantId)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return ExecuteAppealWriteAsync(async repository =>
        {
            var result = await repository.WithdrawAppealAsync(new ContentModerationAppealWithdrawCommand(
                Math.Max(tenantId, 0), dto.AppealPublicId.Trim(), appellantUserId,
                dto.ExpectedVersion, dto.OperationKey.Trim(), DateTime.UtcNow));
            return (result.Appeal, result.IsIdempotentReplay, (long?)appellantUserId);
        }, tenantId);
    }

    public Task<ContentModerationAppealVo> StartAppealReviewAsync(
        ContentModerationAppealVersionedOperationDto dto,
        long operatorUserId,
        string operatorName,
        long tenantId)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return ExecuteAppealWriteAsync(async repository =>
        {
            var result = await repository.StartAppealReviewAsync(new ContentModerationAppealStartReviewCommand(
                Math.Max(tenantId, 0), dto.AppealPublicId.Trim(), dto.ExpectedVersion,
                dto.OperationKey.Trim(), operatorUserId, NormalizeActorName(operatorName, operatorUserId),
                DateTime.UtcNow));
            return (result.Appeal, result.IsIdempotentReplay, null);
        }, tenantId);
    }

    public async Task<ContentModerationAppealVo> CaptureAppealEvidenceAsync(
        CaptureContentModerationAppealEvidenceDto dto,
        long operatorUserId,
        string operatorName,
        long tenantId)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var repository = RequireCaseRepository();
        var aggregate = await repository.QueryAppealAggregateAsync(
            Math.Max(tenantId, 0), dto.AppealPublicId.Trim()) ?? throw AppealNotFound();
        var targetState = ContentModerationTargetState.Available;
        string? title = dto.SnapshotTitle?.Trim();
        string? summary = dto.SnapshotSummary?.Trim();
        long? postId = null;
        long? commentId = null;
        long? channelId = null;
        long? messageId = null;
        try
        {
            var snapshot = await ResolveReportTargetSnapshotAsync(
                (ContentReportTargetTypeEnum)aggregate.Case.TargetType,
                aggregate.Case.TargetContentId);
            title = snapshot.SnapshotTitle;
            summary = snapshot.SnapshotSummary;
            postId = snapshot.TargetPostId;
            channelId = snapshot.TargetChannelId;
            commentId = aggregate.Case.TargetType == (int)ContentReportTargetTypeEnum.Comment
                ? aggregate.Case.TargetContentId
                : null;
            messageId = aggregate.Case.TargetType == (int)ContentReportTargetTypeEnum.ChatMessage
                ? aggregate.Case.TargetContentId
                : null;
        }
        catch (InvalidOperationException)
        {
            targetState = ContentModerationTargetState.Unavailable;
        }

        try
        {
            var result = await repository.AppendAppealEvidenceAsync(
                new ContentModerationAppealEvidenceCommand(
                    Math.Max(tenantId, 0),
                    aggregate.Appeal.PublicId,
                    dto.ExpectedVersion,
                    (int)targetState,
                    title,
                    summary,
                    postId,
                    commentId,
                    channelId,
                    messageId,
                    ComputeSnapshotHash(
                        aggregate.Case.TargetType,
                        aggregate.Case.TargetContentId,
                        aggregate.Case.TargetUserId,
                        title,
                        summary),
                    dto.OperationKey.Trim(),
                    operatorUserId,
                    NormalizeActorName(operatorName, operatorUserId),
                    DateTime.UtcNow));
            var mapped = await GetAppealAsync(result.Appeal.PublicId, tenantId);
            mapped.VoIsIdempotentReplay = result.IsIdempotentReplay;
            return mapped;
        }
        catch (ContentModerationConcurrencyException)
        {
            throw AppealVersionConflict();
        }
        catch (ContentModerationIdempotencyConflictException)
        {
            throw new BusinessException("operationKey 已用于不同申诉操作", 409,
                "Moderation.AppealOperationConflict", "error.moderation.operation_conflict");
        }
    }

    public Task<ContentModerationAppealVo> ReviewAppealAsync(
        ReviewContentModerationAppealDto dto,
        long operatorUserId,
        string operatorName,
        long tenantId)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var outcome = (ContentModerationAppealOutcome)dto.Outcome;
        if (!Enum.IsDefined(outcome) || outcome == ContentModerationAppealOutcome.None)
        {
            throw new ArgumentException("申诉结论无效");
        }

        return ExecuteAppealWriteAsync(async repository =>
        {
            var current = await repository.QueryAppealAggregateAsync(
                Math.Max(tenantId, 0), dto.AppealPublicId.Trim()) ?? throw new ContentModerationAppealNotFoundException();
            var eligible = (ContentModerationReliefScope)current.Appeal.EligibleScopeSnapshot;
            var granted = (ContentModerationReliefScope)dto.GrantedScope;
            var scopeValid = (granted & ~eligible) == 0 &&
                             (outcome == ContentModerationAppealOutcome.Upheld && granted == ContentModerationReliefScope.None ||
                              outcome == ContentModerationAppealOutcome.Granted && granted == eligible ||
                              outcome == ContentModerationAppealOutcome.PartiallyGranted &&
                              granted != ContentModerationReliefScope.None && granted != eligible);
            if (!scopeValid)
            {
                throw new ArgumentException("申诉结论与纠正范围不一致");
            }

            var resultCode = outcome.ToString();
            var result = await repository.ReviewAppealAsync(new ContentModerationAppealReviewCommand(
                Math.Max(tenantId, 0), dto.AppealPublicId.Trim(), dto.ExpectedVersion, dto.Outcome,
                dto.GrantedScope, resultCode, dto.PublicResultSummary.Trim(),
                string.IsNullOrWhiteSpace(dto.InternalRemark) ? null : dto.InternalRemark.Trim(),
                dto.OperationKey.Trim(), operatorUserId, NormalizeActorName(operatorName, operatorUserId),
                DateTime.UtcNow));
            return (result.Appeal, result.IsIdempotentReplay, null);
        }, tenantId);
    }

    public async Task<ContentModerationAppealVo> ExecuteAppealReliefAsync(
        ContentModerationAppealVersionedOperationDto dto,
        long operatorUserId,
        string operatorName,
        long tenantId)
    {
        ArgumentNullException.ThrowIfNull(dto);
        try
        {
            var result = await RequireCaseRepository().ExecuteAppealReliefAsync(new ContentModerationAppealReliefCommand(
                Math.Max(tenantId, 0), dto.AppealPublicId.Trim(), dto.ExpectedVersion,
                dto.OperationKey.Trim(), operatorUserId, NormalizeActorName(operatorName, operatorUserId),
                DateTime.UtcNow));
            var mapped = await GetAppealAsync(result.Appeal.PublicId, tenantId);
            mapped.VoIsIdempotentReplay = result.IsIdempotentReplay;
            return mapped;
        }
        catch (ContentModerationAppealNotFoundException)
        {
            throw AppealNotFound();
        }
        catch (ContentModerationConcurrencyException)
        {
            throw AppealVersionConflict();
        }
    }

    private async Task<ContentModerationAppealVo> ExecuteAppealWriteAsync(
        Func<IContentModerationCaseRepository,
            Task<(ContentModerationAppeal appeal, bool replay, long? appellantUserId)>> action,
        long tenantId)
    {
        try
        {
            var result = await action(RequireCaseRepository());
            var mapped = await GetAppealAsync(result.appeal.PublicId, tenantId, result.appellantUserId);
            mapped.VoIsIdempotentReplay = result.replay;
            return mapped;
        }
        catch (ContentModerationAppealNotFoundException)
        {
            throw AppealNotFound();
        }
        catch (ContentModerationConcurrencyException)
        {
            throw AppealVersionConflict();
        }
        catch (ContentModerationIdempotencyConflictException)
        {
            throw new BusinessException("operationKey 已用于不同申诉操作", 409,
                "Moderation.AppealOperationConflict", "error.moderation.operation_conflict");
        }
    }

    private ContentModerationAppealVo MapAppeal(
        ContentModerationAppealAggregate aggregate,
        bool includeInternal)
    {
        return new ContentModerationAppealVo
        {
            VoAppealPublicId = aggregate.Appeal.PublicId,
            VoCasePublicId = aggregate.Case.PublicId,
            VoStatus = ((ContentModerationAppealStatus)aggregate.Appeal.Status).ToString(),
            VoOutcome = ((ContentModerationAppealOutcome)aggregate.Appeal.Outcome).ToString(),
            VoEligibleScope = aggregate.Appeal.EligibleScopeSnapshot,
            VoGrantedScope = aggregate.Appeal.GrantedScope,
            VoVersion = aggregate.Appeal.Version,
            VoStatement = aggregate.Appeal.Statement,
            VoPublicResultCode = aggregate.Appeal.PublicResultCode,
            VoPublicResultSummary = aggregate.Appeal.PublicResultSummary,
            VoInternalRemark = includeInternal ? aggregate.Appeal.InternalRemark : null,
            VoSubmittedAt = aggregate.Appeal.SubmittedAt,
            VoEligibleUntilUtc = aggregate.Appeal.EligibleUntilUtc,
            VoResolvedAt = aggregate.Appeal.ResolvedAt,
            VoEvents = aggregate.Events.Select(item => new ContentModerationAppealEventVo
            {
                VoSequence = item.EventSequence,
                VoEventType = item.EventType,
                VoResultCode = item.ResultCode,
                VoRemark = includeInternal ? item.Remark : null,
                VoActorUserId = includeInternal ? item.ActorUserId : 0,
                VoActorName = includeInternal ? item.ActorName : string.Empty,
                VoCreateTime = item.CreateTime
            }).ToList(),
            VoTargetActions = aggregate.TargetActions.Select(item => new ContentModerationTargetActionVo
            {
                VoActionType = ((ContentModerationTargetActionType)item.ActionType).ToString(),
                VoStatus = ((ContentModerationTargetActionStatus)item.Status).ToString(),
                VoResultCode = item.ResultCode,
                VoChangedTargetState = item.ChangedTargetState,
                VoRequestedAt = item.RequestedAt,
                VoCompletedAt = item.CompletedAt
            }).ToList(),
            VoUserActions = aggregate.UserActions
                .Select(item => MapAppealUserAction(item, includeInternal))
                .ToList()
        };
    }

    private UserModerationActionVo MapAppealUserAction(
        UserModerationAction action,
        bool includeInternal)
    {
        var result = Mapper.Map<UserModerationActionVo>(action);
        if (includeInternal)
        {
            return result;
        }

        result.VoOperatorUserId = 0;
        result.VoOperatorUserName = string.Empty;
        result.VoSourceReportId = null;
        result.VoSourceReportTargetType = null;
        result.VoSourceReportTargetContentId = null;
        result.VoSourceReportTargetPostId = null;
        result.VoSourceReportTargetCommentId = null;
        result.VoSourceReportTargetChannelId = null;
        result.VoSourceReportTargetMessageId = null;
        result.VoSourceReportTargetSnapshotTitle = null;
        result.VoSourceReportTargetSnapshotSummary = null;
        return result;
    }

    private static ContentModerationDecisionNoticeVo MapDecisionNotice(
        ContentModerationDecisionCandidate candidate)
    {
        var scope = ContentModerationReliefScope.None;
        if (candidate.TargetActions.Any(item =>
                item.ActionType == (int)ContentModerationTargetActionType.Restrict && item.ChangedTargetState))
        {
            scope |= ContentModerationReliefScope.TargetContent;
        }

        if (candidate.UserActions.Any(item => item.ActionType == (int)ModerationActionTypeEnum.Mute))
        {
            scope |= ContentModerationReliefScope.Mute;
        }

        if (candidate.UserActions.Any(item => item.ActionType == (int)ModerationActionTypeEnum.Ban))
        {
            scope |= ContentModerationReliefScope.Ban;
        }

        return new ContentModerationDecisionNoticeVo
        {
            VoCasePublicId = candidate.Case.PublicId,
            VoTargetType = ((ContentReportTargetTypeEnum)candidate.Case.TargetType).ToString(),
            VoTargetContentId = candidate.Case.TargetContentId,
            VoPublicResultCode = candidate.Case.PublicResultCode,
            VoEligibleScope = (int)scope,
            VoResolvedAt = candidate.Case.ResolvedAt!.Value,
            VoEligibleUntilUtc = candidate.Case.ResolvedAt.Value.AddDays(30),
            VoAppealPublicId = candidate.Appeal?.PublicId,
            VoAppealStatus = candidate.Appeal == null
                ? null
                : ((ContentModerationAppealStatus)candidate.Appeal.Status).ToString()
        };
    }

    private static BusinessException AppealNotFound() => new(
        "治理申诉不存在", 404, "Moderation.AppealNotFound", "error.moderation.appeal_not_found");

    private static BusinessException AppealVersionConflict() => new(
        "治理申诉已被其他操作更新，请刷新后重试", 409,
        "Moderation.AppealVersionConflict", "error.moderation.version_conflict");
}
