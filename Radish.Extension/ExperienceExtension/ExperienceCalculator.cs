using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Serilog;
using System.Text.Json;

namespace Radish.Extension.ExperienceExtension;

/// <summary>
/// 经验值计算器实现
/// </summary>
public class ExperienceCalculator : IExperienceCalculator
{
    private readonly ExperienceCalculatorOptions _options;
    private readonly IDistributedCache? _cache;
    private readonly string _cacheKey = "ExperienceCalculator:AllLevels";

    public ExperienceCalculator(
        IOptions<ExperienceCalculatorOptions> options,
        IDistributedCache? cache = null)
    {
        _options = options.Value;
        _cache = cache;
    }

    /// <summary>
    /// 计算指定等级升到下一级所需的经验值
    /// </summary>
    public long CalculateExpRequired(int level)
    {
        if (level < 0 || level > _options.MaxLevel)
        {
            Log.Warning("等级 {Level} 超出范围 [0, {MaxLevel}]", level, _options.MaxLevel);
            return 0;
        }

        // 达到最高等级，不需要更多经验
        if (level >= _options.MaxLevel)
        {
            return 0;
        }

        long expRequired = _options.FormulaType switch
        {
            "Exponential" => CalculateExponential(level),
            "Polynomial" => CalculatePolynomial(level),
            "Hybrid" => CalculateHybrid(level),
            "Segmented" => CalculateSegmented(level),
            _ => CalculateHybrid(level) // 默认使用混合公式
        };

        // 应用最小经验值限制
        return Math.Max(expRequired, _options.MinExpRequired);
    }

    /// <summary>
    /// 计算达到指定等级所需的累计经验值
    /// </summary>
    public long CalculateExpCumulative(int level)
    {
        if (level <= 0)
        {
            return 0;
        }

        long cumulative = 0;
        for (int i = 0; i < level; i++)
        {
            cumulative += CalculateExpRequired(i);
        }

        return cumulative;
    }

    /// <summary>
    /// 批量计算所有等级的经验值配置
    /// </summary>
    public Dictionary<int, (long ExpRequired, long ExpCumulative)> CalculateAllLevels()
    {
        // 尝试从缓存读取
        if (_options.EnableCache && _cache != null)
        {
            try
            {
                var cachedData = _cache.GetString(_cacheKey);
                if (!string.IsNullOrEmpty(cachedData))
                {
                    var cached = JsonSerializer.Deserialize<Dictionary<int, (long ExpRequired, long ExpCumulative)>>(cachedData);
                    if (cached != null)
                    {
                        Log.Information("从缓存加载经验值配置");
                        return cached;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "从缓存读取经验值配置失败，将重新计算");
            }
        }

        // 计算所有等级
        var result = new Dictionary<int, (long ExpRequired, long ExpCumulative)>();
        long cumulative = 0;

        for (int level = 0; level <= _options.MaxLevel; level++)
        {
            long expRequired = CalculateExpRequired(level);
            result[level] = (expRequired, cumulative);
            cumulative += expRequired;
        }

        // 写入缓存
        if (_options.EnableCache && _cache != null)
        {
            try
            {
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheExpirationMinutes)
                };
                var serialized = JsonSerializer.Serialize(result);
                _cache.SetString(_cacheKey, serialized, cacheOptions);
                Log.Information("经验值配置已缓存，过期时间 {Minutes} 分钟", _options.CacheExpirationMinutes);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "缓存经验值配置失败");
            }
        }

        return result;
    }

    /// <summary>
    /// 获取当前使用的公式类型
    /// </summary>
    public string GetFormulaType() => _options.FormulaType;

    /// <summary>
    /// 获取当前配置的摘要信息
    /// </summary>
    public string GetConfigSummary()
    {
        return _options.FormulaType switch
        {
            "Exponential" => $"指数公式: {_options.BaseExp} × {_options.Multiplier}^level × {_options.ScaleFactor}",
            "Polynomial" => $"多项式: {_options.PolynomialA}×level³ + {_options.PolynomialB}×level² + {_options.PolynomialC}×level + {_options.PolynomialD}",
            "Hybrid" => $"混合公式: {_options.BaseExp} × (level+1)^{_options.Exponent} × {_options.ScaleFactor}",
            "Segmented" => $"分段公式: 阈值 [{_options.SegmentThreshold1}, {_options.SegmentThreshold2}], 指数 [{_options.SegmentExponent1}, {_options.SegmentExponent2}, {_options.SegmentExponent3}]",
            _ => "未知公式"
        };
    }

    #region 私有计算方法

    /// <summary>
    /// 指数增长公式: baseExp × multiplier^level × scaleFactor
    /// </summary>
    private long CalculateExponential(int level)
    {
        double exp = _options.BaseExp * Math.Pow(_options.Multiplier, level) * _options.ScaleFactor;
        return (long)Math.Floor(exp);
    }

    /// <summary>
    /// 多项式公式: a×level³ + b×level² + c×level + d
    /// </summary>
    private long CalculatePolynomial(int level)
    {
        double exp = _options.PolynomialA * Math.Pow(level, 3)
                   + _options.PolynomialB * Math.Pow(level, 2)
                   + _options.PolynomialC * level
                   + _options.PolynomialD;

        exp *= _options.ScaleFactor;
        return (long)Math.Floor(exp);
    }

    /// <summary>
    /// 混合公式: baseExp × (level+1)^exponent × scaleFactor
    /// </summary>
    private long CalculateHybrid(int level)
    {
        double exp = _options.BaseExp * Math.Pow(level + 1, _options.Exponent) * _options.ScaleFactor;
        return (long)Math.Floor(exp);
    }

    /// <summary>
    /// 分段公式: 不同等级区间使用不同的指数
    /// </summary>
    private long CalculateSegmented(int level)
    {
        double exponent;

        if (level <= _options.SegmentThreshold1)
        {
            exponent = _options.SegmentExponent1;
        }
        else if (level <= _options.SegmentThreshold2)
        {
            exponent = _options.SegmentExponent2;
        }
        else
        {
            exponent = _options.SegmentExponent3;
        }

        double exp = _options.BaseExp * Math.Pow(level + 1, exponent) * _options.ScaleFactor;
        return (long)Math.Floor(exp);
    }

    #endregion

    /// <summary>
    /// 清除缓存（配置更新时调用）
    /// </summary>
    public void ClearCache()
    {
        if (_cache != null)
        {
            try
            {
                _cache.Remove(_cacheKey);
                Log.Information("经验值配置缓存已清除");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "清除经验值配置缓存失败");
            }
        }
    }
}
