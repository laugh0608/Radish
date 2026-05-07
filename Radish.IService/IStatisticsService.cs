using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>统计报表服务接口</summary>
public interface IStatisticsService
{
    /// <summary>获取仪表盘统计数据</summary>
    Task<DashboardStatsVo> GetDashboardStatsAsync();

    /// <summary>获取订单趋势数据</summary>
    Task<List<OrderTrendItemVo>> GetOrderTrendAsync(int days = 30);

    /// <summary>获取商品销售排行</summary>
    Task<List<ProductSalesRankingVo>> GetProductSalesRankingAsync(int limit = 10);

    /// <summary>获取用户等级分布</summary>
    Task<List<UserLevelDistributionVo>> GetUserLevelDistributionAsync();
}
