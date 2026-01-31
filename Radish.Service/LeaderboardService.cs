using AutoMapper;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;
using Serilog;
using SqlSugar;

namespace Radish.Service;

/// <summary>排行榜服务实现</summary>
public class LeaderboardService : ILeaderboardService
{
    private readonly IBaseRepository<UserExperience> _userExpRepository;
    private readonly IBaseRepository<UserBalance> _userBalanceRepository;
    private readonly IBaseRepository<Product> _productRepository;
    private readonly IBaseRepository<User> _userRepository;
    private readonly IBaseRepository<LevelConfig> _levelConfigRepository;
    private readonly ILeaderboardRepository _leaderboardRepository;
    private readonly IMapper _mapper;

    /// <summary>排行榜类型配置</summary>
    private static readonly Dictionary<LeaderboardType, LeaderboardTypeVo> LeaderboardTypeConfigs = new()
    {
        [LeaderboardType.Experience] = new LeaderboardTypeVo
        {
            VoType = LeaderboardType.Experience,
            VoCategory = LeaderboardCategory.User,
            VoName = "等级排行",
            VoDescription = "按用户总经验值排序",
            VoIcon = "mdi:trophy",
            VoPrimaryLabel = "总经验值",
            VoSortOrder = 1
        },
        [LeaderboardType.Balance] = new LeaderboardTypeVo
        {
            VoType = LeaderboardType.Balance,
            VoCategory = LeaderboardCategory.User,
            VoName = "萝卜余额榜",
            VoDescription = "按用户当前萝卜币余额排序",
            VoIcon = "mdi:currency-usd",
            VoPrimaryLabel = "萝卜余额",
            VoSortOrder = 2
        },
        [LeaderboardType.TotalSpent] = new LeaderboardTypeVo
        {
            VoType = LeaderboardType.TotalSpent,
            VoCategory = LeaderboardCategory.User,
            VoName = "萝卜花销榜",
            VoDescription = "按用户累计消费萝卜币排序",
            VoIcon = "mdi:cart",
            VoPrimaryLabel = "累计花销",
            VoSortOrder = 3
        },
        [LeaderboardType.PurchaseCount] = new LeaderboardTypeVo
        {
            VoType = LeaderboardType.PurchaseCount,
            VoCategory = LeaderboardCategory.User,
            VoName = "购买达人榜",
            VoDescription = "按用户购买商品总数量排序",
            VoIcon = "mdi:shopping",
            VoPrimaryLabel = "购买数量",
            VoSortOrder = 4
        },
        [LeaderboardType.HotProduct] = new LeaderboardTypeVo
        {
            VoType = LeaderboardType.HotProduct,
            VoCategory = LeaderboardCategory.Product,
            VoName = "热门商品榜",
            VoDescription = "按商品销量排序",
            VoIcon = "mdi:fire",
            VoPrimaryLabel = "销量",
            VoSortOrder = 5
        },
        [LeaderboardType.PostCount] = new LeaderboardTypeVo
        {
            VoType = LeaderboardType.PostCount,
            VoCategory = LeaderboardCategory.User,
            VoName = "发帖达人榜",
            VoDescription = "按用户发帖数量排序",
            VoIcon = "mdi:post",
            VoPrimaryLabel = "帖子数",
            VoSortOrder = 6
        },
        [LeaderboardType.CommentCount] = new LeaderboardTypeVo
        {
            VoType = LeaderboardType.CommentCount,
            VoCategory = LeaderboardCategory.User,
            VoName = "评论达人榜",
            VoDescription = "按用户评论数量排序",
            VoIcon = "mdi:comment",
            VoPrimaryLabel = "评论数",
            VoSortOrder = 7
        },
        [LeaderboardType.Popularity] = new LeaderboardTypeVo
        {
            VoType = LeaderboardType.Popularity,
            VoCategory = LeaderboardCategory.User,
            VoName = "人气排行榜",
            VoDescription = "按用户获得的总点赞数排序",
            VoIcon = "mdi:heart",
            VoPrimaryLabel = "被点赞数",
            VoSortOrder = 8
        }
    };

