using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using Moq;
using Radish.Api.Tests.ErrorHandling;
using Radish.Common.Exceptions;
using Radish.Model;
using Radish.Service;
using Radish.Shared.CustomEnum;
using Serilog.Events;
using Xunit;
using static Radish.Api.Tests.ShopOutputAssertions;

namespace Radish.Api.Tests;

public sealed partial class ShopFulfillmentLoggingTests
{
    [Theory]
    [MemberData(nameof(ShopOutputLoggingTests.OutputScopes), MemberType = typeof(ShopOutputLoggingTests))]
    public async Task FinalOutput_ShouldPreserveConfigurationAndStockRetryResults(bool candidate, string environment, bool audit, bool mel)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var global = new ApplicationLogScope(capture.Logger);
        using var scope = Pollute(capture, mel);
        using var activity = new Activity("shop-stock-results").SetIdFormat(ActivityIdFormat.W3C).Start();
        using (var f = new Fixture())
        {
            Assert.True((await f.Product.CheckCanBuyAsync(1, 3, 2)).canBuy);
            Assert.True((await f.Order.PurchaseAsync(1, PurchaseRequest())).Success);
            Assert.Empty(capture.Events);
            f.Products.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Product, bool>>>())).ReturnsAsync(new Product
            { Id = 3, IsEnabled = true, IsOnSale = true, ProductType = ProductType.Benefit, BenefitType = BenefitType.Title, BenefitValue = new string('x', 41), Name = Secret });
            var result = await f.Product.CheckCanBuyAsync(1, 3, 2);
            Assert.False(result.canBuy);
            Assert.Equal("商品配置不完整，请联系管理员", result.reason);
            Check(capture, new Expected(typeof(ProductService), "product.configuration_rejected", LogEventLevel.Warning, null));
        }
        using (var f = new Fixture())
        {
            f.Products.SetupSequence(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<Product, Product>>>(), It.IsAny<Expression<Func<Product, bool>>>())).ReturnsAsync(0).ReturnsAsync(1);
            Assert.True(await f.Product.DeductStockAsync(3, 2));
            Check(capture, new Expected(typeof(ProductService), "product.stock_retrying", LogEventLevel.Warning, null, ("attempt", 1), ("delayMs", 50)));
            f.Products.Setup(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<Product, Product>>>(), It.IsAny<Expression<Func<Product, bool>>>())).ReturnsAsync(0);
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Product.DeductStockAsync(3, 2));
            Assert.Contains("乐观锁冲突", failure.Message);
            f.Products.Verify(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<Product, Product>>>(), It.IsAny<Expression<Func<Product, bool>>>()), Times.Exactly(7));
            Check(capture, new[] { 50, 100, 200, 400 }.Select((delay, index) =>
                new Expected(typeof(ProductService), "product.stock_retrying", LogEventLevel.Warning, null, ("attempt", index + 1), ("delayMs", delay))).ToArray());
        }
    }

    [Theory]
    [MemberData(nameof(ShopOutputLoggingTests.OutputScopes), MemberType = typeof(ShopOutputLoggingTests))]
    public async Task FinalOutput_ShouldPreservePurchaseStagesAndIndependentCompensationFailures(bool candidate, string environment, bool audit, bool mel)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var global = new ApplicationLogScope(capture.Logger);
        using var scope = Pollute(capture, mel);
        using var activity = new Activity("shop-purchase-results").SetIdFormat(ActivityIdFormat.W3C).Start();
        using (var f = new Fixture())
        {
            f.Coin.Setup(s => s.ConsumeCoinAsync(1, 46, "Order", 4, It.IsAny<string>())).ThrowsAsync(new IOException(Secret));
            var result = await f.Order.PurchaseAsync(1, PurchaseRequest());
            Assert.False(result.Success);
            Assert.Equal("扣除萝卜币失败", result.ErrorMessage);
            Assert.Equal(12, f.StockWrites[1].Stock);
            f.Orders.Verify(r => r.UpdateAsync(It.Is<Order>(o => o.FailureStage == OrderFailureStage.Payment && o.FailReason!.Contains(Secret))), Times.Once);
            f.Benefits.Verify(r => r.AddAsync(It.IsAny<UserBenefit>()), Times.Never);
            Check(capture, new Expected(typeof(OrderService), "order.purchase_failed", LogEventLevel.Error, "io", ("purchaseStage", "payment")));
        }
        using (var f = new Fixture())
        {
            f.Benefits.Setup(r => r.AddAsync(It.IsAny<UserBenefit>())).ThrowsAsync(new IOException(Secret));
            var result = await f.Order.PurchaseAsync(1, PurchaseRequest());
            Assert.False(result.Success);
            Assert.Contains(Secret, result.ErrorMessage);
            f.Orders.Verify(r => r.UpdateAsync(It.Is<Order>(o => o.FailureStage == OrderFailureStage.Fulfillment && o.CoinTransactionId == 9)), Times.Once);
            Assert.Equal(2, f.StockWrites.Count);
            Check(capture, new Expected(typeof(OrderService), "order.purchase_failed", LogEventLevel.Error, "io", ("purchaseStage", "fulfillment")));
        }
        using (var f = new Fixture())
        {
            var original = new IOException(Secret);
            f.Products.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Product, bool>>>())).ThrowsAsync(original);
            var failure = await Assert.ThrowsAsync<BusinessException>(() => f.Order.PurchaseAsync(1, PurchaseRequest()));
            Assert.Equal(400, failure.StatusCode);
            Assert.Same(original, failure.InnerException);
            f.Orders.Verify(r => r.AddAsync(It.IsAny<Order>()), Times.Never);
            Check(capture, new Expected(typeof(OrderService), "order.purchase_interrupted", LogEventLevel.Error, "io"));
        }
        using (var f = new Fixture())
        {
            var compensation = new TimeoutException(Secret);
            f.Coin.Setup(s => s.ConsumeCoinAsync(1, 46, "Order", 4, It.IsAny<string>())).ThrowsAsync(new IOException(Secret));
            f.Products.SetupSequence(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<Product, Product>>>(), It.IsAny<Expression<Func<Product, bool>>>()))
                .ReturnsAsync(1).ThrowsAsync(compensation);
            var failure = await Assert.ThrowsAsync<BusinessException>(() => f.Order.PurchaseAsync(1, PurchaseRequest()));
            Assert.Equal(400, failure.StatusCode);
            Assert.Same(compensation, failure.InnerException);
            f.Orders.Verify(r => r.UpdateAsync(It.IsAny<Order>()), Times.Never);
            Check(capture,
                new Expected(typeof(OrderService), "order.purchase_failed", LogEventLevel.Error, "io", ("purchaseStage", "payment")),
                new Expected(typeof(OrderService), "order.purchase_interrupted", LogEventLevel.Error, "timeout"));
        }
        using (var f = new Fixture())
        {
            var writeFailure = new InvalidOperationException(Secret);
            f.Benefits.Setup(r => r.AddAsync(It.IsAny<UserBenefit>())).ThrowsAsync(new IOException(Secret));
            f.Orders.Setup(r => r.UpdateAsync(It.IsAny<Order>())).ThrowsAsync(writeFailure);
            var failure = await Assert.ThrowsAsync<BusinessException>(() => f.Order.PurchaseAsync(1, PurchaseRequest()));
            Assert.Same(writeFailure, failure.InnerException);
            Assert.Single(f.StockWrites); // 只有初次扣库存，后续已售数量更新尚未执行。
            Check(capture,
                new Expected(typeof(OrderService), "order.purchase_failed", LogEventLevel.Error, "io", ("purchaseStage", "fulfillment")),
                new Expected(typeof(OrderService), "order.purchase_interrupted", LogEventLevel.Error, "invalid-operation"));
        }
    }

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public async Task FinalOutput_ShouldPreserveStockAndPurchaseOutputFailureOwnership(bool candidate, string environment, bool audit)
    {
        using var broken = new HangfireBrokenWriter();
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit, output: broken);
        using var global = new ApplicationLogScope(capture.Logger);
        var outputFailure = new IOException(Secret);
        capture.Sink.EmitFailure = outputFailure;
        using (var f = new Fixture())
        {
            f.Products.Setup(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<Product, Product>>>(), It.IsAny<Expression<Func<Product, bool>>>())).ReturnsAsync(0);
            if (audit) await AssertAuditFailure(() => f.Product.DeductStockAsync(3, 2), outputFailure);
            else await Assert.ThrowsAsync<InvalidOperationException>(() => f.Product.DeductStockAsync(3, 2));
            f.Products.Verify(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<Product, Product>>>(), It.IsAny<Expression<Func<Product, bool>>>()), Times.Exactly(audit ? 1 : 5));
        }
        using (var f = new Fixture())
        {
            f.Coin.Setup(s => s.ConsumeCoinAsync(1, 46, "Order", 4, It.IsAny<string>())).ThrowsAsync(new IOException("payment"));
            if (audit) await AssertAuditFailure(() => f.Order.PurchaseAsync(1, PurchaseRequest()), outputFailure);
            else Assert.False((await f.Order.PurchaseAsync(1, PurchaseRequest())).Success);
            Assert.Equal(audit ? 1 : 2, f.StockWrites.Count);
            f.Orders.Verify(r => r.UpdateAsync(It.IsAny<Order>()), audit ? Times.Never() : Times.Once());
            f.Orders.Verify(r => r.AddAsync(It.IsAny<Order>()), Times.Once);
        }
        AssertOutputFailed(capture);
    }
}
