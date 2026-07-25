using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Radish.Common.Exceptions;
using Radish.Common.OptionTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service.Base;
using SqlSugar;

namespace Radish.Service;

/// <summary>用户关系链服务实现</summary>
public class UserFollowService : BaseService<UserFollow, UserFollowVo>, IUserFollowService
{
    private readonly IUserFollowRepository _userFollowRepository;
    private readonly IBaseRepository<User> _userRepository;
    private readonly IBaseRepository<Attachment> _attachmentRepository;
    private readonly IPostService _postService;
    private readonly IReliableOutboxService? _reliableOutboxService;
    private readonly ILogger<UserFollowService> _logger;
    private readonly FeedDistributionOptions _feedDistributionOptions;
    private readonly IAttachmentUrlResolver _attachmentUrlResolver;
    private readonly IUserInteractionPolicyService _interactionPolicyService;

    public UserFollowService(
        IMapper mapper,
        IUserFollowRepository baseRepository,
        IBaseRepository<User> userRepository,
        IPostService postService,
        IBaseRepository<Attachment> attachmentRepository,
        INotificationService notificationService,
        ILogger<UserFollowService> logger,
        IOptions<FeedDistributionOptions> feedDistributionOptions,
        IAttachmentUrlResolver attachmentUrlResolver,
        IUserInteractionPolicyService interactionPolicyService,
        IReliableOutboxService? reliableOutboxService = null)
        : base(mapper, baseRepository)
    {
        _userFollowRepository = baseRepository;
        _userRepository = userRepository;
        _attachmentRepository = attachmentRepository;
        _postService = postService;
        _reliableOutboxService = reliableOutboxService;
        _logger = logger;
        _feedDistributionOptions = feedDistributionOptions.Value;
        _attachmentUrlResolver = attachmentUrlResolver;
        _interactionPolicyService = interactionPolicyService;
    }

    public async Task<bool> FollowAsync(long followerUserId, long targetUserId, long tenantId, string? operatorName)
    {
        if (followerUserId <= 0 || targetUserId <= 0)
        {
            throw new ArgumentException("用户 ID 无效");
        }

        if (followerUserId == targetUserId)
        {
            throw new ArgumentException("不能关注自己");
        }

        var normalizedTenantId = tenantId > 0 ? tenantId : 0;
        var targetUser = await _userRepository.QueryFirstAsync(u =>
            u.Id == targetUserId &&
            u.TenantId == normalizedTenantId &&
            u.IsEnable &&
            !u.IsDeleted);
        if (targetUser == null)
        {
            throw new InvalidOperationException("目标用户不存在或不可用");
        }

        await _interactionPolicyService.EnsureCanInteractAsync(
            normalizedTenantId,
            followerUserId,
            targetUserId);
        var normalizedOperator = string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName.Trim();
        var now = DateTime.UtcNow;
        bool changed;
        try
        {
            changed = await _userFollowRepository.FollowAsync(
                normalizedTenantId,
                followerUserId,
                targetUserId,
                normalizedOperator,
                now);
        }
        catch (UserFollowInteractionBlockedException)
        {
            throw new BusinessException(
                "当前无法与该用户互动",
                StatusCodes.Status409Conflict,
                "UserBlock.InteractionUnavailable",
                "error.user_block.interaction_unavailable");
        }

        if (changed)
        {
            await TrySendFollowNotificationAsync(followerUserId, targetUser, normalizedTenantId, now);
        }

        return changed;
    }

    public async Task<bool> UnfollowAsync(
        long followerUserId,
        long targetUserId,
        long tenantId,
        string? operatorName)
    {
        if (followerUserId <= 0 || targetUserId <= 0 || followerUserId == targetUserId)
        {
            return false;
        }

        var existing = await _userFollowRepository.QueryFirstAsync(f =>
            f.TenantId == Math.Max(0, tenantId) &&
            f.FollowerUserId == followerUserId &&
            f.FollowingUserId == targetUserId &&
            !f.IsDeleted);
        if (existing == null)
        {
            return false;
        }

        var normalizedOperator = string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName.Trim();
        return await _userFollowRepository.SoftDeleteByIdAsync(existing.Id, normalizedOperator);
    }

    public Task<UserFollowStatusVo> GetFollowStatusAsync(long currentUserId, long targetUserId) =>
        GetFollowStatusAsync(currentUserId, targetUserId, 0);

