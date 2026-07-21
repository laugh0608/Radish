using System.Security.Cryptography;
using System.Text;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;

namespace Radish.Service;

public partial class ContentModerationService
{
    public async Task<ContentReportReceiptVo> SubmitCaseReportAsync(
        SubmitContentReportDto dto,
        long reporterUserId,
        string reporterUserName,
        long tenantId)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.TargetContentId <= 0 || reporterUserId <= 0)
        {
            throw new ArgumentException("举报目标或举报人无效");
        }

        var repository = RequireCaseRepository();
        var targetType = ParseTargetType(dto.TargetType);
        ResolvedReportTargetSnapshot targetSnapshot;
        try
        {
            targetSnapshot = await ResolveReportTargetSnapshotAsync(targetType, dto.TargetContentId);
        }
        catch (InvalidOperationException)
        {
            throw new BusinessException(
                "举报目标不存在或已不可用",
                404,
                "Moderation.TargetUnavailable",
                "error.moderation.target_unavailable");
        }

        if (targetSnapshot.TargetUserId < 0)
        {
            throw new BusinessException(
                "举报目标用户不存在",
                404,
                "Moderation.TargetUserNotFound",
                "error.moderation.target_user_not_found");
        }

        if (targetSnapshot.TargetUserId > 0 && targetSnapshot.TargetUserId == reporterUserId)
        {
            throw new ArgumentException("不能举报自己的内容");
        }

        var normalizedReporterName = NormalizeActorName(reporterUserName, reporterUserId);
        var normalizedReasonType = string.IsNullOrWhiteSpace(dto.ReasonType) ? "Other" : dto.ReasonType.Trim();
        var snapshotHash = ComputeSnapshotHash(
            (int)targetType,
            dto.TargetContentId,
            targetSnapshot.TargetUserId,
            targetSnapshot.SnapshotTitle,
            targetSnapshot.SnapshotSummary);
        var result = await repository.SubmitReportAsync(new ContentModerationReportWriteCommand(
            Math.Max(tenantId, 0),
            (int)targetType,
            dto.TargetContentId,
            targetSnapshot.TargetUserId,
            targetSnapshot.TargetUserName,
            targetSnapshot.TargetPostId,
            targetSnapshot.TargetChannelId,
            targetSnapshot.SnapshotTitle,
            targetSnapshot.SnapshotSummary,
            snapshotHash,
            reporterUserId,
            normalizedReporterName,
            normalizedReasonType,
            string.IsNullOrWhiteSpace(dto.ReasonDetail) ? null : dto.ReasonDetail.Trim(),
            DateTime.UtcNow));
        return MapReceipt(result.Report, result.Case, result.IsDuplicate);
    }

    public async Task<VoPagedResult<ContentReportReceiptVo>> GetMyReportsAsync(
        MyContentReportQueryDto query,
        long reporterUserId,
        long tenantId)
    {
        ArgumentNullException.ThrowIfNull(query);
        var repository = RequireCaseRepository();
        var pageIndex = NormalizePageIndex(query.PageIndex);
        var pageSize = NormalizePageSize(query.PageSize);
        var (reports, totalCount) = await repository.QueryMyReportsAsync(
            Math.Max(tenantId, 0),
            reporterUserId,
            pageIndex,
            pageSize);
        var caseById = (await repository.QueryCasesByIdsAsync(
                Math.Max(tenantId, 0),
                reports.Where(report => report.CaseId.HasValue).Select(report => report.CaseId!.Value).ToArray()))
            .ToDictionary(item => item.Id);
        var items = new List<ContentReportReceiptVo>(reports.Count);
        foreach (var report in reports)
        {
            var moderationCase = report.CaseId.HasValue && caseById.TryGetValue(report.CaseId.Value, out var matchedCase)
                ? matchedCase
                : null;
            items.Add(MapReceipt(report, moderationCase, false));
        }

        return new VoPagedResult<ContentReportReceiptVo>
        {
            VoItems = items,
            VoTotal = totalCount,
            VoPageIndex = pageIndex,
            VoPageSize = pageSize
        };
    }

    public async Task<ContentReportReceiptVo> GetMyReportAsync(
        string reportPublicId,
        long reporterUserId,
        long tenantId)
    {
        if (string.IsNullOrWhiteSpace(reportPublicId))
        {
            throw new ArgumentException("举报公开标识不能为空");
        }

        var repository = RequireCaseRepository();
        var report = await repository.QueryMyReportAsync(Math.Max(tenantId, 0), reporterUserId, reportPublicId.Trim());
        if (report == null)
        {
            throw new BusinessException(
                "举报记录不存在",
                404,
                "Moderation.ReportNotFound",
                "error.moderation.report_not_found");
        }

        var moderationCase = report.CaseId.HasValue
            ? await repository.QueryCaseByIdAsync(Math.Max(tenantId, 0), report.CaseId.Value)
            : null;
        return MapReceipt(report, moderationCase, false);
    }

    public async Task<VoPagedResult<ContentModerationCaseQueueItemVo>> GetCaseQueueAsync(
        ContentModerationCaseQueueDto query,
        long tenantId)
    {
        ArgumentNullException.ThrowIfNull(query);
        var status = NormalizeCaseStatus(query.Status);
        var targetType = string.IsNullOrWhiteSpace(query.TargetType)
            ? null
            : (int?)ParseTargetType(query.TargetType);
        var pageIndex = NormalizePageIndex(query.PageIndex);
        var pageSize = NormalizePageSize(query.PageSize);
        var repository = RequireCaseRepository();
        var (cases, totalCount) = await repository.QueryCaseQueueAsync(new ContentModerationCaseQueueCommand(
            Math.Max(tenantId, 0),
            status,
            targetType,
            NormalizeQueueKeyword(query.Keyword),
            pageIndex,
            pageSize));
        var reportCounts = await repository.QueryReportCountsByCaseIdsAsync(
            Math.Max(tenantId, 0),
            cases.Select(item => item.Id).ToArray());
        var items = new List<ContentModerationCaseQueueItemVo>(cases.Count);
        foreach (var moderationCase in cases)
        {
            items.Add(MapCaseQueueItem(
                moderationCase,
                reportCounts.TryGetValue(moderationCase.Id, out var count) ? count : 0));
        }

        return new VoPagedResult<ContentModerationCaseQueueItemVo>
        {
            VoItems = items,
            VoTotal = totalCount,
            VoPageIndex = pageIndex,
            VoPageSize = pageSize
        };
    }

    public async Task<ContentModerationCaseDetailVo> GetCaseAsync(string casePublicId, long tenantId)
    {
        if (string.IsNullOrWhiteSpace(casePublicId))
        {
            throw new ArgumentException("案件公开标识不能为空");
        }

        var aggregate = await RequireCaseRepository().QueryCaseAggregateAsync(Math.Max(tenantId, 0), casePublicId.Trim());
        if (aggregate == null)
        {
            throw CaseNotFound();
        }

        return MapCaseDetail(aggregate);
    }

    public async Task<ContentModerationCaseDetailVo> CaptureEvidenceAsync(
        CaptureContentModerationEvidenceDto dto,
        long operatorUserId,
        string operatorName,
        long tenantId)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.EvidenceType is not
            (int)ContentModerationEvidenceType.CurrentTargetSnapshot and not
            (int)ContentModerationEvidenceType.ModeratorNote)
        {
            throw new ArgumentException("仅支持采集当前快照或追加管理员备注");
        }

        var repository = RequireCaseRepository();
        var aggregate = await repository.QueryCaseAggregateAsync(Math.Max(tenantId, 0), dto.CasePublicId.Trim());
        if (aggregate == null)
        {
            throw CaseNotFound();
        }

        var moderationCase = aggregate.Case;
        var title = NormalizeEvidenceText(dto.SnapshotTitle, 200);
        var summary = NormalizeEvidenceText(dto.SnapshotSummary, 500);
        var targetState = ContentModerationTargetState.Available;
        long? targetPostId = null;
        long? targetChannelId = null;
        if (dto.EvidenceType == (int)ContentModerationEvidenceType.CurrentTargetSnapshot)
        {
            try
            {
                var snapshot = await ResolveReportTargetSnapshotAsync(
                    (ContentReportTargetTypeEnum)moderationCase.TargetType,
                    moderationCase.TargetContentId);
                title = snapshot.SnapshotTitle;
                summary = snapshot.SnapshotSummary;
                targetPostId = snapshot.TargetPostId;
                targetChannelId = snapshot.TargetChannelId;
            }
            catch (InvalidOperationException)
            {
                targetState = ContentModerationTargetState.Unavailable;
                title = null;
                summary = null;
            }
        }
        else if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("管理员备注不能为空");
        }

        try
        {
            await repository.AppendEvidenceAsync(new ContentModerationEvidenceWriteCommand(
                moderationCase.TenantId,
                moderationCase.PublicId,
                dto.ExpectedVersion,
                dto.EvidenceType,
                (int)targetState,
                title,
                summary,
                moderationCase.TargetUserId,
                aggregate.Evidence.LastOrDefault()?.TargetUserName,
                targetPostId,
                moderationCase.TargetType == (int)ContentReportTargetTypeEnum.Comment
                    ? moderationCase.TargetContentId
                    : null,
                targetChannelId,
                moderationCase.TargetType == (int)ContentReportTargetTypeEnum.ChatMessage
                    ? moderationCase.TargetContentId
                    : null,
                null,
                null,
                ComputeSnapshotHash(
                    moderationCase.TargetType,
                    moderationCase.TargetContentId,
                    moderationCase.TargetUserId,
                    title,
                    summary),
                operatorUserId,
                NormalizeActorName(operatorName, operatorUserId),
                DateTime.UtcNow));
        }
        catch (ContentModerationConcurrencyException)
        {
            throw CaseVersionConflict();
        }

        return await GetCaseAsync(moderationCase.PublicId, moderationCase.TenantId);
    }

    public async Task<ContentModerationCaseReviewResultVo> ReviewCaseAsync(
        ReviewContentModerationCaseDto dto,
        long operatorUserId,
        string operatorName,
        long tenantId)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ValidateDecision(dto);
        var repository = RequireCaseRepository();
        var aggregate = await repository.QueryCaseAggregateAsync(Math.Max(tenantId, 0), dto.CasePublicId.Trim());
        if (aggregate == null)
        {
            throw CaseNotFound();
        }

        ContentModerationUserActionWriteCommand? userAction = null;
        if (dto.UserAction != null)
        {
            if (operatorUserId == aggregate.Case.TargetUserId)
            {
                throw new BusinessException(
                    "不能对自己执行治理动作",
                    403,
                    "Moderation.SelfActionForbidden",
                    "error.moderation.self_action_forbidden");
            }

            var targetUser = await _userRepository.QueryFirstAsync(user =>
                user.Id == aggregate.Case.TargetUserId &&
                user.TenantId == aggregate.Case.TenantId &&
                !user.IsDeleted);
            if (targetUser == null)
            {
                throw new BusinessException(
                    "治理目标用户不存在",
                    404,
                    "Moderation.TargetUserNotFound",
                    "error.moderation.target_user_not_found");
            }

            if (targetUser.RoleNames.Any(role =>
                    role.Equals("System", StringComparison.OrdinalIgnoreCase) ||
                    role.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
            {
                throw new BusinessException(
                    "受保护账号不能通过内容治理案件直接处置",
                    403,
                    "Moderation.ProtectedAccount",
                    "error.moderation.protected_account");
            }

            ValidateUserAction(dto.UserAction);
            userAction = new ContentModerationUserActionWriteCommand(
                aggregate.Case.TargetUserId,
                targetUser.UserName,
                dto.UserAction.ActionType,
                dto.UserAction.ExpectedStateVersion,
                dto.UserAction.DurationHours,
                dto.UserAction.Reason.Trim());
        }

        try
        {
            var result = await repository.ReviewCaseAsync(new ContentModerationCaseReviewWriteCommand(
                aggregate.Case.TenantId,
                aggregate.Case.PublicId,
                dto.ExpectedVersion,
                dto.Decision,
                dto.TargetDisposition,
                dto.ExpectedTargetVersion,
                dto.PublicResultCode.Trim(),
                NormalizeEvidenceText(dto.InternalRemark, 1000),
                userAction,
                dto.OperationKey.Trim(),
                operatorUserId,
                NormalizeActorName(operatorName, operatorUserId),
                DateTime.UtcNow));
            return new ContentModerationCaseReviewResultVo
            {
                VoCasePublicId = result.Case.PublicId,
                VoStatus = ToCaseStatusName(result.Case.Status),
                VoDecision = ToDecisionName(result.Case.Decision),
                VoTargetDisposition = ToDispositionName(result.Case.TargetDisposition),
                VoVersion = result.Case.Version,
                VoUserActionId = result.UserAction?.Id,
                VoUserStateVersion = result.UserState?.Version,
                VoIsIdempotentReplay = result.IsIdempotentReplay
            };
        }
        catch (ContentModerationCaseNotFoundException)
        {
            throw CaseNotFound();
        }
        catch (ContentModerationConcurrencyException)
        {
            throw CaseVersionConflict();
        }
        catch (ContentModerationIdempotencyConflictException)
        {
            throw new BusinessException(
                "operationKey 已用于不同治理动作",
                409,
                "Moderation.OperationConflict",
                "error.moderation.operation_conflict");
        }
        catch (ContentModerationTargetActionException ex)
        {
            throw new BusinessException(
                "目标内容已变化或当前无法执行处置，请刷新证据后重试",
                409,
                $"Moderation.TargetAction{ex.ResultCode}",
                "error.moderation.target_action_conflict");
        }
    }

    public async Task<ContentModerationCaseReviewResultVo> ApplyCorrectiveActionAsync(
        ApplyContentModerationCorrectiveActionDto dto,
        long operatorUserId,
        string operatorName,
        long tenantId)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ValidateUserAction(dto.UserAction);
        var repository = RequireCaseRepository();
        var aggregate = await repository.QueryCaseAggregateAsync(Math.Max(tenantId, 0), dto.CasePublicId.Trim());
        if (aggregate == null)
        {
            throw CaseNotFound();
        }

        if (operatorUserId == aggregate.Case.TargetUserId)
        {
            throw new BusinessException("不能对自己执行治理动作", 403,
                "Moderation.SelfActionForbidden", "error.moderation.self_action_forbidden");
        }

        var targetUser = await _userRepository.QueryFirstAsync(user =>
            user.Id == aggregate.Case.TargetUserId &&
            user.TenantId == aggregate.Case.TenantId &&
            !user.IsDeleted);
        if (targetUser == null)
        {
            throw new BusinessException("治理目标用户不存在", 404,
                "Moderation.TargetUserNotFound", "error.moderation.target_user_not_found");
        }

        if (targetUser.RoleNames.Any(role =>
                role.Equals("System", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException("受保护账号不能通过内容治理案件直接处置", 403,
                "Moderation.ProtectedAccount", "error.moderation.protected_account");
        }

        try
        {
            var result = await repository.ApplyCorrectiveUserActionAsync(
                new ContentModerationCorrectiveActionCommand(
                    aggregate.Case.TenantId,
                    aggregate.Case.PublicId,
                    dto.ExpectedVersion,
                    new ContentModerationUserActionWriteCommand(
                        aggregate.Case.TargetUserId,
                        targetUser.UserName,
                        dto.UserAction.ActionType,
                        dto.UserAction.ExpectedStateVersion,
                        dto.UserAction.DurationHours,
                        dto.UserAction.Reason.Trim()),
                    dto.OperationKey.Trim(),
                    dto.Remark.Trim(),
                    operatorUserId,
                    NormalizeActorName(operatorName, operatorUserId),
                    DateTime.UtcNow));
            return MapReviewResult(result);
        }
        catch (ContentModerationCaseNotFoundException)
        {
            throw CaseNotFound();
        }
        catch (ContentModerationConcurrencyException)
        {
            throw CaseVersionConflict();
        }
        catch (ContentModerationIdempotencyConflictException)
        {
            throw new BusinessException("operationKey 已用于不同治理动作", 409,
                "Moderation.OperationConflict", "error.moderation.operation_conflict");
        }
    }

    private IContentModerationCaseRepository RequireCaseRepository()
    {
        return _moderationCaseRepository
            ?? throw new InvalidOperationException("内容治理案件仓储未注册");
    }

    private static ContentReportReceiptVo MapReceipt(
        ContentReport report,
        ContentModerationCase? moderationCase,
        bool isDuplicate)
    {
        return new ContentReportReceiptVo
        {
            VoReportPublicId = report.PublicId ?? string.Empty,
            VoTargetType = ToReportTargetTypeName(report.ReportTargetType),
            VoTargetSnapshotTitle = report.TargetSnapshotTitle,
            VoReporterState = report.ReporterState,
            VoPublicResultCode = moderationCase?.PublicResultCode,
            VoSubmittedAt = report.CreateTime,
            VoResolvedAt = moderationCase?.ResolvedAt,
            VoIsDuplicate = isDuplicate
        };
    }

    private static ContentModerationCaseReviewResultVo MapReviewResult(
        ContentModerationCaseReviewWriteResult result)
    {
        return new ContentModerationCaseReviewResultVo
        {
            VoCasePublicId = result.Case.PublicId,
            VoStatus = ToCaseStatusName(result.Case.Status),
            VoDecision = ToDecisionName(result.Case.Decision),
            VoTargetDisposition = ToDispositionName(result.Case.TargetDisposition),
            VoVersion = result.Case.Version,
            VoUserActionId = result.UserAction?.Id,
            VoUserStateVersion = result.UserState?.Version,
            VoIsIdempotentReplay = result.IsIdempotentReplay
        };
    }

    private static ContentModerationCaseQueueItemVo MapCaseQueueItem(
        ContentModerationCase moderationCase,
        int reportCount)
    {
        return new ContentModerationCaseQueueItemVo
        {
            VoCasePublicId = moderationCase.PublicId,
            VoTargetType = ToReportTargetTypeName(moderationCase.TargetType),
            VoTargetContentId = moderationCase.TargetContentId,
            VoTargetUserId = moderationCase.TargetUserId,
            VoStatus = ToCaseStatusName(moderationCase.Status),
            VoDecision = ToDecisionName(moderationCase.Decision),
            VoTargetDisposition = ToDispositionName(moderationCase.TargetDisposition),
            VoVersion = moderationCase.Version,
            VoReportCount = reportCount,
            VoOpenedAt = moderationCase.OpenedAt,
            VoModifiedAt = moderationCase.ModifyTime
        };
    }

    private static ContentModerationCaseDetailVo MapCaseDetail(ContentModerationCaseAggregate aggregate)
    {
        var nowUtc = DateTime.UtcNow;
        return new ContentModerationCaseDetailVo
        {
            VoCase = MapCaseQueueItem(aggregate.Case, aggregate.Reports.Count),
            VoReports = aggregate.Reports.Select(report => new ContentModerationCaseReportVo
            {
                VoReportPublicId = report.PublicId ?? string.Empty,
                VoReporterUserId = report.ReporterUserId,
                VoReporterUserName = report.ReporterUserName,
                VoReasonType = report.ReasonType,
                VoReasonDetail = report.ReasonDetail,
                VoCreateTime = report.CreateTime
            }).ToList(),
            VoEvidence = aggregate.Evidence.Select(item => new ContentModerationEvidenceVo
            {
                VoSequence = item.EvidenceSequence,
                VoEvidenceType = ((ContentModerationEvidenceType)item.EvidenceType).ToString(),
                VoTargetState = ((ContentModerationTargetState)item.TargetState).ToString(),
                VoSnapshotTitle = item.SnapshotTitle,
                VoSnapshotSummary = item.SnapshotSummary,
                VoSnapshotHash = item.SnapshotHash,
                VoCapturedAt = item.CapturedAt
            }).ToList(),
            VoEvents = aggregate.Events.Select(item => new ContentModerationCaseEventVo
            {
                VoSequence = item.EventSequence,
                VoEventType = item.EventType,
                VoExpectedCaseVersion = item.ExpectedCaseVersion,
                VoResultCaseVersion = item.ResultCaseVersion,
                VoResultCode = item.ResultCode,
                VoRemark = item.Remark,
                VoActorUserId = item.ActorUserId,
                VoActorName = item.ActorName,
                VoCreateTime = item.CreateTime
            }).ToList(),
            VoUserStates = aggregate.UserStates.Select(item => new UserModerationStateVo
            {
                VoPolicyType = ((UserModerationPolicyType)item.PolicyType).ToString(),
                VoState = ((UserModerationStateValue)item.State).ToString(),
                VoIsEffective = item.State == (int)UserModerationStateValue.Active &&
                                (!item.EffectiveUntil.HasValue || item.EffectiveUntil > nowUtc),
                VoEffectiveUntil = item.EffectiveUntil,
                VoVersion = item.Version
            }).ToList(),
            VoPublicResultCode = aggregate.Case.PublicResultCode,
            VoInternalRemark = aggregate.Case.InternalRemark,
            VoResolvedAt = aggregate.Case.ResolvedAt
        };
    }

    private static void ValidateDecision(ReviewContentModerationCaseDto dto)
    {
        if (!Enum.IsDefined((ContentModerationDecision)dto.Decision) ||
            dto.Decision == (int)ContentModerationDecision.None)
        {
            throw new ArgumentException("案件决定无效");
        }

        if (dto.TargetDisposition is not
            (int)ContentModerationTargetDisposition.Keep and not
            (int)ContentModerationTargetDisposition.Restricted and not
            (int)ContentModerationTargetDisposition.Unavailable)
        {
            throw new ArgumentException("目标处置无效");
        }

        if (dto.Decision == (int)ContentModerationDecision.NoViolation &&
            (dto.TargetDisposition != (int)ContentModerationTargetDisposition.Keep || dto.UserAction != null))
        {
            throw new ArgumentException("未发现违规只能保留目标且不能附带用户动作");
        }

        if (dto.Decision == (int)ContentModerationDecision.InsufficientEvidence &&
            (dto.TargetDisposition == (int)ContentModerationTargetDisposition.Restricted || dto.UserAction != null))
        {
            throw new ArgumentException("证据不足不能限制目标或用户");
        }

        if (dto.Decision == (int)ContentModerationDecision.Violation &&
            dto.TargetDisposition == (int)ContentModerationTargetDisposition.Keep && dto.UserAction == null)
        {
            throw new ArgumentException("确认违规时必须明确至少一项处置");
        }
    }

    private static void ValidateUserAction(ContentModerationCaseUserActionDto action)
    {
        if (!Enum.IsDefined((ModerationActionTypeEnum)action.ActionType) ||
            action.ActionType == (int)ModerationActionTypeEnum.None)
        {
            throw new ArgumentException("用户治理动作无效");
        }

        var activating = action.ActionType is
            (int)ModerationActionTypeEnum.Mute or (int)ModerationActionTypeEnum.Ban;
        if (!activating && action.DurationHours.HasValue)
        {
            throw new ArgumentException("解除动作不能设置持续时间");
        }
    }

    private static int? NormalizeCaseStatus(int? status)
    {
        if (!status.HasValue)
        {
            return null;
        }

        return Enum.IsDefined((ContentModerationCaseStatus)status.Value)
            ? status.Value
            : throw new ArgumentException("案件状态无效");
    }

    private static string ComputeSnapshotHash(
        int targetType,
        long targetContentId,
        long targetUserId,
        string? title,
        string? summary)
    {
        var canonical = string.Join('\n', targetType, targetContentId, targetUserId, title ?? string.Empty, summary ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string NormalizeActorName(string? actorName, long actorUserId) =>
        string.IsNullOrWhiteSpace(actorName) ? $"User-{actorUserId}" : actorName.Trim();

    private static string? NormalizeEvidenceText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = MultiWhitespacePattern.Replace(value.Trim(), " ");
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string ToCaseStatusName(int value) => ((ContentModerationCaseStatus)value).ToString();
    private static string ToDecisionName(int value) => ((ContentModerationDecision)value).ToString();
    private static string ToDispositionName(int value) => ((ContentModerationTargetDisposition)value).ToString();

    private static BusinessException CaseNotFound() => new(
        "治理案件不存在",
        404,
        "Moderation.CaseNotFound",
        "error.moderation.case_not_found");

    private static BusinessException CaseVersionConflict() => new(
        "案件已被其他治理人员更新，请刷新后重试",
        409,
        "Moderation.CaseVersionConflict",
        "error.moderation.case_version_conflict");
}
