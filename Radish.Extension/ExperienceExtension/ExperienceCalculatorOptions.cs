using Radish.Common.ConfigOptions;

namespace Radish.Extension.ExperienceExtension;

/// <summary>
/// 经验值计算配置选项
/// </summary>
public class ExperienceCalculatorOptions : IConfigurableOptions
{
    /// <summary>
    /// 计算公式类型
    /// </summary>
    /// <remarks>
    /// - Exponential: 指数增长公式
    /// - Polynomial: 多项式公式
    /// - Hybrid: 混合公式（默认）
    /// - Segmented: 分段公式
    /// </remarks>
    public string FormulaType { get; set; } = "Hybrid";

    /// <summary>
    /// 基础经验值
    /// </summary>
    /// <remarks>所有公式的基础值，建议范围 50-200</remarks>
    public double BaseExp { get; set; } = 100.0;

    /// <summary>
    /// 指数增长倍数
    /// </summary>
    /// <remarks>用于 Exponential 公式，建议范围 1.5-2.5，越大增长越快</remarks>
    public double Multiplier { get; set; } = 2.0;

    /// <summary>
    /// 指数幂次
    /// </summary>
    /// <remarks>用于 Hybrid 和 Polynomial 公式，建议范围 1.5-2.5，控制曲线陡度</remarks>
    public double Exponent { get; set; } = 2.0;

    /// <summary>
    /// 缩放因子
    /// </summary>
    /// <remarks>全局缩放系数，可用于后期调整整体难度，建议范围 0.5-2.0</remarks>
    public double ScaleFactor { get; set; } = 1.0;

    #region 多项式系数

    /// <summary>
    /// 多项式系数 A (level^3 项)
    /// </summary>
    public double PolynomialA { get; set; } = 10.0;

    /// <summary>
    /// 多项式系数 B (level^2 项)
    /// </summary>
    public double PolynomialB { get; set; } = 50.0;

    /// <summary>
    /// 多项式系数 C (level 项)
    /// </summary>
    public double PolynomialC { get; set; } = 100.0;

    /// <summary>
    /// 多项式系数 D (常数项)
    /// </summary>
    public double PolynomialD { get; set; } = 0.0;

    #endregion

    #region 分段公式配置

    /// <summary>
    /// 分段阈值 1（低级到中级）
    /// </summary>
    public int SegmentThreshold1 { get; set; } = 3;

    /// <summary>
    /// 分段阈值 2（中级到高级）
    /// </summary>
    public int SegmentThreshold2 { get; set; } = 7;

    /// <summary>
    /// 低级段指数
    /// </summary>
    public double SegmentExponent1 { get; set; } = 1.0;

    /// <summary>
    /// 中级段指数
    /// </summary>
    public double SegmentExponent2 { get; set; } = 1.5;

    /// <summary>
    /// 高级段指数
    /// </summary>
    public double SegmentExponent3 { get; set; } = 2.0;

    #endregion

    /// <summary>
    /// 最小经验值要求
    /// </summary>
    /// <remarks>防止计算结果过小，建议至少 10</remarks>
    public long MinExpRequired { get; set; } = 10;

    /// <summary>
    /// 最大等级
    /// </summary>
    /// <remarks>系统支持的最大等级，默认 10（飞升）</remarks>
    public int MaxLevel { get; set; } = 10;

    /// <summary>
    /// 是否启用缓存
    /// </summary>
    /// <remarks>计算结果是否缓存，建议开启以提升性能</remarks>
    public bool EnableCache { get; set; } = true;

    /// <summary>
    /// 缓存过期时间（分钟）
    /// </summary>
    public int CacheExpirationMinutes { get; set; } = 60;
}