    public async Task<UserFollowStatusVo> GetFollowStatusAsync(
        long currentUserId,
        long targetUserId,
        long tenantId)
    {
        if (targetUserId <= 0)
        {
            return new UserFollowStatusVo();
        }

        var followerCount = await _userFollowRepository.QueryCountAsync(f =>
            f.TenantId == Math.Max(0, tenantId) && f.FollowingUserId == targetUserId && !f.IsDeleted);
        var followingCount = await _userFollowRepository.QueryCountAsync(f =>
            f.TenantId == Math.Max(0, tenantId) && f.FollowerUserId == targetUserId && !f.IsDeleted);

        if (currentUserId <= 0 || currentUserId == targetUserId)
        {
            return new UserFollowStatusVo
            {
                VoTargetUserId = targetUserId,
                VoFollowerCount = followerCount,
                VoFollowingCount = followingCount,
                VoIsFollowing = false,
                VoIsFollower = false,
                VoCanFollow = currentUserId > 0 && currentUserId != targetUserId,
                VoCanDirectMessage = currentUserId > 0 && currentUserId != targetUserId,
                VoCanInteract = currentUserId > 0 && currentUserId != targetUserId
            };
        }

        var policy = await _interactionPolicyService.GetSnapshotAsync(
            tenantId,
            currentUserId,
            targetUserId);
        var isFollowing = await _userFollowRepository.QueryExistsAsync(f =>
            f.TenantId == Math.Max(0, tenantId) &&
            f.FollowerUserId == currentUserId &&
            f.FollowingUserId == targetUserId &&
            !f.IsDeleted);

        var isFollower = await _userFollowRepository.QueryExistsAsync(f =>
            f.TenantId == Math.Max(0, tenantId) &&
            f.FollowerUserId == targetUserId &&
            f.FollowingUserId == currentUserId &&
            !f.IsDeleted);

        return new UserFollowStatusVo
        {
            VoTargetUserId = targetUserId,
            VoFollowerCount = followerCount,
            VoFollowingCount = followingCount,
            VoIsFollowing = isFollowing,
            VoIsFollower = isFollower,
            VoCanFollow = policy.CanInteract,
            VoCanDirectMessage = policy.CanInteract,
            VoCanInteract = policy.CanInteract,
            VoInteractionUnavailable = policy.HasInteractionBarrier,
            VoIsBlockedByCurrentUser = policy.IsBlockedByCurrentUser
        };
    }

    public Task<VoPagedResult<UserFollowUserVo>> GetMyFollowersAsync(
        long userId,
        int pageIndex,
        int pageSize) =>
        GetMyFollowersAsync(userId, 0, pageIndex, pageSize);

    public async Task<VoPagedResult<UserFollowUserVo>> GetMyFollowersAsync(
        long userId,
        long tenantId,
        int pageIndex,
        int pageSize)
    {
        var safePageIndex = NormalizePageIndex(pageIndex);
        var safePageSize = NormalizePageSize(pageSize);

        var barrierUserIds = (await _interactionPolicyService.GetBarrierUserIdsAsync(tenantId, userId)).ToList();
        var (relations, totalCount) = await _userFollowRepository.QueryPageAsync(
            f => f.TenantId == Math.Max(0, tenantId) &&
                 f.FollowingUserId == userId &&
                 !barrierUserIds.Contains(f.FollowerUserId) &&
                 !f.IsDeleted,
            safePageIndex,
            safePageSize,
            f => f.FollowTime,
            OrderByType.Desc);

        if (relations.Count == 0)
        {
            return BuildEmptyPagedResult<UserFollowUserVo>(safePageIndex, safePageSize, totalCount);
        }

        var userIds = relations.Select(r => r.FollowerUserId).Distinct().ToList();
        var users = await _userRepository.QueryAsync(u =>
            u.TenantId == Math.Max(0, tenantId) &&
            userIds.Contains(u.Id) &&
            u.IsEnable &&
            !u.IsDeleted);
        await EnsureFollowUserPublicIdsAsync(users);
        var userMap = users.ToDictionary(u => u.Id, u => u);
        var avatarMap = await LoadAvatarUrlMapAsync(userIds);

        var myFollowingIds = await _userFollowRepository.QueryDistinctAsync(
            f => f.FollowingUserId,
            f => f.TenantId == Math.Max(0, tenantId) &&
                 f.FollowerUserId == userId &&
                 userIds.Contains(f.FollowingUserId) &&
                 !f.IsDeleted);
        var myFollowingSet = myFollowingIds.ToHashSet();

        var items = relations
            .Where(r => userMap.ContainsKey(r.FollowerUserId))
            .Select(r => MapFollowUserVo(
                userMap[r.FollowerUserId],
                avatarMap.GetValueOrDefault(r.FollowerUserId),
                r.FollowTime,
                myFollowingSet.Contains(r.FollowerUserId)))
            .ToList();

        return new VoPagedResult<UserFollowUserVo>
        {
            VoItems = items,
            VoTotal = totalCount,
            VoPageIndex = safePageIndex,
            VoPageSize = safePageSize
        };
    }

