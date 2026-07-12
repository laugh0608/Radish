using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Common.OptionTool;
using Radish.Common.TimeTool;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Service.Jobs;
using Radish.Shared.CustomEnum;
using Xunit;

namespace Radish.Api.Tests.Services;

public class ShopJobTest
{
    private static readonly DateTime FixedNow = new(2026, 7, 11, 16, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CancelTimeoutOrdersAsync_ShouldDelegateToOrderServiceCancellation()
    {
        const int timeoutMinutes = 30;
        var timeoutOrder = new Order
        {
            Id = 7003,
            UserId = 9527,
            OrderNo = "ORD_7003",
            ProductId = 100201,
            Quantity = 1,
            Status = OrderStatus.Pending,
            CreateTime = FixedNow.AddMinutes(-(timeoutMinutes + 5)),
            CreateBy = "User",
            CreateId = 9527
        };

        var orderRepository = new Mock<IBaseRepository<Order>>(MockBehavior.Strict);
        var benefitRepository = new Mock<IBaseRepository<UserBenefit>>(MockBehavior.Strict);
        var orderService = new Mock<IOrderService>(MockBehavior.Strict);

        orderRepository
            .Setup(repository => repository.QueryDistinctAsync(
                It.IsAny<Expression<Func<Order, long>>>(),
                It.IsAny<Expression<Func<Order, bool>>?>()))
            .ReturnsAsync((
                Expression<Func<Order, long>> selectExpression,
                Expression<Func<Order, bool>>? whereExpression) =>
            {
                var selector = selectExpression.Compile();
                if (whereExpression == null)
                {
                    return [selector(timeoutOrder)];
                }

                var predicate = whereExpression.Compile();
                return predicate(timeoutOrder) ? [selector(timeoutOrder)] : [];
            });
        orderService
            .Setup(service => service.CancelOrderBySystemAsync(
                timeoutOrder.Id,
                It.Is<string>(reason => reason.Contains($"{timeoutMinutes} 分钟未支付", StringComparison.Ordinal))))
            .ReturnsAsync(true);

        var job = CreateJob(orderRepository, benefitRepository, orderService);

        var result = await job.CancelTimeoutOrdersAsync(timeoutMinutes);

        Assert.Equal(1, result);
        orderService.VerifyAll();
    }

    [Fact]
    public async Task CancelTimeoutOrdersAsync_ShouldReturnZero_WhenOrderQueryFails()
    {
        var orderRepository = new Mock<IBaseRepository<Order>>(MockBehavior.Strict);
        var benefitRepository = new Mock<IBaseRepository<UserBenefit>>(MockBehavior.Strict);
        var orderService = new Mock<IOrderService>(MockBehavior.Strict);

        orderRepository
            .Setup(repository => repository.QueryDistinctAsync(
                It.IsAny<Expression<Func<Order, long>>>(),
                It.IsAny<Expression<Func<Order, bool>>?>()))
            .ThrowsAsync(new InvalidOperationException("reader closed"));

        var job = CreateJob(orderRepository, benefitRepository, orderService);

        var result = await job.CancelTimeoutOrdersAsync();

        Assert.Equal(0, result);
        orderService.Verify(
            service => service.CancelOrderBySystemAsync(It.IsAny<long>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task GenerateDailyStatsAsync_ShouldUseConfiguredBusinessDateWindow()
    {
        var orders = new List<Order>
        {
            new()
            {
                Id = 1,
                Status = OrderStatus.Completed,
                TotalPrice = 20,
                CreateTime = new DateTime(2026, 7, 11, 16, 10, 0, DateTimeKind.Utc)
            },
            new()
            {
                Id = 2,
                Status = OrderStatus.Completed,
                TotalPrice = 30,
                CreateTime = new DateTime(2026, 7, 11, 15, 59, 59, DateTimeKind.Utc)
            }
        };
        var orderRepository = new Mock<IBaseRepository<Order>>(MockBehavior.Strict);
        orderRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<Order, bool>>?>()))
            .ReturnsAsync((Expression<Func<Order, bool>>? predicate) =>
                predicate == null ? orders : orders.Where(predicate.Compile()).ToList());
        var benefitRepository = new Mock<IBaseRepository<UserBenefit>>(MockBehavior.Strict);
        var orderService = new Mock<IOrderService>(MockBehavior.Strict);
        var job = CreateJob(orderRepository, benefitRepository, orderService);

        var result = await job.GenerateDailyStatsAsync();

        Assert.Equal(new DateTime(2026, 7, 12), result.Date);
        Assert.Equal(1, result.TotalOrders);
        Assert.Equal(20, result.TotalRevenue);
    }

    private static ShopJob CreateJob(
        Mock<IBaseRepository<Order>> orderRepository,
        Mock<IBaseRepository<UserBenefit>> benefitRepository,
        Mock<IOrderService> orderService)
    {
        var timeProvider = new FixedTimeProvider(FixedNow);
        var calendar = new BusinessCalendar(
            timeProvider,
            Options.Create(new TimeOptions { DefaultTimeZoneId = "Asia/Shanghai" }));
        return new ShopJob(
            orderRepository.Object,
            benefitRepository.Object,
            orderService.Object,
            timeProvider,
            calendar);
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        private readonly DateTimeOffset _utcNow = new(utcNow);

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
