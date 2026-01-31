namespace Radish.Model.ViewModels;

/// <summary>
/// 仪表盘统计数据 VO
/// </summary>
public class DashboardStatsVo
{
    /// <summary>总用户数</summary>
    public int VoTotalUsers { get; set; }

    /// <summary>总订单数</summary>
    public int VoTotalOrders { get; set; }

    /// <summary>总商品数</summary>
    public int VoTotalProducts { get; set; }

    /// <summary>总收入</summary>
    public decimal VoTotalRevenue { get; set; }
}

/// <summary>
/// 订单趋势项 VO
/// </summary>
public class OrderTrendItemVo
{
    /// <summary>日期</summary>
    public string VoDate { get; set; } = string.Empty;

    /// <summary>订单数量</summary>
    public int VoOrderCount { get; set; }

    /// <summary>收入</summary>
    public decimal VoRevenue { get; set; }
}

/// <summary>
/// 商品销售排行 VO
/// </summary>
public class ProductSalesRankingVo
{
    /// <summary>商品名称</summary>
    public string VoProductName { get; set; } = string.Empty;

    /// <summary>销售数量</summary>
    public int VoSalesCount { get; set; }

    /// <summary>收入</summary>
    public decimal VoRevenue { get; set; }
}

/// <summary>
/// 用户等级分布 VO
/// </summary>
public class UserLevelDistributionVo
{
    /// <summary>等级</summary>
    public int VoLevel { get; set; }

    /// <summary>等级名称</summary>
    public string VoLevelName { get; set; } = string.Empty;

    /// <summary>用户数量</summary>
    public int VoUserCount { get; set; }
}
