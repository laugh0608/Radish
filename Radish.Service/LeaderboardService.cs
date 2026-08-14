using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;
using Serilog;

namespace Radish.Service;

/// <summary>公开排行榜服务实现。</summary>
public class LeaderboardService : ILeaderboardService
{
    private readonly IBaseRepository<UserExperience> _userExperienceRepository;
    private readonly IBaseRepository<LevelConfig> _levelConfigRepository;
    private readonly ILeaderboardRepository _leaderboardRepository;
    private readonly IAttachmentService _attachmentService;
    private readonly IAttachmentUrlResolver _attachmentUrlResolver;

    private static readonly Dictionary<LeaderboardType, LeaderboardTypeVo> LeaderboardTypeConfigs = new()
    {
        [LeaderboardType.Experience] = new LeaderboardTypeVo
        {
            VoType = LeaderboardType.Experience,
            VoCategory = LeaderboardCategory.User,
            VoName = "经验排行",
            VoDescription = "按有效用户累计经验值排序",
            VoIcon = "mdi:trophy",
            VoPrimaryLabel = "总经验值",
            VoSortOrder = 1
        },
        [LeaderboardType.PostCount] = new LeaderboardTypeVo
        {
            VoType = LeaderboardType.PostCount,
            VoCategory = LeaderboardCategory.User,
            VoName = "发帖达人",
            VoDescription = "按用户公开有效帖子数量排序",
            VoIcon = "mdi:post",
            VoPrimaryLabel = "帖子数",
            VoSortOrder = 2
        },
        [LeaderboardType.CommentCount] = new LeaderboardTypeVo
        {
            VoType = LeaderboardType.CommentCount,
            VoCategory = LeaderboardCategory.User,
            VoName = "评论达人",
            VoDescription = "按用户公开有效评论数量排序",
            VoIcon = "mdi:comment",
            VoPrimaryLabel = "评论数",
            VoSortOrder = 3
        },
        [LeaderboardType.Popularity] = new LeaderboardTypeVo
        {
            VoType = LeaderboardType.Popularity,
            VoCategory = LeaderboardCategory.User,
            VoName = "人气排行",
            VoDescription = "按公开帖子与评论获得的点赞总数排序",
            VoIcon = "mdi:heart",
            VoPrimaryLabel = "被点赞数",
            VoSortOrder = 4
        },
        [LeaderboardType.HotProduct] = new LeaderboardTypeVo
        {
            VoType = LeaderboardType.HotProduct,
            VoCategory = LeaderboardCategory.Product,
            VoName = "热门商品",
            VoDescription = "按公开在售商品销量排序",
            VoIcon = "mdi:fire",
            VoPrimaryLabel = "销量",
            VoSortOrder = 5
        }
    };

    public LeaderboardService(
        IBaseRepository<UserExperience> userExperienceRepository,
        IBaseRepository<LevelConfig> levelConfigRepository,
        ILeaderboardRepository leaderboardRepository,
        IAttachmentService attachmentService,
        IAttachmentUrlResolver attachmentUrlResolver)
    {
        _userExperienceRepository = userExperienceRepository;
        _levelConfigRepository = levelConfigRepository;
        _leaderboardRepository = leaderboardRepository;
        _attachmentService = attachmentService;
        _attachmentUrlResolver = attachmentUrlResolver;
    }

