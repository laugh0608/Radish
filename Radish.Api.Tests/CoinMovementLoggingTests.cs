using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Common;
using Radish.Common.Exceptions;
using Radish.Common.HelpTool;
using Radish.Extension.Log;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.Models;
using Radish.Model.ViewModels;
using Radish.Repository.UnitOfWorks;
using Radish.Service;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;
using Radish.Shared.Security;
using Serilog;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class CoinMovementLoggingTests
{
    private const string Secret = "MOVEMENT_PRIVATE_SENTINEL";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Consume_ShouldKeepLedgerAndPropagateFailuresWithoutDuplicateLogs(bool candidate)
    {
        using var capture = new Capture(candidate);
        using var fixture = new Fixture();
        CoinTransaction? saved = null;
        fixture.Transactions.Setup(r => r.AddAsync(It.IsAny<CoinTransaction>())).Callback<CoinTransaction>(t => saved = t).ReturnsAsync(1);
        var result = await fixture.Coin.ConsumeCoinAsync(1, 23, Secret, 42, Secret);
        Assert.NotNull(saved);
        Assert.Equal(saved.Id, result.transactionId);
        Assert.Equal(saved.TransactionNo, result.transactionNo);
        Assert.Equal(Secret, saved.Remark);
        Assert.Equal(Secret, saved.BusinessType);
        Assert.Equal(23, saved.Amount);
        fixture.Changes.Verify(r => r.AddAsync(It.Is<BalanceChangeLog>(x => x.ChangeAmount == -23 && x.BalanceAfter == 77)), Times.Once);
        var failure = new IOException(Secret);
        fixture.Transactions.Setup(r => r.AddAsync(It.IsAny<CoinTransaction>())).ThrowsAsync(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => fixture.Coin.ConsumeCoinAsync(1, 23, Secret, 42, Secret)));
        Assert.Equal("", capture.Output.ToString());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Transfer_ShouldRetryCompletionOnly_AndReportOnlyRecoveredFailure(bool candidate, bool recover)
    {
        using var capture = new Capture(candidate);
        using var fixture = new Fixture();
        var failure = new IOException(Secret);
        var completions = new List<OperationIdempotencyCompletionRequest>();
        fixture.Idempotency.Setup(s => s.CompleteSuccessAsync(It.IsAny<OperationIdempotencyCompletionRequest>()))
            .Returns((OperationIdempotencyCompletionRequest request) =>
            {
                completions.Add(request);
                return completions.Count == 1 || !recover ? Task.FromException(failure) : Task.CompletedTask;
            });
        if (recover)
        {
            var transactionNo = await fixture.Coin.TransferAsync(1, 2, 23, "274958", Secret, Secret);
            Assert.All(completions, item => Assert.Equal(transactionNo, item.ResourceNo));
            capture.AssertSingle("coin.transfer_completion_recovered");
            Assert.DoesNotContain("Error", capture.Output.ToString());
        }
        else
        {
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => fixture.Coin.TransferAsync(1, 2, 23, "274958", Secret, Secret)));
            Assert.Equal("", capture.Output.ToString());
        }
        Assert.Equal(2, completions.Count);
        fixture.Transactions.Verify(r => r.AddAsync(It.Is<CoinTransaction>(t => t.Amount == 23 && t.Remark == Secret)), Times.Once);
        fixture.Changes.Verify(r => r.AddAsync(It.IsAny<BalanceChangeLog>()), Times.Exactly(2));
        Assert.Equal(77, fixture.Accounts[0].Balance);
        Assert.Equal(123, fixture.Accounts[1].Balance);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Transfer_ShouldKeepSuccessReplayAndBusinessRejectionQuiet(bool candidate)
    {
        using var capture = new Capture(candidate);
        using var fixture = new Fixture();
        var result = await fixture.Coin.TransferAsync(1, 2, 23, "274958", Secret, Secret);
        fixture.Idempotency.Setup(s => s.BeginAsync(It.IsAny<OperationIdempotencyBeginRequest>()))
            .ReturnsAsync(new OperationIdempotencyBeginResult { Status = OperationIdempotencyBeginStatus.Succeeded, ResponsePayload = Secret });
        fixture.Idempotency.Setup(s => s.DeserializeResponse<TransactionResultVo>(Secret)).Returns(new TransactionResultVo { VoTransactionNo = result });
        Assert.Equal(result, await fixture.Coin.TransferAsync(1, 2, 23, "274958", Secret, Secret));
        fixture.Payment.Setup(s => s.VerifyPaymentPasswordAsync(1, It.IsAny<VerifyPaymentPasswordRequest>()))
            .ReturnsAsync(new PaymentPasswordVerifyResult { IsSuccess = false, ErrorMessage = Secret, ErrorCode = PaymentPasscodeErrorCodes.Invalid });
        var error = await Assert.ThrowsAsync<BusinessException>(() => fixture.Coin.TransferAsync(1, 2, 23, "274958", Secret, Secret));
        Assert.Equal(Secret, error.Message);
        Assert.Equal(PaymentPasscodeErrorCodes.Invalid, error.ErrorCode);
        fixture.Transactions.Verify(r => r.AddAsync(It.IsAny<CoinTransaction>()), Times.Once);
        Assert.Equal("", capture.Output.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Transfer_ShouldPreserveTerminalFailureAndDoNotLogPropagatedError(bool candidate)
    {
        using var capture = new Capture(candidate);
        using var fixture = new Fixture();
        fixture.Accounts[0].Balance = 0;
        var error = await Assert.ThrowsAsync<BusinessException>(() => fixture.Coin.TransferAsync(1, 2, 23, "274958", Secret, Secret));
        Assert.Equal(CoinErrorCodes.TransferInsufficientBalance, error.ErrorCode);
        fixture.Idempotency.Verify(s => s.CompleteSuccessAsync(It.Is<OperationIdempotencyCompletionRequest>(r =>
            r.ErrorCode == error.ErrorCode && r.ErrorMessage == error.Message)), Times.Once);
        fixture.Transactions.Verify(r => r.AddAsync(It.IsAny<CoinTransaction>()), Times.Never);
        Assert.Equal("", capture.Output.ToString());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Purchase_ShouldConsumeStageFailureOnceAndKeepCompensation(bool candidate, bool fulfillment)
    {
        using var capture = new Capture(candidate);
        var orders = new Mock<IBaseRepository<Order>>();
        var products = new Mock<IBaseRepository<Product>>();
        products.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Product, bool>>>())).ReturnsAsync(new Product
        { Id = 3, Name = Secret, Price = 23, StockType = StockType.Limited });
        var productService = new Mock<IProductService>();
        productService.Setup(s => s.CheckCanBuyAsync(1, 3, 1)).ReturnsAsync((true, (string?)null));
        productService.Setup(s => s.DeductStockAsync(3, 1)).ReturnsAsync(true);
        var coin = new Mock<ICoinService>();
        coin.Setup(s => s.GetBalanceAsync(1)).ReturnsAsync(new UserBalanceVo { VoBalance = 100 });
        coin.Setup(s => s.ConsumeCoinAsync(1, 23, "Order", 4, It.IsAny<string>())).ThrowsAsync(new IOException(Secret));
        var payment = new Mock<IPaymentPasswordService>();
        payment.Setup(s => s.VerifyPaymentPasswordAsync(1, It.IsAny<VerifyPaymentPasswordRequest>())).ReturnsAsync(new PaymentPasswordVerifyResult { IsSuccess = true });
        orders.Setup(r => r.AddAsync(It.IsAny<Order>())).ReturnsAsync(4);
        var benefits = new Mock<IUserBenefitService>();
        if (fulfillment)
        {
            coin.Setup(s => s.ConsumeCoinAsync(1, 23, "Order", 4, It.IsAny<string>())).ReturnsAsync((11L, Secret));
            benefits.Setup(s => s.GrantOrderFulfillmentAsync(It.IsAny<Order>())).ThrowsAsync(new IOException(Secret));
        }
        var service = new OrderService(Mock.Of<IMapper>(), orders.Object, products.Object, Mock.Of<IBaseRepository<User>>(),
            Mock.Of<IBaseRepository<CoinTransaction>>(), productService.Object, benefits.Object, coin.Object, payment.Object, Mock.Of<IAttachmentUrlResolver>());
        var result = await service.PurchaseAsync(1, new CreateOrderDto { ProductId = 3, Quantity = 1, PaymentPassword = "274958", UserRemark = Secret });
        Assert.False(result.Success);
        if (fulfillment) Assert.Contains(Secret, result.ErrorMessage);
        else Assert.Equal("扣除萝卜币失败", result.ErrorMessage);
        productService.Verify(s => s.RestoreStockAsync(3, 1, StockType.Limited), fulfillment ? Times.Never() : Times.Once());
        var expectedStage = fulfillment ? OrderFailureStage.Fulfillment : OrderFailureStage.Payment;
        orders.Verify(r => r.UpdateAsync(It.Is<Order>(o => o.FailureStage == expectedStage && o.FailReason!.Contains(Secret))), Times.Once);
        benefits.Verify(s => s.GrantOrderFulfillmentAsync(It.IsAny<Order>()), fulfillment ? Times.Once() : Times.Never());
        capture.AssertSingle("order.purchase_failed");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PaymentVerification_ShouldKeepCountersAndPropagateRepositoryFailureQuietly(bool candidate)
    {
        using var capture = new Capture(candidate);
        using var factory = LoggerFactory.Create(builder => builder.AddSerilog(capture.Logger));
        var repository = new Mock<IPaymentPasswordRepository>();
        var stored = new UserPaymentPassword { UserId = 1, PasswordHash = PasswordHasher.HashPassword("274958"), PasscodeVersion = PaymentPasscodeRules.CurrentPasscodeVersion, FailedAttempts = 4 };
        repository.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(stored);
        var service = new PaymentPasswordService(repository.Object, Mock.Of<IAuditLogService>(), Mock.Of<IMapper>(), factory.CreateLogger<PaymentPasswordService>(), TimeProvider.System);
        var good = await service.VerifyPaymentPasswordAsync(1, new VerifyPaymentPasswordRequest { Password = "274958", BusinessType = Secret });
        Assert.True(good.IsSuccess);
        repository.Verify(r => r.ResetFailedAttemptsAsync(1, It.IsAny<DateTime>()), Times.Once);
        var bad = await service.VerifyPaymentPasswordAsync(1, new VerifyPaymentPasswordRequest { Password = "951372", BusinessType = Secret });
        Assert.True(bad.IsLocked);
        Assert.Equal(0, bad.RemainingAttempts);
        repository.Verify(r => r.UpdateFailedAttemptsAsync(1, 5, It.IsAny<DateTime?>(), It.IsAny<DateTime>()), Times.Once);
        var failure = new IOException(Secret);
        repository.Setup(r => r.GetByUserIdAsync(1)).ThrowsAsync(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => service.VerifyPaymentPasswordAsync(1, new VerifyPaymentPasswordRequest { Password = "274958" })));
        Assert.Equal("", capture.Output.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Idempotency_ShouldKeepConflictRecoveryQuietAndMissingCompletionSafe(bool candidate)
    {
        using var capture = new Capture(candidate);
        var repository = new Mock<IBaseRepository<OperationIdempotencyRecord>>();
        repository.SetupSequence(r => r.QueryFirstAsync(It.IsAny<Expression<Func<OperationIdempotencyRecord, bool>>>()))
            .ReturnsAsync((OperationIdempotencyRecord?)null)
            .ReturnsAsync(new OperationIdempotencyRecord { Id = 1, RequestHash = Secret, Status = OperationIdempotencyStatuses.Processing, ExpiresAt = DateTime.UtcNow.AddHours(1) });
        repository.Setup(r => r.AddAsync(It.IsAny<OperationIdempotencyRecord>())).ThrowsAsync(new InvalidOperationException("23505 duplicate key " + Secret));
        var unit = new Mock<IUnitOfWorkManage>();
        unit.Setup(u => u.ExecuteInSavepointAsync(It.IsAny<Func<Task<long>>>())).Returns((Func<Task<long>> action) => action());
        var service = new OperationIdempotencyService(repository.Object, unit.Object, TimeProvider.System);
        var begin = await service.BeginAsync(new OperationIdempotencyBeginRequest { UserId = 1, OperationType = Secret, IdempotencyKey = Secret, RequestHash = Secret });
        Assert.Equal(OperationIdempotencyBeginStatus.Processing, begin.Status);
        Assert.Equal("", capture.Output.ToString());
        await service.CompleteSuccessAsync(new OperationIdempotencyCompletionRequest { RecordId = 23, ErrorMessage = Secret });
        capture.AssertSingle("idempotency.completion_missing");
        capture.Output.GetStringBuilder().Clear();
        await service.CompleteFailureAsync(23, Secret, Secret);
        capture.AssertSingle("idempotency.completion_missing");
        repository.Verify(r => r.UpdateAsync(It.IsAny<OperationIdempotencyRecord>()), Times.Never);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly IConfiguration _previous = AppSettingsTool.Configuration;
        public Mock<IUserBalanceRepository> Balances { get; } = new();
        public Mock<IBaseRepository<CoinTransaction>> Transactions { get; } = new();
        public Mock<IBaseRepository<BalanceChangeLog>> Changes { get; } = new();
        public Mock<IPaymentPasswordService> Payment { get; } = new();
        public Mock<IOperationIdempotencyService> Idempotency { get; } = new();
        public List<UserBalance> Accounts { get; } = [new() { UserId = 1, Balance = 100 }, new() { UserId = 2, Balance = 100 }];
        public CoinService Coin { get; }
        public Fixture()
        {
            AppSettingsTool.Configuration = new ConfigurationBuilder().Build();
            var users = new Mock<IBaseRepository<User>>();
            users.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync(new User { Id = 1 });
            users.Setup(r => r.QueryExistsAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync(true);
            Balances.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserBalance, bool>>>())).ReturnsAsync((Expression<Func<UserBalance, bool>> expr) => Accounts.FirstOrDefault(expr.Compile()));
            Balances.Setup(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<UserBalance, UserBalance>>>(), It.IsAny<Expression<Func<UserBalance, bool>>>())).ReturnsAsync(1);
            Balances.Setup(r => r.UpdateAsync(It.IsAny<UserBalance>())).ReturnsAsync(true);
            Payment.Setup(s => s.VerifyPaymentPasswordAsync(It.IsAny<long>(), It.IsAny<VerifyPaymentPasswordRequest>())).ReturnsAsync(new PaymentPasswordVerifyResult { IsSuccess = true });
            Idempotency.Setup(s => s.NormalizeKey(Secret)).Returns(Secret);
            Idempotency.Setup(s => s.CreateRequestSnapshot(It.IsAny<IReadOnlyDictionary<string, object?>>())).Returns(new OperationIdempotencyRequestSnapshot { RequestHash = Secret, RequestSummary = Secret });
            Idempotency.Setup(s => s.BeginAsync(It.IsAny<OperationIdempotencyBeginRequest>())).ReturnsAsync(new OperationIdempotencyBeginResult { Status = OperationIdempotencyBeginStatus.Started, RecordId = 3 });
            Coin = new CoinService(Mock.Of<IMapper>(), Balances.Object, users.Object, Transactions.Object, Changes.Object, Payment.Object, Idempotency.Object);
        }
        public void Dispose() => AppSettingsTool.Configuration = _previous;
    }
    private sealed class Capture : IDisposable
    {
        private readonly Serilog.ILogger _previous = Log.Logger;
        public StringWriter Output { get; } = new();
        public Serilog.Core.Logger Logger { get; }
        public Capture(bool candidate)
        {
            var config = new LoggerConfiguration();
            if (candidate) RuntimeLoggingConfiguration.Configure(config, new ConfigurationBuilder().Build(), "Production", "api", Output, Output);
            else config.Enrich.FromLogContext().WriteTo.Sink(new LegacySink(Output));
            Logger = config.CreateLogger(); Log.Logger = Logger;
        }
        public void AssertSingle(string code)
        {
            Assert.Single(Output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries));
            Assert.Contains(code, Output.ToString());
            Assert.DoesNotContain(Secret, Output.ToString());
        }
        public void Dispose() { Log.Logger = _previous; Logger.Dispose(); Output.Dispose(); }
    }
    private sealed class LegacySink(TextWriter output) : Serilog.Core.ILogEventSink
    {
        private readonly Serilog.Formatting.Display.MessageTemplateTextFormatter _formatter = new("{Level} {Message:lj} {Properties:j} {Exception}{NewLine}");
        public void Emit(Serilog.Events.LogEvent value) => _formatter.Format(value, output);
    }
}
