using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Moq;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.Model;
using Radish.Service;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.Services;

/// <summary>统计报表服务测试</summary>
[TestSubject(typeof(StatisticsService))]
public class StatisticsServiceTest
{
    private readonly Mock<IBaseRepository<User>> _userRepositoryMock = new();
    private readonly Mock<IStatisticsRepository> _statisticsRepositoryMock = new();

    [Fact]
    public async Task GetDashboardStatsAsync_ShouldUseRealRepositoryAggregates()
    {
        _userRepositoryMock
            .Setup(repository => repository.QueryCountAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(12);
        _statisticsRepositoryMock
            .Setup(repository => repository.GetTotalOrderCountAsync())
            .ReturnsAsync(34);
        _statisticsRepositoryMock
            .Setup(repository => repository.GetTotalProductCountAsync())
            .ReturnsAsync(5);
        _statisticsRepositoryMock
            .Setup(repository => repository.GetCompletedOrderRevenueAsync(null, null))
            .ReturnsAsync(6789m);

        var service = CreateService();

        var result = await service.GetDashboardStatsAsync();

        result.VoTotalUsers.ShouldBe(12);
        result.VoTotalOrders.ShouldBe(34);
        result.VoTotalProducts.ShouldBe(5);
        result.VoTotalRevenue.ShouldBe(6789m);
    }

    [Fact]
    public async Task GetOrderTrendAsync_ShouldReturnContinuousDatesIncludingToday()
    {
        var today = DateTime.Today;
        _statisticsRepositoryMock
            .Setup(repository => repository.GetOrderCountAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync((DateTime? startTime, DateTime? _) => startTime == today ? 3 : 0);
        _statisticsRepositoryMock
            .Setup(repository => repository.GetCompletedOrderRevenueAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync((DateTime? startTime, DateTime? _) => startTime == today ? 99m : 0m);

        var service = CreateService();

        var result = await service.GetOrderTrendAsync(2);

        result.Count.ShouldBe(2);
        result[0].VoDate.ShouldBe(today.AddDays(-1).ToString("yyyy-MM-dd"));
        result[0].VoOrderCount.ShouldBe(0);
        result[0].VoRevenue.ShouldBe(0m);
        result[1].VoDate.ShouldBe(today.ToString("yyyy-MM-dd"));
        result[1].VoOrderCount.ShouldBe(3);
        result[1].VoRevenue.ShouldBe(99m);
    }

    [Fact]
    public async Task GetProductSalesRankingAsync_ShouldClampLimitAndMapAggregates()
    {
        _statisticsRepositoryMock
            .Setup(repository => repository.GetProductSalesRankingAsync(50))
            .ReturnsAsync(new List<ProductSalesRankingAggregate>
            {
                new()
                {
                    ProductId = 100,
                    ProductName = "经验卡",
                    SalesCount = 7,
                    Revenue = 350m
                }
            });

        var service = CreateService();

        var result = await service.GetProductSalesRankingAsync(999);

        result.Count.ShouldBe(1);
        result[0].VoProductName.ShouldBe("经验卡");
        result[0].VoSalesCount.ShouldBe(7);
        result[0].VoRevenue.ShouldBe(350m);
        _statisticsRepositoryMock.Verify(repository => repository.GetProductSalesRankingAsync(50), Times.Once);
    }

    [Fact]
    public async Task GetUserLevelDistributionAsync_ShouldPutUsersWithoutExperienceIntoLevelZero()
    {
        _userRepositoryMock
            .Setup(repository => repository.QueryCountAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(5);
        _statisticsRepositoryMock
            .Setup(repository => repository.GetUserExperienceLevelDistributionAsync())
            .ReturnsAsync(new List<UserLevelDistributionAggregate>
            {
                new() { Level = 2, UserCount = 2 }
            });
        _statisticsRepositoryMock
            .Setup(repository => repository.GetUserExperienceUserCountAsync())
            .ReturnsAsync(2);
        _statisticsRepositoryMock
            .Setup(repository => repository.GetLevelConfigsAsync())
            .ReturnsAsync(new List<LevelConfigAggregate>
            {
                new() { Level = 0, LevelName = "凡人" },
                new() { Level = 2, LevelName = "筑基" }
            });

        var service = CreateService();

        var result = await service.GetUserLevelDistributionAsync();

        result.Count.ShouldBe(2);
        result[0].VoLevel.ShouldBe(0);
        result[0].VoLevelName.ShouldBe("凡人");
        result[0].VoUserCount.ShouldBe(3);
        result[1].VoLevel.ShouldBe(2);
        result[1].VoLevelName.ShouldBe("筑基");
        result[1].VoUserCount.ShouldBe(2);
    }

    private StatisticsService CreateService()
    {
        return new StatisticsService(
            _userRepositoryMock.Object,
            _statisticsRepositoryMock.Object);
    }
}
