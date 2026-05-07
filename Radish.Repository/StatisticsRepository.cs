using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Repository;

/// <summary>统计报表仓储</summary>
public class StatisticsRepository : BaseRepository<User>, IStatisticsRepository
{
    public StatisticsRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    /// <inheritdoc />
    public async Task<int> GetTotalOrderCountAsync()
    {
        return await CreateTenantQueryableFor<Order>()
            .Where(order => !order.IsDeleted)
            .CountAsync();
    }

    /// <inheritdoc />
    public async Task<int> GetTotalProductCountAsync()
    {
        return await CreateTenantQueryableFor<Product>()
            .Where(product => !product.IsDeleted)
            .CountAsync();
    }

    /// <inheritdoc />
    public async Task<int> GetOrderCountAsync(DateTime? startTime = null, DateTime? endTimeExclusive = null)
    {
        var query = CreateTenantQueryableFor<Order>()
            .Where(order => !order.IsDeleted);

        if (startTime.HasValue)
        {
            query = query.Where(order => order.CreateTime >= startTime.Value);
        }

        if (endTimeExclusive.HasValue)
        {
            query = query.Where(order => order.CreateTime < endTimeExclusive.Value);
        }

        return await query.CountAsync();
    }

    /// <inheritdoc />
    public async Task<decimal> GetCompletedOrderRevenueAsync(DateTime? startTime = null, DateTime? endTimeExclusive = null)
    {
        var query = CreateTenantQueryableFor<Order>()
            .Where(order => order.Status == OrderStatus.Completed && !order.IsDeleted);

        if (startTime.HasValue)
        {
            query = query.Where(order => order.CreateTime >= startTime.Value);
        }

        if (endTimeExclusive.HasValue)
        {
            query = query.Where(order => order.CreateTime < endTimeExclusive.Value);
        }

        var revenue = await query.SumAsync(order => order.TotalPrice);
        return revenue;
    }

    /// <inheritdoc />
    public async Task<List<ProductSalesRankingAggregate>> GetProductSalesRankingAsync(int limit)
    {
        var result = await CreateTenantQueryableFor<Order>()
            .Where(order => order.Status == OrderStatus.Completed && !order.IsDeleted)
            .GroupBy(order => new { order.ProductId, order.ProductName })
            .Select(order => new
            {
                order.ProductId,
                order.ProductName,
                SalesCount = SqlFunc.AggregateSum(order.Quantity),
                Revenue = SqlFunc.AggregateSum(order.TotalPrice)
            })
            .OrderByDescending(item => item.SalesCount)
            .Take(limit)
            .ToListAsync();

        return result
            .Select(item => new ProductSalesRankingAggregate
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                SalesCount = item.SalesCount,
                Revenue = item.Revenue
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<List<UserLevelDistributionAggregate>> GetUserExperienceLevelDistributionAsync()
    {
        var result = await CreateTenantQueryableFor<UserExperience>()
            .Where(experience => experience.UserId > 0 && !experience.IsDeleted)
            .GroupBy(experience => experience.CurrentLevel)
            .Select(experience => new
            {
                Level = experience.CurrentLevel,
                UserCount = SqlFunc.AggregateCount(experience.UserId)
            })
            .OrderBy(item => item.Level)
            .ToListAsync();

        return result
            .Select(item => new UserLevelDistributionAggregate
            {
                Level = item.Level,
                UserCount = item.UserCount
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<int> GetUserExperienceUserCountAsync()
    {
        return await CreateTenantQueryableFor<UserExperience>()
            .Where(experience => experience.UserId > 0 && !experience.IsDeleted)
            .CountAsync();
    }

    /// <inheritdoc />
    public async Task<List<LevelConfigAggregate>> GetLevelConfigsAsync()
    {
        var configs = await CreateTenantQueryableFor<LevelConfig>()
            .Where(config => config.IsEnabled)
            .OrderBy(config => config.Level)
            .Select(config => new LevelConfigAggregate
            {
                Level = config.Level,
                LevelName = config.LevelName
            })
            .ToListAsync();

        return configs;
    }
}
