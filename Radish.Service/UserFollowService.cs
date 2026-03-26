using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Radish.Common.OptionTool;
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
    private readonly IBaseRepository<UserFollow> _userFollowRepository;
    private readonly IBaseRepository<User> _userRepository;
    private readonly IBaseRepository<Attachment> _attachmentRepository;
    private readonly IPostService _postService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<UserFollowService> _logger;
    private readonly FeedDistributionOptions _feedDistributionOptions;

    public UserFollowService(
        IMapper mapper,
        IBaseRepository<UserFollow> baseRepository,
        IBaseRepository<User> userRepository,
        IPostService postService,
        IBaseRepository<Attachment> attachmentRepository,
        INotificationService notificationService,
        ILogger<UserFollowService> logger,
        IOptions<FeedDistributionOptions> feedDistributionOptions)
        : base(mapper, baseRepository)
    {
        _userFollowRepository = baseRepository;
        _userRepository = userRepository;
        _attachmentRepository = attachmentRepository;
        _postService = postService;
        _notificationService = notificationService;
        _logger = logger;
        _feedDistributionOptions = feedDistributionOptions.Value;
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

        var targetUser = await _userRepository.QueryFirstAsync(u => u.Id == targetUserId && u.IsEnable && !u.IsDeleted);
        if (targetUser == null)
        {
            throw new InvalidOperationException("目标用户不存在或不可用");
        }

        var existing = await _userFollowRepository.QueryFirstAsync(f =>
            f.FollowerUserId == followerUserId && f.FollowingUserId == targetUserId);

        var normalizedOperator = string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName.Trim();
        var normalizedTenantId = tenantId > 0 ? tenantId : 0;
        var now = DateTime.UtcNow;

        if (existing != null)
        {
            if (!existing.IsDeleted)
            {
                return false;
            }

            existing.IsDeleted = false;
            existing.DeletedAt = null;
            existing.DeletedBy = null;
            existing.FollowTime = now;
            existing.ModifyTime = now;
            existing.ModifyBy = normalizedOperator;
            existing.ModifyId = followerUserId;
            if (existing.TenantId <= 0)
            {
                existing.TenantId = normalizedTenantId;
            }

            var restored = await _userFollowRepository.UpdateAsync(existing);
            if (restored)
            {
                await TrySendFollowNotificationAsync(followerUserId, targetUser, normalizedTenantId);
            }

            return restored;
        }

        await _userFollowRepository.AddAsync(new UserFollow
        {
            FollowerUserId = followerUserId,
            FollowingUserId = targetUserId,
            FollowTime = now,
            TenantId = normalizedTenantId,
            CreateTime = now,
            CreateBy = normalizedOperator,
            CreateId = followerUserId
        });

        await TrySendFollowNotificationAsync(followerUserId, targetUser, normalizedTenantId);

        return true;
    }

    public async Task<bool> UnfollowAsync(long followerUserId, long targetUserId, string? operatorName)
    {
        if (followerUserId <= 0 || targetUserId <= 0 || followerUserId == targetUserId)
        {
            return false;
        }

        var existing = await _userFollowRepository.QueryFirstAsync(f =>
            f.FollowerUserId == followerUserId && f.FollowingUserId == targetUserId && !f.IsDeleted);
        if (existing == null)
        {
            return false;
        }

        var normalizedOperator = string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName.Trim();
        return await _userFollowRepository.SoftDeleteByIdAsync(existing.Id, normalizedOperator);
    }

    public async Task<UserFollowStatusVo> GetFollowStatusAsync(long currentUserId, long targetUserId)
    {
        if (targetUserId <= 0)
        {
            return new UserFollowStatusVo();
        }

        var followerCount = await _userFollowRepository.QueryCountAsync(f =>
            f.FollowingUserId == targetUserId && !f.IsDeleted);
        var followingCount = await _userFollowRepository.QueryCountAsync(f =>
            f.FollowerUserId == targetUserId && !f.IsDeleted);

        if (currentUserId <= 0 || currentUserId == targetUserId)
        {
            return new UserFollowStatusVo
            {
                VoTargetUserId = targetUserId,
                VoFollowerCount = followerCount,
                VoFollowingCount = followingCount,
                VoIsFollowing = false,
                VoIsFollower = false
            };
        }

        var isFollowing = await _userFollowRepository.QueryExistsAsync(f =>
            f.FollowerUserId == currentUserId &&
            f.FollowingUserId == targetUserId &&
            !f.IsDeleted);

        var isFollower = await _userFollowRepository.QueryExistsAsync(f =>
            f.FollowerUserId == targetUserId &&
            f.FollowingUserId == currentUserId &&
            !f.IsDeleted);

        return new UserFollowStatusVo
        {
            VoTargetUserId = targetUserId,
            VoFollowerCount = followerCount,
            VoFollowingCount = followingCount,
            VoIsFollowing = isFollowing,
            VoIsFollower = isFollower
        };
    }

    public async Task<VoPagedResult<UserFollowUserVo>> GetMyFollowersAsync(long userId, int pageIndex, int pageSize)
    {
        var safePageIndex = NormalizePageIndex(pageIndex);
        var safePageSize = NormalizePageSize(pageSize);

        var (relations, totalCount) = await _userFollowRepository.QueryPageAsync(
            f => f.FollowingUserId == userId && !f.IsDeleted,
            safePageIndex,
            safePageSize,
            f => f.FollowTime,
            OrderByType.Desc);

        if (relations.Count == 0)
        {
            return BuildEmptyPagedResult<UserFollowUserVo>(safePageIndex, safePageSize, totalCount);
        }

        var userIds = relations.Select(r => r.FollowerUserId).Distinct().ToList();
        var users = await _userRepository.QueryAsync(u => userIds.Contains(u.Id) && u.IsEnable && !u.IsDeleted);
        var userMap = users.ToDictionary(u => u.Id, u => u);
        var avatarMap = await LoadAvatarUrlMapAsync(userIds);

        var myFollowingIds = await _userFollowRepository.QueryDistinctAsync(
            f => f.FollowingUserId,
            f => f.FollowerUserId == userId && userIds.Contains(f.FollowingUserId) && !f.IsDeleted);
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

    public async Task<VoPagedResult<UserFollowUserVo>> GetMyFollowingAsync(long userId, int pageIndex, int pageSize)
    {
        var safePageIndex = NormalizePageIndex(pageIndex);
        var safePageSize = NormalizePageSize(pageSize);

        var (relations, totalCount) = await _userFollowRepository.QueryPageAsync(
            f => f.FollowerUserId == userId && !f.IsDeleted,
            safePageIndex,
            safePageSize,
            f => f.FollowTime,
            OrderByType.Desc);

        if (relations.Count == 0)
        {
            return BuildEmptyPagedResult<UserFollowUserVo>(safePageIndex, safePageSize, totalCount);
        }

        var userIds = relations.Select(r => r.FollowingUserId).Distinct().ToList();
        var users = await _userRepository.QueryAsync(u => userIds.Contains(u.Id) && u.IsEnable && !u.IsDeleted);
        var userMap = users.ToDictionary(u => u.Id, u => u);
        var avatarMap = await LoadAvatarUrlMapAsync(userIds);

        var followedBackIds = await _userFollowRepository.QueryDistinctAsync(
            f => f.FollowerUserId,
            f => f.FollowingUserId == userId && userIds.Contains(f.FollowerUserId) && !f.IsDeleted);
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

    public async Task<VoPagedResult<PostVo>> GetMyFollowingFeedAsync(long userId, int pageIndex, int pageSize)
    {
        var safePageIndex = NormalizePageIndex(pageIndex);
        var safePageSize = NormalizePageSize(pageSize);

        var followingIds = await _userFollowRepository.QueryDistinctAsync(
            f => f.FollowingUserId,
            f => f.FollowerUserId == userId && !f.IsDeleted);

        if (followingIds.Count == 0)
        {
            return BuildEmptyPagedResult<PostVo>(safePageIndex, safePageSize, 0);
        }

        var (posts, totalCount) = await _postService.QueryPageAsync(
            p => followingIds.Contains(p.AuthorId) && p.IsPublished && !p.IsDeleted,
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

    public async Task<VoPagedResult<PostVo>> GetMyDistributionFeedAsync(long userId, string streamType, int pageIndex, int pageSize)
    {
        var safePageIndex = NormalizePageIndex(pageIndex);
        var safePageSize = NormalizePageSize(pageSize);
        var normalizedStreamType = NormalizeStreamType(streamType);

        if (normalizedStreamType == "newest")
        {
            var (newestPosts, newestTotal) = await _postService.QueryPageAsync(
                p => p.IsPublished && !p.IsDeleted,
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

        var candidates = await LoadDistributionCandidatesAsync();
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
            f => f.FollowerUserId == userId && !f.IsDeleted);
        var followingSet = followingIds.ToHashSet();

        var sortedRecommend = candidates
            .OrderByDescending(p => p.VoIsTop)
            .ThenByDescending(p => CalculateRecommendScore(p, followingSet))
            .ThenByDescending(p => p.VoCreateTime)
            .ToList();

        return BuildPostPagedResult(sortedRecommend, safePageIndex, safePageSize);
    }

    public async Task<UserFollowSummaryVo> GetMyFollowSummaryAsync(long userId)
    {
        var followerCount = await _userFollowRepository.QueryCountAsync(f =>
            f.FollowingUserId == userId && !f.IsDeleted);
        var followingCount = await _userFollowRepository.QueryCountAsync(f =>
            f.FollowerUserId == userId && !f.IsDeleted);

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
                    return !string.IsNullOrWhiteSpace(attachment.Url)
                        ? attachment.Url!
                        : attachment.ThumbnailPath ?? string.Empty;
                });
    }

    private async Task TrySendFollowNotificationAsync(long followerUserId, User targetUser, long tenantId)
    {
        if (followerUserId <= 0 || targetUser.Id <= 0 || followerUserId == targetUser.Id)
        {
            return;
        }

        try
        {
            var follower = await _userRepository.QueryFirstAsync(u => u.Id == followerUserId && u.IsEnable && !u.IsDeleted);
            var followerName = follower?.UserName?.Trim();
            if (string.IsNullOrWhiteSpace(followerName))
            {
                followerName = $"User-{followerUserId}";
            }

            var avatarMap = await LoadAvatarUrlMapAsync(new[] { followerUserId });
            await _notificationService.CreateNotificationAsync(new CreateNotificationDto
            {
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
                TenantId = tenantId
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[UserFollowService] 发送关注通知失败，FollowerUserId={FollowerUserId}, TargetUserId={TargetUserId}",
                followerUserId,
                targetUser.Id);
        }
    }

    private static UserFollowUserVo MapFollowUserVo(User user, string? avatarUrl, DateTime followTime, bool isMutualFollow)
    {
        return new UserFollowUserVo
        {
            VoUserId = user.Id,
            VoUserName = user.UserName,
            VoDisplayName = string.IsNullOrWhiteSpace(user.UserRealName) ? null : user.UserRealName,
            VoAvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl,
            VoIsMutualFollow = isMutualFollow,
            VoFollowTime = followTime
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

    private async Task<List<PostVo>> LoadDistributionCandidatesAsync()
    {
        var maxCandidateCount = NormalizeMaxCandidateCount(_feedDistributionOptions.MaxCandidateCount);
        var hasWindow = _feedDistributionOptions.CandidateWindowDays > 0;
        var windowStart = DateTime.Now.AddDays(-_feedDistributionOptions.CandidateWindowDays);

        return await _postService.QueryWithOrderAsync(
            p => p.IsPublished && !p.IsDeleted && (!hasWindow || p.CreateTime >= windowStart),
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
