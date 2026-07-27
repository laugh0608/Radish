using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Radish.Common.Exceptions;
using Radish.Common.TimeTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.Constants;

namespace Radish.Service;

/// <summary>论坛内容赞赏的应用服务。</summary>
public sealed class ContentRewardService : IContentRewardService
{
    private const int MaxWriteAttempts = 3;

    private readonly IContentRewardRepository _contentRewardRepository;
    private readonly IOperationIdempotencyService _idempotencyService;
    private readonly ISystemSettingProvider _systemSettingProvider;
    private readonly BusinessCalendar _businessCalendar;
    private readonly TimeProvider _timeProvider;
    private readonly IBaseRepository<Post> _postRepository;
    private readonly IBaseRepository<Comment> _commentRepository;
    private readonly IBaseRepository<User> _userRepository;
    private readonly IBaseRepository<Attachment> _attachmentRepository;
    private readonly IAttachmentUrlResolver _attachmentUrlResolver;
    private readonly ILogger<ContentRewardService> _logger;

    public ContentRewardService(
        IContentRewardRepository contentRewardRepository,
        IOperationIdempotencyService idempotencyService,
        ISystemSettingProvider systemSettingProvider,
        BusinessCalendar businessCalendar,
        TimeProvider timeProvider,
        IBaseRepository<Post> postRepository,
        IBaseRepository<Comment> commentRepository,
        IBaseRepository<User> userRepository,
        IBaseRepository<Attachment> attachmentRepository,
        IAttachmentUrlResolver attachmentUrlResolver,
        ILogger<ContentRewardService> logger)
    {
        _contentRewardRepository = contentRewardRepository;
        _idempotencyService = idempotencyService;
        _systemSettingProvider = systemSettingProvider;
        _businessCalendar = businessCalendar;
        _timeProvider = timeProvider;
        _postRepository = postRepository;
        _commentRepository = commentRepository;
        _userRepository = userRepository;
        _attachmentRepository = attachmentRepository;
        _attachmentUrlResolver = attachmentUrlResolver;
        _logger = logger;
    }

