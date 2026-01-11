using AutoMapper;
using Radish.Common;
using Radish.Common.AttributeTool;
using Radish.Common.Exceptions;
using Radish.Infrastructure;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Serilog;
using SqlSugar;

namespace Radish.Service;

/// <summary>经验值服务实现</summary>
public class ExperienceService : BaseService<UserExperience, UserExperienceVo>, IExperienceService
{
    private readonly IBaseRepository<UserExperience> _userExpRepository;
    private readonly IBaseRepository<ExpTransaction> _expTransactionRepository;
    private readonly IBaseRepository<LevelConfig> _levelConfigRepository;
    private readonly IBaseRepository<UserExpDailyStats> _dailyStatsRepository;
    private readonly IBaseRepository<User> _userRepository;

    /// <summary>乐观锁冲突重试次数</summary>
    private const int MaxRetryCount = 3;

    /// <summary>重试基础延迟（毫秒）</summary>
    private const int BaseRetryDelayMs = 100;

    public ExperienceService(
        IMapper mapper,
        IBaseRepository<UserExperience> userExpRepository,
        IBaseRepository<ExpTransaction> expTransactionRepository,
        IBaseRepository<LevelConfig> levelConfigRepository,
        IBaseRepository<UserExpDailyStats> dailyStatsRepository,
        IBaseRepository<User> userRepository)
        : base(mapper, userExpRepository)
    {
        _userExpRepository = userExpRepository;
        _expTransactionRepository = expTransactionRepository;
        _levelConfigRepository = levelConfigRepository;
        _dailyStatsRepository = dailyStatsRepository;
        _userRepository = userRepository;
    }

    #region 经验值查询

