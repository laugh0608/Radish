using AutoMapper;
using Radish.Common;
using Radish.Common.AttributeTool;
using Radish.Common.CacheTool;
using Radish.Common.Exceptions;
using Radish.Infrastructure;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Serilog;
using SqlSugar;
using System.Linq.Expressions;
using Radish.IRepository.Base;
using Radish.Model.DtoModels;
using Radish.Service.Base;

namespace Radish.Service;

	/// <summary>经验值服务实现</summary>
	public class ExperienceService : BaseService<UserExperience, UserExperienceVo>, IExperienceService
	{
    private readonly IBaseRepository<UserExperience> _userExpRepository;
    private readonly IBaseRepository<ExpTransaction> _expTransactionRepository;
    private readonly IBaseRepository<LevelConfig> _levelConfigRepository;
    private readonly IBaseRepository<UserExpDailyStats> _dailyStatsRepository;
    private readonly IBaseRepository<User> _userRepository;
    private readonly IExperienceCalculator _experienceCalculator;
    private readonly ICoinService _coinService;
    private readonly IAttachmentUrlResolver _attachmentUrlResolver;
    private readonly INotificationService _notificationService;
    private readonly ICaching _caching;

    private const string LevelConfigsCacheKey = "experience:level-configs:enabled";

	    /// <summary>乐观锁冲突重试次数</summary>
	    private const int MaxRetryCount = 6;

	    /// <summary>重试基础延迟（毫秒）</summary>
	    private const int BaseRetryDelayMs = 100;

	    /// <summary>重试最大延迟（毫秒），避免指数退避无限放大</summary>
	    private const int MaxRetryDelayMs = 1000;

    /// <summary>交易记录分页最大页大小</summary>
    private const int MaxTransactionPageSize = 100;

    public ExperienceService(
        IMapper mapper,
        IBaseRepository<UserExperience> userExpRepository,
        IBaseRepository<ExpTransaction> expTransactionRepository,
        IBaseRepository<LevelConfig> levelConfigRepository,
        IBaseRepository<UserExpDailyStats> dailyStatsRepository,
        IBaseRepository<User> userRepository,
        IExperienceCalculator experienceCalculator,
        ICoinService coinService,
        IAttachmentUrlResolver attachmentUrlResolver,
        INotificationService notificationService,
        ICaching caching)
        : base(mapper, userExpRepository)
    {
        _userExpRepository = userExpRepository;
        _expTransactionRepository = expTransactionRepository;
        _levelConfigRepository = levelConfigRepository;
        _dailyStatsRepository = dailyStatsRepository;
        _userRepository = userRepository;
        _experienceCalculator = experienceCalculator;
        _coinService = coinService;
        _attachmentUrlResolver = attachmentUrlResolver;
        _notificationService = notificationService;
        _caching = caching;
    }

    #region 经验值查询

    /// <summary>
    /// 获取用户经验值信息
    /// </summary>
    public async Task<UserExperienceVo?> GetUserExperienceAsync(long userId)
    {
        try
        {
            if (userId <= 0)
            {
                Log.Warning("获取用户经验值信息失败：userId 无效（{UserId}）", userId);
                return null;
            }

            var userExp = await _userExpRepository.QueryFirstAsync(e => e.UserId == userId && !e.IsDeleted);

            if (userExp == null)
            {
                // 如果用户经验值记录不存在，自动创建初始记录
                Log.Information("用户 {UserId} 经验值记录不存在，自动创建初始记录", userId);
                userExp = await InitializeUserExperienceAsync(userId);
                if (userExp == null)
                {
                    return null;
                }
            }

            userExp = await NormalizeFreezeStateAsync(userExp);
            return await MapToVoAsync(userExp);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取用户 {UserId} 经验值信息失败", userId);
            throw;
        }
    }

    /// <summary>
    /// 批量获取用户经验值信息
    /// </summary>
    public async Task<Dictionary<long, UserExperienceVo>> GetUserExperiencesAsync(List<long> userIds)
    {
        try
        {
            var userExps = await _userExpRepository.QueryAsync(
                e => userIds.Contains(e.UserId) && !e.IsDeleted
            );

            var result = new Dictionary<long, UserExperienceVo>();

            foreach (var userExp in userExps)
            {
                var normalizedUserExp = await NormalizeFreezeStateAsync(userExp);
                var vo = await MapToVoAsync(normalizedUserExp);
                if (vo != null)
                {
                    result[normalizedUserExp.UserId] = vo;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "批量获取用户经验值信息失败");
            throw;
        }
    }

    #endregion

    #region 经验值发放

    /// <summary>
    /// 发放经验值
    /// </summary>
    public async Task<bool> GrantExperienceAsync(
        long userId,
        int amount,
        string expType,
        string? businessType = null,
        long? businessId = null,
        string? remark = null)
    {
        if (userId <= 0)
        {
            Log.Warning("经验值发放失败：userId 无效（{UserId}），amount={Amount}, expType={ExpType}",
                userId, amount, expType);
            return false;
        }

        if (amount <= 0)
        {
            Log.Warning("经验值发放失败：金额必须大于 0，userId={UserId}, amount={Amount}", userId, amount);
            return false;
        }

        try
        {
            // 使用乐观锁重试机制
            return await ExecuteWithRetryAsync(async () =>
                await GrantExperienceInternalAsync(userId, amount, expType, businessType, businessId, remark)
            );
        }
        catch (Exception ex)
        {
            Log.Error(ex, "经验值发放失败：userId={UserId}, amount={Amount}, expType={ExpType}",
                userId, amount, expType);
            return false;
        }
    }

    /// <summary>
    /// 经验值发放内部实现（带乐观锁和事务）
    /// </summary>
    [UseTran(Propagation = Propagation.Required)]
    private async Task<bool> GrantExperienceInternalAsync(
        long userId,
        int amount,
        string expType,
        string? businessType,
        long? businessId,
        string? remark)
    {
        // 1. 获取或初始化用户经验值记录
        // 注意：不要在初始化后立即重新查询，会因事务未提交而查询不到
        // 重试机制会重新执行整个方法，自然获取到最新版本
        var userExp = await _userExpRepository.QueryFirstAsync(e => e.UserId == userId && !e.IsDeleted);
        if (userExp == null)
        {
            userExp = await InitializeUserExperienceAsync(userId);
            if (userExp == null)
            {
                Log.Error("用户 {UserId} 经验值记录初始化失败", userId);
                return false;
            }
        }

        userExp = await NormalizeFreezeStateAsync(userExp);

        // 2. 检查是否冻结
        if (IsFreezeActive(userExp, DateTime.Now))
        {
            Log.Warning("用户 {UserId} 经验值已冻结，无法获得经验值", userId);
            return false;
        }

        // 3. 检查每日上限（防刷机制）
        var dailyStats = await GetOrCreateDailyStatsAsync(userId, DateTime.Today);
        if (!CheckDailyLimit(dailyStats, amount, expType))
        {
            Log.Warning("用户 {UserId} 经验值已达每日上限，expType={ExpType}, 当日已获得={ExpEarned}",
                userId, expType, dailyStats.ExpEarned);
            return false;
        }

        // 4. 获取所有等级配置
        var levelConfigs = await GetLevelConfigsCacheAsync();

        // 5. 计算新的经验值和等级
        var oldTotalExp = userExp.TotalExp;
        var oldLevel = userExp.CurrentLevel;
        var oldCurrentExp = userExp.CurrentExp;

        var newTotalExp = oldTotalExp + amount;
        var (newLevel, newCurrentExp) = CalculateLevel(newTotalExp, levelConfigs);

        // 6. 更新用户经验值（使用乐观锁）
        var updatedRows = await _userExpRepository.UpdateColumnsAsync(
            e => new UserExperience
            {
                CurrentLevel = newLevel,
                CurrentExp = newCurrentExp,
                TotalExp = newTotalExp,
                LevelUpAt = newLevel > oldLevel ? DateTime.Now : e.LevelUpAt,
                Version = e.Version + 1,
                ModifyTime = DateTime.Now
            },
            e => e.UserId == userId && e.Version == userExp.Version
        );

        if (updatedRows == 0)
        {
            throw new ConcurrencyException("乐观锁冲突：经验值已被其他操作修改");
        }

        // 7. 更新每日统计
        await UpdateDailyStatsAsync(dailyStats, amount, expType);

        // 8. 记录交易日志
        var transaction = new ExpTransaction
        {
            Id = SnowFlakeSingle.instance.getID(),
            UserId = userId,
            ExpType = expType,
            ExpAmount = amount,
            BusinessType = businessType,
            BusinessId = businessId,
            Remark = remark,
            ExpBefore = oldTotalExp,
            ExpAfter = newTotalExp,
            LevelBefore = oldLevel,
            LevelAfter = newLevel,
            CreatedDate = DateTime.Today,
            CreateTime = DateTime.Now
        };

        await _expTransactionRepository.AddAsync(transaction);

        // 7. 记录日志
        Log.Information(
            "经验值发放成功：userId={UserId}, amount={Amount}, expType={ExpType}, " +
            "oldLevel={OldLevel}, newLevel={NewLevel}, oldExp={OldExp}, newExp={NewExp}",
            userId, amount, expType, oldLevel, newLevel, oldTotalExp, newTotalExp);

        // 8. 如果升级，触发升级事件（异步处理）
        if (newLevel > oldLevel)
        {
            Log.Information("用户 {UserId} 从 Lv.{OldLevel} 升级到 Lv.{NewLevel}", userId, oldLevel, newLevel);

            // 异步处理升级奖励和通知，不影响主流程
            _ = Task.Run(async () =>
            {
                try
                {
                    await HandleLevelUpAsync(userId, oldLevel, newLevel);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "处理升级事件失败：userId={UserId}, oldLevel={OldLevel}, newLevel={NewLevel}",
                        userId, oldLevel, newLevel);
                }
            });
        }

        return true;
    }

    /// <summary>
    /// 批量发放经验值
    /// </summary>
    public async Task<int> BatchGrantExperienceAsync(List<ExpGrantInfo> grantInfos)
    {
        int successCount = 0;

        foreach (var info in grantInfos)
        {
            try
            {
                var success = await GrantExperienceAsync(
                    info.UserId,
                    info.Amount,
                    info.ExpType,
                    info.BusinessType,
                    info.BusinessId,
                    info.Remark);

                if (success)
                {
                    successCount++;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "批量发放经验值失败：userId={UserId}, amount={Amount}", info.UserId, info.Amount);
            }
        }

        return successCount;
    }

    #endregion

    #region 等级配置

    /// <summary>
    /// 获取所有等级配置
    /// </summary>
    public async Task<List<LevelConfigVo>> GetLevelConfigsAsync()
    {
        try
        {
            var levelConfigs = await GetLevelConfigsCacheAsync();
            var configVos = Mapper.Map<List<LevelConfigVo>>(levelConfigs);
            FillLevelConfigUrls(configVos);
            return configVos;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取等级配置失败");
            throw;
        }
    }

    /// <summary>
    /// 获取指定等级的配置
    /// </summary>
    public async Task<LevelConfigVo?> GetLevelConfigAsync(int level)
    {
        try
        {
            var levelConfig = (await GetLevelConfigsCacheAsync()).FirstOrDefault(l => l.Level == level);
            if (levelConfig == null)
            {
                return null;
            }

            var configVo = Mapper.Map<LevelConfigVo>(levelConfig);
            FillLevelConfigUrl(configVo);
            return configVo;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取等级 {Level} 配置失败", level);
            throw;
        }
    }

    /// <summary>
    /// 根据累计经验值计算等级
    /// </summary>
    public async Task<(int level, long currentExp)> CalculateLevelAsync(long totalExp)
    {
        var levelConfigs = await GetLevelConfigsCacheAsync();
        return CalculateLevel(totalExp, levelConfigs);
    }

    #endregion

    #region 交易记录查询

    /// <summary>
    /// 获取用户经验值交易记录（分页）
    /// </summary>
    public async Task<PageModel<ExpTransactionVo>> GetTransactionsAsync(
        long userId,
        int pageIndex,
        int pageSize,
        string? expType = null,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        try
        {
            var safePageIndex = NormalizePositivePageIndex(pageIndex);
            var safePageSize = NormalizeTransactionPageSize(pageSize);
            var normalizedExpType = NormalizeOptionalFilter(expType);

            // 构建动态 Where 条件（使用 And 扩展方法组合多个条件）
            Expression<Func<ExpTransaction, bool>> whereExpression = t => t.UserId == userId;

            // 如果有 expType 筛选条件
            if (normalizedExpType != null)
            {
                whereExpression = whereExpression.And(t => t.ExpType == normalizedExpType);
            }

            // 如果有开始日期筛选条件
            if (startDate.HasValue)
            {
                whereExpression = whereExpression.And(t => t.CreateTime >= startDate.Value);
            }

            // 如果有结束日期筛选条件
            if (endDate.HasValue)
            {
                whereExpression = whereExpression.And(t => t.CreateTime <= endDate.Value);
            }

            // 使用 BaseRepository 的分页查询方法（数据库层面筛选、排序和分页）
            var (pagedData, totalCount) = await _expTransactionRepository.QueryPageAsync(
                whereExpression: whereExpression,
                pageIndex: safePageIndex,
                pageSize: safePageSize,
                orderByExpression: t => t.CreateTime,
                orderByType: OrderByType.Desc
            );

            // 映射为 VO
            var transactions = Mapper.Map<List<ExpTransactionVo>>(pagedData);
            await FillTransactionUserNamesAsync(pagedData, transactions);

            var pageCount = (int)Math.Ceiling(totalCount / (double)safePageSize);

            return new PageModel<ExpTransactionVo>
            {
                Page = safePageIndex,
                PageSize = safePageSize,
                DataCount = totalCount,
                PageCount = pageCount,
                Data = transactions
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取用户 {UserId} 交易记录失败", userId);
            throw;
        }
    }

    #endregion

    #region 每日统计

    /// <summary>
    /// 获取用户每日经验值统计
    /// </summary>
    public async Task<UserExpDailyStatsWindowVo> GetDailyStatsAsync(long userId, int days = 7)
    {
        try
        {
            var normalizedDays = days <= 0 ? 7 : Math.Min(days, 30);
            var endDate = DateTime.Today;
            var startDate = endDate.AddDays(-normalizedDays + 1);
            var dailyLimits = GetDailyLimitOptions();

            var stats = await _dailyStatsRepository.QueryAsync(
                s => s.UserId == userId && s.StatDate >= startDate && s.StatDate <= endDate
            );

            var sortedStats = stats.OrderByDescending(s => s.StatDate).ToList();
            var statVos = Mapper.Map<List<UserExpDailyStatsVo>>(sortedStats);
            var normalizedStats = NormalizeDailyStatsWindow(userId, startDate, endDate, statVos, dailyLimits);

            return new UserExpDailyStatsWindowVo
            {
                VoWindowDays = normalizedDays,
                VoStats = normalizedStats,
                VoSummary = BuildDailyStatsSummary(normalizedStats, dailyLimits),
                VoLimits = BuildDailyLimitSnapshot(dailyLimits)
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取用户 {UserId} 每日统计失败", userId);
            throw;
        }
    }

    /// <summary>
    /// 更新每日统计（内部方法，经验值发放时调用）
    /// </summary>
    [UseTran(Propagation = Propagation.Required)]
    public async Task UpdateDailyStatsAsync(long userId, string expType, int amount, DateTime statDate)
    {
        if (userId <= 0)
        {
            Log.Warning("更新每日经验统计失败：userId 无效（{UserId}）", userId);
            return;
        }

        if (amount <= 0)
        {
            Log.Warning("更新每日经验统计失败：amount 必须大于 0，userId={UserId}, amount={Amount}", userId, amount);
            return;
        }

        var stats = await GetOrCreateDailyStatsAsync(userId, statDate);
        await UpdateDailyStatsAsync(stats, amount, expType);
    }

    private static List<UserExpDailyStatsVo> NormalizeDailyStatsWindow(
        long userId,
        DateTime startDate,
        DateTime endDate,
        List<UserExpDailyStatsVo> stats,
        ExperienceDailyLimitSettings dailyLimits)
    {
        var statMap = stats
            .GroupBy(stat => stat.VoStatDate.Date)
            .ToDictionary(group => group.Key, group => group.First());

        var normalizedStats = new List<UserExpDailyStatsVo>();
        for (var date = endDate.Date; date >= startDate.Date; date = date.AddDays(-1))
        {
            if (!statMap.TryGetValue(date, out var stat))
            {
                stat = CreateEmptyDailyStatsVo(userId, date);
            }
            else
            {
                stat.VoStatDate = date;
            }

            stat.VoObservations = BuildDailyStatObservations(stat, dailyLimits);
            normalizedStats.Add(stat);
        }

        return normalizedStats;
    }

    private static UserExpDailyStatsVo CreateEmptyDailyStatsVo(long userId, DateTime statDate)
    {
        return new UserExpDailyStatsVo
        {
            VoId = 0,
            VoUserId = userId,
            VoStatDate = statDate.Date,
            VoExpEarned = 0,
            VoExpFromPost = 0,
            VoExpFromComment = 0,
            VoExpFromLike = 0,
            VoExpFromHighlight = 0,
            VoExpFromLogin = 0,
            VoPostCount = 0,
            VoCommentCount = 0,
            VoLikeGivenCount = 0,
            VoLikeReceivedCount = 0,
            VoObservations = []
        };
    }

    private static UserExpDailyStatsSummaryVo BuildDailyStatsSummary(
        List<UserExpDailyStatsVo> stats,
        ExperienceDailyLimitSettings dailyLimits)
    {
        if (stats.Count == 0)
        {
            return new UserExpDailyStatsSummaryVo
            {
                VoReviewDays = 0,
                VoNotices = ["当前窗口暂无经验统计数据。"]
            };
        }

        long totalExp = 0;
        var zeroGainDays = 0;
        var reviewDays = 0;
        var likeHeavyDays = 0;
        var highlightHeavyDays = 0;
        var cappedDailyLimitDays = 0;
        var nearDailyLimitDays = 0;
        var sourceLimitDays = 0;
        var peakDayExp = long.MinValue;
        DateTime? peakStatDate = null;

        foreach (var stat in stats)
        {
            totalExp += stat.VoExpEarned;
            if (HasWarningObservation(stat))
            {
                reviewDays++;
            }

            if (stat.VoExpEarned <= 0)
            {
                zeroGainDays++;
            }

            if (stat.VoExpEarned >= 20 && stat.VoExpFromLike / (double)stat.VoExpEarned >= 0.6d)
            {
                likeHeavyDays++;
            }

            if (stat.VoExpEarned >= 30 && stat.VoExpFromHighlight / (double)stat.VoExpEarned >= 0.5d)
            {
                highlightHeavyDays++;
            }

            if (HasObservation(stat, "触达总上限"))
            {
                cappedDailyLimitDays++;
            }
            else if (HasObservation(stat, "接近总上限"))
            {
                nearDailyLimitDays++;
            }

            if (
                HasObservation(stat, "点赞触达上限")
                || HasObservation(stat, "点赞接近上限")
                || HasObservation(stat, "高亮触达上限")
                || HasObservation(stat, "高亮接近上限")
            )
            {
                sourceLimitDays++;
            }

            if (stat.VoExpEarned > peakDayExp)
            {
                peakDayExp = stat.VoExpEarned;
                peakStatDate = stat.VoStatDate.Date;
            }
        }

        var notices = new List<string>();
        if (!dailyLimits.EnableDailyLimit)
        {
            notices.Add("当前未启用每日经验上限，观察结果仅基于经验来源结构。");
        }
        else
        {
            if (cappedDailyLimitDays > 0)
            {
                notices.Add($"最近 {stats.Count} 天中有 {cappedDailyLimitDays} 天触达当日总上限。");
            }
            else if (nearDailyLimitDays > 0)
            {
                notices.Add($"最近 {stats.Count} 天中有 {nearDailyLimitDays} 天接近当日总上限。");
            }

            if (sourceLimitDays > 0)
            {
                notices.Add($"其中 {sourceLimitDays} 天的点赞或高亮经验接近来源上限，建议结合行为计数复核。");
            }
        }

        if (zeroGainDays >= 3)
        {
            notices.Add($"最近 {stats.Count} 天中有 {zeroGainDays} 天没有经验增长。");
        }

        if (likeHeavyDays > 0)
        {
            notices.Add($"其中 {likeHeavyDays} 天经验主要来自点赞，建议结合互动来源复核。");
        }

        if (highlightHeavyDays > 0)
        {
            notices.Add($"其中 {highlightHeavyDays} 天经验主要来自高亮评论，建议确认是否集中触发奖励。");
        }

        if (notices.Count == 0)
        {
            notices.Add("最近窗口内经验分布整体平稳，暂未看到明显需要人工复核的集中模式。");
        }

        return new UserExpDailyStatsSummaryVo
        {
            VoTotalExp = totalExp,
            VoAverageExp = stats.Count == 0 ? 0 : totalExp / (double)stats.Count,
            VoPeakDayExp = peakDayExp == long.MinValue ? 0 : peakDayExp,
            VoPeakStatDate = peakStatDate,
            VoZeroGainDays = zeroGainDays,
            VoReviewDays = reviewDays,
            VoNotices = notices
        };
    }

    private static List<UserExpDailyStatObservationVo> BuildDailyStatObservations(
        UserExpDailyStatsVo stat,
        ExperienceDailyLimitSettings dailyLimits)
    {
        if (stat.VoExpEarned <= 0)
        {
            return
            [
                new UserExpDailyStatObservationVo
                {
                    VoLabel = "零增长",
                    VoTone = "default"
                }
            ];
        }

        var observations = new List<UserExpDailyStatObservationVo>();
        var dominantSource = GetDominantSourceObservation(stat);
        if (dominantSource != null)
        {
            observations.Add(dominantSource);
        }

        if (dailyLimits.EnableDailyLimit)
        {
            AddLimitObservation(observations, stat.VoExpEarned, dailyLimits.MaxDailyExp, "总上限");
            AddLimitObservation(observations, stat.VoExpFromLike, dailyLimits.MaxExpFromLike, "点赞");
            AddLimitObservation(observations, stat.VoExpFromHighlight, dailyLimits.MaxExpFromHighlight, "高亮");
        }

        if (stat.VoExpEarned >= 20 && stat.VoExpFromLike / (double)stat.VoExpEarned >= 0.6d)
        {
            observations.Add(new UserExpDailyStatObservationVo
            {
                VoLabel = "点赞占比偏高",
                VoTone = "warning"
            });
        }

        if (stat.VoExpEarned >= 30 && stat.VoExpFromHighlight / (double)stat.VoExpEarned >= 0.5d)
        {
            observations.Add(new UserExpDailyStatObservationVo
            {
                VoLabel = "高亮奖励集中",
                VoTone = "warning"
            });
        }

        return observations;
    }

    private static UserExpDailyLimitSnapshotVo BuildDailyLimitSnapshot(ExperienceDailyLimitSettings dailyLimits)
    {
        return new UserExpDailyLimitSnapshotVo
        {
            VoDailyLimitEnabled = dailyLimits.EnableDailyLimit,
            VoMaxDailyExp = dailyLimits.MaxDailyExp,
            VoMaxExpFromPost = dailyLimits.MaxExpFromPost,
            VoMaxExpFromComment = dailyLimits.MaxExpFromComment,
            VoMaxExpFromLike = dailyLimits.MaxExpFromLike,
            VoMaxExpFromHighlight = dailyLimits.MaxExpFromHighlight,
            VoMaxExpFromLogin = dailyLimits.MaxExpFromLogin
        };
    }

    private static bool HasWarningObservation(UserExpDailyStatsVo stat)
    {
        return stat.VoObservations.Any(observation =>
            string.Equals(observation.VoTone, "warning", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasObservation(UserExpDailyStatsVo stat, string label)
    {
        return stat.VoObservations.Any(observation =>
            string.Equals(observation.VoLabel, label, StringComparison.Ordinal));
    }

    private static void AddLimitObservation(
        ICollection<UserExpDailyStatObservationVo> observations,
        long currentValue,
        int limitValue,
        string labelPrefix)
    {
        if (limitValue <= 0 || currentValue <= 0)
        {
            return;
        }

        if (currentValue >= limitValue)
        {
            observations.Add(new UserExpDailyStatObservationVo
            {
                VoLabel = labelPrefix == "总上限" ? "触达总上限" : $"{labelPrefix}触达上限",
                VoTone = "warning"
            });
            return;
        }

        if (currentValue >= Math.Ceiling(limitValue * 0.8d))
        {
            observations.Add(new UserExpDailyStatObservationVo
            {
                VoLabel = labelPrefix == "总上限" ? "接近总上限" : $"{labelPrefix}接近上限",
                VoTone = "warning"
            });
        }
    }

    private static UserExpDailyStatObservationVo? GetDominantSourceObservation(UserExpDailyStatsVo stat)
    {
        var sourceObservations = new[]
        {
            new UserExpDailyStatObservationVo { VoLabel = "发帖驱动", VoTone = "success" },
            new UserExpDailyStatObservationVo { VoLabel = "评论驱动", VoTone = "processing" },
            new UserExpDailyStatObservationVo { VoLabel = "点赞驱动", VoTone = "warning" },
            new UserExpDailyStatObservationVo { VoLabel = "高亮驱动", VoTone = "warning" },
            new UserExpDailyStatObservationVo { VoLabel = "登录驱动", VoTone = "default" }
        };

        var sourceValues = new[]
        {
            stat.VoExpFromPost,
            stat.VoExpFromComment,
            stat.VoExpFromLike,
            stat.VoExpFromHighlight,
            stat.VoExpFromLogin
        };

        var dominantIndex = 0;
        for (var i = 1; i < sourceValues.Length; i++)
        {
            if (sourceValues[i] > sourceValues[dominantIndex])
            {
                dominantIndex = i;
            }
        }

        return sourceValues[dominantIndex] > 0 ? sourceObservations[dominantIndex] : null;
    }

    #endregion

    #region 排行榜

    /// <summary>
    /// 获取等级排行榜
    /// </summary>
    public async Task<PageModel<LeaderboardItemVo>> GetLeaderboardAsync(
        int pageIndex,
        int pageSize = 50,
        long? currentUserId = null)
    {
        try
        {
            // 限制每页数量
            if (pageSize > 100) pageSize = 100;
            if (pageSize < 1) pageSize = 50;
            if (pageIndex < 1) pageIndex = 1;
            var now = DateTime.Now;

            // 查询排行榜（按 TotalExp 降序，CurrentLevel 降序）
            var (pagedData, totalCount) = await _userExpRepository.QueryPageAsync(
                whereExpression: e =>
                    (!e.ExpFrozen || (e.FrozenUntil != null && e.FrozenUntil <= now)) &&
                    e.UserId > 0 &&
                    !e.IsDeleted,
                pageIndex: pageIndex,
                pageSize: pageSize,
                orderByExpression: e => e.TotalExp,
                orderByType: OrderByType.Desc
            );

            // 获取用户信息
            var userIds = pagedData.Select(e => e.UserId).ToList();
            var users = await _userRepository.QueryAsync(u => userIds.Contains(u.Id));
            var userDict = users.ToDictionary(u => u.Id);

            // 获取等级配置
            var levels = pagedData.Select(e => e.CurrentLevel).Distinct().ToList();
            var levelConfigs = await _levelConfigRepository.QueryAsync(l => levels.Contains(l.Level));
            var levelConfigDict = levelConfigs.ToDictionary(l => l.Level);

            // 计算起始排名
            var startRank = (pageIndex - 1) * pageSize + 1;

            // 映射为 LeaderboardItemVo
            var leaderboard = new List<LeaderboardItemVo>();
            var rank = startRank;
            for (int i = 0; i < pagedData.Count; i++)
            {
                var exp = pagedData[i];
                if (!userDict.TryGetValue(exp.UserId, out var user))
                {
                    Log.Warning("排行榜跳过不存在的用户：userId={UserId}", exp.UserId);
                    continue;
                }

                var item = new LeaderboardItemVo
                {
                    VoRank = rank,
                    VoUserId = exp.UserId,
                    VoUserName = user.UserName,
                    VoCurrentLevel = exp.CurrentLevel,
                    VoCurrentLevelName = levelConfigDict.ContainsKey(exp.CurrentLevel)
                        ? levelConfigDict[exp.CurrentLevel].LevelName
                        : $"Lv.{exp.CurrentLevel}",
                    VoThemeColor = levelConfigDict.ContainsKey(exp.CurrentLevel)
                        ? levelConfigDict[exp.CurrentLevel].ThemeColor
                        : "#9E9E9E",
                    VoTotalExp = exp.TotalExp,
                    VoIsCurrentUser = currentUserId.HasValue && exp.UserId == currentUserId.Value
                };

                leaderboard.Add(item);
                rank++;
            }

            var pageCount = (int)Math.Ceiling(totalCount / (double)pageSize);

            return new PageModel<LeaderboardItemVo>
            {
                Page = pageIndex,
                PageSize = pageSize,
                DataCount = totalCount,
                PageCount = pageCount,
                Data = leaderboard
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取排行榜失败");
            throw;
        }
    }

    /// <summary>
    /// 获取用户排名
    /// </summary>
    public async Task<int> GetUserRankAsync(long userId)
    {
        try
        {
            if (userId <= 0)
            {
                return 0;
            }

            var userExp = await _userExpRepository.QueryFirstAsync(e => e.UserId == userId && !e.IsDeleted);
            if (userExp == null)
            {
                return 0;
            }

            userExp = await NormalizeFreezeStateAsync(userExp);
            if (IsFreezeActive(userExp, DateTime.Now))
            {
                return 0; // 未上榜或仍处于冻结中
            }

            var now = DateTime.Now;

            // 统计比该用户经验值高的用户数量
            var higherCount = await _userExpRepository.QueryCountAsync(
                e =>
                    (!e.ExpFrozen || (e.FrozenUntil != null && e.FrozenUntil <= now)) &&
                    e.UserId > 0 &&
                    e.TotalExp > userExp.TotalExp &&
                    !e.IsDeleted
            );

            return (int)higherCount + 1; // 排名 = 比自己高的数量 + 1
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取用户 {UserId} 排名失败", userId);
            return 0;
        }
    }

    #endregion

    #region 管理员操作

    /// <summary>
    /// 管理员调整用户经验值
    /// </summary>
    public async Task<bool> AdminAdjustExperienceAsync(
        long userId,
        int deltaExp,
        string reason,
        long operatorId,
        string operatorName)
    {
        if (userId <= 0)
        {
            Log.Warning("管理员调整经验失败：userId 无效（{UserId}）", userId);
            return false;
        }

        if (deltaExp == 0)
        {
            Log.Warning("管理员调整经验失败：变动量不能为 0，userId={UserId}", userId);
            return false;
        }

        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? "管理员调整" : reason.Trim();

        try
        {
            return await ExecuteWithRetryAsync(async () =>
                await AdminAdjustExperienceInternalAsync(userId, deltaExp, normalizedReason, operatorId, operatorName));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "管理员调整用户 {UserId} 经验失败，deltaExp={DeltaExp}", userId, deltaExp);
            return false;
        }
    }

    /// <summary>
    /// 冻结用户经验值
    /// </summary>
    public async Task<bool> FreezeExperienceAsync(long userId, DateTime? frozenUntil, string reason, long operatorId, string operatorName)
    {
        if (userId <= 0)
        {
            Log.Warning("冻结用户经验失败：userId 无效（{UserId}）", userId);
            return false;
        }

        if (frozenUntil.HasValue && frozenUntil.Value <= DateTime.Now)
        {
            Log.Warning("冻结用户经验失败：冻结到期时间早于当前时间，userId={UserId}, frozenUntil={FrozenUntil}", userId, frozenUntil);
            return false;
        }

        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? "管理员冻结经验" : reason.Trim();

        try
        {
            return await ExecuteWithRetryAsync(async () =>
                await FreezeExperienceInternalAsync(userId, frozenUntil, normalizedReason, operatorId, operatorName));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "冻结用户 {UserId} 经验失败", userId);
            return false;
        }
    }

    /// <summary>
    /// 解冻用户经验值
    /// </summary>
    public async Task<bool> UnfreezeExperienceAsync(long userId, long operatorId, string operatorName)
    {
        if (userId <= 0)
        {
            Log.Warning("解冻用户经验失败：userId 无效（{UserId}）", userId);
            return false;
        }

        try
        {
            return await ExecuteWithRetryAsync(async () =>
                await UnfreezeExperienceInternalAsync(userId, operatorId, operatorName));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "解冻用户 {UserId} 经验失败", userId);
            return false;
        }
    }

    /// <summary>
    /// 管理员重新计算并更新所有等级配置（根据当前配置文件）
    /// </summary>
    public async Task<List<LevelConfigVo>> RecalculateLevelConfigsAsync(long operatorId, string operatorName)
    {
        try
        {
            Log.Information("管理员 {OperatorName}({OperatorId}) 开始重新计算等级配置", operatorName, operatorId);

            // 1. 使用计算器生成新的经验值配置
            var levelExpData = _experienceCalculator.CalculateAllLevels();
            Log.Information("使用 {FormulaType} 公式计算经验值: {Summary}",
                _experienceCalculator.GetFormulaType(),
                _experienceCalculator.GetConfigSummary());

            // 2. 更新数据库中的等级配置
            var updatedConfigs = new List<LevelConfig>();

            foreach (var (level, (expRequired, expCumulative)) in levelExpData)
            {
                var config = await _levelConfigRepository.QueryFirstAsync(l => l.Level == level);
                if (config != null)
                {
                    // 更新已存在的配置
                    config.ExpRequired = expRequired;
                    config.ExpCumulative = expCumulative;
                    config.ModifyTime = DateTime.Now;
                    config.ModifyBy = operatorName;
                    config.ModifyId = operatorId;

                    await _levelConfigRepository.UpdateAsync(config);
                    updatedConfigs.Add(config);

                    Log.Information("更新等级配置: Lv.{Level} ({Name}) - ExpRequired={ExpRequired}, ExpCumulative={ExpCumulative}",
                        level, config.LevelName, expRequired, expCumulative);
                }
                else
                {
                    Log.Warning("等级 {Level} 配置不存在，跳过更新", level);
                }
            }

            // 3. 清除计算器缓存
            _experienceCalculator.ClearCache();
            await InvalidateLevelConfigsCacheAsync();

            Log.Information("等级配置重新计算完成，共更新 {Count} 个等级", updatedConfigs.Count);

            // 4. 返回更新后的配置列表
            return await GetLevelConfigsAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "重新计算等级配置失败");
            throw;
        }
    }

    #endregion

    #region 私有辅助方法

    /// <summary>
    /// 初始化用户经验值记录（独立事务，避免被父事务回滚）
    /// </summary>
    [UseTran(Propagation = Propagation.RequiresNew)]
    private async Task<UserExperience?> InitializeUserExperienceAsync(long userId)
    {
        if (userId <= 0)
        {
            Log.Warning("初始化用户经验值记录失败：userId 无效（{UserId}）", userId);
            return null;
        }

        // 先检查是否已存在未删除的记录
        var existing = await _userExpRepository.QueryFirstAsync(e => e.UserId == userId && !e.IsDeleted);
        if (existing != null)
        {
            Log.Information("用户 {UserId} 经验值记录已存在（Version={Version}），跳过初始化",
                userId, existing.Version);
            return existing;
        }

        // 检查是否有被软删除的记录
        var deletedExp = await _userExpRepository.QueryFirstAsync(e => e.UserId == userId && e.IsDeleted);
        if (deletedExp != null)
        {
            // 恢复被软删除的经验记录
            await _userExpRepository.UpdateColumnsAsync(
                e => new UserExperience
                {
                    IsDeleted = false,
                    ModifyTime = DateTime.Now,
                    ModifyBy = "System",
                    ModifyId = 0
                },
                e => e.Id == deletedExp.Id);

            Log.Information("恢复用户 {UserId} 的经验值记录（ID={ExpId}）", userId, deletedExp.Id);

            // 重新查询恢复后的记录
            return await _userExpRepository.QueryByIdAsync(deletedExp.Id);
        }

        var userExists = await _userRepository.QueryExistsAsync(u => u.Id == userId);
        if (!userExists)
        {
            Log.Warning("初始化用户经验值记录失败：用户不存在（userId={UserId}）", userId);
            return null;
        }

        var userExp = new UserExperience
        {
            UserId = userId,
            CurrentLevel = 0,
            CurrentExp = 0,
            TotalExp = 0,
            LevelUpAt = null,
            ExpFrozen = false,
            Version = 0,
            CreateTime = DateTime.Now
        };

        try
        {
            await _userExpRepository.AddAsync(userExp);
            Log.Information("用户 {UserId} 经验值记录初始化成功（独立事务）", userId);
            return userExp;
        }
        catch (Exception ex)
        {
            // 并发竞争：其他线程已经初始化了记录
            Log.Information(ex, "用户 {UserId} 经验值记录初始化时检测到并发，尝试查询已存在的记录", userId);

            existing = await _userExpRepository.QueryFirstAsync(e => e.UserId == userId);
            if (existing != null)
            {
                Log.Information("用户 {UserId} 经验值记录已存在（Version={Version}），跳过初始化",
                    userId, existing.Version);
                return existing;
            }

            // 如果仍然查询不到，说明有其他问题
            Log.Error(ex, "用户 {UserId} 经验值记录初始化失败且无法查询到已存在记录", userId);
            return null;
        }
    }

    [UseTran(Propagation = Propagation.Required)]
    private async Task<bool> AdminAdjustExperienceInternalAsync(
        long userId,
        int deltaExp,
        string reason,
        long operatorId,
        string operatorName)
    {
        var userExp = await _userExpRepository.QueryFirstAsync(e => e.UserId == userId && !e.IsDeleted);
        if (userExp == null)
        {
            userExp = await InitializeUserExperienceAsync(userId);
            if (userExp == null)
            {
                Log.Error("管理员调整经验失败：用户 {UserId} 经验值记录初始化失败", userId);
                return false;
            }
        }

        userExp = await NormalizeFreezeStateAsync(userExp);

        var levelConfigs = await GetLevelConfigsCacheAsync();
        var oldTotalExp = userExp.TotalExp;
        var oldLevel = userExp.CurrentLevel;
        var newTotalExp = oldTotalExp + deltaExp;
        var actualDelta = deltaExp;

        if (newTotalExp < 0)
        {
            actualDelta = -(int)oldTotalExp;
            newTotalExp = 0;
        }

        if (actualDelta == 0)
        {
            Log.Warning("管理员调整经验未生效：userId={UserId}, oldTotalExp={OldTotalExp}, deltaExp={DeltaExp}",
                userId, oldTotalExp, deltaExp);
            return false;
        }

        var (newLevel, newCurrentExp) = CalculateLevel(newTotalExp, levelConfigs);
        var now = DateTime.Now;

        var updatedRows = await _userExpRepository.UpdateColumnsAsync(
            e => new UserExperience
            {
                CurrentLevel = newLevel,
                CurrentExp = newCurrentExp,
                TotalExp = newTotalExp,
                LevelUpAt = newLevel > oldLevel ? now : e.LevelUpAt,
                Version = e.Version + 1,
                ModifyTime = now,
                ModifyBy = operatorName,
                ModifyId = operatorId
            },
            e => e.UserId == userId && e.Version == userExp.Version && !e.IsDeleted
        );

        if (updatedRows == 0)
        {
            throw new ConcurrencyException("乐观锁冲突：管理员调整经验时记录已被其他操作修改");
        }

        var transaction = new ExpTransaction
        {
            Id = SnowFlakeSingle.instance.getID(),
            UserId = userId,
            ExpType = actualDelta > 0 ? "ADMIN_ADJUST" : "PENALTY",
            ExpAmount = actualDelta,
            BusinessType = "User",
            BusinessId = userId,
            Remark = reason,
            ExpBefore = oldTotalExp,
            ExpAfter = newTotalExp,
            LevelBefore = oldLevel,
            LevelAfter = newLevel,
            CreatedDate = DateTime.Today,
            TenantId = userExp.TenantId,
            CreateTime = now,
            CreateBy = operatorName,
            CreateId = operatorId
        };
        await _expTransactionRepository.AddAsync(transaction);

        Log.Information(
            "管理员 {OperatorName}({OperatorId}) 调整用户 {UserId} 经验成功，实际变动 {ActualDelta}，总经验 {OldTotalExp} -> {NewTotalExp}",
            operatorName,
            operatorId,
            userId,
            actualDelta,
            oldTotalExp,
            newTotalExp);

        if (newLevel > oldLevel)
        {
            Log.Information("管理员调整触发升级：用户 {UserId} 从 Lv.{OldLevel} 升级到 Lv.{NewLevel}", userId, oldLevel, newLevel);
            _ = Task.Run(async () =>
            {
                try
                {
                    await HandleLevelUpAsync(userId, oldLevel, newLevel);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "处理管理员调整后的升级事件失败：userId={UserId}, oldLevel={OldLevel}, newLevel={NewLevel}",
                        userId, oldLevel, newLevel);
                }
            });
        }

        return true;
    }

    [UseTran(Propagation = Propagation.Required)]
    private async Task<bool> FreezeExperienceInternalAsync(
        long userId,
        DateTime? frozenUntil,
        string reason,
        long operatorId,
        string operatorName)
    {
        var userExp = await _userExpRepository.QueryFirstAsync(e => e.UserId == userId && !e.IsDeleted);
        if (userExp == null)
        {
            userExp = await InitializeUserExperienceAsync(userId);
            if (userExp == null)
            {
                Log.Error("冻结经验失败：用户 {UserId} 经验值记录初始化失败", userId);
                return false;
            }
        }

        var now = DateTime.Now;
        var updatedRows = await _userExpRepository.UpdateColumnsAsync(
            e => new UserExperience
            {
                ExpFrozen = true,
                FrozenUntil = frozenUntil,
                FrozenReason = reason,
                Version = e.Version + 1,
                ModifyTime = now,
                ModifyBy = operatorName,
                ModifyId = operatorId
            },
            e => e.UserId == userId && e.Version == userExp.Version && !e.IsDeleted
        );

        if (updatedRows == 0)
        {
            throw new ConcurrencyException("乐观锁冲突：经验冻结状态已被其他操作修改");
        }

        Log.Information("用户 {UserId} 经验已冻结，frozenUntil={FrozenUntil}, reason={Reason}", userId, frozenUntil, reason);
        return true;
    }

    [UseTran(Propagation = Propagation.Required)]
    private async Task<bool> UnfreezeExperienceInternalAsync(long userId, long operatorId, string operatorName)
    {
        var userExp = await _userExpRepository.QueryFirstAsync(e => e.UserId == userId && !e.IsDeleted);
        if (userExp == null)
        {
            userExp = await InitializeUserExperienceAsync(userId);
            if (userExp == null)
            {
                Log.Error("解冻经验失败：用户 {UserId} 经验值记录初始化失败", userId);
                return false;
            }
        }

        var needsReset = userExp.ExpFrozen || userExp.FrozenUntil.HasValue || !string.IsNullOrWhiteSpace(userExp.FrozenReason);
        if (!needsReset)
        {
            return true;
        }

        var now = DateTime.Now;
        var updatedRows = await _userExpRepository.UpdateColumnsAsync(
            e => new UserExperience
            {
                ExpFrozen = false,
                FrozenUntil = null,
                FrozenReason = string.Empty,
                Version = e.Version + 1,
                ModifyTime = now,
                ModifyBy = operatorName,
                ModifyId = operatorId
            },
            e => e.UserId == userId && e.Version == userExp.Version && !e.IsDeleted
        );

        if (updatedRows == 0)
        {
            throw new ConcurrencyException("乐观锁冲突：经验解冻状态已被其他操作修改");
        }

        Log.Information("用户 {UserId} 经验已解冻", userId);
        return true;
    }

    /// <summary>
    /// 映射实体到 VO（包含额外信息）
    /// </summary>
    private async Task<UserExperienceVo?> MapToVoAsync(UserExperience userExp)
    {
        var vo = Mapper.Map<UserExperienceVo>(userExp);
        if (vo == null) return null;

        // 获取等级配置
        var levelConfigs = await GetLevelConfigsCacheAsync();
        var currentLevelConfig = levelConfigs.FirstOrDefault(l => l.Level == userExp.CurrentLevel);
        var nextLevelConfig = levelConfigs.FirstOrDefault(l => l.Level == userExp.CurrentLevel + 1);

        if (currentLevelConfig != null)
        {
            vo.VoCurrentLevelName = currentLevelConfig.LevelName;
            vo.VoThemeColor = currentLevelConfig.ThemeColor ?? "#9E9E9E";
            vo.VoIconUrl = ResolveAttachmentUrl(currentLevelConfig.IconAttachmentId);
            vo.VoBadgeUrl = ResolveAttachmentUrl(currentLevelConfig.BadgeAttachmentId);
        }

        if (nextLevelConfig != null)
        {
            vo.VoNextLevel = nextLevelConfig.Level;
            vo.VoNextLevelName = nextLevelConfig.LevelName;
            vo.VoExpToNextLevel = nextLevelConfig.ExpCumulative - userExp.TotalExp;

            // 计算进度（当前等级内的进度）
            var currentLevelBaseExp = currentLevelConfig?.ExpCumulative ?? 0;
            var nextLevelBaseExp = nextLevelConfig.ExpCumulative;
            var expRange = nextLevelBaseExp - currentLevelBaseExp;

            if (expRange > 0)
            {
                vo.VoLevelProgress = (double)(userExp.TotalExp - currentLevelBaseExp) / expRange;
            }
            else
            {
                vo.VoLevelProgress = 0;
            }
        }
        else
        {
            // 已达最高等级
            vo.VoNextLevel = userExp.CurrentLevel;
            vo.VoNextLevelName = "已达最高等级";
            vo.VoExpToNextLevel = 0;
            vo.VoLevelProgress = 1.0;
        }

        // 获取用户信息
        var user = await _userRepository.QueryFirstAsync(u => u.Id == userExp.UserId);
        if (user != null)
        {
            vo.VoUserName = user.UserName;
            // TODO: 设置 AvatarUrl
        }

        return vo;
    }

    private async Task FillTransactionUserNamesAsync(
        IReadOnlyList<ExpTransaction> sourceTransactions,
        IReadOnlyList<ExpTransactionVo> transactionVos)
    {
        if (sourceTransactions.Count == 0 || transactionVos.Count == 0)
        {
            return;
        }

        var userIds = sourceTransactions
            .Select(transaction => transaction.UserId)
            .Where(userId => userId > 0)
            .Distinct()
            .ToList();

        if (userIds.Count == 0)
        {
            return;
        }

        var users = await _userRepository.QueryAsync(user => userIds.Contains(user.Id) && !user.IsDeleted);
        var userNameMap = users.ToDictionary(user => user.Id, user => user.UserName);

        for (var index = 0; index < sourceTransactions.Count && index < transactionVos.Count; index++)
        {
            if (userNameMap.TryGetValue(sourceTransactions[index].UserId, out var userName))
            {
                transactionVos[index].VoUserName = userName;
            }
        }
    }

    private void FillLevelConfigUrls(List<LevelConfigVo> configs)
    {
        foreach (var config in configs)
        {
            FillLevelConfigUrl(config);
        }
    }

    private void FillLevelConfigUrl(LevelConfigVo config)
    {
        config.VoIconUrl = ResolveAttachmentUrl(config.VoIconAttachmentId);
        config.VoBadgeUrl = ResolveAttachmentUrl(config.VoBadgeAttachmentId);
    }

    private string? ResolveAttachmentUrl(long? attachmentId)
    {
        if (!attachmentId.HasValue || attachmentId.Value <= 0)
        {
            return null;
        }

        return _attachmentUrlResolver.ResolveAttachmentUrl(attachmentId.Value);
    }

    /// <summary>
    /// 获取等级配置（带缓存）
    /// </summary>
    private async Task<List<LevelConfig>> GetLevelConfigsCacheAsync()
    {
        if (IsExperienceCacheEnabled())
        {
            try
            {
                var cachedConfigs = await _caching.GetAsync<List<LevelConfig>>(LevelConfigsCacheKey);
                if (cachedConfigs != null)
                {
                    return cachedConfigs.OrderBy(l => l.Level).ToList();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "读取等级配置缓存失败，将回退到数据库查询");
            }
        }

        var configs = (await _levelConfigRepository.QueryAsync(l => l.IsEnabled))
            .OrderBy(l => l.Level)
            .ToList();

        if (IsExperienceCacheEnabled())
        {
            try
            {
                await _caching.SetAsync(LevelConfigsCacheKey, configs, GetLevelConfigCacheExpiration());
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "写入等级配置缓存失败");
            }
        }

        return configs;
    }

    private async Task InvalidateLevelConfigsCacheAsync()
    {
        try
        {
            await _caching.RemoveAsync(LevelConfigsCacheKey);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "清除等级配置缓存失败");
        }
    }

    private TimeSpan GetLevelConfigCacheExpiration()
    {
        var cacheMinutes = GetIntConfig("ExperienceCalculator:CacheExpirationMinutes", 60);
        return TimeSpan.FromMinutes(cacheMinutes);
    }

    private async Task<UserExperience> NormalizeFreezeStateAsync(UserExperience userExp)
    {
        var now = DateTime.Now;
        if (!IsFreezeExpired(userExp, now))
        {
            return userExp;
        }

        var updatedRows = await _userExpRepository.UpdateColumnsAsync(
            e => new UserExperience
            {
                ExpFrozen = false,
                FrozenUntil = null,
                FrozenReason = string.Empty,
                Version = e.Version + 1,
                ModifyTime = now,
                ModifyBy = "System",
                ModifyId = 0
            },
            e => e.Id == userExp.Id && e.Version == userExp.Version && !e.IsDeleted
        );

        if (updatedRows > 0)
        {
            userExp.ExpFrozen = false;
            userExp.FrozenUntil = null;
            userExp.FrozenReason = string.Empty;
            userExp.Version += 1;
            userExp.ModifyTime = now;
            userExp.ModifyBy = "System";
            userExp.ModifyId = 0;
            Log.Information("用户 {UserId} 的临时经验冻结已到期，自动释放", userExp.UserId);
            return userExp;
        }

        var refreshed = await _userExpRepository.QueryFirstAsync(e => e.Id == userExp.Id && !e.IsDeleted);
        return refreshed ?? userExp;
    }

    private static bool IsFreezeActive(UserExperience userExp, DateTime referenceTime)
        => userExp.ExpFrozen && (!userExp.FrozenUntil.HasValue || userExp.FrozenUntil > referenceTime);

    private static bool IsFreezeExpired(UserExperience userExp, DateTime referenceTime)
        => userExp.ExpFrozen && userExp.FrozenUntil.HasValue && userExp.FrozenUntil.Value <= referenceTime;

    /// <summary>
    /// 根据总经验值计算等级（纯计算，不访问数据库）
    /// </summary>
    private (int level, long currentExp) CalculateLevel(long totalExp, List<LevelConfig> levelConfigs)
    {
        // 从高到低遍历等级配置
        for (int i = levelConfigs.Count - 1; i >= 0; i--)
        {
            if (totalExp >= levelConfigs[i].ExpCumulative)
            {
                var level = levelConfigs[i].Level;
                var currentExp = totalExp - levelConfigs[i].ExpCumulative;
                return (level, currentExp);
            }
        }

        // 如果没有匹配，返回 0 级
        return (0, totalExp);
    }

    /// <summary>
    /// 执行带重试的异步操作（乐观锁冲突时自动重试）
    /// </summary>
	    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action)
	    {
	        var retryCount = 0;
	        Exception? lastException = null;

        while (retryCount <= MaxRetryCount)
        {
            try
            {
                return await action();
            }
	            catch (ConcurrencyException ex)
	            {
	                lastException = ex;
	                retryCount++;

                if (retryCount > MaxRetryCount)
                {
                    Log.Error(ex, "乐观锁冲突重试 {MaxRetryCount} 次后仍然失败", MaxRetryCount);
                    throw;
	                }

	                var exponentialDelayMs = BaseRetryDelayMs * (int)Math.Pow(2, retryCount - 1);
	                var cappedDelayMs = Math.Min(exponentialDelayMs, MaxRetryDelayMs);
	                var delayMs = Random.Shared.Next(0, cappedDelayMs + 1);
	                Log.Warning("乐观锁冲突，第 {RetryCount} 次重试（延迟 {DelayMs}ms）: {Message}",
	                    retryCount, delayMs, ex.Message);

	                await Task.Delay(delayMs);
	            }
        }

        // 理论上不会执行到这里，但为了类型安全
        throw lastException ?? new ConcurrencyException("重试失败");
    }

    /// <summary>
    /// 处理用户升级事件（发放萝卜币奖励、推送通知）
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="oldLevel">旧等级</param>
    /// <param name="newLevel">新等级</param>
    private async Task HandleLevelUpAsync(long userId, int oldLevel, int newLevel)
    {
        try
        {
            // 1. 获取新等级配置
            var newLevelConfig = await _levelConfigRepository.QueryFirstAsync(l => l.Level == newLevel && l.IsEnabled);
            if (newLevelConfig == null)
            {
                Log.Warning("未找到等级 {Level} 的配置，跳过升级奖励", newLevel);
                return;
            }

            // 2. 获取旧等级名称（用于通知）
            var oldLevelConfig = await _levelConfigRepository.QueryFirstAsync(l => l.Level == oldLevel && l.IsEnabled);
            var oldLevelName = oldLevelConfig?.LevelName ?? $"Lv.{oldLevel}";

            // 3. 计算并发放萝卜币奖励（等级 × 100）
            var coinReward = newLevel * 100;
            if (coinReward > 0)
            {
                try
                {
                    var transactionNo = await _coinService.GrantCoinAsync(
                        userId: userId,
                        amount: coinReward,
                        transactionType: "LEVEL_UP_REWARD",
                        businessType: "User",
                        businessId: userId,
                        remark: $"升级到 Lv.{newLevel} {newLevelConfig.LevelName}"
                    );

                    if (!string.IsNullOrEmpty(transactionNo))
                    {
                        Log.Information("升级萝卜币奖励发放成功：userId={UserId}, level={Level}, reward={Reward}, transactionNo={TransactionNo}",
                            userId, newLevel, coinReward, transactionNo);
                    }
                    else
                    {
                        Log.Warning("升级萝卜币奖励发放失败：userId={UserId}, level={Level}",
                            userId, newLevel);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "发放升级萝卜币奖励时出错：userId={UserId}, level={Level}",
                        userId, newLevel);
                }
            }

            // 4. 推送升级通知
            try
            {
                var notificationContent = coinReward > 0
                    ? $"恭喜你从 {oldLevelName} 升级到 {newLevelConfig.LevelName}！获得 {coinReward} 个萝卜币奖励！"
                    : $"恭喜你从 {oldLevelName} 升级到 {newLevelConfig.LevelName}！";

                await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                {
                    Type = NotificationType.LevelUp,
                    Title = "等级提升",
                    Content = notificationContent,
                    Priority = (int)NotificationPriority.High,
                    BusinessType = BusinessType.User,
                    BusinessId = userId,
                    TriggerId = null, // 系统触发
                    TriggerName = "系统",
                    TriggerAvatar = null,
                    ReceiverUserIds = new List<long> { userId }
                });

                Log.Information("升级通知推送成功：userId={UserId}, oldLevel={OldLevel}, newLevel={NewLevel}",
                    userId, oldLevel, newLevel);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "推送升级通知时出错：userId={UserId}, level={Level}",
                    userId, newLevel);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "处理升级事件时出错：userId={UserId}, oldLevel={OldLevel}, newLevel={NewLevel}",
                userId, oldLevel, newLevel);
            throw;
        }
    }

    /// <summary>
    /// 获取或创建每日统计记录
    /// </summary>
    private async Task<UserExpDailyStats> GetOrCreateDailyStatsAsync(long userId, DateTime statDate)
    {
        var stats = await _dailyStatsRepository.QueryFirstAsync(
            s => s.UserId == userId && s.StatDate == statDate.Date);

        if (stats == null)
        {
            stats = new UserExpDailyStats
            {
                UserId = userId,
                StatDate = statDate.Date,
                ExpEarned = 0,
                ExpFromPost = 0,
                ExpFromComment = 0,
                ExpFromLike = 0,
                ExpFromHighlight = 0,
                ExpFromLogin = 0,
                PostCount = 0,
                CommentCount = 0,
                LikeGivenCount = 0,
                LikeReceivedCount = 0,
                CreateTime = DateTime.Now
            };

            await _dailyStatsRepository.AddAsync(stats);
        }

        return stats;
    }

    /// <summary>
    /// 检查每日上限
    /// </summary>
    private bool CheckDailyLimit(UserExpDailyStats dailyStats, int amount, string expType)
    {
        var dailyLimits = GetDailyLimitOptions();
        if (!dailyLimits.EnableDailyLimit)
        {
            return true;
        }

        // 检查总经验值上限
        if (dailyStats.ExpEarned + amount > dailyLimits.MaxDailyExp)
        {
            Log.Warning("用户每日总经验值已达上限：userId={UserId}, current={Current}, max={Max}",
                dailyStats.UserId, dailyStats.ExpEarned, dailyLimits.MaxDailyExp);
            return false;
        }

        // 检查各类型经验值上限
        switch (expType)
        {
            case "POST_CREATE":
            case "FIRST_POST":
                if (dailyStats.ExpFromPost + amount > dailyLimits.MaxExpFromPost)
                {
                    Log.Warning("用户每日发帖经验值已达上限：userId={UserId}, current={Current}, max={Max}",
                        dailyStats.UserId, dailyStats.ExpFromPost, dailyLimits.MaxExpFromPost);
                    return false;
                }
                break;

            case "COMMENT_CREATE":
            case "FIRST_COMMENT":
                if (dailyStats.ExpFromComment + amount > dailyLimits.MaxExpFromComment)
                {
                    Log.Warning("用户每日评论经验值已达上限：userId={UserId}, current={Current}, max={Max}",
                        dailyStats.UserId, dailyStats.ExpFromComment, dailyLimits.MaxExpFromComment);
                    return false;
                }
                break;

            case "POST_LIKED":
            case "COMMENT_LIKED":
            case "RECEIVE_LIKE":
            case "LIKE_OTHERS":
            case "GIVE_LIKE":
                if (dailyStats.ExpFromLike + amount > dailyLimits.MaxExpFromLike)
                {
                    Log.Warning("用户每日点赞经验值已达上限：userId={UserId}, current={Current}, max={Max}",
                        dailyStats.UserId, dailyStats.ExpFromLike, dailyLimits.MaxExpFromLike);
                    return false;
                }
                break;

            case "GOD_COMMENT":
            case "SOFA_COMMENT":
                if (dailyStats.ExpFromHighlight + amount > dailyLimits.MaxExpFromHighlight)
                {
                    Log.Warning("用户每日神评/沙发经验值已达上限：userId={UserId}, current={Current}, max={Max}",
                        dailyStats.UserId, dailyStats.ExpFromHighlight, dailyLimits.MaxExpFromHighlight);
                    return false;
                }
                break;

            case "DAILY_LOGIN":
            case "WEEKLY_LOGIN":
            case "CONTINUOUS_LOGIN":
                if (dailyStats.ExpFromLogin + amount > dailyLimits.MaxExpFromLogin)
                {
                    Log.Warning("用户每日登录经验值已达上限：userId={UserId}, current={Current}, max={Max}",
                        dailyStats.UserId, dailyStats.ExpFromLogin, dailyLimits.MaxExpFromLogin);
                    return false;
                }
                break;
        }

        return true;
    }

    private ExperienceDailyLimitSettings GetDailyLimitOptions()
    {
        return new ExperienceDailyLimitSettings
        {
            EnableDailyLimit = GetBoolConfig("ExperienceCalculator:DailyLimits:EnableDailyLimit", true),
            MaxDailyExp = GetIntConfig("ExperienceCalculator:DailyLimits:MaxDailyExp", 500),
            MaxExpFromPost = GetIntConfig("ExperienceCalculator:DailyLimits:MaxExpFromPost", 100),
            MaxExpFromComment = GetIntConfig("ExperienceCalculator:DailyLimits:MaxExpFromComment", 100),
            MaxExpFromLike = GetIntConfig("ExperienceCalculator:DailyLimits:MaxExpFromLike", 50),
            MaxExpFromHighlight = GetIntConfig("ExperienceCalculator:DailyLimits:MaxExpFromHighlight", 200),
            MaxExpFromLogin = GetIntConfig("ExperienceCalculator:DailyLimits:MaxExpFromLogin", 20)
        };
    }

    private static bool IsExperienceCacheEnabled()
    {
        return GetBoolConfig("ExperienceCalculator:EnableCache", true);
    }

    private static int GetIntConfig(string path, int fallbackValue)
    {
        var rawValue = AppSettingsTool.GetValue(path);
        return int.TryParse(rawValue, out var parsedValue) && parsedValue > 0
            ? parsedValue
            : fallbackValue;
    }

    private static bool GetBoolConfig(string path, bool fallbackValue)
    {
        var rawValue = AppSettingsTool.GetValue(path);
        return bool.TryParse(rawValue, out var parsedValue)
            ? parsedValue
            : fallbackValue;
    }

    private static int NormalizePositivePageIndex(int pageIndex)
    {
        return pageIndex > 0 ? pageIndex : 1;
    }

    private static int NormalizeTransactionPageSize(int pageSize)
    {
        if (pageSize <= 0)
        {
            return 20;
        }

        return Math.Min(pageSize, MaxTransactionPageSize);
    }

    private static string? NormalizeOptionalFilter(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// 更新每日统计
    /// </summary>
    private async Task UpdateDailyStatsAsync(UserExpDailyStats dailyStats, int amount, string expType)
    {
        // 更新总经验值
        dailyStats.ExpEarned += amount;

        // 更新各类型经验值
        switch (expType)
        {
            case "POST_CREATE":
            case "FIRST_POST":
                dailyStats.ExpFromPost += amount;
                dailyStats.PostCount++;
                break;

            case "COMMENT_CREATE":
            case "FIRST_COMMENT":
                dailyStats.ExpFromComment += amount;
                dailyStats.CommentCount++;
                break;

            case "POST_LIKED":
            case "COMMENT_LIKED":
            case "RECEIVE_LIKE":
                dailyStats.ExpFromLike += amount;
                dailyStats.LikeReceivedCount++;
                break;

            case "LIKE_OTHERS":
            case "GIVE_LIKE":
                dailyStats.ExpFromLike += amount;
                dailyStats.LikeGivenCount++;
                break;

            case "GOD_COMMENT":
            case "SOFA_COMMENT":
                dailyStats.ExpFromHighlight += amount;
                break;

            case "DAILY_LOGIN":
            case "WEEKLY_LOGIN":
            case "CONTINUOUS_LOGIN":
                dailyStats.ExpFromLogin += amount;
                break;
        }

        dailyStats.ModifyTime = DateTime.Now;
        await _dailyStatsRepository.UpdateAsync(dailyStats);
    }

    private sealed class ExperienceDailyLimitSettings
    {
        public bool EnableDailyLimit { get; init; }

        public int MaxDailyExp { get; init; }

        public int MaxExpFromPost { get; init; }

        public int MaxExpFromComment { get; init; }

        public int MaxExpFromLike { get; init; }

        public int MaxExpFromHighlight { get; init; }

        public int MaxExpFromLogin { get; init; }
    }

    #endregion
}