    public LeaderboardService(
        IMapper mapper,
        IBaseRepository<UserExperience> userExpRepository,
        IBaseRepository<UserBalance> userBalanceRepository,
        IBaseRepository<Product> productRepository,
        IBaseRepository<User> userRepository,
        IBaseRepository<LevelConfig> levelConfigRepository,
        ILeaderboardRepository leaderboardRepository)
    {
        _mapper = mapper;
        _userExpRepository = userExpRepository;
        _userBalanceRepository = userBalanceRepository;
        _productRepository = productRepository;
        _userRepository = userRepository;
        _levelConfigRepository = levelConfigRepository;
        _leaderboardRepository = leaderboardRepository;
    }

    /// <inheritdoc />
    public async Task<PageModel<UnifiedLeaderboardItemVo>> GetLeaderboardAsync(
        LeaderboardType type,
        int pageIndex,
        int pageSize = 50,
        long? currentUserId = null)
    {
        // 限制每页数量
        if (pageSize > 100) pageSize = 100;
        if (pageSize < 1) pageSize = 50;
        if (pageIndex < 1) pageIndex = 1;

        try
        {
            return type switch
            {
                LeaderboardType.Experience => await GetExperienceLeaderboardAsync(pageIndex, pageSize, currentUserId),
                LeaderboardType.Balance => await GetBalanceLeaderboardAsync(pageIndex, pageSize, currentUserId),
                LeaderboardType.TotalSpent => await GetTotalSpentLeaderboardAsync(pageIndex, pageSize, currentUserId),
                LeaderboardType.PurchaseCount => await GetPurchaseCountLeaderboardAsync(pageIndex, pageSize, currentUserId),
                LeaderboardType.HotProduct => await GetHotProductLeaderboardAsync(pageIndex, pageSize),
                LeaderboardType.PostCount => await GetPostCountLeaderboardAsync(pageIndex, pageSize, currentUserId),
                LeaderboardType.CommentCount => await GetCommentCountLeaderboardAsync(pageIndex, pageSize, currentUserId),
                LeaderboardType.Popularity => await GetPopularityLeaderboardAsync(pageIndex, pageSize, currentUserId),
                _ => throw new ArgumentException($"不支持的排行榜类型: {type}")
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取排行榜失败: type={Type}, pageIndex={PageIndex}, pageSize={PageSize}",
                type, pageIndex, pageSize);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> GetUserRankAsync(LeaderboardType type, long userId)
    {
        if (userId <= 0) return 0;

        try
        {
            return type switch
            {
                LeaderboardType.Experience => await GetExperienceRankAsync(userId),
                LeaderboardType.Balance => await GetBalanceRankAsync(userId),
                LeaderboardType.TotalSpent => await GetTotalSpentRankAsync(userId),
                LeaderboardType.PurchaseCount => await _leaderboardRepository.GetUserPurchaseCountRankAsync(userId),
                LeaderboardType.HotProduct => 0, // 商品排行榜不支持用户排名
                LeaderboardType.PostCount => await _leaderboardRepository.GetUserPostCountRankAsync(userId),
                LeaderboardType.CommentCount => await _leaderboardRepository.GetUserCommentCountRankAsync(userId),
                LeaderboardType.Popularity => await _leaderboardRepository.GetUserPopularityRankAsync(userId),
                _ => 0
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取用户排名失败: type={Type}, userId={UserId}", type, userId);
            return 0;
        }
    }

    /// <inheritdoc />
    public Task<List<LeaderboardTypeVo>> GetLeaderboardTypesAsync()
    {
        var types = LeaderboardTypeConfigs.Values
            .OrderBy(t => t.VoSortOrder)
            .ToList();
        return Task.FromResult(types);
    }

    #region 等级排行榜

    private async Task<PageModel<UnifiedLeaderboardItemVo>> GetExperienceLeaderboardAsync(
        int pageIndex, int pageSize, long? currentUserId)
    {
        var (pagedData, totalCount) = await _userExpRepository.QueryPageAsync(
            whereExpression: e => !e.ExpFrozen && e.UserId > 0 && !e.IsDeleted,
            pageIndex: pageIndex,
            pageSize: pageSize,
            orderByExpression: e => e.TotalExp,
            orderByType: OrderByType.Desc
        );

        var userIds = pagedData.Select(e => e.UserId).ToList();
        var users = await _userRepository.QueryAsync(u => userIds.Contains(u.Id));
        var userDict = users.ToDictionary(u => u.Id);

        var levels = pagedData.Select(e => e.CurrentLevel).Distinct().ToList();
        var levelConfigs = await _levelConfigRepository.QueryAsync(l => levels.Contains(l.Level));
        var levelConfigDict = levelConfigs.ToDictionary(l => l.Level);

        var startRank = (pageIndex - 1) * pageSize + 1;
        var leaderboard = new List<UnifiedLeaderboardItemVo>();
        var rank = startRank;

        foreach (var exp in pagedData)
        {
            if (!userDict.TryGetValue(exp.UserId, out var user))
            {
                continue;
            }

            var item = new UnifiedLeaderboardItemVo
            {
                VoLeaderboardType = LeaderboardType.Experience,
                VoCategory = LeaderboardCategory.User,
                VoRank = rank,
                VoUserId = exp.UserId,
                VoUserName = user.UserName,
                VoCurrentLevel = exp.CurrentLevel,
                VoCurrentLevelName = levelConfigDict.TryGetValue(exp.CurrentLevel, out var config)
                    ? config.LevelName
                    : $"Lv.{exp.CurrentLevel}",
                VoThemeColor = levelConfigDict.TryGetValue(exp.CurrentLevel, out var colorConfig)
                    ? colorConfig.ThemeColor
                    : "#9E9E9E",
                VoIsCurrentUser = currentUserId.HasValue && exp.UserId == currentUserId.Value,
                VoPrimaryValue = exp.TotalExp,
                VoPrimaryLabel = "总经验值",
                VoSecondaryValue = exp.CurrentLevel,
                VoSecondaryLabel = "等级"
            };

            leaderboard.Add(item);
            rank++;
        }

        return CreatePageModel(leaderboard, totalCount, pageIndex, pageSize);
    }

    private async Task<int> GetExperienceRankAsync(long userId)
    {
        var userExp = await _userExpRepository.QueryFirstAsync(e => e.UserId == userId && !e.IsDeleted);
        if (userExp == null || userExp.ExpFrozen) return 0;

        var higherCount = await _userExpRepository.QueryCountAsync(
            e => !e.ExpFrozen && e.UserId > 0 && e.TotalExp > userExp.TotalExp && !e.IsDeleted
        );

        return (int)higherCount + 1;
    }

    #endregion

    #region 萝卜余额排行榜

    private async Task<PageModel<UnifiedLeaderboardItemVo>> GetBalanceLeaderboardAsync(
        int pageIndex, int pageSize, long? currentUserId)
    {
        var (pagedData, totalCount) = await _userBalanceRepository.QueryPageAsync(
            whereExpression: b => b.UserId > 0 && !b.IsDeleted && b.Balance > 0,
            pageIndex: pageIndex,
            pageSize: pageSize,
            orderByExpression: b => b.Balance,
            orderByType: OrderByType.Desc
        );

        var userIds = pagedData.Select(b => b.UserId).ToList();
        var users = await _userRepository.QueryAsync(u => userIds.Contains(u.Id));
        var userDict = users.ToDictionary(u => u.Id);

        // 获取用户等级信息
        var userExps = await _userExpRepository.QueryAsync(e => userIds.Contains(e.UserId) && !e.IsDeleted);
        var userExpDict = userExps.ToDictionary(e => e.UserId);

        var levels = userExps.Select(e => e.CurrentLevel).Distinct().ToList();
        var levelConfigs = await _levelConfigRepository.QueryAsync(l => levels.Contains(l.Level));
        var levelConfigDict = levelConfigs.ToDictionary(l => l.Level);

        var startRank = (pageIndex - 1) * pageSize + 1;
        var leaderboard = new List<UnifiedLeaderboardItemVo>();
        var rank = startRank;

        foreach (var balance in pagedData)
        {
            if (!userDict.TryGetValue(balance.UserId, out var user))
            {
                continue;
            }

            userExpDict.TryGetValue(balance.UserId, out var userExp);
            var level = userExp?.CurrentLevel ?? 0;

            var item = new UnifiedLeaderboardItemVo
            {
                VoLeaderboardType = LeaderboardType.Balance,
                VoCategory = LeaderboardCategory.User,
                VoRank = rank,
                VoUserId = balance.UserId,
                VoUserName = user.UserName,
                VoCurrentLevel = level,
                VoCurrentLevelName = levelConfigDict.TryGetValue(level, out var config)
                    ? config.LevelName
                    : $"Lv.{level}",
                VoThemeColor = levelConfigDict.TryGetValue(level, out var colorConfig)
                    ? colorConfig.ThemeColor
                    : "#9E9E9E",
                VoIsCurrentUser = currentUserId.HasValue && balance.UserId == currentUserId.Value,
                VoPrimaryValue = balance.Balance,
                VoPrimaryLabel = "萝卜余额",
                VoSecondaryValue = balance.TotalEarned,
                VoSecondaryLabel = "累计获得"
            };

            leaderboard.Add(item);
            rank++;
        }

        return CreatePageModel(leaderboard, totalCount, pageIndex, pageSize);
    }

    private async Task<int> GetBalanceRankAsync(long userId)
    {
        var userBalance = await _userBalanceRepository.QueryFirstAsync(b => b.UserId == userId && !b.IsDeleted);
        if (userBalance == null || userBalance.Balance <= 0) return 0;

        var higherCount = await _userBalanceRepository.QueryCountAsync(
            b => b.UserId > 0 && b.Balance > userBalance.Balance && !b.IsDeleted
        );

        return (int)higherCount + 1;
    }

    #endregion

    #region 萝卜花销排行榜

    private async Task<PageModel<UnifiedLeaderboardItemVo>> GetTotalSpentLeaderboardAsync(
        int pageIndex, int pageSize, long? currentUserId)
    {
        var (pagedData, totalCount) = await _userBalanceRepository.QueryPageAsync(
            whereExpression: b => b.UserId > 0 && !b.IsDeleted && b.TotalSpent > 0,
            pageIndex: pageIndex,
            pageSize: pageSize,
            orderByExpression: b => b.TotalSpent,
            orderByType: OrderByType.Desc
        );

        var userIds = pagedData.Select(b => b.UserId).ToList();
        var users = await _userRepository.QueryAsync(u => userIds.Contains(u.Id));
        var userDict = users.ToDictionary(u => u.Id);

        // 获取用户等级信息
        var userExps = await _userExpRepository.QueryAsync(e => userIds.Contains(e.UserId) && !e.IsDeleted);
        var userExpDict = userExps.ToDictionary(e => e.UserId);

        var levels = userExps.Select(e => e.CurrentLevel).Distinct().ToList();
        var levelConfigs = await _levelConfigRepository.QueryAsync(l => levels.Contains(l.Level));
        var levelConfigDict = levelConfigs.ToDictionary(l => l.Level);

        var startRank = (pageIndex - 1) * pageSize + 1;
        var leaderboard = new List<UnifiedLeaderboardItemVo>();
        var rank = startRank;

        foreach (var balance in pagedData)
        {
            if (!userDict.TryGetValue(balance.UserId, out var user))
            {
                continue;
            }

            userExpDict.TryGetValue(balance.UserId, out var userExp);
            var level = userExp?.CurrentLevel ?? 0;

            var item = new UnifiedLeaderboardItemVo
            {
                VoLeaderboardType = LeaderboardType.TotalSpent,
                VoCategory = LeaderboardCategory.User,
                VoRank = rank,
                VoUserId = balance.UserId,
                VoUserName = user.UserName,
                VoCurrentLevel = level,
                VoCurrentLevelName = levelConfigDict.TryGetValue(level, out var config)
                    ? config.LevelName
                    : $"Lv.{level}",
                VoThemeColor = levelConfigDict.TryGetValue(level, out var colorConfig)
                    ? colorConfig.ThemeColor
                    : "#9E9E9E",
                VoIsCurrentUser = currentUserId.HasValue && balance.UserId == currentUserId.Value,
                VoPrimaryValue = balance.TotalSpent,
                VoPrimaryLabel = "累计花销"
            };

            leaderboard.Add(item);
            rank++;
        }

        return CreatePageModel(leaderboard, totalCount, pageIndex, pageSize);
    }

    private async Task<int> GetTotalSpentRankAsync(long userId)
    {
        var userBalance = await _userBalanceRepository.QueryFirstAsync(b => b.UserId == userId && !b.IsDeleted);
        if (userBalance == null || userBalance.TotalSpent <= 0) return 0;

        var higherCount = await _userBalanceRepository.QueryCountAsync(
            b => b.UserId > 0 && b.TotalSpent > userBalance.TotalSpent && !b.IsDeleted
        );

        return (int)higherCount + 1;
    }

    #endregion

    #region 购买数量排行榜

    private async Task<PageModel<UnifiedLeaderboardItemVo>> GetPurchaseCountLeaderboardAsync(
        int pageIndex, int pageSize, long? currentUserId)
    {
        var rankingData = await _leaderboardRepository.GetPurchaseCountRankingAsync(pageIndex, pageSize);
        var totalCount = await _leaderboardRepository.GetPurchaseCountRankingTotalAsync();

        var userIds = rankingData.Select(r => r.UserId).ToList();
        var users = await _userRepository.QueryAsync(u => userIds.Contains(u.Id));
        var userDict = users.ToDictionary(u => u.Id);

        // 获取用户等级信息
        var userExps = await _userExpRepository.QueryAsync(e => userIds.Contains(e.UserId) && !e.IsDeleted);
        var userExpDict = userExps.ToDictionary(e => e.UserId);

        var levels = userExps.Select(e => e.CurrentLevel).Distinct().ToList();
        var levelConfigs = await _levelConfigRepository.QueryAsync(l => levels.Contains(l.Level));
        var levelConfigDict = levelConfigs.ToDictionary(l => l.Level);

        var startRank = (pageIndex - 1) * pageSize + 1;
        var leaderboard = new List<UnifiedLeaderboardItemVo>();
        var rank = startRank;

        foreach (var (userId, totalQuantity) in rankingData)
        {
            if (!userDict.TryGetValue(userId, out var user))
            {
                continue;
            }

            userExpDict.TryGetValue(userId, out var userExp);
            var level = userExp?.CurrentLevel ?? 0;

            var item = new UnifiedLeaderboardItemVo
            {
                VoLeaderboardType = LeaderboardType.PurchaseCount,
                VoCategory = LeaderboardCategory.User,
                VoRank = rank,
                VoUserId = userId,
                VoUserName = user.UserName,
                VoCurrentLevel = level,
                VoCurrentLevelName = levelConfigDict.TryGetValue(level, out var config)
                    ? config.LevelName
                    : $"Lv.{level}",
                VoThemeColor = levelConfigDict.TryGetValue(level, out var colorConfig)
                    ? colorConfig.ThemeColor
                    : "#9E9E9E",
                VoIsCurrentUser = currentUserId.HasValue && userId == currentUserId.Value,
                VoPrimaryValue = totalQuantity,
                VoPrimaryLabel = "购买数量"
            };

            leaderboard.Add(item);
            rank++;
        }

        return CreatePageModel(leaderboard, totalCount, pageIndex, pageSize);
    }

    #endregion

    #region 热门商品排行榜

    private async Task<PageModel<UnifiedLeaderboardItemVo>> GetHotProductLeaderboardAsync(
        int pageIndex, int pageSize)
    {
        var (pagedData, totalCount) = await _productRepository.QueryPageAsync(
            whereExpression: p => p.IsEnabled && p.IsOnSale && !p.IsDeleted && p.SoldCount > 0,
            pageIndex: pageIndex,
            pageSize: pageSize,
            orderByExpression: p => p.SoldCount,
            orderByType: OrderByType.Desc
        );

        var startRank = (pageIndex - 1) * pageSize + 1;
        var leaderboard = new List<UnifiedLeaderboardItemVo>();
        var rank = startRank;

        foreach (var product in pagedData)
        {
            var item = new UnifiedLeaderboardItemVo
            {
                VoLeaderboardType = LeaderboardType.HotProduct,
                VoCategory = LeaderboardCategory.Product,
                VoRank = rank,
                VoProductId = product.Id,
                VoProductName = product.Name,
                VoProductIcon = product.Icon,
                VoProductPrice = product.Price,
                VoPrimaryValue = product.SoldCount,
                VoPrimaryLabel = "销量",
                VoSecondaryValue = product.Price,
                VoSecondaryLabel = "价格"
            };

            leaderboard.Add(item);
            rank++;
        }

        return CreatePageModel(leaderboard, totalCount, pageIndex, pageSize);
    }

    #endregion

    #region 发帖达人排行榜

    private async Task<PageModel<UnifiedLeaderboardItemVo>> GetPostCountLeaderboardAsync(
        int pageIndex, int pageSize, long? currentUserId)
    {
        var rankingData = await _leaderboardRepository.GetPostCountRankingAsync(pageIndex, pageSize);
        var totalCount = await _leaderboardRepository.GetPostCountRankingTotalAsync();

        return await BuildUserLeaderboardAsync(
            rankingData.Select(r => (r.UserId, (long)r.PostCount)).ToList(),
            totalCount,
            pageIndex,
            pageSize,
            currentUserId,
            LeaderboardType.PostCount,
            "帖子数"
        );
    }

    #endregion

    #region 评论达人排行榜

    private async Task<PageModel<UnifiedLeaderboardItemVo>> GetCommentCountLeaderboardAsync(
        int pageIndex, int pageSize, long? currentUserId)
    {
        var rankingData = await _leaderboardRepository.GetCommentCountRankingAsync(pageIndex, pageSize);
        var totalCount = await _leaderboardRepository.GetCommentCountRankingTotalAsync();

        return await BuildUserLeaderboardAsync(
            rankingData.Select(r => (r.UserId, (long)r.CommentCount)).ToList(),
            totalCount,
            pageIndex,
            pageSize,
            currentUserId,
            LeaderboardType.CommentCount,
            "评论数"
        );
    }

    #endregion

    #region 人气排行榜

    private async Task<PageModel<UnifiedLeaderboardItemVo>> GetPopularityLeaderboardAsync(
        int pageIndex, int pageSize, long? currentUserId)
    {
        var rankingData = await _leaderboardRepository.GetPopularityRankingAsync(pageIndex, pageSize);
        var totalCount = await _leaderboardRepository.GetPopularityRankingTotalAsync();

        return await BuildUserLeaderboardAsync(
            rankingData.Select(r => (r.UserId, (long)r.TotalLikes)).ToList(),
            totalCount,
            pageIndex,
            pageSize,
            currentUserId,
            LeaderboardType.Popularity,
            "被点赞数"
        );
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 构建用户类排行榜（通用方法）
    /// </summary>
    private async Task<PageModel<UnifiedLeaderboardItemVo>> BuildUserLeaderboardAsync(
        List<(long UserId, long Value)> rankingData,
        int totalCount,
        int pageIndex,
        int pageSize,
        long? currentUserId,
        LeaderboardType type,
        string primaryLabel)
    {
        var userIds = rankingData.Select(r => r.UserId).ToList();
        var users = await _userRepository.QueryAsync(u => userIds.Contains(u.Id));
        var userDict = users.ToDictionary(u => u.Id);

        // 获取用户等级信息
        var userExps = await _userExpRepository.QueryAsync(e => userIds.Contains(e.UserId) && !e.IsDeleted);
        var userExpDict = userExps.ToDictionary(e => e.UserId);

        var levels = userExps.Select(e => e.CurrentLevel).Distinct().ToList();
        var levelConfigs = await _levelConfigRepository.QueryAsync(l => levels.Contains(l.Level));
        var levelConfigDict = levelConfigs.ToDictionary(l => l.Level);

        var startRank = (pageIndex - 1) * pageSize + 1;
        var leaderboard = new List<UnifiedLeaderboardItemVo>();
        var rank = startRank;

        foreach (var (userId, value) in rankingData)
        {
            if (!userDict.TryGetValue(userId, out var user))
            {
                continue;
            }

            userExpDict.TryGetValue(userId, out var userExp);
            var level = userExp?.CurrentLevel ?? 0;

            var item = new UnifiedLeaderboardItemVo
            {
                VoLeaderboardType = type,
                VoCategory = LeaderboardCategory.User,
                VoRank = rank,
                VoUserId = userId,
                VoUserName = user.UserName,
                VoCurrentLevel = level,
                VoCurrentLevelName = levelConfigDict.TryGetValue(level, out var config)
                    ? config.LevelName
                    : $"Lv.{level}",
                VoThemeColor = levelConfigDict.TryGetValue(level, out var colorConfig)
                    ? colorConfig.ThemeColor
                    : "#9E9E9E",
                VoIsCurrentUser = currentUserId.HasValue && userId == currentUserId.Value,
                VoPrimaryValue = value,
                VoPrimaryLabel = primaryLabel
            };

            leaderboard.Add(item);
            rank++;
        }

        return CreatePageModel(leaderboard, totalCount, pageIndex, pageSize);
    }

    /// <summary>
    /// 创建分页模型
    /// </summary>
    private static PageModel<UnifiedLeaderboardItemVo> CreatePageModel(
        List<UnifiedLeaderboardItemVo> data,
        int totalCount,
        int pageIndex,
        int pageSize)
    {
        var pageCount = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PageModel<UnifiedLeaderboardItemVo>
        {
            Page = pageIndex,
            PageSize = pageSize,
            DataCount = totalCount,
            PageCount = pageCount,
            Data = data
        };
    }

    #endregion
}
