using AutoMapper;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service.Base;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Service;

/// <summary>内容治理服务实现</summary>
public class ContentModerationService : BaseService<ContentReport, ContentReportVo>, IContentModerationService
{
    private readonly IBaseRepository<ContentReport> _contentReportRepository;
    private readonly IBaseRepository<UserModerationAction> _moderationActionRepository;
    private readonly IBaseRepository<Post> _postRepository;
    private readonly IBaseRepository<Comment> _commentRepository;
    private readonly IBaseRepository<User> _userRepository;

    public ContentModerationService(
        IMapper mapper,
        IBaseRepository<ContentReport> baseRepository,
        IBaseRepository<UserModerationAction> moderationActionRepository,
        IBaseRepository<Post> postRepository,
        IBaseRepository<Comment> commentRepository,
        IBaseRepository<User> userRepository)
        : base(mapper, baseRepository)
    {
        _contentReportRepository = baseRepository;
        _moderationActionRepository = moderationActionRepository;
        _postRepository = postRepository;
        _commentRepository = commentRepository;
        _userRepository = userRepository;
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
        var (targetUserId, targetUserName) = await ResolveReportTargetAsync(targetType, dto.TargetContentId);
        if (targetUserId <= 0)
        {
            throw new InvalidOperationException("举报目标用户不存在");
        }

        if (targetUserId == reporterUserId)
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
            throw new InvalidOperationException("该内容已存在待处理举报，请勿重复提交");
        }

        var normalizedReporterName = string.IsNullOrWhiteSpace(reporterUserName) ? $"User-{reporterUserId}" : reporterUserName.Trim();
        var normalizedTenantId = tenantId > 0 ? tenantId : 0;
        var normalizedReasonType = string.IsNullOrWhiteSpace(dto.ReasonType) ? "Other" : dto.ReasonType.Trim();

