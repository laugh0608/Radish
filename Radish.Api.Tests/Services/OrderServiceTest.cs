using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Radish.Common.CoreTool;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service;
using Radish.Shared.Constants;
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
        var coinTransactionRepository = new Mock<IBaseRepository<CoinTransaction>>(MockBehavior.Loose);
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
            .Setup(service => service.GrantOrderFulfillmentAsync(It.IsAny<Order>()))
            .ReturnsAsync(new OrderFulfillmentResultDto { GrantedInventoryId = 8001 });
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
            coinTransactionRepository.Object,
            productService.Object,
            userBenefitService.Object,
            coinService.Object,
            paymentPasswordService.Object,
            attachmentUrlResolver.Object,
            notificationService: null,
            reliableOutboxService: Mock.Of<IReliableOutboxService>());

        var result = await service.PurchaseAsync(userId, new CreateOrderDto
        {
            ProductId = productId,
            Quantity = quantity,
            PaymentPassword = "274958"
        });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(orderId, result.OrderId);
        Assert.Equal(8001, result.UserBenefitId);
        Assert.Equal(8001, result.GrantedInventoryId);
        Assert.Equal(totalPrice, result.DeductedCoins);
        Assert.Equal(850, result.RemainingBalance);
        Assert.NotNull(createdOrder);
        Assert.Equal(StockType.Unlimited, createdOrder!.StockType);
        Assert.Equal(quantity, createdOrder.Quantity);
        Assert.Equal(10001L, createdOrder.CoinTransactionId);
        Assert.Equal(8001, createdOrder.GrantedInventoryId);

        userBenefitService.Verify(service => service.GrantOrderFulfillmentAsync(
            It.Is<Order>(order =>
                order.Id == orderId &&
                order.UserId == userId &&
                order.ProductId == productId &&
                order.ConsumableType == ConsumableType.ExpCard &&
                order.Quantity == quantity)), Times.Once);
        paymentPasswordService.Verify(service => service.VerifyPaymentPasswordAsync(
            userId,
            It.Is<VerifyPaymentPasswordRequest>(request =>
                request.Password == "274958"
                && request.BusinessType == "ShopPurchase"
                && request.BusinessId == productId.ToString())), Times.Once);
    }

    [Fact]
    public async Task PurchaseAsync_ShouldReplaySucceededIdempotencyRecordWithoutAssetWrite()
    {
        const long userId = 9527;
        const long productId = 100063;
        const string idempotencyKey = "shop:replay";
        var replayResult = new PurchaseResultDto
        {
            Success = true,
            OrderId = 7002,
            OrderNo = "ORD_7002",
            DeductedCoins = 50,
            RemainingBalance = 950
        };

        var product = new Product
        {
            Id = productId,
            Name = "复用测试道具",
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
        var operationIdempotencyService = new Mock<IOperationIdempotencyService>(MockBehavior.Loose);

        productService
            .Setup(service => service.CheckCanBuyAsync(userId, productId, 1))
            .ReturnsAsync((true, null as string));
        productRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Product, bool>>?>()))
            .ReturnsAsync(product);
        coinService
            .Setup(service => service.GetBalanceAsync(userId))
            .ReturnsAsync(new UserBalanceVo { VoUserId = userId, VoBalance = 1000 });
        paymentPasswordService
            .Setup(service => service.VerifyPaymentPasswordAsync(userId, It.IsAny<VerifyPaymentPasswordRequest>()))
            .ReturnsAsync(new PaymentPasswordVerifyResult { IsSuccess = true });
        operationIdempotencyService
            .Setup(service => service.NormalizeKey(idempotencyKey))
            .Returns(idempotencyKey);
        operationIdempotencyService
            .Setup(service => service.CreateRequestSnapshot(It.IsAny<IReadOnlyDictionary<string, object?>>()))
            .Returns(new OperationIdempotencyRequestSnapshot
            {
                RequestHash = "hash-a",
                RequestSummary = "{}"
            });
        operationIdempotencyService
            .Setup(service => service.BeginAsync(It.IsAny<OperationIdempotencyBeginRequest>()))
            .ReturnsAsync(new OperationIdempotencyBeginResult
            {
                Status = OperationIdempotencyBeginStatus.Succeeded,
                RecordId = 9001,
                ResponsePayload = "payload"
            });
        operationIdempotencyService
            .Setup(service => service.DeserializeResponse<PurchaseResultDto>("payload"))
            .Returns(replayResult);

        var service = new OrderService(
            mapper.Object,
            orderRepository.Object,
            productRepository.Object,
            userRepository.Object,
            Mock.Of<IBaseRepository<CoinTransaction>>(),
            productService.Object,
            userBenefitService.Object,
            coinService.Object,
            paymentPasswordService.Object,
            attachmentUrlResolver.Object,
            operationIdempotencyService.Object,
            notificationService: null,
            reliableOutboxService: Mock.Of<IReliableOutboxService>());

        var result = await service.PurchaseAsync(userId, new CreateOrderDto
        {
            ProductId = productId,
            Quantity = 1,
            PaymentPassword = "274958",
            IdempotencyKey = idempotencyKey
        });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(replayResult.OrderId, result.OrderId);
        Assert.Equal(replayResult.OrderNo, result.OrderNo);
        productService.Verify(service => service.DeductStockAsync(It.IsAny<long>(), It.IsAny<int>()), Times.Never);
        orderRepository.Verify(repository => repository.AddAsync(It.IsAny<Order>()), Times.Never);
        coinService.Verify(service => service.ConsumeCoinAsync(
            It.IsAny<long>(),
            It.IsAny<long>(),
            It.IsAny<string?>(),
            It.IsAny<long?>(),
            It.IsAny<string?>()), Times.Never);
        userBenefitService.Verify(service => service.GrantOrderFulfillmentAsync(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task PurchaseAsync_ShouldRejectConflictingIdempotencyKeyBeforeAssetWrite()
    {
        const long userId = 9527;
        const long productId = 100064;
        const string idempotencyKey = "shop:conflict";

        var product = new Product
        {
            Id = productId,
            Name = "冲突测试道具",
            CategoryId = "effect",
            ProductType = ProductType.Consumable,
            ConsumableType = ConsumableType.ExpCard,
            Price = 50,
            StockType = StockType.Limited,
            Stock = 2,
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
        var operationIdempotencyService = new Mock<IOperationIdempotencyService>(MockBehavior.Loose);

        productService
            .Setup(service => service.CheckCanBuyAsync(userId, productId, 1))
            .ReturnsAsync((true, null as string));
        productRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Product, bool>>?>()))
            .ReturnsAsync(product);
        coinService
            .Setup(service => service.GetBalanceAsync(userId))
            .ReturnsAsync(new UserBalanceVo { VoUserId = userId, VoBalance = 1000 });
        paymentPasswordService
            .Setup(service => service.VerifyPaymentPasswordAsync(userId, It.IsAny<VerifyPaymentPasswordRequest>()))
            .ReturnsAsync(new PaymentPasswordVerifyResult { IsSuccess = true });
        operationIdempotencyService
            .Setup(service => service.NormalizeKey(idempotencyKey))
            .Returns(idempotencyKey);
        operationIdempotencyService
            .Setup(service => service.CreateRequestSnapshot(It.IsAny<IReadOnlyDictionary<string, object?>>()))
            .Returns(new OperationIdempotencyRequestSnapshot
            {
                RequestHash = "hash-b",
                RequestSummary = "{}"
            });
        operationIdempotencyService
            .Setup(service => service.BeginAsync(It.IsAny<OperationIdempotencyBeginRequest>()))
            .ReturnsAsync(new OperationIdempotencyBeginResult
            {
                Status = OperationIdempotencyBeginStatus.Conflict,
                Message = "幂等键已被不同请求使用"
            });

        var service = new OrderService(
            mapper.Object,
            orderRepository.Object,
            productRepository.Object,
            userRepository.Object,
            Mock.Of<IBaseRepository<CoinTransaction>>(),
            productService.Object,
            userBenefitService.Object,
            coinService.Object,
            paymentPasswordService.Object,
            attachmentUrlResolver.Object,
            operationIdempotencyService.Object,
            notificationService: null,
            reliableOutboxService: Mock.Of<IReliableOutboxService>());

        var result = await service.PurchaseAsync(userId, new CreateOrderDto
        {
            ProductId = productId,
            Quantity = 1,
            PaymentPassword = "274958",
            IdempotencyKey = idempotencyKey
        });

        Assert.False(result.Success);
        Assert.Equal("幂等键已被不同请求使用", result.ErrorMessage);
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
    public async Task PurchaseAsync_ShouldThrowAndLeaveIdempotencyPending_WhenOrderWriteFails()
    {
        const long userId = 9527;
        const long productId = 100065;
        const string idempotencyKey = "shop:terminal-failure";
        OperationIdempotencyCompletionRequest? completionRequest = null;

        var product = new Product
        {
            Id = productId,
            Name = "异常边界测试道具",
            CategoryId = "effect",
            ProductType = ProductType.Consumable,
            ConsumableType = ConsumableType.ExpCard,
            Price = 50,
            StockType = StockType.Limited,
            Stock = 2,
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
        var operationIdempotencyService = new Mock<IOperationIdempotencyService>(MockBehavior.Loose);

        productService
            .Setup(service => service.CheckCanBuyAsync(userId, productId, 1))
            .ReturnsAsync((true, null as string));
        productRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Product, bool>>?>()))
            .ReturnsAsync(product);
        coinService
            .Setup(service => service.GetBalanceAsync(userId))
            .ReturnsAsync(new UserBalanceVo { VoUserId = userId, VoBalance = 1000 });
        paymentPasswordService
            .Setup(service => service.VerifyPaymentPasswordAsync(userId, It.IsAny<VerifyPaymentPasswordRequest>()))
            .ReturnsAsync(new PaymentPasswordVerifyResult { IsSuccess = true });
        operationIdempotencyService
            .Setup(service => service.NormalizeKey(idempotencyKey))
            .Returns(idempotencyKey);
        operationIdempotencyService
            .Setup(service => service.CreateRequestSnapshot(It.IsAny<IReadOnlyDictionary<string, object?>>()))
            .Returns(new OperationIdempotencyRequestSnapshot
            {
                RequestHash = "hash-c",
                RequestSummary = "{}"
            });
        operationIdempotencyService
            .Setup(service => service.BeginAsync(It.IsAny<OperationIdempotencyBeginRequest>()))
            .ReturnsAsync(new OperationIdempotencyBeginResult
            {
                Status = OperationIdempotencyBeginStatus.Started,
                RecordId = 9005
            });
        productService
            .Setup(service => service.DeductStockAsync(productId, 1))
            .ReturnsAsync(true);
        orderRepository
            .Setup(repository => repository.AddAsync(It.IsAny<Order>()))
            .ThrowsAsync(new InvalidOperationException("订单写入失败"));
        operationIdempotencyService
            .Setup(service => service.SerializeResponse(It.IsAny<PurchaseResultDto>()))
            .Returns("payload");
        operationIdempotencyService
            .Setup(service => service.CompleteSuccessAsync(It.IsAny<OperationIdempotencyCompletionRequest>()))
            .Callback<OperationIdempotencyCompletionRequest>(request => completionRequest = request)
            .Returns(Task.CompletedTask);

        var service = new OrderService(
            mapper.Object,
            orderRepository.Object,
            productRepository.Object,
            userRepository.Object,
            Mock.Of<IBaseRepository<CoinTransaction>>(),
            productService.Object,
            userBenefitService.Object,
            coinService.Object,
            paymentPasswordService.Object,
            attachmentUrlResolver.Object,
            operationIdempotencyService.Object,
            notificationService: null,
            reliableOutboxService: Mock.Of<IReliableOutboxService>());

        var exception = await Assert.ThrowsAsync<Radish.Common.Exceptions.BusinessException>(() =>
            service.PurchaseAsync(userId, new CreateOrderDto
            {
                ProductId = productId,
                Quantity = 1,
                PaymentPassword = "274958",
                IdempotencyKey = idempotencyKey
            }));

        Assert.Equal("购买失败，请稍后重试", exception.Message);
        Assert.Null(completionRequest);
        operationIdempotencyService.Verify(service => service.CompleteSuccessAsync(
            It.IsAny<OperationIdempotencyCompletionRequest>()), Times.Never);
        operationIdempotencyService.Verify(service => service.CompleteFailureAsync(
            It.IsAny<long>(),
            It.IsAny<string?>(),
            It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task PurchaseAsync_ShouldFailAndRestoreStockWhenConsumeCoinFails()
    {
        new ServiceCollection().ConfigureApplication();

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
            Mock.Of<IBaseRepository<CoinTransaction>>(),
            productService.Object,
            userBenefitService.Object,
            coinService.Object,
            paymentPasswordService.Object,
            attachmentUrlResolver.Object,
            notificationService: null,
            reliableOutboxService: Mock.Of<IReliableOutboxService>());

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
        Assert.Equal(OrderFailureStage.Payment, createdOrder.FailureStage);
        Assert.Contains("余额不足", createdOrder.FailReason, StringComparison.Ordinal);
        productService.Verify(service => service.RestoreStockAsync(productId, 1, StockType.Limited), Times.Once);
        userBenefitService.Verify(service => service.GrantOrderFulfillmentAsync(It.IsAny<Order>()), Times.Never);
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
            Mock.Of<IBaseRepository<CoinTransaction>>(),
            productService.Object,
            userBenefitService.Object,
            coinService.Object,
            paymentPasswordService.Object,
            attachmentUrlResolver.Object,
            notificationService: null,
            reliableOutboxService: Mock.Of<IReliableOutboxService>());

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
    public async Task RetryGrantBenefitAsync_ShouldRejectPaymentFailureWithoutGrant()
    {
        const long orderId = 7101;
        var order = new Order
        {
            Id = orderId,
            Status = OrderStatus.Failed,
            FailureStage = OrderFailureStage.Payment,
            CreateTime = DateTime.Now,
            CreateBy = "User"
        };
        var orderRepository = new Mock<IBaseRepository<Order>>(MockBehavior.Loose);
        orderRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Order, bool>>?>()))
            .ReturnsAsync(order);
        var userBenefitService = new Mock<IUserBenefitService>(MockBehavior.Loose);
        var coinTransactionRepository = new Mock<IBaseRepository<CoinTransaction>>(MockBehavior.Strict);
        var service = CreateOrderServiceForRetry(
            orderRepository,
            coinTransactionRepository,
            userBenefitService);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RetryGrantBenefitAsync(orderId));

        Assert.Equal("支付阶段失败的订单不能重试发放", exception.Message);
        userBenefitService.Verify(
            benefitService => benefitService.GrantOrderFulfillmentAsync(It.IsAny<Order>()),
            Times.Never);
        coinTransactionRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RetryGrantBenefitAsync_ShouldRejectMismatchedPaymentEvidence()
    {
        const long orderId = 7102;
        var order = CreateFulfillmentFailedOrder(orderId);
        var orderRepository = new Mock<IBaseRepository<Order>>(MockBehavior.Loose);
        orderRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Order, bool>>?>()))
            .ReturnsAsync(order);
        var coinTransactionRepository = new Mock<IBaseRepository<CoinTransaction>>(MockBehavior.Loose);
        coinTransactionRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<CoinTransaction, bool>>?>()))
            .ReturnsAsync((CoinTransaction?)null);
        var userBenefitService = new Mock<IUserBenefitService>(MockBehavior.Loose);
        var service = CreateOrderServiceForRetry(
            orderRepository,
            coinTransactionRepository,
            userBenefitService);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RetryGrantBenefitAsync(orderId));

        Assert.Equal("订单扣款流水与履约快照不匹配，不能自动重试发放", exception.Message);
        userBenefitService.Verify(
            benefitService => benefitService.GrantOrderFulfillmentAsync(It.IsAny<Order>()),
            Times.Never);
    }

    [Fact]
    public async Task RetryGrantBenefitAsync_ShouldGrantOnlyFromOrderSnapshot()
    {
        const long orderId = 7103;
        const long inventoryId = 8103;
        var order = CreateFulfillmentFailedOrder(orderId);
        var transaction = new CoinTransaction
        {
            Id = order.CoinTransactionId!.Value,
            TenantId = order.TenantId,
            FromUserId = order.UserId,
            TransactionType = "CONSUME",
            Status = "SUCCESS",
            BusinessType = "Order",
            BusinessId = order.Id,
            Amount = order.TotalPrice
        };
        var orderRepository = new Mock<IBaseRepository<Order>>(MockBehavior.Loose);
        orderRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Order, bool>>?>()))
            .ReturnsAsync(order);
        orderRepository
            .Setup(repository => repository.UpdateAsync(order))
            .ReturnsAsync(true);
        var coinTransactionRepository = new Mock<IBaseRepository<CoinTransaction>>(MockBehavior.Loose);
        coinTransactionRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<CoinTransaction, bool>>?>()))
            .ReturnsAsync((Expression<Func<CoinTransaction, bool>>? expression) =>
                expression?.Compile()(transaction) == true ? transaction : null);
        var userBenefitService = new Mock<IUserBenefitService>(MockBehavior.Loose);
        userBenefitService
            .Setup(benefitService => benefitService.GrantOrderFulfillmentAsync(order))
            .ReturnsAsync(new OrderFulfillmentResultDto { GrantedInventoryId = inventoryId });
        var productRepository = new Mock<IBaseRepository<Product>>(MockBehavior.Strict);
        var service = CreateOrderServiceForRetry(
            orderRepository,
            coinTransactionRepository,
            userBenefitService,
            productRepository);

        var result = await service.RetryGrantBenefitAsync(orderId);

        Assert.True(result);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(OrderFailureStage.None, order.FailureStage);
        Assert.Equal(inventoryId, order.GrantedInventoryId);
        Assert.Null(order.FailReason);
        productRepository.VerifyNoOtherCalls();
        userBenefitService.Verify(
            benefitService => benefitService.GrantOrderFulfillmentAsync(It.Is<Order>(snapshot =>
                snapshot.Id == orderId &&
                snapshot.ProductName == "订单快照经验卡" &&
                snapshot.BenefitValue == "100" &&
                snapshot.Quantity == 2)),
            Times.Once);
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
            Mock.Of<IBaseRepository<CoinTransaction>>(),
            productService.Object,
            userBenefitService.Object,
            coinService.Object,
            paymentPasswordService.Object,
            attachmentUrlResolver.Object,
            notificationService: null,
            reliableOutboxService: Mock.Of<IReliableOutboxService>());

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
            Mock.Of<IBaseRepository<CoinTransaction>>(),
            productService.Object,
            userBenefitService.Object,
            coinService.Object,
            paymentPasswordService.Object,
            attachmentUrlResolver.Object,
            notificationService: null,
            reliableOutboxService: Mock.Of<IReliableOutboxService>());

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
            Mock.Of<IBaseRepository<CoinTransaction>>(),
            productService.Object,
            userBenefitService.Object,
            coinService.Object,
            paymentPasswordService.Object,
            attachmentUrlResolver.Object,
            notificationService: null,
            reliableOutboxService: Mock.Of<IReliableOutboxService>());

        var result = await service.CancelOrderAsync(userId, orderId, "用户手动取消");

        Assert.True(result);
        productService.Verify(service => service.RestoreStockAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<StockType>()), Times.Never);
    }

    [Fact]
    public async Task GetOrderDetailForAdminAsync_ShouldFillUserNameAndProductIcon()
    {
        const long orderId = 7005;
        const long userId = 9528;
        const long attachmentId = 9001;

        var order = new Order
        {
            Id = orderId,
            OrderNo = "ORD_7005",
            UserId = userId,
            ProductId = 100501,
            ProductName = "体验红包",
            ProductIconAttachmentId = attachmentId,
            ProductType = ProductType.Consumable,
            Quantity = 1,
            UnitPrice = 50,
            TotalPrice = 50,
            Status = OrderStatus.Completed,
            CreateTime = DateTime.Now,
            CreateBy = "User",
            CreateId = userId
        };
        var user = new User
        {
            Id = userId,
            UserName = "OrderAdmin"
        };

        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        mapper
            .Setup(m => m.Map<OrderVo>(It.IsAny<Order>()))
            .Returns<Order>(source => new OrderVo
            {
                VoId = source.Id,
                VoOrderNo = source.OrderNo,
                VoUserId = source.UserId,
                VoProductId = source.ProductId,
                VoProductName = source.ProductName,
                VoProductIconAttachmentId = source.ProductIconAttachmentId,
                VoProductType = source.ProductType,
                VoQuantity = source.Quantity,
                VoUnitPrice = source.UnitPrice,
                VoTotalPrice = source.TotalPrice,
                VoStatus = source.Status,
                VoCreateTime = source.CreateTime
            });
        var orderRepository = new Mock<IBaseRepository<Order>>(MockBehavior.Strict);
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
        var productRepository = new Mock<IBaseRepository<Product>>(MockBehavior.Loose);
        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Strict);
        userRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<User, bool>>?>()))
            .ReturnsAsync((Expression<Func<User, bool>>? expression) =>
            {
                if (expression == null)
                {
                    return [user];
                }

                var predicate = expression.Compile();
                return predicate(user) ? [user] : [];
            });
        var productService = new Mock<IProductService>(MockBehavior.Loose);
        var userBenefitService = new Mock<IUserBenefitService>(MockBehavior.Loose);
        var coinService = new Mock<ICoinService>(MockBehavior.Loose);
        var paymentPasswordService = new Mock<IPaymentPasswordService>(MockBehavior.Loose);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);
        attachmentUrlResolver
            .Setup(resolver => resolver.ResolveAttachmentUrl(attachmentId))
            .Returns("https://cdn.example.com/order-icon.png");

        var service = new OrderService(
            mapper.Object,
            orderRepository.Object,
            productRepository.Object,
            userRepository.Object,
            Mock.Of<IBaseRepository<CoinTransaction>>(),
            productService.Object,
            userBenefitService.Object,
            coinService.Object,
            paymentPasswordService.Object,
            attachmentUrlResolver.Object,
            notificationService: null,
            reliableOutboxService: Mock.Of<IReliableOutboxService>());

        var result = await service.GetOrderDetailForAdminAsync(orderId);

        Assert.NotNull(result);
        Assert.Equal("OrderAdmin", result!.VoUserName);
        Assert.Equal("https://cdn.example.com/order-icon.png", result.VoProductIcon);
        Assert.Equal(order.ProductName, result.VoProductName);
    }

    private static OrderService CreateOrderServiceForRetry(
        Mock<IBaseRepository<Order>> orderRepository,
        Mock<IBaseRepository<CoinTransaction>> coinTransactionRepository,
        Mock<IUserBenefitService> userBenefitService,
        Mock<IBaseRepository<Product>>? productRepository = null)
    {
        return new OrderService(
            Mock.Of<IMapper>(),
            orderRepository.Object,
            productRepository?.Object ?? Mock.Of<IBaseRepository<Product>>(),
            Mock.Of<IBaseRepository<User>>(),
            coinTransactionRepository.Object,
            Mock.Of<IProductService>(),
            userBenefitService.Object,
            Mock.Of<ICoinService>(),
            Mock.Of<IPaymentPasswordService>(),
            Mock.Of<IAttachmentUrlResolver>(),
            notificationService: null,
            reliableOutboxService: Mock.Of<IReliableOutboxService>());
    }

    private static Order CreateFulfillmentFailedOrder(long orderId)
    {
        return new Order
        {
            Id = orderId,
            OrderNo = $"ORD_{orderId}",
            TenantId = 7,
            UserId = 9527,
            ProductId = 100501,
            ProductName = "订单快照经验卡",
            ProductType = ProductType.Consumable,
            ConsumableType = ConsumableType.ExpCard,
            BenefitValue = "100",
            Quantity = 2,
            UnitPrice = 50,
            TotalPrice = 100,
            Status = OrderStatus.Failed,
            FailureStage = OrderFailureStage.Fulfillment,
            PaidTime = DateTime.Now.AddMinutes(-1),
            CoinTransactionId = 61001,
            FailReason = "发放权益失败：测试异常",
            CreateTime = DateTime.Now,
            CreateBy = "User"
        };
    }
}
