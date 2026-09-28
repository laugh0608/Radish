using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.ErrorHandling;
using Radish.Api.Resources;
using Radish.Common;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Extension.Log;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service;
using Radish.Shared.CustomEnum;
using Serilog;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class ShopFulfillmentLoggingTests
{
    private const string Secret = "SHOP_PRIVATE_SENTINEL";
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Stock_ShouldKeepMutationsAndBusinessResultsWithoutPayload(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        Assert.True((await f.Product.CheckCanBuyAsync(1, 3, 2)).canBuy);
        Assert.False((await f.Product.CheckCanBuyAsync(1, 3, 11)).canBuy);
        Assert.True(await f.Product.DeductStockAsync(3, 2));
        Assert.True(await f.Product.RestoreStockAsync(3, 2, StockType.Limited));
        Assert.True(await f.Product.IncreaseSoldCountAsync(3, 2));
        Assert.Equal(8, f.StockWrites[0].Stock);
        Assert.Equal(4, f.StockWrites[0].Version);
        Assert.Equal(12, f.StockWrites[1].Stock);
        Assert.Equal(2, f.StockWrites[2].SoldCount);
        f.Products.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Product, bool>>>())).ReturnsAsync((Product?)null);
        Assert.False(await f.Product.RestoreStockAsync(3, 2, StockType.Limited));
        Assert.False(await f.Product.IncreaseSoldCountAsync(3, 2));
        Assert.True(await f.Product.RestoreStockAsync(3, 2, StockType.Unlimited));
        capture.AssertQuiet();
        f.Products.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Product, bool>>>())).ReturnsAsync(new Product
        { Id = 3, IsEnabled = true, IsOnSale = true, ProductType = ProductType.Benefit, BenefitType = BenefitType.Title, BenefitValue = new string('x', 41), Name = Secret });
        var rejected = await f.Product.CheckCanBuyAsync(1, 3, 2);
        Assert.False(rejected.canBuy);
        Assert.Equal("商品配置不完整，请联系管理员", rejected.reason);
        capture.AssertSingle("product.configuration_rejected", "Warning");
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Stock_ShouldRetainFiveAttemptsAndOnlyLogActualRetries(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        f.Products.SetupSequence(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<Product, Product>>>(), It.IsAny<Expression<Func<Product, bool>>>()))
            .ReturnsAsync(0).ReturnsAsync(1);
        Assert.True(await f.Product.DeductStockAsync(3, 2));
        capture.AssertSingle("product.stock_retrying", "Warning");
        Assert.Contains("50", capture.Output.ToString());
        capture.Clear();
        f.Products.Setup(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<Product, Product>>>(), It.IsAny<Expression<Func<Product, bool>>>())).ReturnsAsync(0);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Product.DeductStockAsync(3, 2));
        Assert.Contains("乐观锁冲突", error.Message);
        f.Products.Verify(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<Product, Product>>>(), It.IsAny<Expression<Func<Product, bool>>>()), Times.Exactly(7));
        Assert.Equal(4, capture.Lines.Length);
        Assert.All(capture.Lines, line => Assert.Contains("product.stock_retrying", line));
        Assert.DoesNotContain("Error", capture.Output.ToString());
        foreach (var delay in new[] { 50, 100, 200, 400 }) Assert.Contains(delay.ToString(), capture.Output.ToString());
        capture.AssertSafe();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Stock_ShouldPropagateRepositoryFailureWithoutDuplicateLogs(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        var error = new IOException(Secret);
        f.Products.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Product, bool>>>())).ThrowsAsync(error);
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => f.Product.CheckCanBuyAsync(1, 3, 2)));
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => f.Product.DeductStockAsync(3, 2)));
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => f.Product.RestoreStockAsync(3, 2, StockType.Limited)));
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => f.Product.IncreaseSoldCountAsync(3, 2)));
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Benefits_ShouldPreserveSnapshotAndUniqueConflictRecoveryQuietly(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        var order = CreateOrder();
        order.DurationType = DurationType.FixedDate;
        order.FixedExpiresAt = DateTime.UtcNow.AddDays(7);
        var result = await f.Benefit.GrantOrderFulfillmentAsync(order);
        Assert.Equal(7, result.GrantedBenefitId);
        Assert.Equal(order.FixedExpiresAt, result.ExpiresAt);
        f.Benefits.Verify(r => r.AddAsync(It.Is<UserBenefit>(b => b.SourceOrderId == 4 && b.SourceProductId == 3 &&
            b.UserId == 1 && b.BenefitName == Secret && b.BenefitValue == Secret && b.ExpiresAt == order.FixedExpiresAt)), Times.Once);
        var existing = new UserBenefit { Id = 7, ExpiresAt = order.FixedExpiresAt };
        f.Benefits.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserBenefit, bool>>>())).ReturnsAsync(existing);
        Assert.Equal(7, (await f.Benefit.GrantOrderFulfillmentAsync(order)).GrantedBenefitId);
        f.Benefits.Verify(r => r.AddAsync(It.IsAny<UserBenefit>()), Times.Once);
        var failure = new InvalidOperationException("duplicate key " + Secret);
        f.Benefits.Setup(r => r.AddAsync(It.IsAny<UserBenefit>())).ThrowsAsync(failure);
        f.Benefits.SetupSequence(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserBenefit, bool>>>()))
            .ReturnsAsync((UserBenefit?)null).ReturnsAsync(existing);
        Assert.Equal(7, (await f.Benefit.GrantOrderFulfillmentAsync(order)).GrantedBenefitId);
        f.Benefits.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserBenefit, bool>>>())).ReturnsAsync((UserBenefit?)null);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => f.Benefit.GrantOrderFulfillmentAsync(order)));
        await Assert.ThrowsAsync<ArgumentNullException>(() => f.Benefit.GrantOrderFulfillmentAsync(null!));
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Consumables_ShouldKeepRepositoryIdempotencyResultsAndFailurePropagation(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        var order = CreateOrder();
        order.ProductType = ProductType.Consumable;
        order.ConsumableType = ConsumableType.ExpCard;
        f.Inventory.SetupSequence(r => r.GrantConsumableForOrderAsync(0, 1, ConsumableType.ExpCard, Secret, Secret, null, 2, 4, 3))
            .ReturnsAsync(new UserInventoryGrantPersistenceResult(8, true, 2, 5))
            .ReturnsAsync(new UserInventoryGrantPersistenceResult(8, false, 0, 5))
            .ThrowsAsync(new IOException(Secret));
        Assert.Equal(8, (await f.Benefit.GrantOrderFulfillmentAsync(order)).GrantedInventoryId);
        Assert.Equal(8, (await f.Benefit.GrantOrderFulfillmentAsync(order)).GrantedInventoryId);
        await Assert.ThrowsAsync<IOException>(() => f.Benefit.GrantOrderFulfillmentAsync(order));
        f.Benefits.Verify(r => r.AddAsync(It.IsAny<UserBenefit>()), Times.Never);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Purchase_ShouldConsumeRealFulfillmentFailureOnce(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        f.Benefits.Setup(r => r.AddAsync(It.IsAny<UserBenefit>())).ThrowsAsync(new IOException(Secret));
        var result = await f.Order.PurchaseAsync(1, PurchaseRequest());
        Assert.False(result.Success);
        Assert.Contains(Secret, result.ErrorMessage);
        f.Orders.Verify(r => r.UpdateAsync(It.Is<Order>(o => o.Status == OrderStatus.Failed &&
            o.FailureStage == OrderFailureStage.Fulfillment && o.CoinTransactionId == 9 && o.FailReason!.Contains(Secret))), Times.Once);
        Assert.Equal(2, f.StockWrites.Count); // 扣库存和已售数量；履约失败不回补库存。
        capture.AssertSingle("order.purchase_failed", "Error");
        Assert.Contains("fulfillment", capture.Output.ToString());
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Purchase_ShouldKeepPaymentCompensationAndSeparateCompensationFailure(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        var paymentFailure = new IOException(Secret);
        f.Coin.Setup(s => s.ConsumeCoinAsync(1, 46, "Order", 4, It.IsAny<string>())).ThrowsAsync(paymentFailure);
        var result = await f.Order.PurchaseAsync(1, PurchaseRequest());
        Assert.False(result.Success);
        Assert.Equal("扣除萝卜币失败", result.ErrorMessage);
        Assert.Equal(12, f.StockWrites[1].Stock);
        f.Orders.Verify(r => r.UpdateAsync(It.Is<Order>(o => o.FailureStage == OrderFailureStage.Payment && o.FailReason!.Contains(Secret))), Times.Once);
        f.Benefits.Verify(r => r.AddAsync(It.IsAny<UserBenefit>()), Times.Never);
        capture.AssertSingle("order.purchase_failed", "Error");
        capture.Clear();
        var compensationFailure = new IOException(Secret + "_COMPENSATION");
        f.Products.SetupSequence(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<Product, Product>>>(), It.IsAny<Expression<Func<Product, bool>>>()))
            .ReturnsAsync(1).ThrowsAsync(compensationFailure);
        var wrapped = await Assert.ThrowsAsync<BusinessException>(() => f.Order.PurchaseAsync(1, PurchaseRequest()));
        Assert.Same(compensationFailure, wrapped.InnerException);
        Assert.Equal(400, wrapped.StatusCode);
        Assert.Equal(2, capture.Lines.Length);
        Assert.Single(capture.Lines, line => line.Contains("order.purchase_failed"));
        Assert.Single(capture.Lines, line => line.Contains("order.purchase_interrupted"));
        capture.AssertSafe();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Purchase_ShouldOwnWrappedFailureEvenWhenApiReturns400(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        f.Products.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Product, bool>>>())).ThrowsAsync(new IOException(Secret));
        await InvokeApiAsync(() => f.Controller.Purchase(PurchaseRequest()), capture.Logger, 400);
        capture.AssertSingle("order.purchase_interrupted", "Error");
        f.Coin.Verify(s => s.ConsumeCoinAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<string?>()), Times.Never);
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Cancellation_ShouldPreserveCompensationAndFinalConsumer(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        var order = CreateOrder();
        order.Status = OrderStatus.Pending;
        f.Orders.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Order, bool>>>())).ReturnsAsync(order);
        Assert.True(await f.Order.CancelOrderAsync(1, 4, Secret));
        Assert.Equal(12, Assert.Single(f.StockWrites).Stock);
        Assert.Equal(Secret, Assert.Single(f.OrderWrites).CancelReason);
        Assert.Equal(OrderStatus.Cancelled, f.OrderWrites[0].Status);
        capture.AssertQuiet();
        f.Products.Setup(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<Product, Product>>>(), It.IsAny<Expression<Func<Product, bool>>>())).ReturnsAsync(0);
        var rejected = await f.Controller.CancelOrder(4, Secret);
        Assert.False(rejected.IsSuccess);
        Assert.Equal("取消订单失败，库存回补未完成", rejected.MessageInfo);
        capture.AssertSingle("order.cancellation_rejected", "Warning");
        capture.Clear();
        f.Products.Setup(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<Product, Product>>>(), It.IsAny<Expression<Func<Product, bool>>>())).ThrowsAsync(new IOException(Secret));
        await InvokeApiAsync(() => f.Controller.CancelOrder(4, Secret), capture.Logger, 500);
        capture.AssertSingle("http.failed", "Error");
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Retry_ShouldUsePaidSnapshotAndKeepBusinessRejectionQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        var order = CreateOrder();
        order.Status = OrderStatus.Failed;
        order.FailureStage = OrderFailureStage.Fulfillment;
        f.Orders.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Order, bool>>>())).ReturnsAsync(order);
        Assert.True(await f.Order.RetryGrantBenefitAsync(4));
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(7, order.GrantedBenefitId);
        Assert.Null(order.FailReason);
        f.Products.Verify(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Product, bool>>>()), Times.Never);
        var rejected = await f.Controller.RetryGrantBenefit(4);
        Assert.False(rejected.IsSuccess);
        Assert.Equal(409, rejected.StatusCode);
        f.Benefits.Verify(r => r.AddAsync(It.IsAny<UserBenefit>()), Times.Once);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Retry_ShouldLeaveConsumedAndPropagatedFailuresToFinalBoundary(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        var order = CreateOrder();
        order.Status = OrderStatus.Failed;
        order.FailureStage = OrderFailureStage.Fulfillment;
        f.Orders.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Order, bool>>>())).ReturnsAsync(order);
        f.Benefits.Setup(r => r.AddAsync(It.IsAny<UserBenefit>())).ThrowsAsync(new InvalidOperationException(Secret));
        Assert.Equal(409, (await f.Controller.RetryGrantBenefit(4)).StatusCode);
        capture.AssertSingle("order.fulfillment_retry_rejected", "Warning");
        capture.Clear();
        f.Benefits.Setup(r => r.AddAsync(It.IsAny<UserBenefit>())).ThrowsAsync(new IOException(Secret));
        await InvokeApiAsync(() => f.Controller.RetryGrantBenefit(4), capture.Logger, 500);
        capture.AssertSingle("http.failed", "Error");
        capture.Clear();
        f.Benefits.Setup(r => r.AddAsync(It.IsAny<UserBenefit>())).ThrowsAsync(new BusinessException(Secret, 503, Secret));
        Assert.Equal(503, (await f.Controller.RetryGrantBenefit(4)).StatusCode);
        capture.AssertSingle("http.failed", "Error");
        f.Orders.Verify(r => r.UpdateAsync(It.IsAny<Order>()), Times.Never);
    }

    private static CreateOrderDto PurchaseRequest() => new() { ProductId = 3, Quantity = 2, PaymentPassword = "274958", UserRemark = Secret };
    private static Order CreateOrder() => new()
    {
        Id = 4, UserId = 1, ProductId = 3, ProductName = Secret, BenefitValue = Secret,
        ProductType = ProductType.Benefit, BenefitType = BenefitType.Title, DurationType = DurationType.Permanent,
        Quantity = 2, StockType = StockType.Limited, TotalPrice = 46, Status = OrderStatus.Paid, PaidTime = DateTime.UtcNow, CoinTransactionId = 9
    };

    private static async Task InvokeApiAsync(Func<Task> action, Serilog.ILogger logger, int status)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(logger, dispose: false);
        builder.Services.AddSingleton<ApiExceptionHandler>();
        await using var app = builder.Build();
        app.UseApiExceptionHandler();
        app.Run(async _ => await action());
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test/" + Secret;
        context.Response.Body = new MemoryStream();
        context.RequestServices = app.Services;
        await ((IApplicationBuilder)app).Build()(context);
        Assert.Equal(status, context.Response.StatusCode);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly IConfiguration _previous = AppSettingsTool.Configuration;
        public Mock<IBaseRepository<Product>> Products { get; } = new();
        public Mock<IBaseRepository<Order>> Orders { get; } = new();
        public Mock<IBaseRepository<UserBenefit>> Benefits { get; } = new();
        public Mock<IUserInventoryRepository> Inventory { get; } = new();
        public Mock<ICoinService> Coin { get; } = new();
        public List<Product> StockWrites { get; } = [];
        public List<Order> OrderWrites { get; } = [];
        public ProductService Product { get; }
        public UserBenefitService Benefit { get; }
        public OrderService Order { get; }
        public ShopController Controller { get; }
        public Fixture()
        {
            AppSettingsTool.Configuration = new ConfigurationBuilder().Build();
            Products.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Product, bool>>>())).ReturnsAsync(() => new Product
            {
                Id = 3, Name = Secret, BenefitValue = Secret, ProductType = ProductType.Benefit, BenefitType = BenefitType.Title,
                IsOnSale = true, IsEnabled = true, Price = 23, StockType = StockType.Limited, Stock = 10, Version = 3
            });
            Products.Setup(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<Product, Product>>>(), It.IsAny<Expression<Func<Product, bool>>>()))
                .Callback((Expression<Func<Product, Product>> update, Expression<Func<Product, bool>> where) =>
                {
                    Assert.True(where.Compile()(new Product { Id = 3, Version = 3 }));
                    StockWrites.Add(update.Compile()(new Product { Stock = 10 }));
                }).ReturnsAsync(1);
            Orders.Setup(r => r.AddAsync(It.IsAny<Order>())).ReturnsAsync(4);
            Orders.Setup(r => r.UpdateAsync(It.IsAny<Order>())).ReturnsAsync(true);
            Orders.Setup(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<Order, Order>>>(), It.IsAny<Expression<Func<Order, bool>>>()))
                .Callback((Expression<Func<Order, Order>> update, Expression<Func<Order, bool>> where) => OrderWrites.Add(update.Compile()(CreateOrder())))
                .ReturnsAsync(1);
            Benefits.Setup(r => r.AddAsync(It.IsAny<UserBenefit>())).ReturnsAsync(7);
            Coin.Setup(s => s.GetBalanceAsync(1)).ReturnsAsync(new UserBalanceVo { VoBalance = 100 });
            Coin.Setup(s => s.ConsumeCoinAsync(1, 46, "Order", 4, It.IsAny<string>())).ReturnsAsync((9L, Secret));
            var payment = new Mock<IPaymentPasswordService>();
            payment.Setup(s => s.VerifyPaymentPasswordAsync(1, It.IsAny<VerifyPaymentPasswordRequest>())).ReturnsAsync(new PaymentPasswordVerifyResult { IsSuccess = true });
            var transactions = new Mock<IBaseRepository<CoinTransaction>>();
            var transaction = new CoinTransaction { Id = 9, FromUserId = 1, TransactionType = "CONSUME", Status = "SUCCESS", BusinessType = "Order", BusinessId = 4, Amount = 46 };
            transactions.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<CoinTransaction, bool>>>()))
                .ReturnsAsync((Expression<Func<CoinTransaction, bool>> expression) => expression.Compile()(transaction) ? transaction : null);
            Product = new ProductService(Mock.Of<IMapper>(), Products.Object, Mock.Of<IBaseRepository<ProductCategory>>(), Orders.Object, Mock.Of<IAttachmentUrlResolver>());
            Benefit = new UserBenefitService(Mock.Of<IMapper>(), Benefits.Object, Mock.Of<IBaseRepository<ShopEntitlementOperation>>(), Mock.Of<IUserBenefitRepository>(), Inventory.Object, Mock.Of<IAttachmentUrlResolver>());
            Order = new OrderService(Mock.Of<IMapper>(), Orders.Object, Products.Object, Mock.Of<IBaseRepository<User>>(), transactions.Object,
                Product, Benefit, Coin.Object, payment.Object, Mock.Of<IAttachmentUrlResolver>(), reliableOutboxService: Mock.Of<IReliableOutboxService>());
            var current = new Mock<ICurrentUserAccessor>();
            current.SetupGet(c => c.Current).Returns(new CurrentUser { UserId = 1, UserName = Secret });
            var localizer = new Mock<IStringLocalizer<Errors>>();
            localizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => new LocalizedString(key, key, true));
            Controller = new ShopController(Product, Order, Benefit, Mock.Of<IUserInventoryService>(), Mock.Of<IUserBrowseHistoryService>(), current.Object, localizer.Object, Mock.Of<IProductReviewService>());
        }
        public void Dispose() => AppSettingsTool.Configuration = _previous;
    }

    private sealed class Capture : IDisposable
    {
        private readonly Serilog.ILogger _previous = Log.Logger;
        public StringWriter Output { get; } = new();
        public Serilog.Core.Logger Logger { get; }
        public string[] Lines => Output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        public Capture(bool candidate, string environment)
        {
            var config = new LoggerConfiguration().MinimumLevel.Verbose();
            if (candidate) RuntimeLoggingConfiguration.Configure(config, new ConfigurationBuilder().Build(), environment, "api", Output, Output);
            else config.Enrich.FromLogContext().WriteTo.Sink(new LegacySink(Output));
            Logger = config.CreateLogger(); Log.Logger = Logger;
        }
        public void AssertQuiet() => Assert.Equal("", Output.ToString());
        public void AssertSingle(string code, string level)
        {
            Assert.Single(Lines);
            Assert.Contains(code, Output.ToString());
            Assert.Contains(level, Output.ToString());
            AssertSafe();
        }
        public void AssertSafe()
        {
            Assert.DoesNotContain(Secret, Output.ToString());
            Assert.DoesNotContain("runtime.unclassified", Output.ToString());
        }
        public void Clear() => Output.GetStringBuilder().Clear();
        public void Dispose() { Log.Logger = _previous; Logger.Dispose(); Output.Dispose(); }
    }
    private sealed class LegacySink(TextWriter output) : Serilog.Core.ILogEventSink
    {
        private readonly Serilog.Formatting.Display.MessageTemplateTextFormatter _formatter = new("{Level} {Message:lj} {Properties:j} {Exception}{NewLine}");
        public void Emit(Serilog.Events.LogEvent value) => _formatter.Format(value, output);
    }
}