        var reportId = await _contentReportRepository.AddAsync(new ContentReport
        {
            ReportTargetType = (int)targetType,
            TargetContentId = dto.TargetContentId,
            TargetUserId = targetUserId,
            TargetUserName = targetUserName,
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

    public async Task<VoPagedResult<ContentReportQueueItemVo>> GetReportQueueAsync(int? status, int pageIndex, int pageSize)
    {
        var safePageIndex = NormalizePageIndex(pageIndex);
        var safePageSize = NormalizePageSize(pageSize);
        var hasStatusFilter = status.HasValue && status.Value >= 0;
        var statusValue = status ?? (int)ContentReportStatusEnum.Pending;

        var (reports, totalCount) = await _contentReportRepository.QueryPageAsync(
            r => !r.IsDeleted && (!hasStatusFilter || r.Status == statusValue),
            safePageIndex,
            safePageSize,
            r => r.CreateTime,
            OrderByType.Desc);

        return new VoPagedResult<ContentReportQueueItemVo>
        {
            VoItems = reports.Select(MapReportQueueItem).ToList(),
            VoTotal = totalCount,
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
            throw new InvalidOperationException("举报单不存在");
        }

        if (report.Status != (int)ContentReportStatusEnum.Pending)
        {
            throw new InvalidOperationException("该举报单已处理，请勿重复审核");
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

        await _contentReportRepository.UpdateAsync(report);

        if (dto.IsApproved && actionType is ModerationActionTypeEnum.Mute or ModerationActionTypeEnum.Ban)
        {
            var actionReason = BuildReviewActionReason(report);
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

        return MapReportQueueItem(report);
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
            throw new InvalidOperationException("目标用户不存在");
        }

        var targetUserName = string.IsNullOrWhiteSpace(targetUser.UserName) ? $"User-{targetUser.Id}" : targetUser.UserName.Trim();
        var normalizedTenantId = tenantId > 0 ? tenantId : targetUser.TenantId;

        if (actionType == ModerationActionTypeEnum.Mute && (!dto.DurationHours.HasValue || dto.DurationHours.Value <= 0))
        {
            throw new ArgumentException("禁言动作必须指定有效时长（小时）");
        }

        if (actionType == ModerationActionTypeEnum.Ban && dto.DurationHours.HasValue && dto.DurationHours.Value <= 0)
        {
            throw new ArgumentException("封禁时长必须大于 0（为空表示永久封禁）");
        }

        var actionReason = string.IsNullOrWhiteSpace(dto.Reason) ? "管理员手动操作" : dto.Reason.Trim();

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

    public async Task<VoPagedResult<UserModerationActionVo>> GetActionLogsAsync(int pageIndex, int pageSize, long? targetUserId = null)
    {
        var safePageIndex = NormalizePageIndex(pageIndex);
        var safePageSize = NormalizePageSize(pageSize);
        var hasTargetUserFilter = targetUserId.HasValue && targetUserId.Value > 0;
        var targetUserValue = targetUserId ?? 0;

        var (actions, totalCount) = await _moderationActionRepository.QueryPageAsync(
            a => !a.IsDeleted && (!hasTargetUserFilter || a.TargetUserId == targetUserValue),
            safePageIndex,
            safePageSize,
            a => a.CreateTime,
            OrderByType.Desc);

        return new VoPagedResult<UserModerationActionVo>
        {
            VoItems = actions.Select(MapAction).ToList(),
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

    private async Task<(long targetUserId, string? targetUserName)> ResolveReportTargetAsync(
        ContentReportTargetTypeEnum targetType,
        long targetContentId)
    {
        return targetType switch
        {
            ContentReportTargetTypeEnum.Post => await ResolvePostTargetAsync(targetContentId),
            ContentReportTargetTypeEnum.Comment => await ResolveCommentTargetAsync(targetContentId),
            _ => throw new ArgumentException("不支持的举报目标类型")
        };
    }

    private async Task<(long targetUserId, string? targetUserName)> ResolvePostTargetAsync(long postId)
    {
        var post = await _postRepository.QueryFirstAsync(p => p.Id == postId && !p.IsDeleted);
        if (post == null)
        {
            throw new InvalidOperationException("目标帖子不存在");
        }

        return (post.AuthorId, post.AuthorName);
    }

    private async Task<(long targetUserId, string? targetUserName)> ResolveCommentTargetAsync(long commentId)
    {
        var comment = await _commentRepository.QueryFirstAsync(c => c.Id == commentId && !c.IsDeleted);
        if (comment == null)
        {
            throw new InvalidOperationException("目标评论不存在");
        }

        return (comment.AuthorId, comment.AuthorName);
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
            _ => throw new ArgumentException("举报目标类型仅支持 Post 或 Comment")
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

    private static ContentReportQueueItemVo MapReportQueueItem(ContentReport report)
    {
        return new ContentReportQueueItemVo
        {
            VoReportId = report.Id,
            VoTargetType = ToReportTargetTypeName(report.ReportTargetType),
            VoTargetContentId = report.TargetContentId,
            VoTargetUserId = report.TargetUserId,
            VoTargetUserName = report.TargetUserName,
            VoReporterUserId = report.ReporterUserId,
            VoReporterUserName = report.ReporterUserName,
            VoReasonType = report.ReasonType,
            VoReasonDetail = report.ReasonDetail,
            VoStatus = ToReportStatusName(report.Status),
            VoReviewActionType = ToActionTypeName(report.ReviewActionType),
            VoReviewDurationHours = report.ReviewDurationHours,
            VoReviewRemark = report.ReviewRemark,
            VoReviewedById = report.ReviewedById,
            VoReviewedByName = report.ReviewedByName,
            VoReviewedAt = report.ReviewedAt,
            VoCreateTime = report.CreateTime
        };
    }

    private static UserModerationActionVo MapAction(UserModerationAction action)
    {
        return new UserModerationActionVo
        {
            VoActionId = action.Id,
            VoTargetUserId = action.TargetUserId,
            VoTargetUserName = action.TargetUserName,
            VoActionType = ToActionTypeName(action.ActionType),
            VoReason = action.Reason,
            VoSourceReportId = action.SourceReportId,
            VoDurationHours = action.DurationHours,
            VoStartTime = action.StartTime,
            VoEndTime = action.EndTime,
            VoIsActive = action.IsActive,
            VoOperatorUserId = action.CreateId,
            VoOperatorUserName = action.CreateBy,
            VoCreateTime = action.CreateTime
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
}