    public Task<VoPagedResult<UserFollowUserVo>> GetMyFollowingAsync(
        long userId,
        int pageIndex,
        int pageSize) =>
        GetMyFollowingAsync(userId, 0, pageIndex, pageSize);

    public async Task<VoPagedResult<UserFollowUserVo>> GetMyFollowingAsync(
        long userId,
        long tenantId,
        int pageIndex,
        int pageSize)
    {
        var safePageIndex = NormalizePageIndex(pageIndex);
        var safePageSize = NormalizePageSize(pageSize);

        var barrierUserIds = (await _interactionPolicyService.GetBarrierUserIdsAsync(tenantId, userId)).ToList();
        var (relations, totalCount) = await _userFollowRepository.QueryPageAsync(
            f => f.TenantId == Math.Max(0, tenantId) &&
                 f.FollowerUserId == userId &&
                 !barrierUserIds.Contains(f.FollowingUserId) &&
                 !f.IsDeleted,
            safePageIndex,
            safePageSize,
            f => f.FollowTime,
            OrderByType.Desc);

        if (relations.Count == 0)
        {
            return BuildEmptyPagedResult<UserFollowUserVo>(safePageIndex, safePageSize, totalCount);
        }

        var userIds = relations.Select(r => r.FollowingUserId).Distinct().ToList();
        var users = await _userRepository.QueryAsync(u =>
            u.TenantId == Math.Max(0, tenantId) &&
            userIds.Contains(u.Id) &&
            u.IsEnable &&
            !u.IsDeleted);
        await EnsureFollowUserPublicIdsAsync(users);
        var userMap = users.ToDictionary(u => u.Id, u => u);
        var avatarMap = await LoadAvatarUrlMapAsync(userIds);

        var followedBackIds = await _userFollowRepository.QueryDistinctAsync(
            f => f.FollowerUserId,
            f => f.TenantId == Math.Max(0, tenantId) &&
                 f.FollowingUserId == userId &&
                 userIds.Contains(f.FollowerUserId) &&
                 !f.IsDeleted);
        var followedBackSet = followedBackIds.ToHashSet();

        var items = relations
            .Where(r => userMap.ContainsKey(r.FollowingUserId))
            .Select(r => MapFollowUserVo(
                userMap[r.FollowingUserId],
                avatarMap.GetValueOrDefault(r.FollowingUserId),
                r.FollowTime,
                followedBackSet.Contains(r.FollowingUserId)))
            .ToList();

        return new VoPagedResult<UserFollowUserVo>
        {
            VoItems = items,
            VoTotal = totalCount,
            VoPageIndex = safePageIndex,
            VoPageSize = safePageSize
        };
    }

    public Task<VoPagedResult<PostVo>> GetMyFollowingFeedAsync(
        long userId,
        int pageIndex,
        int pageSize) =>
        GetMyFollowingFeedAsync(userId, 0, pageIndex, pageSize);

    public async Task<VoPagedResult<PostVo>> GetMyFollowingFeedAsync(
        long userId,
        long tenantId,
        int pageIndex,
        int pageSize)
    {
        var safePageIndex = NormalizePageIndex(pageIndex);
        var safePageSize = NormalizePageSize(pageSize);

        var barrierUserIds = (await _interactionPolicyService.GetBarrierUserIdsAsync(tenantId, userId)).ToList();
        var followingIds = await _userFollowRepository.QueryDistinctAsync(
            f => f.FollowingUserId,
            f => f.TenantId == Math.Max(0, tenantId) &&
                 f.FollowerUserId == userId &&
                 !barrierUserIds.Contains(f.FollowingUserId) &&
                 !f.IsDeleted);

        if (followingIds.Count == 0)
        {
            return BuildEmptyPagedResult<PostVo>(safePageIndex, safePageSize, 0);
        }

        var (posts, totalCount) = await _postService.QueryPageAsync(
            p => p.TenantId == Math.Max(0, tenantId) &&
                 followingIds.Contains(p.AuthorId) &&
                 p.IsPublished &&
                 !p.IsDeleted,
            safePageIndex,
            safePageSize,
            p => p.CreateTime,
            OrderByType.Desc);

        return new VoPagedResult<PostVo>
        {
            VoItems = posts,
            VoTotal = totalCount,
            VoPageIndex = safePageIndex,
            VoPageSize = safePageSize
        };
    }