    /// <summary>
    /// 获取用户经验值信息
    /// </summary>
    public async Task<UserExperienceVo?> GetUserExperienceAsync(long userId)
    {
        try
        {
            var userExp = await _userExpRepository.QueryFirstAsync(e => e.Id == userId);

            if (userExp == null)
            {
                // 如果用户经验值记录不存在，自动创建初始记录
                Log.Information("用户 {UserId} 经验值记录不存在，自动创建初始记录", userId);
                userExp = await InitializeUserExperienceAsync(userId);
            }

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
                e => userIds.Contains(e.Id)
            );

            var result = new Dictionary<long, UserExperienceVo>();

            foreach (var userExp in userExps)
            {
                var vo = await MapToVoAsync(userExp);
                if (vo != null)
                {
                    result[userExp.Id] = vo;
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
        var userExp = await _userExpRepository.QueryFirstAsync(e => e.Id == userId);
        if (userExp == null)
        {
            userExp = await InitializeUserExperienceAsync(userId);
        }

        // 2. 检查是否冻结
        if (userExp.ExpFrozen && userExp.FrozenUntil.HasValue && userExp.FrozenUntil > DateTime.Now)
        {
            Log.Warning("用户 {UserId} 经验值已冻结，无法获得经验值", userId);
            return false;
        }

        // 3. 获取所有等级配置
        var levelConfigs = await GetLevelConfigsCacheAsync();

        // 4. 计算新的经验值和等级
        var oldTotalExp = userExp.TotalExp;
        var oldLevel = userExp.CurrentLevel;
        var oldCurrentExp = userExp.CurrentExp;

        var newTotalExp = oldTotalExp + amount;
        var (newLevel, newCurrentExp) = CalculateLevel(newTotalExp, levelConfigs);

        // 5. 更新用户经验值（使用乐观锁）
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
            e => e.Id == userId && e.Version == userExp.Version
        );

        if (updatedRows == 0)
        {
            throw new ConcurrencyException("乐观锁冲突：经验值已被其他操作修改");
        }

        // 6. 记录交易日志
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

        // 8. P1 阶段：如果升级，触发升级事件（暂时留空）
        if (newLevel > oldLevel)
        {
            Log.Information("用户 {UserId} 从 Lv.{OldLevel} 升级到 Lv.{NewLevel}", userId, oldLevel, newLevel);
            // TODO P1: 触发升级事件（萝卜币奖励、通知推送）
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
            var levelConfigs = await _levelConfigRepository.QueryAsync(l => l.IsEnabled);

            // 手动排序
            var sortedConfigs = levelConfigs.OrderBy(l => l.Level).ToList();

            return Mapper.Map<List<LevelConfigVo>>(sortedConfigs);
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
            var levelConfig = await _levelConfigRepository.QueryFirstAsync(l => l.Level == level && l.IsEnabled);
            return levelConfig == null ? null : Mapper.Map<LevelConfigVo>(levelConfig);
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
            // 构建查询条件
            var where = new Func<ExpTransaction, bool>(t => t.UserId == userId);

            if (!string.IsNullOrEmpty(expType))
            {
                var prevWhere = where;
                where = t => prevWhere(t) && t.ExpType == expType;
            }

            if (startDate.HasValue)
            {
                var prevWhere = where;
                where = t => prevWhere(t) && t.CreateTime >= startDate.Value;
            }

            if (endDate.HasValue)
            {
                var prevWhere = where;
                where = t => prevWhere(t) && t.CreateTime <= endDate.Value;
            }

            // 分页查询
            var allRecords = await _expTransactionRepository.QueryAsync(t => where(t));
            var sortedRecords = allRecords.OrderByDescending(t => t.CreateTime).ToList();

            var totalCount = sortedRecords.Count;
            var pageCount = (int)Math.Ceiling(totalCount / (double)pageSize);
            var pagedData = sortedRecords.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();

            // 映射为 VO
            var transactions = Mapper.Map<List<ExpTransactionVo>>(pagedData);

            return new PageModel<ExpTransactionVo>
            {
                Page = pageIndex,
                PageSize = pageSize,
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
    public async Task<List<UserExpDailyStatsVo>> GetDailyStatsAsync(long userId, int days = 7)
    {
        try
        {
            if (days > 30) days = 30; // 最大 30 天

            var startDate = DateTime.Today.AddDays(-days + 1);

            var stats = await _dailyStatsRepository.QueryAsync(
                s => s.UserId == userId && s.StatDate >= startDate
            );

            // 手动排序
            var sortedStats = stats.OrderByDescending(s => s.StatDate).ToList();

            return Mapper.Map<List<UserExpDailyStatsVo>>(sortedStats);
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
    public async Task UpdateDailyStatsAsync(long userId, string expType, int amount, DateTime statDate)
    {
        // TODO P1: 实现每日统计更新
        await Task.CompletedTask;
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
        // TODO P1: 实现排行榜查询
        return new PageModel<LeaderboardItemVo>
        {
            Page = pageIndex,
            PageSize = pageSize,
            DataCount = 0,
            PageCount = 0,
            Data = new List<LeaderboardItemVo>()
        };
    }

    /// <summary>
    /// 获取用户排名
    /// </summary>
    public async Task<int> GetUserRankAsync(long userId)
    {
        // TODO P1: 实现排名查询
        return 0;
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
        // TODO P1: 实现管理员调整
        return await GrantExperienceAsync(
            userId,
            Math.Abs(deltaExp),
            deltaExp > 0 ? "ADMIN_ADJUST" : "PENALTY",
            "User",
            userId,
            reason);
    }

    /// <summary>
    /// 冻结用户经验值
    /// </summary>
    public async Task<bool> FreezeExperienceAsync(long userId, DateTime? frozenUntil, string reason)
    {
        // TODO P1: 实现冻结功能
        return false;
    }

    /// <summary>
    /// 解冻用户经验值
    /// </summary>
    public async Task<bool> UnfreezeExperienceAsync(long userId)
    {
        // TODO P1: 实现解冻功能
        return false;
    }

    #endregion

    #region 私有辅助方法

    /// <summary>
    /// 初始化用户经验值记录
    /// </summary>
    private async Task<UserExperience> InitializeUserExperienceAsync(long userId)
    {
        var userExp = new UserExperience
        {
            Id = userId,
            CurrentLevel = 0,
            CurrentExp = 0,
            TotalExp = 0,
            LevelUpAt = null,
            ExpFrozen = false,
            Version = 0,
            CreateTime = DateTime.Now
        };

        await _userExpRepository.AddAsync(userExp);
        Log.Information("用户 {UserId} 经验值记录初始化成功", userId);

        return userExp;
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
            vo.CurrentLevelName = currentLevelConfig.LevelName;
            vo.ThemeColor = currentLevelConfig.ThemeColor ?? "#9E9E9E";
            vo.IconUrl = currentLevelConfig.IconUrl;
            vo.BadgeUrl = currentLevelConfig.BadgeUrl;
        }

        if (nextLevelConfig != null)
        {
            vo.NextLevel = nextLevelConfig.Level;
            vo.NextLevelName = nextLevelConfig.LevelName;
            vo.ExpToNextLevel = nextLevelConfig.ExpCumulative - userExp.TotalExp;

            // 计算进度（当前等级内的进度）
            var currentLevelBaseExp = currentLevelConfig?.ExpCumulative ?? 0;
            var nextLevelBaseExp = nextLevelConfig.ExpCumulative;
            var expRange = nextLevelBaseExp - currentLevelBaseExp;

            if (expRange > 0)
            {
                vo.LevelProgress = (double)(userExp.TotalExp - currentLevelBaseExp) / expRange;
            }
            else
            {
                vo.LevelProgress = 0;
            }
        }
        else
        {
            // 已达最高等级
            vo.NextLevel = userExp.CurrentLevel;
            vo.NextLevelName = "已达最高等级";
            vo.ExpToNextLevel = 0;
            vo.LevelProgress = 1.0;
        }

        // 获取用户信息
        var user = await _userRepository.QueryFirstAsync(u => u.Id == userExp.Id);
        if (user != null)
        {
            vo.UserName = user.UserName;
            // TODO: 设置 AvatarUrl
        }

        return vo;
    }

    /// <summary>
    /// 获取等级配置（带缓存）
    /// </summary>
    private async Task<List<LevelConfig>> GetLevelConfigsCacheAsync()
    {
        // TODO P1: 添加缓存
        var configs = await _levelConfigRepository.QueryAsync(l => l.IsEnabled);
        return configs.OrderBy(l => l.Level).ToList();
    }

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

                // 指数退避：100ms * 2^(retryCount-1)
                var delayMs = BaseRetryDelayMs * (int)Math.Pow(2, retryCount - 1);
                Log.Warning("乐观锁冲突，第 {RetryCount} 次重试（延迟 {DelayMs}ms）: {Message}",
                    retryCount, delayMs, ex.Message);

                await Task.Delay(delayMs);
            }
        }

        // 理论上不会执行到这里，但为了类型安全
        throw lastException ?? new ConcurrencyException("重试失败");
    }

    #endregion
}
