using AutoMapper;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service.Base;
using Radish.Shared.CustomEnum;
using SqlSugar;
using System.Text.RegularExpressions;

namespace Radish.Service;

/// <summary>内容治理服务实现</summary>
public class ContentModerationService : BaseService<ContentReport, ContentReportVo>, IContentModerationService
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

    public ContentModerationService(
        IMapper mapper,
        IBaseRepository<ContentReport> baseRepository,
        IBaseRepository<UserModerationAction> moderationActionRepository,
        IBaseRepository<Post> postRepository,
        IBaseRepository<Comment> commentRepository,
        IChannelMessageRepository channelMessageRepository,
        IBaseRepository<Product> productRepository,
        IBaseRepository<PostQuickReply> postQuickReplyRepository,
        IBaseRepository<User> userRepository)
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
            throw new InvalidOperationException("举报目标用户不存在");
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
            throw new InvalidOperationException("该内容已存在待处理举报，请勿重复提交");
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
            VoItems = await BuildReportQueueItemsAsync(reports),
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

        if (dto.IsApproved &&
            actionType is ModerationActionTypeEnum.Mute or ModerationActionTypeEnum.Ban &&
            report.TargetUserId <= 0)
        {
            throw new InvalidOperationException("当前目标不支持联动用户治理动作");
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

        return await BuildReportQueueItemAsync(report);
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

    private async Task<ResolvedReportTargetSnapshot> ResolveReportTargetSnapshotAsync(
        ContentReportTargetTypeEnum targetType,
        long targetContentId)
    {
        return targetType switch
        {
            ContentReportTargetTypeEnum.Post => await ResolvePostTargetSnapshotAsync(targetContentId),
            ContentReportTargetTypeEnum.Comment => await ResolveCommentTargetSnapshotAsync(targetContentId),
            ContentReportTargetTypeEnum.ChatMessage => await ResolveChatMessageTargetSnapshotAsync(targetContentId),
            ContentReportTargetTypeEnum.Product => await ResolveProductTargetSnapshotAsync(targetContentId),
            ContentReportTargetTypeEnum.PostQuickReply => await ResolvePostQuickReplyTargetSnapshotAsync(targetContentId),
            _ => throw new ArgumentException("不支持的举报目标类型")
        };
    }

    private async Task<ResolvedReportTargetSnapshot> ResolvePostTargetSnapshotAsync(long postId)
    {
        var post = await _postRepository.QueryFirstAsync(p => p.Id == postId && !p.IsDeleted);
        if (post == null)
        {
            throw new InvalidOperationException("目标帖子不存在");
        }

        return new ResolvedReportTargetSnapshot
        {
            TargetUserId = post.AuthorId,
            TargetUserName = post.AuthorName,
            TargetPostId = post.Id,
            SnapshotTitle = BuildTitleSnapshot(post.Title),
            SnapshotSummary = BuildTextSnapshot(string.IsNullOrWhiteSpace(post.Summary) ? post.Content : post.Summary)
        };
    }

    private async Task<ResolvedReportTargetSnapshot> ResolveCommentTargetSnapshotAsync(long commentId)
    {
        var comment = await _commentRepository.QueryFirstAsync(c => c.Id == commentId && !c.IsDeleted);
        if (comment == null)
        {
            throw new InvalidOperationException("目标评论不存在");
        }

        var postTitle = await ResolvePostTitleAsync(comment.PostId);

        return new ResolvedReportTargetSnapshot
        {
            TargetUserId = comment.AuthorId,
            TargetUserName = comment.AuthorName,
            TargetPostId = comment.PostId > 0 ? comment.PostId : null,
            SnapshotTitle = postTitle,
            SnapshotSummary = BuildTextSnapshot(comment.Content)
        };
    }

    private async Task<ResolvedReportTargetSnapshot> ResolveChatMessageTargetSnapshotAsync(long messageId)
    {
        var message = await _channelMessageRepository.QueryFirstIncludingDeletedAsync(m => m.Id == messageId);
        if (message == null)
        {
            throw new InvalidOperationException("目标聊天室消息不存在");
        }

        return new ResolvedReportTargetSnapshot
        {
            TargetUserId = message.UserId,
            TargetUserName = message.UserName,
            TargetChannelId = message.ChannelId > 0 ? message.ChannelId : null,
            SnapshotSummary = message.IsDeleted ? "消息已撤回" : BuildMessageSnapshot(message)
        };
    }

    private async Task<ResolvedReportTargetSnapshot> ResolveProductTargetSnapshotAsync(long productId)
    {
        var product = await _productRepository.QueryFirstAsync(p => p.Id == productId && !p.IsDeleted);
        if (product == null)
        {
            throw new InvalidOperationException("目标商品不存在");
        }

        return new ResolvedReportTargetSnapshot
        {
            TargetUserId = product.CreateId < 0 ? -1 : product.CreateId,
            TargetUserName = product.CreateBy,
            SnapshotTitle = BuildTitleSnapshot(product.Name),
            SnapshotSummary = BuildTextSnapshot(product.Description)
        };
    }

    private async Task<ResolvedReportTargetSnapshot> ResolvePostQuickReplyTargetSnapshotAsync(long quickReplyId)
    {
        var quickReply = await _postQuickReplyRepository.QueryFirstAsync(reply => reply.Id == quickReplyId && !reply.IsDeleted);
        if (quickReply == null)
        {
            throw new InvalidOperationException("目标轻回应不存在");
        }

        var postTitle = await ResolvePostTitleAsync(quickReply.PostId);

        return new ResolvedReportTargetSnapshot
        {
            TargetUserId = quickReply.AuthorId,
            TargetUserName = quickReply.AuthorName,
            TargetPostId = quickReply.PostId > 0 ? quickReply.PostId : null,
            SnapshotTitle = postTitle,
            SnapshotSummary = BuildTextSnapshot(quickReply.Content)
        };
    }

    private async Task<string?> ResolvePostTitleAsync(long postId)
    {
        if (postId <= 0)
        {
            return null;
        }

        var post = await _postRepository.QueryFirstAsync(p => p.Id == postId && !p.IsDeleted);
        return post == null ? null : BuildTitleSnapshot(post.Title);
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

    private const string TargetNavigationStatusReady = "Ready";
    private const string TargetNavigationStatusFallback = "Fallback";
    private const string TargetNavigationStatusUnavailable = "Unavailable";
    private const string TargetNavigationStatusUnsupported = "Unsupported";

    private sealed class ResolvedReportTargetSnapshot
    {
        public long TargetUserId { get; init; }
        public string? TargetUserName { get; init; }
        public long? TargetPostId { get; init; }
        public long? TargetChannelId { get; init; }
        public string? SnapshotTitle { get; init; }
        public string? SnapshotSummary { get; init; }
    }

    private sealed class ReportTargetNavigationSnapshot
    {
        public string TargetTypeName { get; init; } = "Unknown";
        public long TargetContentId { get; init; }
        public long? TargetPostId { get; init; }
        public long? TargetCommentId { get; init; }
        public long? TargetChannelId { get; init; }
        public long? TargetMessageId { get; init; }
        public string NavigationStatus { get; init; } = TargetNavigationStatusUnavailable;
        public string? NavigationMessage { get; init; }
        public string? SnapshotTitle { get; init; }
        public string? SnapshotSummary { get; init; }
    }

    private sealed class ForumPostNavigationRecord
    {
        public long PostId { get; init; }
        public bool IsPostAvailable { get; init; }
        public string? SnapshotTitle { get; init; }
        public string? SnapshotSummary { get; init; }
    }

    private sealed class ForumCommentNavigationRecord
    {
        public long CommentId { get; init; }
        public long PostId { get; init; }
        public long RootCommentId { get; init; }
        public bool IsCommentAvailable { get; init; }
        public bool IsRootCommentAvailable { get; init; }
        public bool IsPostAvailable { get; init; }
        public string? SnapshotTitle { get; init; }
        public string? SnapshotSummary { get; init; }
    }

    private sealed class ForumQuickReplyNavigationRecord
    {
        public long QuickReplyId { get; init; }
        public long PostId { get; init; }
        public bool IsQuickReplyAvailable { get; init; }
        public bool IsPostAvailable { get; init; }
        public string? SnapshotTitle { get; init; }
        public string? SnapshotSummary { get; init; }
    }

    private sealed class ChatMessageNavigationRecord
    {
        public long MessageId { get; init; }
        public long ChannelId { get; init; }
        public string? SnapshotSummary { get; init; }
    }

    private sealed class ProductNavigationRecord
    {
        public long ProductId { get; init; }
        public bool IsEnabled { get; init; }
        public bool IsDeleted { get; init; }
        public ProductType ProductType { get; init; }
        public BenefitType? BenefitType { get; init; }
        public ConsumableType? ConsumableType { get; init; }
        public string? SnapshotTitle { get; init; }
        public string? SnapshotSummary { get; init; }
    }

    private sealed class ForumRootCommentAvailabilityRecord
    {
        public long CommentId { get; init; }
        public bool IsAvailable { get; init; }
    }

    private async Task<List<ContentReportQueueItemVo>> BuildReportQueueItemsAsync(IReadOnlyCollection<ContentReport> reports)
    {
        if (reports.Count == 0)
        {
            return new List<ContentReportQueueItemVo>();
        }

        var navigationMap = await BuildReportNavigationMapAsync(reports);

        return reports
            .Select(report => MapReportQueueItem(
                report,
                navigationMap.GetValueOrDefault(report.Id)))
            .ToList();
    }

    private async Task<ContentReportQueueItemVo> BuildReportQueueItemAsync(ContentReport report)
    {
        var navigation = await BuildReportNavigationSnapshotAsync(report);
        return MapReportQueueItem(report, navigation);
    }

    private async Task<List<UserModerationActionVo>> BuildActionLogItemsAsync(IReadOnlyCollection<UserModerationAction> actions)
    {
        if (actions.Count == 0)
        {
            return new List<UserModerationActionVo>();
        }

        var sourceReportIds = actions
            .Where(action => action.SourceReportId.HasValue && action.SourceReportId.Value > 0)
            .Select(action => action.SourceReportId!.Value)
            .Distinct()
            .ToList();
        var sourceReportMap = sourceReportIds.Count > 0
            ? (await _contentReportRepository.QueryByIdsAsync(sourceReportIds))
                .GroupBy(report => report.Id)
                .ToDictionary(group => group.Key, group => group.First())
            : new Dictionary<long, ContentReport>();
        var navigationMap = await BuildReportNavigationMapAsync(sourceReportMap.Values.ToList());

        return actions
            .Select(action =>
            {
                ReportTargetNavigationSnapshot? sourceNavigation = null;
                var sourceSnapshotIsPersisted = false;
                if (action.SourceReportId.HasValue && sourceReportMap.TryGetValue(action.SourceReportId.Value, out var matchedReport))
                {
                    sourceNavigation = navigationMap.GetValueOrDefault(matchedReport.Id);
                    sourceSnapshotIsPersisted = IsSnapshotPersisted(matchedReport);
                }

                return MapAction(action, sourceNavigation, sourceSnapshotIsPersisted);
            })
            .ToList();
    }

    private async Task<Dictionary<long, ReportTargetNavigationSnapshot>> BuildReportNavigationMapAsync(IReadOnlyCollection<ContentReport> reports)
    {
        if (reports.Count == 0)
        {
            return new Dictionary<long, ReportTargetNavigationSnapshot>();
        }

        var postNavigationMap = await BuildPostNavigationMapAsync(reports.Select(report =>
            report.ReportTargetType == (int)ContentReportTargetTypeEnum.Post
                ? report.TargetContentId
                : 0));
        var chatMessageMap = await BuildChatMessageNavigationMapAsync(reports.Select(report =>
            report.ReportTargetType == (int)ContentReportTargetTypeEnum.ChatMessage
                ? report.TargetContentId
                : 0));
        var commentNavigationMap = await BuildCommentNavigationMapAsync(reports.Select(report =>
            report.ReportTargetType == (int)ContentReportTargetTypeEnum.Comment
                ? report.TargetContentId
                : 0));
        var quickReplyNavigationMap = await BuildQuickReplyNavigationMapAsync(reports.Select(report =>
            report.ReportTargetType == (int)ContentReportTargetTypeEnum.PostQuickReply
                ? report.TargetContentId
                : 0));
        var productMap = await BuildProductNavigationMapAsync(reports.Select(report =>
            report.ReportTargetType == (int)ContentReportTargetTypeEnum.Product
                ? report.TargetContentId
                : 0));

        return reports.ToDictionary(
            report => report.Id,
            report => BuildReportTargetNavigationSnapshot(
                report,
                postNavigationMap,
                chatMessageMap,
                commentNavigationMap,
                quickReplyNavigationMap,
                productMap));
    }

    private async Task<ReportTargetNavigationSnapshot> BuildReportNavigationSnapshotAsync(ContentReport report)
    {
        var postNavigationMap = await BuildPostNavigationMapAsync(
            report.ReportTargetType == (int)ContentReportTargetTypeEnum.Post
                ? new[] { report.TargetContentId }
                : Array.Empty<long>());
        var chatMessageMap = await BuildChatMessageNavigationMapAsync(
            report.ReportTargetType == (int)ContentReportTargetTypeEnum.ChatMessage
                ? new[] { report.TargetContentId }
                : Array.Empty<long>());
        var commentNavigationMap = await BuildCommentNavigationMapAsync(
            report.ReportTargetType == (int)ContentReportTargetTypeEnum.Comment
                ? new[] { report.TargetContentId }
                : Array.Empty<long>());
        var quickReplyNavigationMap = await BuildQuickReplyNavigationMapAsync(
            report.ReportTargetType == (int)ContentReportTargetTypeEnum.PostQuickReply
                ? new[] { report.TargetContentId }
                : Array.Empty<long>());
        var productMap = await BuildProductNavigationMapAsync(
            report.ReportTargetType == (int)ContentReportTargetTypeEnum.Product
                ? new[] { report.TargetContentId }
                : Array.Empty<long>());

        return BuildReportTargetNavigationSnapshot(
            report,
            postNavigationMap,
            chatMessageMap,
            commentNavigationMap,
            quickReplyNavigationMap,
            productMap);
    }

    private async Task<Dictionary<long, ForumPostNavigationRecord>> BuildPostNavigationMapAsync(IEnumerable<long> postIds)
    {
        var normalizedPostIds = postIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        if (normalizedPostIds.Count == 0)
        {
            return new Dictionary<long, ForumPostNavigationRecord>();
        }

        var items = await _postRepository.QueryMuchAsync<Post, User, User, ForumPostNavigationRecord>(
            (post, primaryUser, secondaryUser) => new object[]
            {
                JoinType.Left, post.AuthorId == primaryUser.Id,
                JoinType.Left, post.AuthorId == secondaryUser.Id
            },
            (post, _, _) => new ForumPostNavigationRecord
            {
                PostId = post.Id,
                IsPostAvailable = !post.IsDeleted,
                SnapshotTitle = string.IsNullOrWhiteSpace(post.Title) ? null : post.Title.Trim(),
                SnapshotSummary = BuildTextSnapshot(string.IsNullOrWhiteSpace(post.Summary) ? post.Content : post.Summary)
            },
            (post, _, _) => normalizedPostIds.Contains(post.Id));

        return items
            .GroupBy(item => item.PostId)
            .ToDictionary(group => group.Key, group => group.First());
    }

    private async Task<Dictionary<long, ChatMessageNavigationRecord>> BuildChatMessageNavigationMapAsync(IEnumerable<long> messageIds)
    {
        var normalizedMessageIds = messageIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        if (normalizedMessageIds.Count == 0)
        {
            return new Dictionary<long, ChatMessageNavigationRecord>();
        }

        return (await _channelMessageRepository.QueryByIdsIncludingDeletedAsync(normalizedMessageIds))
            .GroupBy(message => message.Id)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var message = group.First();
                    return new ChatMessageNavigationRecord
                    {
                        MessageId = message.Id,
                        ChannelId = message.ChannelId,
                        SnapshotSummary = message.IsDeleted
                            ? "消息已撤回"
                            : BuildMessageSnapshot(message)
                    };
                });
    }

    private async Task<Dictionary<long, ProductNavigationRecord>> BuildProductNavigationMapAsync(IEnumerable<long> productIds)
    {
        var normalizedProductIds = productIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        if (normalizedProductIds.Count == 0)
        {
            return new Dictionary<long, ProductNavigationRecord>();
        }

        var items = await _productRepository.QueryMuchAsync<Product, ProductCategory, User, ProductNavigationRecord>(
            (product, category, user) => new object[]
            {
                JoinType.Left, product.CategoryId == category.Id,
                JoinType.Left, product.CreateId == user.Id
            },
            (product, _, _) => new ProductNavigationRecord
            {
                ProductId = product.Id,
                IsEnabled = product.IsEnabled,
                IsDeleted = product.IsDeleted,
                ProductType = product.ProductType,
                BenefitType = product.BenefitType,
                ConsumableType = product.ConsumableType,
                SnapshotTitle = string.IsNullOrWhiteSpace(product.Name) ? null : product.Name.Trim(),
                SnapshotSummary = BuildTextSnapshot(product.Description)
            },
            (product, _, _) => normalizedProductIds.Contains(product.Id));

        return items
            .GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.First());
    }

    private async Task<Dictionary<long, ForumCommentNavigationRecord>> BuildCommentNavigationMapAsync(IEnumerable<long> commentIds)
    {
        var normalizedCommentIds = commentIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        if (normalizedCommentIds.Count == 0)
        {
            return new Dictionary<long, ForumCommentNavigationRecord>();
        }

        var items = await _commentRepository.QueryMuchAsync<Comment, Post, User, ForumCommentNavigationRecord>(
            (comment, post, user) => new object[]
            {
                JoinType.Left, comment.PostId == post.Id,
                JoinType.Left, comment.AuthorId == user.Id
            },
            (comment, post, _) => new ForumCommentNavigationRecord
            {
                CommentId = comment.Id,
                PostId = comment.PostId,
                RootCommentId = comment.ParentId == null ? comment.Id : comment.RootId ?? comment.ParentId ?? comment.Id,
                IsCommentAvailable = !comment.IsDeleted && comment.IsEnabled,
                IsPostAvailable = post.Id > 0 && !post.IsDeleted,
                SnapshotTitle = string.IsNullOrWhiteSpace(post.Title) ? null : post.Title.Trim(),
                SnapshotSummary = BuildTextSnapshot(comment.Content)
            },
            (comment, _, _) => normalizedCommentIds.Contains(comment.Id));

        var rootCommentAvailabilityMap = await BuildRootCommentAvailabilityMapAsync(items.Select(item => item.RootCommentId));

        return items
            .GroupBy(item => item.CommentId)
            .ToDictionary(group => group.Key, group =>
            {
                var item = group.First();
                var isRootCommentAvailable = item.RootCommentId == item.CommentId
                    ? item.IsCommentAvailable
                    : rootCommentAvailabilityMap.GetValueOrDefault(item.RootCommentId);

                return new ForumCommentNavigationRecord
                {
                    CommentId = item.CommentId,
                    PostId = item.PostId,
                    RootCommentId = item.RootCommentId,
                    IsCommentAvailable = item.IsCommentAvailable,
                    IsRootCommentAvailable = isRootCommentAvailable,
                    IsPostAvailable = item.IsPostAvailable,
                    SnapshotTitle = item.SnapshotTitle,
                    SnapshotSummary = item.SnapshotSummary
                };
            });
    }

    private async Task<Dictionary<long, bool>> BuildRootCommentAvailabilityMapAsync(IEnumerable<long> rootCommentIds)
    {
        var normalizedRootCommentIds = rootCommentIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        if (normalizedRootCommentIds.Count == 0)
        {
            return new Dictionary<long, bool>();
        }

        var rootItems = await _commentRepository.QueryMuchAsync<Comment, Post, User, ForumRootCommentAvailabilityRecord>(
            (comment, post, user) => new object[]
            {
                JoinType.Left, comment.PostId == post.Id,
                JoinType.Left, comment.AuthorId == user.Id
            },
            (comment, _, _) => new ForumRootCommentAvailabilityRecord
            {
                CommentId = comment.Id,
                IsAvailable = !comment.IsDeleted && comment.IsEnabled
            },
            (comment, _, _) => normalizedRootCommentIds.Contains(comment.Id));

        return rootItems
            .GroupBy(item => item.CommentId)
            .ToDictionary(group => group.Key, group => group.First().IsAvailable);
    }

    private async Task<Dictionary<long, ForumQuickReplyNavigationRecord>> BuildQuickReplyNavigationMapAsync(IEnumerable<long> quickReplyIds)
    {
        var normalizedQuickReplyIds = quickReplyIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        if (normalizedQuickReplyIds.Count == 0)
        {
            return new Dictionary<long, ForumQuickReplyNavigationRecord>();
        }

        var items = await _postQuickReplyRepository.QueryMuchAsync<PostQuickReply, Post, User, ForumQuickReplyNavigationRecord>(
            (reply, post, user) => new object[]
            {
                JoinType.Left, reply.PostId == post.Id,
                JoinType.Left, reply.AuthorId == user.Id
            },
            (reply, post, _) => new ForumQuickReplyNavigationRecord
            {
                QuickReplyId = reply.Id,
                PostId = reply.PostId,
                IsQuickReplyAvailable = !reply.IsDeleted,
                IsPostAvailable = post.Id > 0 && !post.IsDeleted,
                SnapshotTitle = string.IsNullOrWhiteSpace(post.Title) ? null : post.Title.Trim(),
                SnapshotSummary = BuildTextSnapshot(reply.Content)
            },
            (reply, _, _) => normalizedQuickReplyIds.Contains(reply.Id));

        return items
            .GroupBy(item => item.QuickReplyId)
            .ToDictionary(group => group.Key, group => group.First());
    }

    private static long? ResolveSnapshotPostId(ContentReport report, long? currentPostId = null)
    {
        if (currentPostId.HasValue && currentPostId.Value > 0)
        {
            return currentPostId.Value;
        }

        if (report.TargetSnapshotPostId.HasValue && report.TargetSnapshotPostId.Value > 0)
        {
            return report.TargetSnapshotPostId.Value;
        }

        return (ContentReportTargetTypeEnum)report.ReportTargetType == ContentReportTargetTypeEnum.Post && report.TargetContentId > 0
            ? report.TargetContentId
            : null;
    }

    private static long? ResolveSnapshotChannelId(ContentReport report, long? currentChannelId = null)
    {
        if (currentChannelId.HasValue && currentChannelId.Value > 0)
        {
            return currentChannelId.Value;
        }

        return report.TargetSnapshotChannelId.HasValue && report.TargetSnapshotChannelId.Value > 0
            ? report.TargetSnapshotChannelId.Value
            : null;
    }

    private static string? ResolveSnapshotTitle(ContentReport report, string? currentSnapshotTitle)
    {
        return !string.IsNullOrWhiteSpace(report.TargetSnapshotTitle)
            ? report.TargetSnapshotTitle
            : currentSnapshotTitle;
    }

    private static string? ResolveSnapshotSummary(ContentReport report, string? currentSnapshotSummary)
    {
        return !string.IsNullOrWhiteSpace(report.TargetSnapshotSummary)
            ? report.TargetSnapshotSummary
            : currentSnapshotSummary;
    }

    private static bool IsSnapshotPersisted(ContentReport report)
    {
        return report.TargetSnapshotPostId.HasValue
            || report.TargetSnapshotChannelId.HasValue
            || !string.IsNullOrWhiteSpace(report.TargetSnapshotTitle)
            || !string.IsNullOrWhiteSpace(report.TargetSnapshotSummary);
    }

    private static ReportTargetNavigationSnapshot BuildReportTargetNavigationSnapshot(
        ContentReport report,
        IReadOnlyDictionary<long, ForumPostNavigationRecord>? postNavigationMap = null,
        IReadOnlyDictionary<long, ChatMessageNavigationRecord>? chatMessageMap = null,
        IReadOnlyDictionary<long, ForumCommentNavigationRecord>? commentNavigationMap = null,
        IReadOnlyDictionary<long, ForumQuickReplyNavigationRecord>? quickReplyNavigationMap = null,
        IReadOnlyDictionary<long, ProductNavigationRecord>? productMap = null)
    {
        var targetType = (ContentReportTargetTypeEnum)report.ReportTargetType;
        var targetTypeName = ToReportTargetTypeName(report.ReportTargetType);

        if (targetType == ContentReportTargetTypeEnum.Post)
        {
            ForumPostNavigationRecord? postNavigation = null;
            if (postNavigationMap != null)
            {
                postNavigationMap.TryGetValue(report.TargetContentId, out postNavigation);
            }
            var isPostAvailable = postNavigation?.IsPostAvailable == true;
            var targetPostId = ResolveSnapshotPostId(report, postNavigation?.PostId);
            return new ReportTargetNavigationSnapshot
            {
                TargetTypeName = targetTypeName,
                TargetContentId = report.TargetContentId,
                TargetPostId = targetPostId,
                NavigationStatus = isPostAvailable ? TargetNavigationStatusReady : TargetNavigationStatusUnavailable,
                NavigationMessage = isPostAvailable ? null : "帖子已删除或不存在",
                SnapshotTitle = ResolveSnapshotTitle(report, postNavigation?.SnapshotTitle),
                SnapshotSummary = ResolveSnapshotSummary(report, postNavigation?.SnapshotSummary)
            };
        }

        if (targetType == ContentReportTargetTypeEnum.Comment)
        {
            var snapshotPostId = ResolveSnapshotPostId(report);

            if (commentNavigationMap?.TryGetValue(report.TargetContentId, out var matchedCommentNavigation) != true || matchedCommentNavigation == null)
            {
                return new ReportTargetNavigationSnapshot
                {
                    TargetTypeName = targetTypeName,
                    TargetContentId = report.TargetContentId,
                    TargetPostId = snapshotPostId,
                    TargetCommentId = report.TargetContentId > 0 ? report.TargetContentId : null,
                    NavigationStatus = TargetNavigationStatusUnavailable,
                    NavigationMessage = "评论已删除或不存在",
                    SnapshotTitle = ResolveSnapshotTitle(report, null),
                    SnapshotSummary = ResolveSnapshotSummary(report, null)
                };
            }

            var commentNavigation = matchedCommentNavigation;
            var navigationStatus = commentNavigation.IsPostAvailable
                ? commentNavigation.IsCommentAvailable && commentNavigation.IsRootCommentAvailable
                    ? TargetNavigationStatusReady
                    : TargetNavigationStatusFallback
                : TargetNavigationStatusUnavailable;
            var navigationMessage = navigationStatus switch
            {
                TargetNavigationStatusFallback => "评论已删除或无法定位，已降级为所属帖子回看",
                TargetNavigationStatusUnavailable when !commentNavigation.IsPostAvailable => "所属帖子已删除或不存在",
                TargetNavigationStatusUnavailable => "评论已删除或不存在",
                _ => null
            };

            return new ReportTargetNavigationSnapshot
            {
                TargetTypeName = targetTypeName,
                TargetContentId = report.TargetContentId,
                TargetPostId = ResolveSnapshotPostId(report, commentNavigation.PostId),
                TargetCommentId = report.TargetContentId > 0 ? report.TargetContentId : null,
                NavigationStatus = navigationStatus,
                NavigationMessage = navigationMessage,
                SnapshotTitle = ResolveSnapshotTitle(report, commentNavigation.SnapshotTitle),
                SnapshotSummary = ResolveSnapshotSummary(report, commentNavigation.SnapshotSummary)
            };
        }

        if (targetType == ContentReportTargetTypeEnum.PostQuickReply)
        {
            var snapshotPostId = ResolveSnapshotPostId(report);

            if (quickReplyNavigationMap?.TryGetValue(report.TargetContentId, out var matchedQuickReplyNavigation) != true || matchedQuickReplyNavigation == null)
            {
                return new ReportTargetNavigationSnapshot
                {
                    TargetTypeName = targetTypeName,
                    TargetContentId = report.TargetContentId,
                    TargetPostId = snapshotPostId,
                    NavigationStatus = TargetNavigationStatusUnavailable,
                    NavigationMessage = "轻回应已删除或不存在",
                    SnapshotTitle = ResolveSnapshotTitle(report, null),
                    SnapshotSummary = ResolveSnapshotSummary(report, null)
                };
            }

            var quickReplyNavigation = matchedQuickReplyNavigation;
            var navigationStatus = quickReplyNavigation.IsPostAvailable
                ? quickReplyNavigation.IsQuickReplyAvailable
                    ? TargetNavigationStatusReady
                    : TargetNavigationStatusFallback
                : TargetNavigationStatusUnavailable;
            var navigationMessage = navigationStatus switch
            {
                TargetNavigationStatusFallback => "轻回应已删除，已降级为所属帖子回看",
                TargetNavigationStatusUnavailable when !quickReplyNavigation.IsPostAvailable => "所属帖子已删除或不存在",
                TargetNavigationStatusUnavailable => "轻回应已删除或不存在",
                _ => null
            };

            return new ReportTargetNavigationSnapshot
            {
                TargetTypeName = targetTypeName,
                TargetContentId = report.TargetContentId,
                TargetPostId = ResolveSnapshotPostId(report, quickReplyNavigation.PostId),
                NavigationStatus = navigationStatus,
                NavigationMessage = navigationMessage,
                SnapshotTitle = ResolveSnapshotTitle(report, quickReplyNavigation.SnapshotTitle),
                SnapshotSummary = ResolveSnapshotSummary(report, quickReplyNavigation.SnapshotSummary)
            };
        }

        if (targetType == ContentReportTargetTypeEnum.Product)
        {
            if (productMap?.TryGetValue(report.TargetContentId, out var matchedProduct) != true || matchedProduct == null)
            {
                return new ReportTargetNavigationSnapshot
                {
                    TargetTypeName = targetTypeName,
                    TargetContentId = report.TargetContentId,
                    NavigationStatus = TargetNavigationStatusUnavailable,
                    NavigationMessage = "商品已删除或不存在",
                    SnapshotTitle = ResolveSnapshotTitle(report, null),
                    SnapshotSummary = ResolveSnapshotSummary(report, null)
                };
            }

            var product = matchedProduct;
            var navigationStatus = TargetNavigationStatusReady;
            string? navigationMessage = null;

            if (product.IsDeleted)
            {
                navigationStatus = TargetNavigationStatusUnavailable;
                navigationMessage = "商品已删除或不存在";
            }
            else if (!product.IsEnabled)
            {
                navigationStatus = TargetNavigationStatusUnavailable;
                navigationMessage = "商品已下线";
            }
            else if (ShopProductAvailabilityPolicy.IsUnavailablePublicProduct(product.ProductType, product.BenefitType, product.ConsumableType))
            {
                navigationStatus = TargetNavigationStatusUnavailable;
                navigationMessage = $"{ShopProductAvailabilityPolicy.GetUnavailableProductDisplayName(product.BenefitType, product.ConsumableType)}暂不支持公开回看";
            }

            return new ReportTargetNavigationSnapshot
            {
                TargetTypeName = targetTypeName,
                TargetContentId = report.TargetContentId,
                NavigationStatus = navigationStatus,
                NavigationMessage = navigationMessage,
                SnapshotTitle = ResolveSnapshotTitle(report, product.SnapshotTitle),
                SnapshotSummary = ResolveSnapshotSummary(report, product.SnapshotSummary)
            };
        }

        if (targetType == ContentReportTargetTypeEnum.ChatMessage)
        {
            ChatMessageNavigationRecord? chatMessageNavigation = null;
            if (chatMessageMap != null)
            {
                chatMessageMap.TryGetValue(report.TargetContentId, out chatMessageNavigation);
            }
            var targetChannelId = ResolveSnapshotChannelId(report, chatMessageNavigation?.ChannelId);
            return new ReportTargetNavigationSnapshot
            {
                TargetTypeName = targetTypeName,
                TargetContentId = report.TargetContentId,
                TargetChannelId = targetChannelId,
                TargetMessageId = report.TargetContentId > 0 ? report.TargetContentId : null,
                NavigationStatus = chatMessageNavigation?.ChannelId > 0
                    ? TargetNavigationStatusReady
                    : TargetNavigationStatusUnavailable,
                NavigationMessage = chatMessageNavigation?.ChannelId > 0
                    ? null
                    : "聊天消息定位已失效",
                SnapshotSummary = ResolveSnapshotSummary(report, chatMessageNavigation?.SnapshotSummary)
            };
        }

        return new ReportTargetNavigationSnapshot
        {
            TargetTypeName = targetTypeName,
            TargetContentId = report.TargetContentId,
            NavigationStatus = TargetNavigationStatusUnsupported,
            NavigationMessage = "当前目标暂不支持直接回看",
            SnapshotTitle = ResolveSnapshotTitle(report, null),
            SnapshotSummary = ResolveSnapshotSummary(report, null)
        };
    }

    private static ContentReportQueueItemVo MapReportQueueItem(ContentReport report, ReportTargetNavigationSnapshot? navigation = null)
    {
        navigation ??= BuildReportTargetNavigationSnapshot(report);

        return new ContentReportQueueItemVo
        {
            VoReportId = report.Id,
            VoTargetType = navigation.TargetTypeName,
            VoTargetContentId = navigation.TargetContentId,
            VoTargetPostId = navigation.TargetPostId,
            VoTargetCommentId = navigation.TargetCommentId,
            VoTargetChannelId = navigation.TargetChannelId,
            VoTargetMessageId = navigation.TargetMessageId,
            VoTargetNavigationStatus = navigation.NavigationStatus,
            VoTargetNavigationMessage = navigation.NavigationMessage,
            VoTargetSnapshotTitle = navigation.SnapshotTitle,
            VoTargetSnapshotSummary = navigation.SnapshotSummary,
            VoTargetSnapshotIsPersisted = IsSnapshotPersisted(report),
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

    private static UserModerationActionVo MapAction(
        UserModerationAction action,
        ReportTargetNavigationSnapshot? sourceNavigation = null,
        bool sourceSnapshotIsPersisted = false)
    {
        return new UserModerationActionVo
        {
            VoActionId = action.Id,
            VoTargetUserId = action.TargetUserId,
            VoTargetUserName = action.TargetUserName,
            VoActionType = ToActionTypeName(action.ActionType),
            VoReason = action.Reason,
            VoSourceReportId = action.SourceReportId,
            VoSourceReportTargetType = sourceNavigation?.TargetTypeName,
            VoSourceReportTargetContentId = sourceNavigation?.TargetContentId,
            VoSourceReportTargetPostId = sourceNavigation?.TargetPostId,
            VoSourceReportTargetCommentId = sourceNavigation?.TargetCommentId,
            VoSourceReportTargetChannelId = sourceNavigation?.TargetChannelId,
            VoSourceReportTargetMessageId = sourceNavigation?.TargetMessageId,
            VoSourceReportTargetNavigationStatus = sourceNavigation?.NavigationStatus ?? TargetNavigationStatusUnavailable,
            VoSourceReportTargetNavigationMessage = sourceNavigation?.NavigationMessage,
            VoSourceReportTargetSnapshotTitle = sourceNavigation?.SnapshotTitle,
            VoSourceReportTargetSnapshotSummary = sourceNavigation?.SnapshotSummary,
            VoSourceReportTargetSnapshotIsPersisted = sourceSnapshotIsPersisted,
            VoDurationHours = action.DurationHours,
            VoStartTime = action.StartTime,
            VoEndTime = action.EndTime,
            VoIsActive = action.IsActive,
            VoOperatorUserId = action.CreateId,
            VoOperatorUserName = action.CreateBy,
            VoCreateTime = action.CreateTime
        };
    }

    private static string? BuildTitleSnapshot(string? title, int maxLength = 200)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var normalized = MultiWhitespacePattern.Replace(title, " ").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }

    private static string? BuildMessageSnapshot(ChannelMessage message)
    {
        return message.Type switch
        {
            MessageType.Text => BuildTextSnapshot(message.Content),
            MessageType.Image => "图片消息",
            MessageType.System => BuildTextSnapshot(message.Content) ?? "系统消息",
            _ => BuildTextSnapshot(message.Content)
        };
    }

    private static string? BuildTextSnapshot(string? content, int maxLength = 48)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var normalized = MarkdownImagePattern.Replace(content, match =>
        {
            var source = match.Groups.Count > 1 ? match.Groups[1].Value : string.Empty;
            if (source.StartsWith("sticker://", StringComparison.OrdinalIgnoreCase))
            {
                return "[表情]";
            }

            return "[图片]";
        });

        normalized = MarkdownLinkPattern.Replace(normalized, match =>
        {
            var label = match.Groups["name"].Value.Trim();
            return string.IsNullOrWhiteSpace(label) ? "[附件]" : label;
        });
        normalized = AttachmentUriPattern.Replace(normalized, "[附件]");
        normalized = StickerUriPattern.Replace(normalized, "[表情]");
        normalized = MultiWhitespacePattern.Replace(normalized, " ").Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= maxLength
            ? normalized
            : $"{normalized[..(maxLength - 3)]}...";
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
