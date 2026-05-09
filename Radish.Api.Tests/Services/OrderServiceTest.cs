using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Radish.Common.CoreTool;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service;
using Radish.Shared.CustomEnum;
using Radish.Shared.Security;
using Xunit;

namespace Radish.Api.Tests.Services;

public class OrderServiceTest
{
    [Fact]
    public async Task PurchaseAsync_ShouldPassOrderQuantityToConsumableGrant()
    {
        new ServiceCollection().ConfigureApplication();

        const long userId = 9527;
        const long productId = 100062;
        const long orderId = 7001;
        const int quantity = 3;
        const long totalPrice = 150;
        Order? createdOrder = null;

        var product = new Product
        {
            Id = productId,
            Name = "经验卡（100点）",
            CategoryId = "effect",
            ProductType = ProductType.Consumable,
            ConsumableType = ConsumableType.ExpCard,
            Price = 50,
            StockType = StockType.Unlimited,
            DurationType = DurationType.Permanent,
            IsEnabled = true,
            IsOnSale = true,
            CreateTime = DateTime.Now,
            CreateBy = "System"
        };

        var mapper = new Mock<IMapper>(MockBehavior.Loose);
        var orderRepository = new Mock<IBaseRepository<Order>>(MockBehavior.Loose);
        var productRepository = new Mock<IBaseRepository<Product>>(MockBehavior.Loose);
        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Loose);
        var productService = new Mock<IProductService>(MockBehavior.Loose);
        var userBenefitService = new Mock<IUserBenefitService>(MockBehavior.Loose);
        var coinService = new Mock<ICoinService>(MockBehavior.Loose);
        var paymentPasswordService = new Mock<IPaymentPasswordService>(MockBehavior.Loose);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Loose);

        productService
            .Setup(service => service.CheckCanBuyAsync(userId, productId, quantity))
            .ReturnsAsync((true, null as string));
        productRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Product, bool>>?>()))
            .ReturnsAsync((Expression<Func<Product, bool>>? expression) =>
            {
                if (expression == null)
                {
                    return product;
                }

                var predicate = expression.Compile();
                return predicate(product) ? product : null;
            });
        coinService
            .SetupSequence(service => service.GetBalanceAsync(userId))
            .ReturnsAsync(new UserBalanceVo { VoUserId = userId, VoBalance = 1000 })
            .ReturnsAsync(new UserBalanceVo { VoUserId = userId, VoBalance = 850 });
        paymentPasswordService
            .Setup(service => service.VerifyPaymentPasswordAsync(userId, It.IsAny<VerifyPaymentPasswordRequest>()))
            .ReturnsAsync(new PaymentPasswordVerifyResult { IsSuccess = true });
        orderRepository
            .Setup(repository => repository.AddAsync(It.IsAny<Order>()))
            .Callback<Order>(order => createdOrder = order)
            .ReturnsAsync(orderId);
        coinService
            .Setup(service => service.ConsumeCoinAsync(
                userId,
                It.Is<long>(amount => amount == totalPrice),
                "Order",
                orderId,
                It.IsAny<string?>()))
            .ReturnsAsync((10001L, "TXN_10001"));
        userBenefitService
            .Setup(service => service.GrantBenefitAsync(userId, It.IsAny<Product>(), orderId, quantity))
            .ReturnsAsync(8001);
        orderRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<Order>()))
            .ReturnsAsync(true);
        productService
            .Setup(service => service.IncreaseSoldCountAsync(productId, quantity))
            .ReturnsAsync(true);

        var service = new OrderService(
            mapper.Object,
            orderRepository.Object,
            productRepository.Object,
            userRepository.Object,
            productService.Object,
            userBenefitService.Object,
            coinService.Object,
            paymentPasswordService.Object,
            attachmentUrlResolver.Object,
            notificationService: null);

        var result = await service.PurchaseAsync(userId, new CreateOrderDto
        {
            ProductId = productId,
            Quantity = quantity,
            PaymentPassword = "274958"
        });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(orderId, result.OrderId);
        Assert.Equal(8001, result.UserBenefitId);
        Assert.Equal(totalPrice, result.DeductedCoins);
        Assert.Equal(850, result.RemainingBalance);
        Assert.NotNull(createdOrder);
        Assert.Equal(StockType.Unlimited, createdOrder!.StockType);
        Assert.Equal(quantity, createdOrder.Quantity);
        Assert.Equal(10001L, createdOrder.CoinTransactionId);

        userBenefitService.Verify(service => service.GrantBenefitAsync(
            userId,
            It.Is<Product>(p => p.Id == productId && p.ConsumableType == ConsumableType.ExpCard),
            orderId,
            quantity), Times.Once);
        paymentPasswordService.Verify(service => service.VerifyPaymentPasswordAsync(
            userId,
            It.Is<VerifyPaymentPasswordRequest>(request =>
                request.Password == "274958"
                && request.BusinessType == "ShopPurchase"
                && request.BusinessId == productId.ToString())), Times.Once);
    }

    [Fact]
    public async Task PurchaseAsync_ShouldFailAndRestoreStockWhenConsumeCoinFails()
    {
        const long userId = 9527;
        const long productId = 100201;
        const long orderId = 7004;
        const long totalPrice = 80;
        Order? createdOrder = null;

        var product = new Product
        {
            Id = productId,
            Name = "限量测试红包",
            CategoryId = "effect",
            ProductType = ProductType.Consumable,
            ConsumableType = ConsumableType.CoinCard,
            BenefitValue = "10",
            Price = totalPrice,
            StockType = StockType.Limited,
            Stock = 5,
            DurationType = DurationType.Permanent,
            IsEnabled = true,
            IsOnSale = true,
            CreateTime = DateTime.Now,
            CreateBy = "System"
        };

        var mapper = new Mock<IMapper>(MockBehavior.Loose);
        var orderRepository = new Mock<IBaseRepository<Order>>(MockBehavior.Loose);
        var productRepository = new Mock<IBaseRepository<Product>>(MockBehavior.Loose);
        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Loose);
        var productService = new Mock<IProductService>(MockBehavior.Loose);
        var userBenefitService = new Mock<IUserBenefitService>(MockBehavior.Loose);
        var coinService = new Mock<ICoinService>(MockBehavior.Loose);
        var paymentPasswordService = new Mock<IPaymentPasswordService>(MockBehavior.Loose);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Loose);

        productService
            .Setup(service => service.CheckCanBuyAsync(userId, productId, 1))
            .ReturnsAsync((true, null as string));
        productRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Product, bool>>?>()))
            .ReturnsAsync((Expression<Func<Product, bool>>? expression) =>
            {
                if (expression == null)
                {
                    return product;
                }

                var predicate = expression.Compile();
                return predicate(product) ? product : null;
            });
        coinService
            .Setup(service => service.GetBalanceAsync(userId))
            .ReturnsAsync(new UserBalanceVo { VoUserId = userId, VoBalance = 1000 });
        paymentPasswordService
            .Setup(service => service.VerifyPaymentPasswordAsync(userId, It.IsAny<VerifyPaymentPasswordRequest>()))
            .ReturnsAsync(new PaymentPasswordVerifyResult { IsSuccess = true });
        productService
            .Setup(service => service.DeductStockAsync(productId, 1))
            .ReturnsAsync(true);
        orderRepository
            .Setup(repository => repository.AddAsync(It.IsAny<Order>()))
            .Callback<Order>(order => createdOrder = order)
            .ReturnsAsync(orderId);
        coinService
            .Setup(service => service.ConsumeCoinAsync(
                userId,
                totalPrice,
                "Order",
                orderId,
                It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("余额不足"));
        productService
            .Setup(service => service.RestoreStockAsync(productId, 1, StockType.Limited))
            .ReturnsAsync(true);
        orderRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<Order>()))
            .ReturnsAsync(true);

        var service = new OrderService(
            mapper.Object,
            orderRepository.Object,
            productRepository.Object,
            userRepository.Object,
            productService.Object,
            userBenefitService.Object,
            coinService.Object,
            paymentPasswordService.Object,
            attachmentUrlResolver.Object,
            notificationService: null);

        var result = await service.PurchaseAsync(userId, new CreateOrderDto
        {
            ProductId = productId,
            Quantity = 1,
            PaymentPassword = "246813"
        });

        Assert.False(result.Success);
        Assert.Equal("扣除萝卜币失败", result.ErrorMessage);
        Assert.NotNull(createdOrder);
        Assert.Equal(OrderStatus.Failed, createdOrder!.Status);
        Assert.Contains("余额不足", createdOrder.FailReason, StringComparison.Ordinal);
        productService.Verify(service => service.RestoreStockAsync(productId, 1, StockType.Limited), Times.Once);
        userBenefitService.Verify(service => service.GrantBenefitAsync(It.IsAny<long>(), It.IsAny<Product>(), It.IsAny<long>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task PurchaseAsync_ShouldFailBeforeStockDeductionWhenPaymentPasswordInvalid()
    {
        const long userId = 9527;
        const long productId = 100301;

        var product = new Product
        {
            Id = productId,
            Name = "限量主题卡",
            CategoryId = "effect",
            ProductType = ProductType.Benefit,
            BenefitType = BenefitType.Theme,
            Price = 80,
            StockType = StockType.Limited,
            Stock = 3,
            DurationType = DurationType.Permanent,
            IsEnabled = true,
            IsOnSale = true,
            CreateTime = DateTime.Now,
            CreateBy = "System"
        };

        var mapper = new Mock<IMapper>(MockBehavior.Loose);
        var orderRepository = new Mock<IBaseRepository<Order>>(MockBehavior.Loose);
        var productRepository = new Mock<IBaseRepository<Product>>(MockBehavior.Loose);
        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Loose);
        var productService = new Mock<IProductService>(MockBehavior.Loose);
        var userBenefitService = new Mock<IUserBenefitService>(MockBehavior.Loose);
        var coinService = new Mock<ICoinService>(MockBehavior.Loose);
        var paymentPasswordService = new Mock<IPaymentPasswordService>(MockBehavior.Loose);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Loose);

        productService
            .Setup(service => service.CheckCanBuyAsync(userId, productId, 1))
            .ReturnsAsync((true, null as string));
        productRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Product, bool>>?>()))
            .ReturnsAsync((Expression<Func<Product, bool>>? expression) =>
            {
                if (expression == null)
                {
                    return product;
                }

                var predicate = expression.Compile();
                return predicate(product) ? product : null;
            });
        coinService
            .Setup(service => service.GetBalanceAsync(userId))
            .ReturnsAsync(new UserBalanceVo { VoUserId = userId, VoBalance = 1000 });
        paymentPasswordService
            .Setup(service => service.VerifyPaymentPasswordAsync(userId, It.IsAny<VerifyPaymentPasswordRequest>()))
            .ReturnsAsync(new PaymentPasswordVerifyResult
            {
                IsSuccess = false,
                ErrorMessage = "支付口令错误，还可尝试4次"
            });

        var service = new OrderService(
            mapper.Object,
            orderRepository.Object,
            productRepository.Object,
            userRepository.Object,
            productService.Object,
            userBenefitService.Object,
            coinService.Object,
            paymentPasswordService.Object,
            attachmentUrlResolver.Object,
            notificationService: null);

        var result = await service.PurchaseAsync(userId, new CreateOrderDto
        {
            ProductId = productId,
            Quantity = 1,
            PaymentPassword = "123456"
        });

        Assert.False(result.Success);
        Assert.Equal("支付口令错误，还可尝试4次", result.ErrorMessage);
        productService.Verify(service => service.DeductStockAsync(It.IsAny<long>(), It.IsAny<int>()), Times.Never);
        orderRepository.Verify(repository => repository.AddAsync(It.IsAny<Order>()), Times.Never);
        coinService.Verify(service => service.ConsumeCoinAsync(
            It.IsAny<long>(),
            It.IsAny<long>(),
            It.IsAny<string?>(),
            It.IsAny<long?>(),
            It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task PurchaseAsync_ShouldReturnUpgradePrompt_WhenPaymentPasscodeIsLegacy()
    {
        const long userId = 9527;
        const long productId = 100302;

        var product = new Product
        {
            Id = productId,
            Name = "限量主题卡",
            CategoryId = "effect",
            ProductType = ProductType.Benefit,
            BenefitType = BenefitType.Theme,
            Price = 80,
            StockType = StockType.Limited,
            Stock = 3,
            DurationType = DurationType.Permanent,
            IsEnabled = true,
            IsOnSale = true,
            CreateTime = DateTime.Now,
            CreateBy = "System"
        };

        var mapper = new Mock<IMapper>(MockBehavior.Loose);
        var orderRepository = new Mock<IBaseRepository<Order>>(MockBehavior.Loose);
        var productRepository = new Mock<IBaseRepository<Product>>(MockBehavior.Loose);
        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Loose);
        var productService = new Mock<IProductService>(MockBehavior.Loose);
        var userBenefitService = new Mock<IUserBenefitService>(MockBehavior.Loose);
        var coinService = new Mock<ICoinService>(MockBehavior.Loose);
        var paymentPasswordService = new Mock<IPaymentPasswordService>(MockBehavior.Loose);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Loose);

        productService
            .Setup(service => service.CheckCanBuyAsync(userId, productId, 1))
            .ReturnsAsync((true, null as string));
        productRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Product, bool>>?>()))
            .ReturnsAsync((Expression<Func<Product, bool>>? expression) =>
            {
                if (expression == null)
                {
                    return product;
                }

                var predicate = expression.Compile();
                return predicate(product) ? product : null;
            });
        coinService
            .Setup(service => service.GetBalanceAsync(userId))
            .ReturnsAsync(new UserBalanceVo { VoUserId = userId, VoBalance = 1000 });
        paymentPasswordService
            .Setup(service => service.VerifyPaymentPasswordAsync(userId, It.IsAny<VerifyPaymentPasswordRequest>()))
            .ReturnsAsync(new PaymentPasswordVerifyResult
            {
                IsSuccess = false,
                ErrorCode = PaymentPasscodeErrorCodes.UpgradeRequired,
                ErrorMessage = PaymentPasscodeRules.UpgradeRequiredErrorMessage,
                RequiresPasscodeUpgrade = true
            });

        var service = new OrderService(
            mapper.Object,
            orderRepository.Object,
            productRepository.Object,
            userRepository.Object,
            productService.Object,
            userBenefitService.Object,
            coinService.Object,
            paymentPasswordService.Object,
            attachmentUrlResolver.Object,
            notificationService: null);

        var result = await service.PurchaseAsync(userId, new CreateOrderDto
        {
            ProductId = productId,
            Quantity = 1,
            PaymentPassword = "274958"
        });

        Assert.False(result.Success);
        Assert.True(result.RequiresPasscodeUpgrade);
        Assert.Equal(PaymentPasscodeErrorCodes.UpgradeRequired, result.ErrorCode);
        Assert.Equal(PaymentPasscodeRules.UpgradeRequiredErrorMessage, result.ErrorMessage);
        productService.Verify(service => service.DeductStockAsync(It.IsAny<long>(), It.IsAny<int>()), Times.Never);
        orderRepository.Verify(repository => repository.AddAsync(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task CancelOrderAsync_ShouldRestoreStockForPendingOrder()
    {
        const long userId = 9527;
        const long orderId = 7002;
        const long productId = 100201;

        var order = new Order
        {
            Id = orderId,
            UserId = userId,
            ProductId = productId,
            ProductName = "限量徽章",
            StockType = StockType.Limited,
            Quantity = 2,
            Status = OrderStatus.Pending,
            CreateTime = DateTime.Now,
            CreateBy = "User",
            CreateId = userId
        };

        var mapper = new Mock<IMapper>(MockBehavior.Loose);
        var orderRepository = new Mock<IBaseRepository<Order>>(MockBehavior.Loose);
        var productRepository = new Mock<IBaseRepository<Product>>(MockBehavior.Loose);
        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Loose);
        var productService = new Mock<IProductService>(MockBehavior.Loose);
        var userBenefitService = new Mock<IUserBenefitService>(MockBehavior.Loose);
        var coinService = new Mock<ICoinService>(MockBehavior.Loose);
        var paymentPasswordService = new Mock<IPaymentPasswordService>(MockBehavior.Loose);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Loose);

        orderRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Order, bool>>?>()))
            .ReturnsAsync((Expression<Func<Order, bool>>? expression) =>
            {
                if (expression == null)
                {
                    return order;
                }

                var predicate = expression.Compile();
                return predicate(order) ? order : null;
            });
        orderRepository
            .Setup(repository => repository.UpdateColumnsAsync(It.IsAny<Expression<Func<Order, Order>>>(), It.IsAny<Expression<Func<Order, bool>>>()))
            .ReturnsAsync((Expression<Func<Order, Order>> _, Expression<Func<Order, bool>> whereExpression) =>
            {
                var predicate = whereExpression.Compile();
                return predicate(order) ? 1 : 0;
            });
        productService
            .Setup(service => service.RestoreStockAsync(productId, order.Quantity, StockType.Limited))
            .ReturnsAsync(true);

        var service = new OrderService(
            mapper.Object,
            orderRepository.Object,
            productRepository.Object,
            userRepository.Object,
            productService.Object,
            userBenefitService.Object,
            coinService.Object,
            paymentPasswordService.Object,
            attachmentUrlResolver.Object,
            notificationService: null);

        var result = await service.CancelOrderAsync(userId, orderId, "用户手动取消");

        Assert.True(result);
        productService.Verify(service => service.RestoreStockAsync(productId, order.Quantity, StockType.Limited), Times.Once);
        orderRepository.Verify(
            repository => repository.UpdateColumnsAsync(It.IsAny<Expression<Func<Order, Order>>>(), It.IsAny<Expression<Func<Order, bool>>>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelOrderAsync_ShouldNotRestoreStockForUnlimitedOrder()
    {
        const long userId = 9527;
        const long orderId = 7003;
        const long productId = 100062;

        var order = new Order
        {
            Id = orderId,
            UserId = userId,
            ProductId = productId,
            ProductName = "经验卡（100点）",
            StockType = StockType.Unlimited,
            Quantity = 1,
            Status = OrderStatus.Pending,
            CreateTime = DateTime.Now,
            CreateBy = "User",
            CreateId = userId
        };

        var mapper = new Mock<IMapper>(MockBehavior.Loose);
        var orderRepository = new Mock<IBaseRepository<Order>>(MockBehavior.Loose);
        var productRepository = new Mock<IBaseRepository<Product>>(MockBehavior.Loose);
        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Loose);
        var productService = new Mock<IProductService>(MockBehavior.Loose);
        var userBenefitService = new Mock<IUserBenefitService>(MockBehavior.Loose);
        var coinService = new Mock<ICoinService>(MockBehavior.Loose);
        var paymentPasswordService = new Mock<IPaymentPasswordService>(MockBehavior.Loose);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Loose);

        orderRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Order, bool>>?>()))
            .ReturnsAsync((Expression<Func<Order, bool>>? expression) =>
            {
                if (expression == null)
                {
                    return order;
                }

                var predicate = expression.Compile();
                return predicate(order) ? order : null;
            });
        orderRepository
            .Setup(repository => repository.UpdateColumnsAsync(It.IsAny<Expression<Func<Order, Order>>>(), It.IsAny<Expression<Func<Order, bool>>>()))
            .ReturnsAsync((Expression<Func<Order, Order>> _, Expression<Func<Order, bool>> whereExpression) =>
            {
                var predicate = whereExpression.Compile();
                return predicate(order) ? 1 : 0;
            });
        var service = new OrderService(
            mapper.Object,
            orderRepository.Object,
            productRepository.Object,
            userRepository.Object,
            productService.Object,
            userBenefitService.Object,
            coinService.Object,
            paymentPasswordService.Object,
            attachmentUrlResolver.Object,
            notificationService: null);

        var result = await service.CancelOrderAsync(userId, orderId, "用户手动取消");

        Assert.True(result);
        productService.Verify(service => service.RestoreStockAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<StockType>()), Times.Never);
    }
}