    public async Task<ContentRewardMutationVo> CreateAsync(
        CreateContentRewardDto request,
        long currentUserId,
        string currentUserName,
        long tenantId)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentUserId <= 0)
        {
            throw new BusinessException(
                "请先登录后再送出胡萝卜",
                StatusCodes.Status401Unauthorized,
                "Auth.Unauthorized",
                "error.auth.unauthorized");
        }

        var targetType = NormalizeTargetType(request.TargetType);
        var reasonCode = NormalizeReasonCode(request.ReasonCode);
        if (request.TargetId <= 0)
        {
            throw InvalidArgument("目标 ID 无效");
        }

        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);
        var enabledValue = await _systemSettingProvider.GetEffectiveValueAsync(
            SystemConfigDefaults.ContentRewardEnabledKey);
        if (!bool.TryParse(enabledValue, out var contentRewardEnabled) || !contentRewardEnabled)
        {
            throw Unavailable();
        }
        var requestSnapshot = _idempotencyService.CreateRequestSnapshot(
            new Dictionary<string, object?>
            {
                ["targetType"] = targetType,
                ["targetId"] = request.TargetId,
                ["reasonCode"] = reasonCode
            });
        var normalizedTenantId = Math.Max(0, tenantId);
        var beginRequest = new OperationIdempotencyBeginRequest
        {
            TenantId = normalizedTenantId,
            UserId = currentUserId,
            OperationType = OperationIdempotencyOperationTypes.ContentReward,
            IdempotencyKey = idempotencyKey,
            RequestHash = requestSnapshot.RequestHash,
            RequestSummary = requestSnapshot.RequestSummary,
            AllowExpiredProcessingReset = false
        };
        var beginResult = await _idempotencyService.BeginAsync(beginRequest);
        var recoverExpiredProcessing =
            beginResult.Status == OperationIdempotencyBeginStatus.Processing &&
            beginResult.IsExpiredProcessing;
        var replay = recoverExpiredProcessing ? null : ResolveBeginResult(beginResult);
        if (replay != null)
        {
            return replay;
        }
        if (!beginResult.RecordId.HasValue)
        {
            throw ReplayUnavailable();
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var businessDate = _businessCalendar.GetDate(new DateTimeOffset(nowUtc, TimeSpan.Zero));
        var (startUtc, endUtc) = _businessCalendar.GetUtcRange(businessDate);
        var dailyTotalLimit = await _systemSettingProvider.GetInt32Async(
            SystemConfigDefaults.ContentRewardDailyTotalLimitKey);
        var dailyRecipientLimit = await _systemSettingProvider.GetInt32Async(
            SystemConfigDefaults.ContentRewardDailyRecipientLimitKey);
        dailyTotalLimit = Math.Max(1, dailyTotalLimit);
        dailyRecipientLimit = Math.Clamp(dailyRecipientLimit, 1, dailyTotalLimit);
        var command = new ContentRewardWriteCommand(
            normalizedTenantId,
            currentUserId,
            User.NormalizeDisplayName(currentUserName, currentUserId),
            targetType,
            request.TargetId,
            reasonCode,
            beginResult.RecordId.Value,
            requestSnapshot.RequestHash,
            startUtc,
            endUtc,
            dailyTotalLimit,
            dailyRecipientLimit,
            recoverExpiredProcessing,
            nowUtc);

        try
        {
            ContentRewardWriteResult? result = null;
            for (var attempt = 1; attempt <= MaxWriteAttempts; attempt++)
            {
                try
                {
                    result = await _contentRewardRepository.CreateAsync(command);
                    break;
                }
                catch (ContentRewardConcurrentConflictException) when (attempt < MaxWriteAttempts)
                {
                    _logger.LogWarning(
                        "内容赞赏发生并发冲突，准备重试。SenderUserId={SenderUserId}, TargetType={TargetType}, TargetId={TargetId}, Attempt={Attempt}",
                        currentUserId,
                        targetType,
                        request.TargetId,
                        attempt);
                    await Task.Delay(TimeSpan.FromMilliseconds(20 * attempt));
                }
            }

            if (result == null)
            {
                throw new ContentRewardConcurrentConflictException();
            }

            return new ContentRewardMutationVo
            {
                VoRewardId = result.Reward.Id,
                VoTargetType = result.Reward.TargetType,
                VoTargetId = result.Reward.TargetId,
                VoReasonCode = result.Reward.ReasonCode,
                VoTotalCount = result.TotalCount,
                VoViewerRewarded = true,
                VoSenderAvailableBalance = result.SenderAvailableBalance,
                VoTransactionNo = result.TransactionNo
            };
        }
        catch (Exception exception) when (IsTerminalBusinessFailure(exception))
        {
            var mapped = MapRepositoryException(exception);
            await CompleteTerminalFailureAsync(beginResult.RecordId.Value, mapped);
            throw mapped;
        }
        catch (ContentRewardConcurrentConflictException)
        {
            await _idempotencyService.CompleteFailureAsync(
                beginResult.RecordId.Value,
                ContentRewardErrorCodes.ConcurrentConflict,
                "数据库并发冲突，请稍后重试");
            throw ConcurrentConflict();
        }
        catch (ContentRewardRecoveryUnavailableException)
        {
            throw ReplayUnavailable();
        }
        catch (ContentRewardRelationshipUnavailableException exception)
        {
            await _idempotencyService.CompleteFailureAsync(
                beginResult.RecordId.Value,
                "UserBlock.RelationshipTemporarilyUnavailable",
                "用户关系服务暂时不可用，请稍后重试");
            throw RelationshipTemporarilyUnavailable(exception);
        }
        catch (Exception exception)
        {
            var committed = await TryResolveCommittedResultAsync(beginRequest);
            if (committed != null)
            {
                return committed;
            }

            await _idempotencyService.CompleteFailureAsync(
                beginResult.RecordId.Value,
                null,
                exception.Message);
            throw;
        }
    }

    public async Task<ContentRewardTargetPageVo> GetTargetRewardsAsync(
        string targetType,
        long targetId,
        long currentUserId,
        long tenantId,
        int pageIndex,
        int pageSize)
    {
        var normalizedTargetType = NormalizeTargetType(targetType);
        if (targetId <= 0)
        {
            throw InvalidArgument("目标 ID 无效");
        }

        var normalizedTenantId = Math.Max(0, tenantId);
        await EnsureTargetAvailableAsync(normalizedTenantId, normalizedTargetType, targetId);
        var safePageIndex = Math.Max(1, pageIndex);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await _contentRewardRepository.QueryTargetPageAsync(
            normalizedTenantId,
            normalizedTargetType,
            targetId,
            safePageIndex,
            safePageSize);
        var senderIds = items.Select(item => item.SenderUserId).Distinct().ToList();
        var users = senderIds.Count == 0
            ? []
            : await _userRepository.QueryAsync(user =>
                user.TenantId == normalizedTenantId &&
                senderIds.Contains(user.Id));
        var userMap = users.ToDictionary(user => user.Id);
        var avatarMap = await LoadAvatarMapAsync(
            users.Where(user => user.IsEnable && !user.IsDeleted).Select(user => user.Id).ToList());
        return new ContentRewardTargetPageVo
        {
            VoTargetType = normalizedTargetType,
            VoTargetId = targetId,
            VoTotalCount = total,
            VoViewerRewarded = currentUserId > 0 &&
                               items.Any(item => item.SenderUserId == currentUserId) ||
                               await HasRewardedAsync(
                                   normalizedTenantId,
                                   currentUserId,
                                   normalizedTargetType,
                                   targetId),
            VoItems = items.Select(item =>
            {
                userMap.TryGetValue(item.SenderUserId, out var sender);
                return new ContentRewardRecordVo
                {
                    VoRewardId = item.Id,
                    VoSenderPublicId = sender is { IsEnable: true, IsDeleted: false }
                        ? sender.PublicId
                        : null,
                    VoSenderDisplayName = sender is { IsEnable: true, IsDeleted: false }
                        ? User.BuildDisplayHandle(sender.UserName, sender.PublicIndex, sender.Id)
                          ?? User.NormalizeDisplayName(sender.UserName, sender.Id)
                        : "用户不可用",
                    VoSenderAvatarUrl = avatarMap.GetValueOrDefault(item.SenderUserId),
                    VoReasonCode = item.ReasonCode,
                    VoCreateTime = item.CreateTime
                };
            }).ToList(),
            VoPageIndex = safePageIndex,
            VoPageSize = safePageSize
        };
    }

    public async Task<IReadOnlyList<ContentRewardTargetStateVo>> GetTargetStatesAsync(
        GetContentRewardTargetStatesDto request,
        long currentUserId,
        long tenantId)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Targets.Count is < 1 or > 100)
        {
            throw InvalidArgument("目标数量必须为 1-100");
        }

        var targets = request.Targets
            .Select(item => new ContentRewardTargetKey(
                NormalizeTargetType(item.TargetType),
                item.TargetId))
            .Distinct()
            .ToList();
        if (targets.Any(item => item.TargetId <= 0))
        {
            throw InvalidArgument("目标 ID 无效");
        }

        var normalizedTenantId = Math.Max(0, tenantId);
        await EnsureTargetsAvailableAsync(normalizedTenantId, targets);
        var counts = await _contentRewardRepository.QueryTargetCountsAsync(normalizedTenantId, targets);
        var countMap = counts.ToDictionary(
            item => new ContentRewardTargetKey(item.TargetType, item.TargetId),
            item => item.TotalCount);
        var rewarded = await _contentRewardRepository.QueryRewardedTargetsAsync(
            normalizedTenantId,
            currentUserId,
            targets);
        return targets.Select(target => new ContentRewardTargetStateVo
        {
            VoTargetType = target.TargetType,
            VoTargetId = target.TargetId,
            VoTotalCount = countMap.GetValueOrDefault(target),
            VoViewerRewarded = rewarded.Contains(target)
        }).ToList();
    }

    private ContentRewardMutationVo? ResolveBeginResult(OperationIdempotencyBeginResult beginResult)
    {
        return beginResult.Status switch
        {
            OperationIdempotencyBeginStatus.Started => null,
            OperationIdempotencyBeginStatus.Succeeded => ResolveReplay(beginResult),
            OperationIdempotencyBeginStatus.Processing => throw Processing(),
            OperationIdempotencyBeginStatus.Conflict => throw IdempotencyConflict(),
            OperationIdempotencyBeginStatus.InvalidKey => throw InvalidArgument(
                beginResult.Message ?? "幂等键无效"),
            _ => throw ReplayUnavailable()
        };
    }

    private ContentRewardMutationVo ResolveReplay(OperationIdempotencyBeginResult beginResult)
    {
        if (!string.IsNullOrWhiteSpace(beginResult.ErrorCode))
        {
            throw CreateReplayException(
                beginResult.ErrorCode,
                beginResult.ErrorMessage ?? "先前的赞赏请求未成功");
        }

        var response = _idempotencyService.DeserializeResponse<ContentRewardMutationVo>(
            beginResult.ResponsePayload);
        if (response == null || response.VoRewardId <= 0 || string.IsNullOrWhiteSpace(response.VoTransactionNo))
        {
            throw ReplayUnavailable();
        }

        return response;
    }

    private async Task<ContentRewardMutationVo?> TryResolveCommittedResultAsync(
        OperationIdempotencyBeginRequest beginRequest)
    {
        try
        {
            var current = await _idempotencyService.BeginAsync(beginRequest);
            return current.Status == OperationIdempotencyBeginStatus.Succeeded
                ? ResolveReplay(current)
                : null;
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "内容赞赏提交结果二次确认失败。UserId={UserId}, IdempotencyKey={IdempotencyKey}",
                beginRequest.UserId,
                beginRequest.IdempotencyKey);
            return null;
        }
    }

    private async Task CompleteTerminalFailureAsync(long recordId, BusinessException exception)
    {
        await _idempotencyService.CompleteSuccessAsync(new OperationIdempotencyCompletionRequest
        {
            RecordId = recordId,
            ErrorCode = exception.ErrorCode,
            ErrorMessage = exception.Message,
            ResponsePayload = _idempotencyService.SerializeResponse(new ContentRewardMutationVo()),
            ExtendRetentionFromCompletion = true
        });
    }

    private async Task EnsureTargetAvailableAsync(long tenantId, string targetType, long targetId)
    {
        if (targetType == ContentRewardTargetTypes.Post)
        {
            var post = await _postRepository.QueryFirstAsync(item =>
                item.Id == targetId &&
                item.TenantId == tenantId &&
                item.IsPublished &&
                item.IsEnabled &&
                !item.IsDeleted);
            if (post == null)
            {
                throw TargetUnavailable();
            }
            return;
        }

        var comment = await _commentRepository.QueryFirstAsync(item =>
            item.Id == targetId &&
            item.TenantId == tenantId &&
            item.IsEnabled &&
            !item.IsDeleted);
        if (comment == null)
        {
            throw TargetUnavailable();
        }
        var parentPost = await _postRepository.QueryFirstAsync(item =>
            item.Id == comment.PostId &&
            item.TenantId == tenantId &&
            item.IsPublished &&
            item.IsEnabled &&
            !item.IsDeleted);
        if (parentPost == null)
        {
            throw TargetUnavailable();
        }
    }

    private async Task EnsureTargetsAvailableAsync(
        long tenantId,
        IReadOnlyCollection<ContentRewardTargetKey> targets)
    {
        var postTargetIds = targets
            .Where(target => target.TargetType == ContentRewardTargetTypes.Post)
            .Select(target => target.TargetId)
            .Distinct()
            .ToList();
        var commentTargetIds = targets
            .Where(target => target.TargetType == ContentRewardTargetTypes.Comment)
            .Select(target => target.TargetId)
            .Distinct()
            .ToList();
        var comments = commentTargetIds.Count == 0
            ? []
            : await _commentRepository.QueryAsync(comment =>
                comment.TenantId == tenantId &&
                commentTargetIds.Contains(comment.Id) &&
                comment.IsEnabled &&
                !comment.IsDeleted);
        if (comments.Select(comment => comment.Id).Distinct().Count() != commentTargetIds.Count)
        {
            throw TargetUnavailable();
        }

        var requiredPostIds = postTargetIds
            .Concat(comments.Select(comment => comment.PostId))
            .Distinct()
            .ToList();
        var posts = requiredPostIds.Count == 0
            ? []
            : await _postRepository.QueryAsync(post =>
                post.TenantId == tenantId &&
                requiredPostIds.Contains(post.Id) &&
                post.IsPublished &&
                post.IsEnabled &&
                !post.IsDeleted);
        if (posts.Select(post => post.Id).Distinct().Count() != requiredPostIds.Count)
        {
            throw TargetUnavailable();
        }
    }

    private async Task<bool> HasRewardedAsync(
        long tenantId,
        long currentUserId,
        string targetType,
        long targetId)
    {
        if (currentUserId <= 0)
        {
            return false;
        }

        var rewarded = await _contentRewardRepository.QueryRewardedTargetsAsync(
            tenantId,
            currentUserId,
            [new ContentRewardTargetKey(targetType, targetId)]);
        return rewarded.Count > 0;
    }

    private async Task<Dictionary<long, string>> LoadAvatarMapAsync(IReadOnlyCollection<long> userIds)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var attachments = await _attachmentRepository.QueryAsync(attachment =>
            attachment.BusinessType == AttachmentBusinessTypes.Avatar &&
            attachment.BusinessId.HasValue &&
            userIds.Contains(attachment.BusinessId.Value) &&
            attachment.IsEnabled &&
            !attachment.IsDeleted);
        return attachments
            .Where(attachment => attachment.BusinessId.HasValue)
            .GroupBy(attachment => attachment.BusinessId!.Value)
            .ToDictionary(
                group => group.Key,
                group => _attachmentUrlResolver.ResolveAttachmentUrl(
                    group.OrderByDescending(attachment => attachment.Id).First().Id));
    }

    private static bool IsTerminalBusinessFailure(Exception exception) =>
        exception is ContentRewardTargetUnavailableException or
            ContentRewardSelfNotAllowedException or
            ContentRewardAlreadyExistsException or
            ContentRewardInsufficientBalanceException or
            ContentRewardDailyLimitExceededException or
            ContentRewardAccountUnavailableException or
            ContentRewardInteractionUnavailableException or
            ContentRewardIdempotencyStateException;

    private static BusinessException MapRepositoryException(Exception exception) => exception switch
    {
        ContentRewardTargetUnavailableException => TargetUnavailable(),
        ContentRewardSelfNotAllowedException => SelfNotAllowed(),
        ContentRewardAlreadyExistsException => AlreadyRewarded(),
        ContentRewardInsufficientBalanceException => InsufficientBalance(),
        ContentRewardDailyLimitExceededException => DailyLimitExceeded(),
        ContentRewardAccountUnavailableException => AccountUnavailable(),
        ContentRewardInteractionUnavailableException => InteractionUnavailable(),
        ContentRewardIdempotencyStateException => ReplayUnavailable(),
        _ => throw new ArgumentOutOfRangeException(nameof(exception))
    };

    private static string NormalizeTargetType(string? value)
    {
        var normalized = value?.Trim();
        if (normalized == null || !ContentRewardTargetTypes.All.Contains(normalized))
        {
            throw InvalidArgument("首批只支持 Post / Comment");
        }
        return normalized;
    }

    private static string NormalizeReasonCode(string? value)
    {
        var normalized = value?.Trim();
        if (normalized == null || !ContentRewardReasonCodes.All.Contains(normalized))
        {
            throw InvalidArgument("赞赏理由无效");
        }
        return normalized;
    }

    private string NormalizeIdempotencyKey(string? value)
    {
        var normalized = _idempotencyService.NormalizeKey(value);
        if (normalized == null ||
            !normalized.StartsWith("content-reward:", StringComparison.Ordinal) ||
            normalized.Length > 80)
        {
            throw InvalidArgument("幂等键无效");
        }
        return normalized;
    }

    private static BusinessException CreateReplayException(string errorCode, string message) => errorCode switch
    {
        ContentRewardErrorCodes.TargetUnavailable => TargetUnavailable(message),
        ContentRewardErrorCodes.SelfNotAllowed => SelfNotAllowed(message),
        ContentRewardErrorCodes.AlreadyRewarded => AlreadyRewarded(message),
        ContentRewardErrorCodes.InsufficientBalance => InsufficientBalance(message),
        ContentRewardErrorCodes.DailyLimitExceeded => DailyLimitExceeded(message),
        ContentRewardErrorCodes.AccountUnavailable => AccountUnavailable(message),
        "UserBlock.InteractionUnavailable" => InteractionUnavailable(message),
        _ => ReplayUnavailable()
    };

    private static BusinessException InvalidArgument(string message) => new(
        message,
        StatusCodes.Status400BadRequest,
        ContentRewardErrorCodes.InvalidArgument,
        "error.content_reward.invalid_argument");

    private static BusinessException TargetUnavailable(string message = "目标内容不存在或当前不可访问") => new(
        message,
        StatusCodes.Status404NotFound,
        ContentRewardErrorCodes.TargetUnavailable,
        "error.content_reward.target_unavailable");

    private static BusinessException Unavailable() => new(
        "内容赞赏暂未开放",
        StatusCodes.Status503ServiceUnavailable,
        ContentRewardErrorCodes.Unavailable,
        "error.content_reward.unavailable");

    private static BusinessException SelfNotAllowed(string message = "不能赞赏自己的内容") => new(
        message,
        StatusCodes.Status409Conflict,
        ContentRewardErrorCodes.SelfNotAllowed,
        "error.content_reward.self_not_allowed");

    private static BusinessException AlreadyRewarded(string message = "已经送过胡萝卜") => new(
        message,
        StatusCodes.Status409Conflict,
        ContentRewardErrorCodes.AlreadyRewarded,
        "error.content_reward.already_rewarded");

    private static BusinessException InsufficientBalance(string message = "可用胡萝卜余额不足") => new(
        message,
        StatusCodes.Status409Conflict,
        ContentRewardErrorCodes.InsufficientBalance,
        "error.content_reward.insufficient_balance");

    private static BusinessException DailyLimitExceeded(string message = "已达到今日赞赏上限") => new(
        message,
        StatusCodes.Status409Conflict,
        ContentRewardErrorCodes.DailyLimitExceeded,
        "error.content_reward.daily_limit_exceeded");

    private static BusinessException AccountUnavailable(string message = "发送者或接收者账号当前不可用") => new(
        message,
        StatusCodes.Status409Conflict,
        ContentRewardErrorCodes.AccountUnavailable,
        "error.content_reward.account_unavailable");

    private static BusinessException InteractionUnavailable(string message = "当前无法与该用户互动") => new(
        message,
        StatusCodes.Status409Conflict,
        "UserBlock.InteractionUnavailable",
        "error.user_block.interaction_unavailable");

    private static BusinessException RelationshipTemporarilyUnavailable(Exception innerException) => new(
        "用户关系服务暂时不可用，请稍后重试",
        innerException,
        StatusCodes.Status503ServiceUnavailable,
        "UserBlock.RelationshipTemporarilyUnavailable",
        "error.user_block.relationship_temporarily_unavailable");

    private static BusinessException Processing() => new(
        "请求处理中，请稍后重试",
        StatusCodes.Status409Conflict,
        ContentRewardErrorCodes.Processing,
        "error.content_reward.processing");

    private static BusinessException IdempotencyConflict() => new(
        "幂等键已被不同请求使用",
        StatusCodes.Status409Conflict,
        ContentRewardErrorCodes.IdempotencyConflict,
        "error.content_reward.idempotency_conflict");

    private static BusinessException ConcurrentConflict() => new(
        "数据库并发冲突，请稍后重试",
        StatusCodes.Status503ServiceUnavailable,
        ContentRewardErrorCodes.ConcurrentConflict,
        "error.content_reward.concurrent_conflict");

    private static BusinessException ReplayUnavailable() => new(
        "无法确认先前赞赏请求的结果，请稍后重试",
        StatusCodes.Status409Conflict,
        ContentRewardErrorCodes.ReplayUnavailable,
        "error.content_reward.replay_unavailable");
}