    /// <inheritdoc />
    public async Task<PageModel<UnifiedLeaderboardItemVo>> GetLeaderboardAsync(
        LeaderboardType type,
        int pageIndex,
        int pageSize = 50,
        long? currentUserId = null)
    {
        if (!LeaderboardPublicPolicy.IsPublicType(type))
        {
            throw new ArgumentOutOfRangeException(
                nameof(type),
                type,
                "该排行榜类型不提供公开访问。");
        }

        var safePageIndex = Math.Max(1, pageIndex);
        var safePageSize = pageSize switch
        {
            < 1 => 50,
            > 100 => 100,
            _ => pageSize
        };

        try
        {
            return type switch
            {
                LeaderboardType.Experience => await GetExperienceLeaderboardAsync(
                    safePageIndex,
                    safePageSize,
                    currentUserId),
                LeaderboardType.PostCount => await GetPostCountLeaderboardAsync(
                    safePageIndex,
                    safePageSize,
                    currentUserId),
                LeaderboardType.CommentCount => await GetCommentCountLeaderboardAsync(
                    safePageIndex,
                    safePageSize,
                    currentUserId),
                LeaderboardType.Popularity => await GetPopularityLeaderboardAsync(
                    safePageIndex,
                    safePageSize,
                    currentUserId),
                LeaderboardType.HotProduct => await GetHotProductLeaderboardAsync(
                    safePageIndex,
                    safePageSize),
                _ => throw new InvalidOperationException("公开排行榜类型策略与服务分派不一致。")
            };
        }
        catch (Exception ex)
        {
            Log.Error(
                ex,
                "获取公开排行榜失败: type={Type}, pageIndex={PageIndex}, pageSize={PageSize}",
                type,
                safePageIndex,
                safePageSize);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> GetUserRankAsync(LeaderboardType type, long userId)
    {
        if (!LeaderboardPublicPolicy.IsPublicType(type))
        {
            throw new ArgumentOutOfRangeException(
                nameof(type),
                type,
                "该排行榜类型不提供公开访问。");
        }

        if (!LeaderboardPublicPolicy.SupportsUserRank(type))
        {
            throw new ArgumentOutOfRangeException(
                nameof(type),
                type,
                "该排行榜类型不支持用户个人排名。");
        }

        if (userId <= 0)
        {
            return 0;
        }

        try
        {
            var now = DateTime.Now;
            return type switch
            {
                LeaderboardType.Experience =>
                    await _leaderboardRepository.GetUserExperienceRankAsync(userId, now),
                LeaderboardType.PostCount =>
                    await _leaderboardRepository.GetUserPostCountRankAsync(userId),
                LeaderboardType.CommentCount =>
                    await _leaderboardRepository.GetUserCommentCountRankAsync(userId),
                LeaderboardType.Popularity =>
                    await _leaderboardRepository.GetUserPopularityRankAsync(userId),
                _ => throw new InvalidOperationException("用户排名类型策略与服务分派不一致。")
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取用户公开排名失败: type={Type}, userId={UserId}", type, userId);
            throw;
        }
    }

    /// <inheritdoc />
    public Task<List<LeaderboardTypeVo>> GetLeaderboardTypesAsync()
    {
        var types = LeaderboardTypeConfigs.Values
            .Where(type => LeaderboardPublicPolicy.IsPublicType(type.VoType))
            .OrderBy(type => type.VoSortOrder)
            .ToList();
        return Task.FromResult(types);
    }

    private async Task<PageModel<UnifiedLeaderboardItemVo>> GetExperienceLeaderboardAsync(
        int pageIndex,
        int pageSize,
        long? currentUserId)
    {
        var now = DateTime.Now;
        var (items, totalCount) = await _leaderboardRepository.GetExperienceRankingAsync(
            now,
            pageIndex,
            pageSize);
        return await BuildUserLeaderboardAsync(
            items,
            totalCount,
            pageIndex,
            pageSize,
            currentUserId,
            LeaderboardType.Experience,
            "总经验值");
    }

    private async Task<PageModel<UnifiedLeaderboardItemVo>> GetPostCountLeaderboardAsync(
        int pageIndex,
        int pageSize,
        long? currentUserId)
    {
        var (items, totalCount) = await _leaderboardRepository.GetPostCountRankingAsync(
            pageIndex,
            pageSize);
        return await BuildUserLeaderboardAsync(
            items,
            totalCount,
            pageIndex,
            pageSize,
            currentUserId,
            LeaderboardType.PostCount,
            "帖子数");
    }

    private async Task<PageModel<UnifiedLeaderboardItemVo>> GetCommentCountLeaderboardAsync(
        int pageIndex,
        int pageSize,
        long? currentUserId)
    {
        var (items, totalCount) = await _leaderboardRepository.GetCommentCountRankingAsync(
            pageIndex,
            pageSize);
        return await BuildUserLeaderboardAsync(
            items,
            totalCount,
            pageIndex,
            pageSize,
            currentUserId,
            LeaderboardType.CommentCount,
            "评论数");
    }

    private async Task<PageModel<UnifiedLeaderboardItemVo>> GetPopularityLeaderboardAsync(
        int pageIndex,
        int pageSize,
        long? currentUserId)
    {
        var (items, totalCount) = await _leaderboardRepository.GetPopularityRankingAsync(
            pageIndex,
            pageSize);
        return await BuildUserLeaderboardAsync(
            items,
            totalCount,
            pageIndex,
            pageSize,
            currentUserId,
            LeaderboardType.Popularity,
            "被点赞数");
    }

    private async Task<PageModel<UnifiedLeaderboardItemVo>> GetHotProductLeaderboardAsync(
        int pageIndex,
        int pageSize)
    {
        var (products, totalCount) = await _leaderboardRepository.GetHotProductRankingAsync(
            pageIndex,
            pageSize);
        var startRank = (pageIndex - 1) * pageSize + 1;
        var items = products
            .Select((product, index) => new UnifiedLeaderboardItemVo
            {
                VoLeaderboardType = LeaderboardType.HotProduct,
                VoCategory = LeaderboardCategory.Product,
                VoRank = startRank + index,
                VoProductId = product.Id,
                VoProductName = product.Name,
                VoProductIcon = ResolveAttachmentUrl(product.IconAttachmentId),
                VoProductPrice = product.Price,
                VoPrimaryValue = product.SoldCount,
                VoPrimaryLabel = "销量",
                VoSecondaryValue = product.Price,
                VoSecondaryLabel = "价格"
            })
            .ToList();
        return CreatePageModel(items, totalCount, pageIndex, pageSize);
    }

    private async Task<PageModel<UnifiedLeaderboardItemVo>> BuildUserLeaderboardAsync(
        IReadOnlyList<UserLeaderboardMetric> rankingData,
        int totalCount,
        int pageIndex,
        int pageSize,
        long? currentUserId,
        LeaderboardType type,
        string primaryLabel)
    {
        var rankingUserIds = rankingData.Select(item => item.UserId).ToList();
        var users = await _leaderboardRepository.GetEligibleUsersAsync(rankingUserIds);
        var userMap = users.ToDictionary(user => user.UserId);
        var eligibleUserIds = userMap.Keys.ToList();
        var avatarUrlMap = await LoadUserAvatarUrlMapAsync(eligibleUserIds);

        var userExperiences = eligibleUserIds.Count == 0
            ? []
            : await _userExperienceRepository.QueryAsync(experience =>
                eligibleUserIds.Contains(experience.UserId) &&
                !experience.IsDeleted);
        var userExperienceMap = userExperiences.ToDictionary(experience => experience.UserId);
        var levels = userExperiences
            .Select(experience => experience.CurrentLevel)
            .Distinct()
            .ToList();
        var levelConfigs = levels.Count == 0
            ? []
            : await _levelConfigRepository.QueryAsync(config => levels.Contains(config.Level));
        var levelConfigMap = levelConfigs.ToDictionary(config => config.Level);

        var startRank = (pageIndex - 1) * pageSize + 1;
        var leaderboard = new List<UnifiedLeaderboardItemVo>(rankingData.Count);
        for (var index = 0; index < rankingData.Count; index++)
        {
            var metric = rankingData[index];
            if (!userMap.TryGetValue(metric.UserId, out var user))
            {
                continue;
            }

            userExperienceMap.TryGetValue(metric.UserId, out var userExperience);
            var level = userExperience?.CurrentLevel ?? 0;
            var publicId = User.HasPublicIdFormat(user.PublicId)
                ? user.PublicId!.Trim().ToLowerInvariant()
                : null;
            var publicIndex = User.HasAssignedPublicIndex(user.PublicIndex)
                ? user.PublicIndex
                : null;

            leaderboard.Add(new UnifiedLeaderboardItemVo
            {
                VoLeaderboardType = type,
                VoCategory = LeaderboardCategory.User,
                VoRank = startRank + index,
                VoUserId = metric.UserId,
                VoUserPublicId = publicId,
                VoUserPublicIndex = publicIndex,
                VoUserName = User.NormalizeDisplayName(user.UserName, user.UserId),
                VoUserDisplayName = User.NormalizeDisplayName(user.UserName, user.UserId),
                VoUserDisplayHandle = User.BuildDisplayHandle(
                    user.UserName,
                    publicIndex,
                    user.UserId),
                VoAvatarUrl = avatarUrlMap.GetValueOrDefault(metric.UserId),
                VoCurrentLevel = level,
                VoCurrentLevelName = levelConfigMap.TryGetValue(level, out var levelConfig)
                    ? levelConfig.LevelName
                    : $"Lv.{level}",
                VoThemeColor = levelConfigMap.TryGetValue(level, out var themeConfig)
                    ? themeConfig.ThemeColor
                    : "#9E9E9E",
                VoIsCurrentUser = currentUserId.HasValue && metric.UserId == currentUserId.Value,
                VoPrimaryValue = metric.Value,
                VoPrimaryLabel = primaryLabel,
                VoSecondaryValue = type == LeaderboardType.Experience ? level : null,
                VoSecondaryLabel = type == LeaderboardType.Experience ? "等级" : null
            });
        }

        return CreatePageModel(leaderboard, totalCount, pageIndex, pageSize);
    }

    private static PageModel<UnifiedLeaderboardItemVo> CreatePageModel(
        List<UnifiedLeaderboardItemVo> data,
        int totalCount,
        int pageIndex,
        int pageSize)
    {
        return new PageModel<UnifiedLeaderboardItemVo>
        {
            Page = pageIndex,
            PageSize = pageSize,
            DataCount = totalCount,
            PageCount = (int)Math.Ceiling(totalCount / (double)pageSize),
            Data = data
        };
    }

    private async Task<Dictionary<long, string>> LoadUserAvatarUrlMapAsync(
        IReadOnlyCollection<long> userIds)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<long, string>();
        }

        var avatarMap = await _attachmentService.GetLatestAvatarAssetMapAsync(userIds);
        return avatarMap
            .Where(item => !string.IsNullOrWhiteSpace(item.Value.Url))
            .ToDictionary(item => item.Key, item => item.Value.Url);
    }

    private string? ResolveAttachmentUrl(long? attachmentId)
    {
        return attachmentId is > 0
            ? _attachmentUrlResolver.ResolveAttachmentUrl(attachmentId.Value)
            : null;
    }
}
