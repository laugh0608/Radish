using AutoMapper;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service.Base;
using SqlSugar;

namespace Radish.Service;

/// <summary>用户关系链服务实现</summary>
public class UserFollowService : BaseService<UserFollow, UserFollowVo>, IUserFollowService
{
    private readonly IBaseRepository<UserFollow> _userFollowRepository;
    private readonly IBaseRepository<User> _userRepository;
    private readonly IPostService _postService;

    public UserFollowService(
        IMapper mapper,
        IBaseRepository<UserFollow> baseRepository,
        IBaseRepository<User> userRepository,
        IPostService postService)
        : base(mapper, baseRepository)
    {
        _userFollowRepository = baseRepository;
        _userRepository = userRepository;
        _postService = postService;
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

            return await _userFollowRepository.UpdateAsync(existing);
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

        var myFollowingIds = await _userFollowRepository.QueryDistinctAsync(
            f => f.FollowingUserId,
            f => f.FollowerUserId == userId && userIds.Contains(f.FollowingUserId) && !f.IsDeleted);
        var myFollowingSet = myFollowingIds.ToHashSet();

        var items = relations
            .Where(r => userMap.ContainsKey(r.FollowerUserId))
            .Select(r => MapFollowUserVo(userMap[r.FollowerUserId], r.FollowTime, myFollowingSet.Contains(r.FollowerUserId)))
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

        var followedBackIds = await _userFollowRepository.QueryDistinctAsync(
            f => f.FollowerUserId,
            f => f.FollowingUserId == userId && userIds.Contains(f.FollowerUserId) && !f.IsDeleted);
        var followedBackSet = followedBackIds.ToHashSet();

        var items = relations
            .Where(r => userMap.ContainsKey(r.FollowingUserId))
            .Select(r => MapFollowUserVo(userMap[r.FollowingUserId], r.FollowTime, followedBackSet.Contains(r.FollowingUserId)))
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

    private static UserFollowUserVo MapFollowUserVo(User user, DateTime followTime, bool isMutualFollow)
    {
        return new UserFollowUserVo
        {
            VoUserId = user.Id,
            VoUserName = user.UserName,
            VoDisplayName = string.IsNullOrWhiteSpace(user.UserRealName) ? null : user.UserRealName,
            VoAvatarUrl = null,
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
}
