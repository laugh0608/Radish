using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Serilog;

namespace Radish.Service;

/// <summary>统计报表服务</summary>
public class StatisticsService : IStatisticsService
{
    private const int DefaultDays = 30;
    private const int MaxDays = 90;
    private const int DefaultLimit = 10;
    private const int MaxLimit = 50;

    private readonly IBaseRepository<User> _userRepository;
    private readonly IStatisticsRepository _statisticsRepository;

    public StatisticsService(
        IBaseRepository<User> userRepository,
        IStatisticsRepository statisticsRepository)
    {
        _userRepository = userRepository;
        _statisticsRepository = statisticsRepository;
    }

    /// <inheritdoc />
    public async Task<DashboardStatsVo> GetDashboardStatsAsync()
    {
        try
        {
            return new DashboardStatsVo
            {
                VoTotalUsers = await _userRepository.QueryCountAsync(user => !user.IsDeleted),
                VoTotalOrders = await _statisticsRepository.GetTotalOrderCountAsync(),
                VoTotalProducts = await _statisticsRepository.GetTotalProductCountAsync(),
                VoTotalRevenue = await _statisticsRepository.GetCompletedOrderRevenueAsync()
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取仪表盘统计数据失败");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<OrderTrendItemVo>> GetOrderTrendAsync(int days = DefaultDays)
    {
        try
        {
            var normalizedDays = NormalizeRange(days, DefaultDays, MaxDays);
            var today = DateTime.Today;
            var startDate = today.AddDays(-(normalizedDays - 1));

            var trendData = new List<OrderTrendItemVo>(normalizedDays);
            for (var index = 0; index < normalizedDays; index++)
            {
                var date = startDate.AddDays(index);
                var nextDate = date.AddDays(1);
                trendData.Add(new OrderTrendItemVo
                {
                    VoDate = date.ToString("yyyy-MM-dd"),
                    VoOrderCount = await _statisticsRepository.GetOrderCountAsync(date, nextDate),
                    VoRevenue = await _statisticsRepository.GetCompletedOrderRevenueAsync(date, nextDate)
                });
            }

            return trendData;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取订单趋势失败：days={Days}", days);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ProductSalesRankingVo>> GetProductSalesRankingAsync(int limit = DefaultLimit)
    {
        try
        {
            var normalizedLimit = NormalizeRange(limit, DefaultLimit, MaxLimit);
            var rankingData = await _statisticsRepository.GetProductSalesRankingAsync(normalizedLimit);

            return rankingData
                .Select(item => new ProductSalesRankingVo
                {
                    VoProductName = item.ProductName,
                    VoSalesCount = item.SalesCount,
                    VoRevenue = item.Revenue
                })
                .ToList();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取商品销售排行失败：limit={Limit}", limit);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<UserLevelDistributionVo>> GetUserLevelDistributionAsync()
    {
        try
        {
            var totalUsers = await _userRepository.QueryCountAsync(user => !user.IsDeleted);
            var distribution = (await _statisticsRepository.GetUserExperienceLevelDistributionAsync())
                .ToDictionary(item => item.Level, item => item.UserCount);

            var experienceUserCount = await _statisticsRepository.GetUserExperienceUserCountAsync();
            var noExperienceUserCount = Math.Max(0, totalUsers - experienceUserCount);
            if (noExperienceUserCount > 0)
            {
                distribution[0] = distribution.GetValueOrDefault(0) + noExperienceUserCount;
            }

            var levelNameMap = (await _statisticsRepository.GetLevelConfigsAsync())
                .ToDictionary(item => item.Level, item => item.LevelName);

            return distribution
                .OrderBy(item => item.Key)
                .Select(item => new UserLevelDistributionVo
                {
                    VoLevel = item.Key,
                    VoLevelName = levelNameMap.TryGetValue(item.Key, out var levelName)
                        ? levelName
                        : $"Lv.{item.Key}",
                    VoUserCount = item.Value
                })
                .ToList();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取用户等级分布失败");
            throw;
        }
    }

    private static int NormalizeRange(int value, int defaultValue, int maxValue)
    {
        if (value < 1)
        {
            return defaultValue;
        }

        return Math.Min(value, maxValue);
    }
}