    public Task<VoPagedResult<PostVo>> GetMyDistributionFeedAsync(
        long userId,
        string streamType,
        int pageIndex,
        int pageSize) =>
        GetMyDistributionFeedAsync(userId, 0, streamType, pageIndex, pageSize);

    public async Task<VoPagedResult<PostVo>> GetMyDistributionFeedAsync(
        long userId,
        long tenantId,
        string streamType,
        int pageIndex,
        int pageSize)
    {
        var safePageIndex = NormalizePageIndex(pageIndex);
        var safePageSize = NormalizePageSize(pageSize);
        var normalizedStreamType = NormalizeStreamType(streamType);

        var barrierUserIds = (await _interactionPolicyService.GetBarrierUserIdsAsync(tenantId, userId)).ToList();
        if (normalizedStreamType == "newest")
        {
            var (newestPosts, newestTotal) = await _postService.QueryPageAsync(
                p => p.TenantId == Math.Max(0, tenantId) &&
                     p.IsPublished &&
                     !barrierUserIds.Contains(p.AuthorId) &&
                     !p.IsDeleted,
                safePageIndex,
                safePageSize,
                p => new { p.IsTop, p.CreateTime },
                OrderByType.Desc);

            return new VoPagedResult<PostVo>
            {
                VoItems = newestPosts,
                VoTotal = newestTotal,
                VoPageIndex = safePageIndex,
                VoPageSize = safePageSize
            };
        }

        var candidates = (await LoadDistributionCandidatesAsync(tenantId))
            .Where(post => !barrierUserIds.Contains(post.VoAuthorId))
            .ToList();
        if (candidates.Count == 0)
        {
            return BuildEmptyPagedResult<PostVo>(safePageIndex, safePageSize, 0);
        }

        if (normalizedStreamType == "hot")
        {
            var sortedHot = candidates
                .OrderByDescending(p => p.VoIsTop)
                .ThenByDescending(CalculateHotScore)
                .ThenByDescending(p => p.VoCreateTime)
                .ToList();

            return BuildPostPagedResult(sortedHot, safePageIndex, safePageSize);
        }

        var followingIds = await _userFollowRepository.QueryDistinctAsync(
            f => f.FollowingUserId,
            f => f.TenantId == Math.Max(0, tenantId) &&
                 f.FollowerUserId == userId &&
                 !barrierUserIds.Contains(f.FollowingUserId) &&
                 !f.IsDeleted);
        var followingSet = followingIds.ToHashSet();

        var sortedRecommend = candidates
            .OrderByDescending(p => p.VoIsTop)
            .ThenByDescending(p => CalculateRecommendScore(p, followingSet))
            .ThenByDescending(p => p.VoCreateTime)
            .ToList();

        return BuildPostPagedResult(sortedRecommend, safePageIndex, safePageSize);
    }

    public Task<UserFollowSummaryVo> GetMyFollowSummaryAsync(long userId) =>
        GetMyFollowSummaryAsync(userId, 0);

    public async Task<UserFollowSummaryVo> GetMyFollowSummaryAsync(long userId, long tenantId)
    {
        var followerCount = await _userFollowRepository.QueryCountAsync(f =>
            f.TenantId == Math.Max(0, tenantId) && f.FollowingUserId == userId && !f.IsDeleted);
        var followingCount = await _userFollowRepository.QueryCountAsync(f =>
            f.TenantId == Math.Max(0, tenantId) && f.FollowerUserId == userId && !f.IsDeleted);

        return new UserFollowSummaryVo
        {
            VoFollowerCount = followerCount,
            VoFollowingCount = followingCount
        };
    }

    private static VoPagedResult<T> BuildEmptyPagedResult<T>(int pageIndex, int pageSize, int total)
    {
        return new VoPagedResult<T>
        {
            VoItems = new List<T>(),
            VoTotal = total,
            VoPageIndex = pageIndex,
            VoPageSize = pageSize
        };
    }

