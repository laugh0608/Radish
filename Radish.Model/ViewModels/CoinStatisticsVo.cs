namespace Radish.Model.ViewModels;

/// <summary>
/// 萝卜币统计数据视图模型
/// </summary>
public class CoinStatisticsVo
{
    /// <summary>
    /// 趋势数据（按日期）
    /// </summary>
    public List<TrendDataItem> VoTrendData { get; set; } = new();

    /// <summary>
    /// 分类统计数据
    /// </summary>
    public List<CategoryStatItem> VoCategoryStats { get; set; } = new();
}

/// <summary>
/// 趋势数据项
/// </summary>
public class TrendDataItem
{
    /// <summary>
    /// 日期（格式：YYYY-MM-DD）
    /// </summary>
    public string VoDate { get; set; } = string.Empty;

    /// <summary>
    /// 收入金额
    /// </summary>
    public long VoIncome { get; set; }

    /// <summary>
    /// 支出金额
    /// </summary>
    public long VoExpense { get; set; }
}

/// <summary>
/// 分类统计项
/// </summary>
public class CategoryStatItem
{
    /// <summary>
    /// 分类名称
    /// </summary>
    public string VoCategory { get; set; } = string.Empty;

    /// <summary>
    /// 总金额
    /// </summary>
    public long VoAmount { get; set; }

    /// <summary>
    /// 交易次数
    /// </summary>
    public int VoCount { get; set; }
}
