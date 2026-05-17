using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Service.Jobs;
using Radish.Shared.CustomEnum;
using Xunit;

namespace Radish.Api.Tests.Services;

public class ShopJobTest
{
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
            CreateTime = DateTime.Now.AddMinutes(-(timeoutMinutes + 5)),
            CreateBy = "User",
            CreateId = 9527
        };

        var orderRepository = new Mock<IBaseRepository<Order>>(MockBehavior.Strict);
        var benefitRepository = new Mock<IBaseRepository<UserBenefit>>(MockBehavior.Strict);
        var orderService = new Mock<IOrderService>(MockBehavior.Strict);

        orderRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<Order, bool>>?>()))
            .ReturnsAsync((Expression<Func<Order, bool>>? expression) =>
            {
                if (expression == null)
                {
                    return [timeoutOrder];
                }

                var predicate = expression.Compile();
                return predicate(timeoutOrder) ? [timeoutOrder] : [];
            });
        orderService
            .Setup(service => service.CancelOrderBySystemAsync(
                timeoutOrder.Id,
                It.Is<string>(reason => reason.Contains($"{timeoutMinutes} 分钟未支付", StringComparison.Ordinal))))
            .ReturnsAsync(true);

        var job = new ShopJob(orderRepository.Object, benefitRepository.Object, orderService.Object);

        var result = await job.CancelTimeoutOrdersAsync(timeoutMinutes);

        Assert.Equal(1, result);
        orderService.VerifyAll();
    }
}
