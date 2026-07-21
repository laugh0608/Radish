using AutoMapper;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Repository.UnitOfWorks;
using Radish.Service.Base;
using Radish.Shared.CustomEnum;
using SqlSugar;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace Radish.Service;

/// <summary>内容治理服务实现</summary>
public partial class ContentModerationService : BaseService<ContentReport, ContentReportVo>, IContentModerationService
{
    private static readonly Regex MarkdownImagePattern = new(@"!\[[^\]]*\]\(([^)]+)\)", RegexOptions.Compiled);
    private static readonly Regex MarkdownLinkPattern = new(@"\[(?<name>[^\]]+)\]\(([^)\s]+)\)", RegexOptions.Compiled);
    private static readonly Regex AttachmentUriPattern = new(@"attachment://\S+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex StickerUriPattern = new(@"sticker://\S+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MultiWhitespacePattern = new(@"\s+", RegexOptions.Compiled);

    private readonly IBaseRepository<ContentReport> _contentReportRepository;
    private readonly IBaseRepository<UserModerationAction> _moderationActionRepository;
    private readonly IBaseRepository<Post> _postRepository;
    private readonly IBaseRepository<Comment> _commentRepository;
    private readonly IChannelMessageRepository _channelMessageRepository;
    private readonly IBaseRepository<Product> _productRepository;
    private readonly IBaseRepository<PostQuickReply> _postQuickReplyRepository;
    private readonly IBaseRepository<User> _userRepository;
    private readonly IUnitOfWorkManage? _unitOfWorkManage;
    private readonly IContentModerationCaseRepository? _moderationCaseRepository;

    public ContentModerationService(
        IMapper mapper,
        IBaseRepository<ContentReport> baseRepository,
        IBaseRepository<UserModerationAction> moderationActionRepository,
        IBaseRepository<Post> postRepository,
        IBaseRepository<Comment> commentRepository,
        IChannelMessageRepository channelMessageRepository,
        IBaseRepository<Product> productRepository,
        IBaseRepository<PostQuickReply> postQuickReplyRepository,
        IBaseRepository<User> userRepository,
        IUnitOfWorkManage? unitOfWorkManage = null,
        IContentModerationCaseRepository? moderationCaseRepository = null)
        : base(mapper, baseRepository)
    {
        _contentReportRepository = baseRepository;
        _moderationActionRepository = moderationActionRepository;
        _postRepository = postRepository;
        _commentRepository = commentRepository;
        _channelMessageRepository = channelMessageRepository;
        _productRepository = productRepository;
        _postQuickReplyRepository = postQuickReplyRepository;
        _userRepository = userRepository;
        _unitOfWorkManage = unitOfWorkManage;
        _moderationCaseRepository = moderationCaseRepository;
    }

    public async Task<long> SubmitReportAsync(SubmitContentReportDto dto, long reporterUserId, string reporterUserName, long tenantId)
    {
        if (dto.TargetContentId <= 0)
        {
            throw new ArgumentException("举报目标 ID 无效");
        }

        if (reporterUserId <= 0)
        {
            throw new ArgumentException("举报人 ID 无效");
        }

        var targetType = ParseTargetType(dto.TargetType);
        var targetSnapshot = await ResolveReportTargetSnapshotAsync(targetType, dto.TargetContentId);
        if (targetSnapshot.TargetUserId < 0)
        {
            throw new BusinessException("举报目标用户不存在", 404, "Moderation.TargetUserNotFound", "error.moderation.target_user_not_found");
        }

        if (targetSnapshot.TargetUserId > 0 && targetSnapshot.TargetUserId == reporterUserId)
        {
            throw new ArgumentException("不能举报自己的内容");
        }

        var pendingExists = await _contentReportRepository.QueryExistsAsync(r =>
            r.ReportTargetType == (int)targetType &&
            r.TargetContentId == dto.TargetContentId &&
            r.ReporterUserId == reporterUserId &&
            r.Status == (int)ContentReportStatusEnum.Pending &&
            !r.IsDeleted);
        if (pendingExists)
        {
            throw new BusinessException("该内容已存在待处理举报，请勿重复提交", 409, "Moderation.ReportAlreadyPending", "error.moderation.report_already_pending");
        }

        var normalizedReporterName = string.IsNullOrWhiteSpace(reporterUserName) ? $"User-{reporterUserId}" : reporterUserName.Trim();
        var normalizedTenantId = tenantId > 0 ? tenantId : 0;
        var normalizedReasonType = string.IsNullOrWhiteSpace(dto.ReasonType) ? "Other" : dto.ReasonType.Trim();

        var reportId = await _contentReportRepository.AddAsync(new ContentReport
        {
            ReportTargetType = (int)targetType,
            TargetContentId = dto.TargetContentId,
            TargetSnapshotPostId = targetSnapshot.TargetPostId,
            TargetSnapshotChannelId = targetSnapshot.TargetChannelId,
            TargetSnapshotTitle = targetSnapshot.SnapshotTitle,
            TargetSnapshotSummary = targetSnapshot.SnapshotSummary,
            TargetUserId = targetSnapshot.TargetUserId,
            TargetUserName = targetSnapshot.TargetUserName,
            ReporterUserId = reporterUserId,
            ReporterUserName = normalizedReporterName,
            ReasonType = normalizedReasonType,
            ReasonDetail = string.IsNullOrWhiteSpace(dto.ReasonDetail) ? null : dto.ReasonDetail.Trim(),
            Status = (int)ContentReportStatusEnum.Pending,
            ReviewActionType = (int)ModerationActionTypeEnum.None,
            TenantId = normalizedTenantId,
            CreateTime = DateTime.UtcNow,
            CreateBy = normalizedReporterName,
            CreateId = reporterUserId
        });

        return reportId;
    }

    public async Task<VoPagedResult<ContentReportQueueItemVo>> GetReportQueueAsync(ContentReportQueueQueryDto query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var safePageIndex = NormalizePageIndex(query.PageIndex);
        var safePageSize = NormalizePageSize(query.PageSize);
        var normalizedStatus = NormalizeQueueStatusFilter(query.Status);
        var normalizedTargetType = NormalizeQueueTargetTypeFilter(query.TargetType);
        var normalizedReasonType = NormalizeOptionalFilter(query.ReasonType);
        var normalizedNavigationStatus = NormalizeQueueNavigationStatusFilter(query.NavigationStatus);
        var normalizedKeyword = NormalizeQueueKeyword(query.Keyword);
        var hasKeyword = !string.IsNullOrWhiteSpace(normalizedKeyword);
        var hasKeywordLongId = long.TryParse(normalizedKeyword, out var keywordLongId) && keywordLongId > 0;

        Expression<Func<ContentReport, bool>> whereExpression = report => !report.IsDeleted;

        if (normalizedStatus.HasValue)
        {
            var status = normalizedStatus.Value;
            whereExpression = AndWhere(whereExpression, report => report.Status == status);
        }

        if (normalizedTargetType.HasValue)
        {
            var targetType = normalizedTargetType.Value;
            whereExpression = AndWhere(whereExpression, report => report.ReportTargetType == targetType);
        }

        if (normalizedReasonType != null)
        {
            whereExpression = AndWhere(whereExpression, report => report.ReasonType == normalizedReasonType);
        }

        if (hasKeyword)
        {
            var keyword = normalizedKeyword!;
            if (hasKeywordLongId)
            {
                whereExpression = AndWhere(whereExpression, report =>
                    report.Id == keywordLongId ||
                    report.TargetContentId == keywordLongId ||
                    report.TargetUserId == keywordLongId ||
                    report.ReporterUserId == keywordLongId ||
                    (report.TargetSnapshotTitle != null && report.TargetSnapshotTitle.Contains(keyword)) ||
                    (report.TargetSnapshotSummary != null && report.TargetSnapshotSummary.Contains(keyword)) ||
                    (report.TargetUserName != null && report.TargetUserName.Contains(keyword)) ||
                    report.ReporterUserName.Contains(keyword) ||
                    report.ReasonType.Contains(keyword) ||
                    (report.ReasonDetail != null && report.ReasonDetail.Contains(keyword)));
            }
            else
            {
                whereExpression = AndWhere(whereExpression, report =>
                    (report.TargetSnapshotTitle != null && report.TargetSnapshotTitle.Contains(keyword)) ||
                    (report.TargetSnapshotSummary != null && report.TargetSnapshotSummary.Contains(keyword)) ||
                    (report.TargetUserName != null && report.TargetUserName.Contains(keyword)) ||
                    report.ReporterUserName.Contains(keyword) ||
                    report.ReasonType.Contains(keyword) ||
                    (report.ReasonDetail != null && report.ReasonDetail.Contains(keyword)));
            }
        }

        if (normalizedNavigationStatus == null)
        {
            var (reports, totalCount) = await _contentReportRepository.QueryPageAsync(
                whereExpression,
                safePageIndex,
                safePageSize,
                r => r.CreateTime,
                OrderByType.Desc);

            return new VoPagedResult<ContentReportQueueItemVo>
            {
                VoItems = await BuildReportQueueItemsAsync(reports),
                VoTotal = totalCount,
                VoPageIndex = safePageIndex,
                VoPageSize = safePageSize
            };
        }

        var reportsForNavigationFiltering = await _contentReportRepository.QueryAsync(whereExpression);
        var orderedReports = reportsForNavigationFiltering
            .OrderByDescending(report => report.CreateTime)
            .ThenByDescending(report => report.Id)
            .ToList();
        var filteredItems = (await BuildReportQueueItemsAsync(orderedReports))
            .Where(item => item.VoTargetNavigationStatus == normalizedNavigationStatus)
            .ToList();

        return new VoPagedResult<ContentReportQueueItemVo>
        {
            VoItems = filteredItems
                .Skip((safePageIndex - 1) * safePageSize)
                .Take(safePageSize)
                .ToList(),
            VoTotal = filteredItems.Count,
            VoPageIndex = safePageIndex,
            VoPageSize = safePageSize
        };
    }

    public async Task<ContentReportQueueItemVo> ReviewReportAsync(
        ReviewContentReportDto dto,
        long reviewerUserId,
        string reviewerUserName,
        long tenantId)
    {
        if (dto.ReportId <= 0)
        {
            throw new ArgumentException("举报单 ID 无效");
        }

        if (reviewerUserId <= 0)
        {
            throw new ArgumentException("审核人 ID 无效");
        }

        var actionType = ParseReviewActionType(dto.ActionType);
        if (!dto.IsApproved)
        {
            actionType = ModerationActionTypeEnum.None;
        }

        if (dto.IsApproved && actionType == ModerationActionTypeEnum.Mute && (!dto.DurationHours.HasValue || dto.DurationHours.Value <= 0))
        {
            throw new ArgumentException("禁言动作必须指定有效时长（小时）");
        }

        if (dto.IsApproved && actionType == ModerationActionTypeEnum.Ban && dto.DurationHours.HasValue && dto.DurationHours.Value <= 0)
        {
            throw new ArgumentException("封禁时长必须大于 0（为空表示永久封禁）");
        }

        var report = await _contentReportRepository.QueryFirstAsync(r => r.Id == dto.ReportId && !r.IsDeleted);
        if (report == null)
        {
            throw new BusinessException("举报单不存在", 404, "Moderation.ReportNotFound", "error.moderation.report_not_found");
        }

        if (report.Status != (int)ContentReportStatusEnum.Pending)
        {
            throw new BusinessException("该举报单已处理，请勿重复审核", 409, "Moderation.ReportAlreadyReviewed", "error.moderation.report_already_reviewed");
        }

        if (dto.IsApproved &&
            actionType is ModerationActionTypeEnum.Mute or ModerationActionTypeEnum.Ban &&
            report.TargetUserId <= 0)
        {
            throw new BusinessException("当前目标不支持联动用户治理动作", 400, "Moderation.TargetActionUnsupported", "error.moderation.target_action_unsupported");
        }

        var normalizedReviewerName = string.IsNullOrWhiteSpace(reviewerUserName) ? $"User-{reviewerUserId}" : reviewerUserName.Trim();
        var now = DateTime.UtcNow;

        report.Status = dto.IsApproved ? (int)ContentReportStatusEnum.Approved : (int)ContentReportStatusEnum.Rejected;
        report.ReviewActionType = dto.IsApproved ? (int)actionType : (int)ModerationActionTypeEnum.None;
        report.ReviewDurationHours = dto.IsApproved ? dto.DurationHours : null;
        report.ReviewRemark = string.IsNullOrWhiteSpace(dto.ReviewRemark) ? null : dto.ReviewRemark.Trim();
        report.ReviewedAt = now;
        report.ReviewedById = reviewerUserId;
        report.ReviewedByName = normalizedReviewerName;
        report.ModifyTime = now;
        report.ModifyBy = normalizedReviewerName;
        report.ModifyId = reviewerUserId;

        await ExecuteModerationUnitOfWorkAsync(async () =>
        {
            var affected = await _contentReportRepository.UpdateColumnsAsync(
                r => new ContentReport
                {
                    Status = report.Status,
                    ReviewActionType = report.ReviewActionType,
                    ReviewDurationHours = report.ReviewDurationHours,
                    ReviewRemark = report.ReviewRemark,
                    ReviewedAt = report.ReviewedAt,
                    ReviewedById = report.ReviewedById,
                    ReviewedByName = report.ReviewedByName,
                    ModifyTime = report.ModifyTime,
                    ModifyBy = report.ModifyBy,
                    ModifyId = report.ModifyId
                },
                r => r.Id == report.Id &&
                    r.TenantId == report.TenantId &&
                    r.Status == (int)ContentReportStatusEnum.Pending &&
                    !r.IsDeleted);
            if (affected <= 0)
            {
                throw new BusinessException("举报单已被处理，请刷新审核队列", 409, "Moderation.ConcurrentReviewConflict", "error.moderation.concurrent_review_conflict");
            }

            if (dto.IsApproved && actionType is ModerationActionTypeEnum.Mute or ModerationActionTypeEnum.Ban)
            {
                var actionReason = BuildReviewActionReason(report);
                if (_moderationCaseRepository == null)
                {
                    await ExecuteActionAsync(
                        report.TargetUserId,
                        report.TargetUserName,
                        actionType,
                        dto.DurationHours,
                        actionReason,
                        report.Id,
                        reviewerUserId,
                        normalizedReviewerName,
                        tenantId > 0 ? tenantId : report.TenantId);
                }
                else
                {
                    await _moderationCaseRepository.ApplyStandaloneUserActionAsync(
                        new ContentModerationStandaloneUserActionCommand(
                            tenantId > 0 ? tenantId : report.TenantId,
                            report.TargetUserId,
                            report.TargetUserName,
                            (int)actionType,
                            dto.DurationHours,
                            actionReason,
                            report.Id,
                            $"legacy-review:{report.TenantId}:{report.Id}:{(int)actionType}",
                            reviewerUserId,
                            normalizedReviewerName,
                            now));
                }
            }
        });

        return await BuildReportQueueItemAsync(report);
    }

    private async Task ExecuteModerationUnitOfWorkAsync(Func<Task> action)
    {
        if (_unitOfWorkManage == null)
        {
            await action();
            return;
        }

        using var unitOfWork = _unitOfWorkManage.CreateUnitOfWork();
        await action();
        unitOfWork.Commit();
    }

    public async Task<UserModerationActionVo> ApplyUserActionAsync(
        ApplyUserModerationActionDto dto,
        long operatorUserId,
        string operatorUserName,
        long tenantId)
    {
        if (dto.TargetUserId <= 0)
        {
            throw new ArgumentException("目标用户 ID 无效");
        }

        if (operatorUserId <= 0)
        {
            throw new ArgumentException("操作者 ID 无效");
        }

        var actionType = ParseManualActionType(dto.ActionType);
        var normalizedOperatorName = string.IsNullOrWhiteSpace(operatorUserName) ? $"User-{operatorUserId}" : operatorUserName.Trim();
        var targetUser = await _userRepository.QueryFirstAsync(u => u.Id == dto.TargetUserId && !u.IsDeleted);
        if (targetUser == null)
        {
            throw new BusinessException("目标用户不存在", 404, "Moderation.TargetUserNotFound", "error.moderation.target_user_not_found");
        }

        if (targetUser.Id == operatorUserId)
        {
            throw new BusinessException(
                "不能对自己执行治理动作",
                403,
                "Moderation.SelfActionForbidden",
                "error.moderation.self_action_forbidden");
        }

        if (targetUser.RoleNames.Any(role =>
                role.Equals("System", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException(
                "受保护账号不能通过内容治理直接处置",
                403,
                "Moderation.ProtectedAccount",
                "error.moderation.protected_account");
        }

        var targetUserName = string.IsNullOrWhiteSpace(targetUser.UserName) ? $"User-{targetUser.Id}" : targetUser.UserName.Trim();
        var normalizedTenantId = tenantId > 0 ? tenantId : targetUser.TenantId;

        if (dto.SourceReportId.HasValue)
        {
            var sourceReport = await _contentReportRepository.QueryFirstAsync(report =>
                report.Id == dto.SourceReportId.Value && !report.IsDeleted);
            if (sourceReport == null)
            {
                throw new BusinessException(
                    "来源举报单不存在",
                    404,
                    "Moderation.ReportNotFound",
                    "error.moderation.report_not_found");
            }

            if (sourceReport.TenantId != normalizedTenantId || sourceReport.TargetUserId != targetUser.Id)
            {
                throw new BusinessException(
                    "来源举报单与当前治理目标不匹配",
                    409,
                    "Moderation.SourceReportMismatch",
                    "error.moderation.source_report_mismatch");
            }
        }

        if (actionType == ModerationActionTypeEnum.Mute && (!dto.DurationHours.HasValue || dto.DurationHours.Value <= 0))
        {
            throw new ArgumentException("禁言动作必须指定有效时长（小时）");
        }

        if (actionType == ModerationActionTypeEnum.Ban && dto.DurationHours.HasValue && dto.DurationHours.Value <= 0)
        {
            throw new ArgumentException("封禁时长必须大于 0（为空表示永久封禁）");
        }

        var actionReason = string.IsNullOrWhiteSpace(dto.Reason) ? "管理员手动操作" : dto.Reason.Trim();

        if (_moderationCaseRepository != null)
        {
            var result = await _moderationCaseRepository.ApplyStandaloneUserActionAsync(
                new ContentModerationStandaloneUserActionCommand(
                    normalizedTenantId,
                    targetUser.Id,
                    targetUserName,
                    (int)actionType,
                    dto.DurationHours,
                    actionReason,
                    dto.SourceReportId,
                    $"legacy-manual:{Guid.NewGuid():N}",
                    operatorUserId,
                    normalizedOperatorName,
                    DateTime.UtcNow));
            return MapAction(result.Action);
        }

        return actionType switch
        {
            ModerationActionTypeEnum.Mute or ModerationActionTypeEnum.Ban => await ExecuteActionAsync(
                targetUser.Id,
                targetUserName,
                actionType,
                dto.DurationHours,
                actionReason,
                dto.SourceReportId,
                operatorUserId,
                normalizedOperatorName,
                normalizedTenantId),
            ModerationActionTypeEnum.Unmute => await ExecuteCancelActionAsync(
                targetUser.Id,
                targetUserName,
                ModerationActionTypeEnum.Mute,
                ModerationActionTypeEnum.Unmute,
                actionReason,
                dto.SourceReportId,
                operatorUserId,
                normalizedOperatorName,
                normalizedTenantId),
            ModerationActionTypeEnum.Unban => await ExecuteCancelActionAsync(
                targetUser.Id,
                targetUserName,
                ModerationActionTypeEnum.Ban,
                ModerationActionTypeEnum.Unban,
                actionReason,
                dto.SourceReportId,
                operatorUserId,
                normalizedOperatorName,
                normalizedTenantId),
            _ => throw new ArgumentException("不支持的治理动作")
        };
    }

    public async Task<UserModerationStatusVo> GetUserModerationStatusAsync(long userId)
    {
        if (userId <= 0)
        {
            return new UserModerationStatusVo { VoUserId = userId };
        }

        if (_moderationCaseRepository != null)
        {
            var states = await _moderationCaseRepository.QueryUserStatesAsync(-1, userId);
            var nowUtc = DateTime.UtcNow;
            var mute = states.FirstOrDefault(item => item.PolicyType == (int)UserModerationPolicyType.Mute);
            var ban = states.FirstOrDefault(item => item.PolicyType == (int)UserModerationPolicyType.Ban);
            var muteEffective = IsStateEffective(mute, nowUtc);
            var banEffective = IsStateEffective(ban, nowUtc);
            return new UserModerationStatusVo
            {
                VoUserId = userId,
                VoIsMuted = muteEffective,
                VoMutedUntil = muteEffective ? mute?.EffectiveUntil : null,
                VoIsBanned = banEffective,
                VoBannedUntil = banEffective ? ban?.EffectiveUntil : null
            };
        }

        var actions = await _moderationActionRepository.QueryAsync(a =>
            a.TargetUserId == userId &&
            a.IsActive &&
            !a.IsDeleted &&
            (a.ActionType == (int)ModerationActionTypeEnum.Mute || a.ActionType == (int)ModerationActionTypeEnum.Ban));

        if (actions.Count == 0)
        {
            return new UserModerationStatusVo { VoUserId = userId };
        }

        var now = DateTime.UtcNow;
        var expiredActions = actions.Where(IsExpired).ToList();
        if (expiredActions.Count > 0)
        {
            foreach (var action in expiredActions)
            {
                action.IsActive = false;
                action.ModifyTime = now;
                action.ModifyBy = "System";
                action.ModifyId = 0;
            }

            await _moderationActionRepository.UpdateRangeAsync(expiredActions);
        }

        var activeActions = actions.Where(a => !IsExpired(a)).ToList();
        if (activeActions.Count == 0)
        {
            return new UserModerationStatusVo { VoUserId = userId };
        }

        var activeMutes = activeActions
            .Where(a => a.ActionType == (int)ModerationActionTypeEnum.Mute)
            .ToList();
        var activeBans = activeActions
            .Where(a => a.ActionType == (int)ModerationActionTypeEnum.Ban)
            .ToList();

        return new UserModerationStatusVo
        {
            VoUserId = userId,
            VoIsMuted = activeMutes.Count > 0,
            VoMutedUntil = ResolveUntil(activeMutes),
            VoIsBanned = activeBans.Count > 0,
            VoBannedUntil = ResolveUntil(activeBans)
        };
    }

    private static bool IsStateEffective(UserModerationState? state, DateTime nowUtc)
    {
        return state is
        {
            State: (int)UserModerationStateValue.Active
        } && (!state.EffectiveUntil.HasValue || state.EffectiveUntil.Value > nowUtc);
    }

    public async Task<ContentModerationPermissionVo> GetPublishPermissionAsync(long userId)
    {
        var status = await GetUserModerationStatusAsync(userId);
        if (status.VoIsBanned)
        {
            return new ContentModerationPermissionVo
            {
                VoUserId = userId,
                VoCanPublish = false,
                VoIsMuted = status.VoIsMuted,
                VoMutedUntil = status.VoMutedUntil,
                VoIsBanned = true,
                VoBannedUntil = status.VoBannedUntil,
                VoDenyReason = status.VoBannedUntil.HasValue
                    ? $"账号已被封禁至 {status.VoBannedUntil.Value:yyyy-MM-dd HH:mm:ss}，暂无法发布内容"
                    : "账号已被永久封禁，暂无法发布内容"
            };
        }

        if (status.VoIsMuted)
        {
            return new ContentModerationPermissionVo
            {
                VoUserId = userId,
                VoCanPublish = false,
                VoIsMuted = true,
                VoMutedUntil = status.VoMutedUntil,
                VoIsBanned = false,
                VoBannedUntil = null,
                VoDenyReason = status.VoMutedUntil.HasValue
                    ? $"账号已被禁言至 {status.VoMutedUntil.Value:yyyy-MM-dd HH:mm:ss}，暂无法发布内容"
                    : "账号已被永久禁言，暂无法发布内容"
            };
        }

        return new ContentModerationPermissionVo
        {
            VoUserId = userId,
            VoCanPublish = true,
            VoIsMuted = false,
            VoIsBanned = false
        };
    }

    public async Task<VoPagedResult<UserModerationActionVo>> GetActionLogsAsync(ContentModerationActionLogQueryDto query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var safePageIndex = NormalizePageIndex(query.PageIndex);
        var safePageSize = NormalizePageSize(query.PageSize);
        var normalizedTargetUserId = query.TargetUserId.HasValue && query.TargetUserId.Value > 0
            ? query.TargetUserId.Value
            : (long?)null;
        var normalizedSourceReportId = query.SourceReportId.HasValue && query.SourceReportId.Value > 0
            ? query.SourceReportId.Value
            : (long?)null;
        var normalizedActionType = NormalizeActionLogActionTypeFilter(query.ActionType);
        var normalizedKeyword = NormalizeActionLogKeyword(query.Keyword);
        var hasKeyword = !string.IsNullOrWhiteSpace(normalizedKeyword);
        var hasKeywordLongId = long.TryParse(normalizedKeyword, out var keywordLongId) && keywordLongId > 0;

        Expression<Func<UserModerationAction, bool>> whereExpression = action => !action.IsDeleted;

        if (normalizedTargetUserId.HasValue)
        {
            var targetUserId = normalizedTargetUserId.Value;
            whereExpression = AndWhere(whereExpression, action => action.TargetUserId == targetUserId);
        }

        if (normalizedSourceReportId.HasValue)
        {
            var sourceReportId = normalizedSourceReportId.Value;
            whereExpression = AndWhere(whereExpression, action => action.SourceReportId == sourceReportId);
        }

        if (normalizedActionType.HasValue)
        {
            var actionType = normalizedActionType.Value;
            whereExpression = AndWhere(whereExpression, action => action.ActionType == actionType);
        }

        if (query.IsActive.HasValue)
        {
            var isActive = query.IsActive.Value;
            whereExpression = AndWhere(whereExpression, action => action.IsActive == isActive);
        }

        if (hasKeyword)
        {
            var keyword = normalizedKeyword!;
            if (hasKeywordLongId)
            {
                whereExpression = AndWhere(whereExpression, action =>
                    action.Id == keywordLongId ||
                    action.TargetUserId == keywordLongId ||
                    action.SourceReportId == keywordLongId ||
                    (action.TargetUserName != null && action.TargetUserName.Contains(keyword)) ||
                    action.Reason.Contains(keyword) ||
                    action.CreateBy.Contains(keyword));
            }
            else
            {
                whereExpression = AndWhere(whereExpression, action =>
                    (action.TargetUserName != null && action.TargetUserName.Contains(keyword)) ||
                    action.Reason.Contains(keyword) ||
                    action.CreateBy.Contains(keyword));
            }
        }

        var (actions, totalCount) = await _moderationActionRepository.QueryPageAsync(
            whereExpression,
            safePageIndex,
            safePageSize,
            a => a.CreateTime,
            OrderByType.Desc);

        return new VoPagedResult<UserModerationActionVo>
        {
            VoItems = await BuildActionLogItemsAsync(actions),
            VoTotal = totalCount,
            VoPageIndex = safePageIndex,
            VoPageSize = safePageSize
        };
    }

    private async Task<UserModerationActionVo> ExecuteActionAsync(
        long targetUserId,
        string? targetUserName,
        ModerationActionTypeEnum actionType,
        int? durationHours,
        string reason,
        long? sourceReportId,
        long operatorUserId,
        string operatorUserName,
        long tenantId)
    {
        if (actionType is not (ModerationActionTypeEnum.Mute or ModerationActionTypeEnum.Ban))
        {
            throw new ArgumentException("仅支持禁言或封禁动作");
        }

        await DeactivateActiveActionsAsync(targetUserId, actionType, operatorUserId, operatorUserName);
        if (actionType == ModerationActionTypeEnum.Ban)
        {
            await DeactivateActiveActionsAsync(targetUserId, ModerationActionTypeEnum.Mute, operatorUserId, operatorUserName);
        }

        var now = DateTime.UtcNow;
        var action = new UserModerationAction
        {
            TargetUserId = targetUserId,
            TargetUserName = string.IsNullOrWhiteSpace(targetUserName) ? $"User-{targetUserId}" : targetUserName.Trim(),
            ActionType = (int)actionType,
            Reason = reason,
            SourceReportId = sourceReportId,
            DurationHours = durationHours,
            StartTime = now,
            EndTime = durationHours.HasValue ? now.AddHours(durationHours.Value) : null,
            IsActive = true,
            TenantId = tenantId > 0 ? tenantId : 0,
            CreateTime = now,
            CreateBy = operatorUserName,
            CreateId = operatorUserId
        };

        var actionId = await _moderationActionRepository.AddAsync(action);
        action.Id = actionId;

        return MapAction(action);
    }

    private async Task<UserModerationActionVo> ExecuteCancelActionAsync(
        long targetUserId,
        string? targetUserName,
        ModerationActionTypeEnum deactivatedType,
        ModerationActionTypeEnum cancelActionType,
        string reason,
        long? sourceReportId,
        long operatorUserId,
        string operatorUserName,
        long tenantId)
    {
        await DeactivateActiveActionsAsync(targetUserId, deactivatedType, operatorUserId, operatorUserName);

        var now = DateTime.UtcNow;
        var action = new UserModerationAction
        {
            TargetUserId = targetUserId,
            TargetUserName = string.IsNullOrWhiteSpace(targetUserName) ? $"User-{targetUserId}" : targetUserName.Trim(),
            ActionType = (int)cancelActionType,
            Reason = reason,
            SourceReportId = sourceReportId,
            DurationHours = null,
            StartTime = now,
            EndTime = now,
            IsActive = false,
            TenantId = tenantId > 0 ? tenantId : 0,
            CreateTime = now,
            CreateBy = operatorUserName,
            CreateId = operatorUserId
        };

        var actionId = await _moderationActionRepository.AddAsync(action);
        action.Id = actionId;
        return MapAction(action);
    }

    private async Task DeactivateActiveActionsAsync(
        long targetUserId,
        ModerationActionTypeEnum actionType,
        long operatorUserId,
        string operatorUserName)
    {
        var activeActions = await _moderationActionRepository.QueryAsync(a =>
            a.TargetUserId == targetUserId &&
            a.ActionType == (int)actionType &&
            a.IsActive &&
            !a.IsDeleted);

        if (activeActions.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var action in activeActions)
        {
            action.IsActive = false;
            if (!action.EndTime.HasValue || action.EndTime.Value > now)
            {
                action.EndTime = now;
            }
            action.ModifyTime = now;
            action.ModifyBy = operatorUserName;
            action.ModifyId = operatorUserId;
        }

        await _moderationActionRepository.UpdateRangeAsync(activeActions);
    }


    private static ContentReportTargetTypeEnum ParseTargetType(string? targetType)
    {
        if (string.IsNullOrWhiteSpace(targetType))
        {
            throw new ArgumentException("举报目标类型不能为空");
        }

        return targetType.Trim().ToLowerInvariant() switch
        {
            "post" => ContentReportTargetTypeEnum.Post,
            "comment" => ContentReportTargetTypeEnum.Comment,
            "chatmessage" => ContentReportTargetTypeEnum.ChatMessage,
            "product" => ContentReportTargetTypeEnum.Product,
            "postquickreply" => ContentReportTargetTypeEnum.PostQuickReply,
            _ => throw new ArgumentException("举报目标类型仅支持 Post、Comment、ChatMessage、Product 或 PostQuickReply")
        };
    }

    private static int? NormalizeQueueStatusFilter(int? status)
    {
        if (!status.HasValue || status.Value < 0)
        {
            return null;
        }

        return status.Value switch
        {
            (int)ContentReportStatusEnum.Pending => (int)ContentReportStatusEnum.Pending,
            (int)ContentReportStatusEnum.Approved => (int)ContentReportStatusEnum.Approved,
            (int)ContentReportStatusEnum.Rejected => (int)ContentReportStatusEnum.Rejected,
            _ => throw new ArgumentException("审核状态仅支持 Pending、Approved 或 Rejected")
        };
    }

    private static int? NormalizeQueueTargetTypeFilter(string? targetType)
    {
        var normalizedTargetType = NormalizeOptionalFilter(targetType);
        return normalizedTargetType == null ? null : (int)ParseTargetType(normalizedTargetType);
    }

    private static string? NormalizeQueueNavigationStatusFilter(string? navigationStatus)
    {
        var normalizedNavigationStatus = NormalizeOptionalFilter(navigationStatus);
        if (normalizedNavigationStatus == null)
        {
            return null;
        }

        return normalizedNavigationStatus.Trim().ToLowerInvariant() switch
        {
            "ready" => TargetNavigationStatusReady,
            "fallback" => TargetNavigationStatusFallback,
            "unavailable" => TargetNavigationStatusUnavailable,
            "unsupported" => TargetNavigationStatusUnsupported,
            _ => throw new ArgumentException("目标回看状态仅支持 Ready、Fallback、Unavailable 或 Unsupported")
        };
    }

    private static string? NormalizeQueueKeyword(string? keyword)
    {
        var normalizedKeyword = NormalizeOptionalFilter(keyword);
        return string.IsNullOrWhiteSpace(normalizedKeyword)
            ? null
            : MultiWhitespacePattern.Replace(normalizedKeyword, " ").Trim();
    }

    private static string? NormalizeActionLogKeyword(string? keyword)
    {
        return NormalizeQueueKeyword(keyword);
    }

    private static Expression<Func<TEntity, bool>> AndWhere<TEntity>(
        Expression<Func<TEntity, bool>> current,
        Expression<Func<TEntity, bool>> next)
    {
        var parameter = current.Parameters[0];
        var nextBody = new ParameterReplaceVisitor(next.Parameters[0], parameter).Visit(next.Body)
            ?? throw new InvalidOperationException("查询条件表达式组合失败");

        return Expression.Lambda<Func<TEntity, bool>>(
            Expression.AndAlso(current.Body, nextBody),
            parameter);
    }

    private static string? NormalizeOptionalFilter(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int? NormalizeActionLogActionTypeFilter(string? actionType)
    {
        var normalizedActionType = NormalizeOptionalFilter(actionType);
        if (normalizedActionType == null)
        {
            return null;
        }

        return normalizedActionType.Trim().ToLowerInvariant() switch
        {
            "mute" => (int)ModerationActionTypeEnum.Mute,
            "ban" => (int)ModerationActionTypeEnum.Ban,
            "unmute" => (int)ModerationActionTypeEnum.Unmute,
            "unban" => (int)ModerationActionTypeEnum.Unban,
            _ => throw new ArgumentException("治理动作类型仅支持 Mute、Ban、Unmute 或 Unban")
        };
    }

    private static ModerationActionTypeEnum ParseReviewActionType(int actionType)
    {
        return actionType switch
        {
            (int)ModerationActionTypeEnum.None => ModerationActionTypeEnum.None,
            (int)ModerationActionTypeEnum.Mute => ModerationActionTypeEnum.Mute,
            (int)ModerationActionTypeEnum.Ban => ModerationActionTypeEnum.Ban,
            _ => throw new ArgumentException("审核动作仅支持 None、Mute 或 Ban")
        };
    }

    private static ModerationActionTypeEnum ParseManualActionType(int actionType)
    {
        return actionType switch
        {
            (int)ModerationActionTypeEnum.Mute => ModerationActionTypeEnum.Mute,
            (int)ModerationActionTypeEnum.Ban => ModerationActionTypeEnum.Ban,
            (int)ModerationActionTypeEnum.Unmute => ModerationActionTypeEnum.Unmute,
            (int)ModerationActionTypeEnum.Unban => ModerationActionTypeEnum.Unban,
            _ => throw new ArgumentException("手动治理动作仅支持 Mute、Ban、Unmute、Unban")
        };
    }

    private static string BuildReviewActionReason(ContentReport report)
    {
        var detail = string.IsNullOrWhiteSpace(report.ReasonDetail) ? string.Empty : $"（{report.ReasonDetail}）";
        return $"举报审核通过：{report.ReasonType}{detail}";
    }

    private static bool IsExpired(UserModerationAction action)
    {
        return action.EndTime.HasValue && action.EndTime.Value <= DateTime.UtcNow;
    }

    private static DateTime? ResolveUntil(List<UserModerationAction> actions)
    {
        if (actions.Count == 0)
        {
            return null;
        }

        var hasPermanent = actions.Any(action => !action.EndTime.HasValue);
        if (hasPermanent)
        {
            return null;
        }

        return actions.Max(action => action.EndTime);
    }

    private static string ToReportTargetTypeName(int value)
    {
        return value switch
        {
            (int)ContentReportTargetTypeEnum.Post => "Post",
            (int)ContentReportTargetTypeEnum.Comment => "Comment",
            (int)ContentReportTargetTypeEnum.ChatMessage => "ChatMessage",
            (int)ContentReportTargetTypeEnum.Product => "Product",
            (int)ContentReportTargetTypeEnum.PostQuickReply => "PostQuickReply",
            _ => "Unknown"
        };
    }

    private static string ToReportStatusName(int value)
    {
        return value switch
        {
            (int)ContentReportStatusEnum.Pending => "Pending",
            (int)ContentReportStatusEnum.Approved => "Approved",
            (int)ContentReportStatusEnum.Rejected => "Rejected",
            _ => "Unknown"
        };
    }

    private static string ToActionTypeName(int value)
    {
        return value switch
        {
            (int)ModerationActionTypeEnum.None => "None",
            (int)ModerationActionTypeEnum.Mute => "Mute",
            (int)ModerationActionTypeEnum.Ban => "Ban",
            (int)ModerationActionTypeEnum.Unmute => "Unmute",
            (int)ModerationActionTypeEnum.Unban => "Unban",
            _ => "Unknown"
        };
    }

    private static int NormalizePageIndex(int pageIndex)
    {
        return pageIndex < 1 ? 1 : pageIndex;
    }

    private static int NormalizePageSize(int pageSize)
    {
        if (pageSize <= 0)
        {
            return 20;
        }

        return Math.Min(pageSize, 50);
    }

    private sealed class ParameterReplaceVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _source;
        private readonly ParameterExpression _target;

        public ParameterReplaceVisitor(ParameterExpression source, ParameterExpression target)
        {
            _source = source;
            _target = target;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _source ? _target : base.VisitParameter(node);
        }
    }
}