    private async Task<Dictionary<long, string>> LoadAvatarUrlMapAsync(IReadOnlyCollection<long> userIds)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<long, string>();
        }

        var attachments = await _attachmentRepository.QueryAsync(attachment =>
            attachment.BusinessType == "Avatar" &&
            attachment.BusinessId.HasValue &&
            userIds.Contains(attachment.BusinessId.Value) &&
            attachment.IsEnabled &&
            !attachment.IsDeleted);

        return attachments
            .Where(attachment => attachment.BusinessId.HasValue)
            .OrderByDescending(attachment => attachment.CreateTime)
            .GroupBy(attachment => attachment.BusinessId!.Value)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var attachment = group.First();
                    return _attachmentUrlResolver.ResolveAttachmentUrl(attachment.Id);
                });
    }

    private async Task TrySendFollowNotificationAsync(
        long followerUserId,
        User targetUser,
        long tenantId,
        DateTime occurredAtUtc)
    {
        if (followerUserId <= 0 || targetUser.Id <= 0 || followerUserId == targetUser.Id)
        {
            return;
        }

        try
        {
            var follower = await _userRepository.QueryFirstAsync(u =>
                u.Id == followerUserId &&
                u.TenantId == Math.Max(0, tenantId) &&
                u.IsEnable &&
                !u.IsDeleted);
            var followerName = follower?.UserName?.Trim();
            if (string.IsNullOrWhiteSpace(followerName))
            {
                followerName = $"User-{followerUserId}";
            }

            var avatarMap = await LoadAvatarUrlMapAsync(new[] { followerUserId });
            var notificationId = SnowFlakeSingle.Instance.NextId();
            var notification = new CreateNotificationDto
            {
                NotificationId = notificationId,
                BusinessKey = $"notification:followed:follower:{followerUserId}:target:{targetUser.Id}:event:{notificationId}",
                Type = NotificationType.Followed,
                Title = "新增粉丝",
                Content = $"{followerName} 关注了你",
                Priority = (int)NotificationPriority.Normal,
                BusinessType = BusinessType.User,
                BusinessId = targetUser.Id,
                TriggerId = followerUserId,
                TriggerName = followerName,
                TriggerAvatar = avatarMap.GetValueOrDefault(followerUserId),
                ReceiverUserIds = new List<long> { targetUser.Id },
                TenantId = tenantId,
                TemplateArguments = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["actorName"] = followerName
                },
                TargetKind = NotificationTargetKind.UserProfile,
                Target = new NotificationTargetData
                {
                    UserId = followerUserId,
                    UserPublicId = follower?.PublicId
                },
                OccurredAtUtc = occurredAtUtc
            };
            var reliableOutboxService = _reliableOutboxService
                ?? throw new InvalidOperationException("可靠 Outbox 服务未注册");
            await reliableOutboxService.AddAsync(
                ReliableOutboxSources.Main,
                tenantId,
                ReliableTaskTypes.NotificationRequested,
                $"task:notification:followed:{notificationId}",
                "UserFollow",
                $"{followerUserId}:{targetUser.Id}",
                new NotificationRequestedTaskPayload(notification),
                occurredAtUtc);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[UserFollowService] 发送关注通知失败，FollowerUserId={FollowerUserId}, TargetUserId={TargetUserId}",
                followerUserId,
                targetUser.Id);
            throw;
        }
    }

    private static UserFollowUserVo MapFollowUserVo(User user, string? avatarUrl, DateTime followTime, bool isMutualFollow)
    {
        return new UserFollowUserVo
        {
            VoUserId = user.Id,
            VoPublicId = string.IsNullOrWhiteSpace(user.PublicId) ? null : user.PublicId.Trim(),
            VoPublicIndex = user.PublicIndex,
            VoUserName = User.NormalizeDisplayName(user.UserName, user.Id),
            VoDisplayName = User.NormalizeDisplayName(user.UserName, user.Id),
            VoDisplayHandle = User.BuildDisplayHandle(user.UserName, user.PublicIndex, user.Id),
            VoAvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl,
            VoIsMutualFollow = isMutualFollow,
            VoFollowTime = followTime
        };
    }

    private async Task EnsureFollowUserPublicIdsAsync(List<User> users)
    {
        foreach (var user in users)
        {
            var missingPublicId = string.IsNullOrWhiteSpace(user.PublicId);
            var missingPublicIndex = !User.HasAssignedPublicIndex(user.PublicIndex);

            if (!missingPublicId)
            {
                user.PublicId = user.PublicId?.Trim();
            }

            if (!missingPublicId && !missingPublicIndex)
            {
                continue;
            }

            var publicId = missingPublicId ? User.EnsurePublicId(user.PublicId) : user.PublicId;
            var publicIndex = missingPublicIndex ? await AllocateNextPublicIndexAsync() : user.PublicIndex;
            var affectedRows = await _userRepository.UpdateColumnsAsync(
                item => new User
                {
                    PublicId = publicId,
                    PublicIndex = publicIndex,
                    UpdateTime = DateTime.Now
                },
                item => item.Id == user.Id &&
                        !item.IsDeleted &&
                        ((missingPublicId && (item.PublicId == null || item.PublicId == string.Empty)) ||
                         (missingPublicIndex && (item.PublicIndex == null || item.PublicIndex <= 0))));

            if (affectedRows > 0)
            {
                user.PublicId = publicId;
                user.PublicIndex = publicIndex;
                continue;
            }

            var refreshedUser = await _userRepository.QueryByIdAsync(user.Id);
            if (!string.IsNullOrWhiteSpace(refreshedUser?.PublicId))
            {
                user.PublicId = refreshedUser.PublicId.Trim();
            }

            if (User.HasAssignedPublicIndex(refreshedUser?.PublicIndex))
            {
                user.PublicIndex = refreshedUser!.PublicIndex;
            }
        }
    }

    private async Task<long> AllocateNextPublicIndexAsync()
    {
        var maxPublicIndexTask = _userRepository.QueryMaxAsync<long?>(
            item => item.PublicIndex,
            item => item.PublicIndex >= User.PublicIndexStart);
        var maxPublicIndex = maxPublicIndexTask == null ? null : await maxPublicIndexTask;

        return maxPublicIndex.GetValueOrDefault(User.PublicIndexStart - 1) + 1;
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

    private static string NormalizeStreamType(string? streamType)
    {
        if (string.IsNullOrWhiteSpace(streamType))
        {
            return "recommend";
        }

        return streamType.Trim().ToLowerInvariant() switch
        {
            "hot" or "hottest" => "hot",
            "newest" => "newest",
            _ => "recommend"
        };
    }

    private async Task<List<PostVo>> LoadDistributionCandidatesAsync(long tenantId)
    {
        var maxCandidateCount = NormalizeMaxCandidateCount(_feedDistributionOptions.MaxCandidateCount);
        var hasWindow = _feedDistributionOptions.CandidateWindowDays > 0;
        var windowStart = DateTime.Now.AddDays(-_feedDistributionOptions.CandidateWindowDays);

        return await _postService.QueryWithOrderAsync(
            p => p.TenantId == Math.Max(0, tenantId) &&
                 p.IsPublished &&
                 !p.IsDeleted &&
                 (!hasWindow || p.CreateTime >= windowStart),
            p => p.CreateTime,
            OrderByType.Desc,
            maxCandidateCount);
    }

    private static int NormalizeMaxCandidateCount(int maxCandidateCount)
    {
        if (maxCandidateCount <= 0)
        {
            return 500;
        }

        return Math.Min(maxCandidateCount, 2000);
    }

    private decimal CalculateHotScore(PostVo post)
    {
        return (post.VoViewCount * _feedDistributionOptions.Hot.ViewWeight)
             + (post.VoLikeCount * _feedDistributionOptions.Hot.LikeWeight)
             + (post.VoCommentCount * _feedDistributionOptions.Hot.CommentWeight);
    }

    private decimal CalculateRecommendScore(PostVo post, HashSet<long> followingSet)
    {
        var score = CalculateHotScore(post);

        if (followingSet.Contains(post.VoAuthorId))
        {
            score += _feedDistributionOptions.Recommend.FollowingAuthorBoost;
        }

        var ageHours = Math.Max(0m, (decimal)(DateTime.Now - post.VoCreateTime).TotalHours);
        var halfLifeHours = _feedDistributionOptions.Recommend.FreshnessHalfLifeHours <= 0
            ? 24m
            : _feedDistributionOptions.Recommend.FreshnessHalfLifeHours;
        var freshnessBoost = _feedDistributionOptions.Recommend.FreshnessMaxBoost / (1m + ageHours / halfLifeHours);

        return score + freshnessBoost;
    }

    private static VoPagedResult<PostVo> BuildPostPagedResult(List<PostVo> sortedPosts, int pageIndex, int pageSize)
    {
        var total = sortedPosts.Count;
        var items = sortedPosts
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new VoPagedResult<PostVo>
        {
            VoItems = items,
            VoTotal = total,
            VoPageIndex = pageIndex,
            VoPageSize = pageSize
        };
    }
}
