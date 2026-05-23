using Radish.Model;
using Radish.Model.ViewModels;
using Serilog;

namespace Radish.Service;

public partial class ExperienceService
{
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

    #endregion
}
