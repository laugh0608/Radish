namespace Radish.IRepository;

/// <summary>统计报表仓储接口</summary>
public interface IStatisticsRepository
{
    /// <summary>获取未删除订单总数</summary>
    Task<int> GetTotalOrderCountAsync();

    /// <summary>获取未删除商品总数</summary>
    Task<int> GetTotalProductCountAsync();

    /// <summary>获取指定时间范围内的未删除订单数</summary>
    Task<int> GetOrderCountAsync(DateTime? startTime = null, DateTime? endTimeExclusive = null);

    /// <summary>获取指定时间范围内已完成订单收入汇总</summary>
    Task<decimal> GetCompletedOrderRevenueAsync(DateTime? startTime = null, DateTime? endTimeExclusive = null);

    /// <summary>获取商品销售排行</summary>
    Task<List<ProductSalesRankingAggregate>> GetProductSalesRankingAsync(int limit);

    /// <summary>获取用户经验等级分布</summary>
    Task<List<UserLevelDistributionAggregate>> GetUserExperienceLevelDistributionAsync();

    /// <summary>获取已有经验记录的用户数</summary>
    Task<int> GetUserExperienceUserCountAsync();

    /// <summary>获取等级配置</summary>
    Task<List<LevelConfigAggregate>> GetLevelConfigsAsync();
}

/// <summary>商品销售排行聚合结果</summary>
public sealed class ProductSalesRankingAggregate
{
    public long ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public int SalesCount { get; set; }

    public decimal Revenue { get; set; }
}

/// <summary>用户等级分布聚合结果</summary>
public sealed class UserLevelDistributionAggregate
{
    public int Level { get; set; }

    public int UserCount { get; set; }
}

/// <summary>等级配置聚合结果</summary>
public sealed class LevelConfigAggregate
{
    public int Level { get; set; }

    public string LevelName { get; set; } = string.Empty;
}
