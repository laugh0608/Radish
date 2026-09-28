using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using AutoMapper;
using Castle.DynamicProxy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.ErrorHandling;
using Radish.Api.Resources;
using Radish.Common;
using Radish.Common.CacheTool;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Common.OptionTool;
using Radish.Common.TimeTool;
using Radish.Extension.AopExtension;
using Radish.Extension.Log;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Repository.UnitOfWorks;
using Radish.Service;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;
using Serilog;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class AccountGovernanceLoggingTests
{
    private const string Secret = "ACCOUNT_GOVERNANCE_PRIVATE_SENTINEL";
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Queries_ShouldKeepResultsAndInitializationQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var fixture = new Fixture();
        Assert.Equal(100, (await fixture.Coin.GetBalanceAsync(1))!.VoBalance);
        Assert.Equal(100, (await fixture.Coin.GetBalancesAsync([1]))[1].VoBalance);
        var page = await fixture.Coin.GetTransactionsAsync(1, 1, 20, businessType: Secret);
        Assert.Single(page.Data);
        Assert.Equal(1, page.DataCount);
        Assert.Equal(Secret, (await fixture.Coin.GetTransactionByNoAsync(Secret))!.VoTransactionNo);
        var stats = await fixture.Coin.GetStatisticsAsync(1, Secret);
        Assert.Equal(23, stats.VoTrendData.Sum(item => item.VoIncome));
        Assert.Equal(23, Assert.Single(stats.VoCategoryStats).VoAmount);
        fixture.Balances.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserBalance, bool>>>())).ReturnsAsync((UserBalance?)null);
        Assert.Equal(0, (await fixture.Coin.GetBalanceAsync(1))!.VoBalance);
        fixture.Balances.Verify(r => r.AddAsync(It.Is<UserBalance>(b => b.UserId == 1 && b.Balance == 0)), Times.Once);
        fixture.Transactions.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<CoinTransaction, bool>>>())).ReturnsAsync((CoinTransaction?)null);
        Assert.Null(await fixture.Coin.GetTransactionByNoAsync(Secret));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Coin.GetTransactionsAsync(1, 0, 20));
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Queries_ShouldPropagateOriginalFailuresWithoutLogging(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var fixture = new Fixture();
        var failure = new IOException(Secret);
        fixture.Balances.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserBalance, bool>>>())).ThrowsAsync(failure);
        fixture.Balances.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<UserBalance, bool>>>())).ThrowsAsync(failure);
        fixture.Transactions.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<CoinTransaction, bool>>>())).ThrowsAsync(failure);
        fixture.Transactions.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<CoinTransaction, bool>>>())).ThrowsAsync(failure);
        fixture.Transactions.Setup(r => r.QueryPageAsync(It.IsAny<Expression<Func<CoinTransaction, bool>>>(), 1, 20,
            It.IsAny<Expression<Func<CoinTransaction, object>>>(), OrderByType.Desc,
            It.IsAny<Expression<Func<CoinTransaction, object>>>(), OrderByType.Desc)).ThrowsAsync(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => fixture.Coin.GetBalanceAsync(1)));
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => fixture.Coin.GetBalancesAsync([1])));
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => fixture.Coin.GetTransactionsAsync(1, 1, 20)));
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => fixture.Coin.GetTransactionByNoAsync(Secret)));
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => fixture.Coin.GetStatisticsAsync(1, Secret)));
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task CoinAdjustment_ShouldKeepLedgerAndReplayQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var fixture = new Fixture();
        var result = await fixture.Coin.AdminAdjustBalanceAsync(1, -23, Secret, 9, Secret, 3, Secret);
        fixture.Transactions.Verify(r => r.AddAsync(It.Is<CoinTransaction>(t => t.TransactionNo == result &&
            t.Amount == 23 && t.FromUserId == 1 && t.Remark == Secret && t.CreateBy == Secret && t.CreateId == 9)), Times.Once);
        fixture.Changes.Verify(r => r.AddAsync(It.Is<BalanceChangeLog>(t => t.ChangeAmount == -23 &&
            t.BalanceBefore == 100 && t.BalanceAfter == 77 && t.CreateBy == Secret && t.CreateId == 9)), Times.Once);
        fixture.Idempotency.Verify(s => s.CompleteSuccessAsync(It.Is<OperationIdempotencyCompletionRequest>(r => r.ResourceNo == result)), Times.Once);
        fixture.Idempotency.Setup(s => s.BeginAsync(It.IsAny<OperationIdempotencyBeginRequest>()))
            .ReturnsAsync(new OperationIdempotencyBeginResult { Status = OperationIdempotencyBeginStatus.Succeeded, ResponsePayload = Secret });
        fixture.Idempotency.Setup(s => s.DeserializeResponse<TransactionResultVo>(Secret)).Returns(new TransactionResultVo { VoTransactionNo = result });
        Assert.Equal(result, await fixture.Coin.AdminAdjustBalanceAsync(1, -23, Secret, 9, Secret, 3, Secret));
        fixture.Transactions.Verify(r => r.AddAsync(It.IsAny<CoinTransaction>()), Times.Once);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Controller_ShouldOwnHandledRejectionsAndPreserveResponse(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var fixture = new Fixture();
        var failure = new InvalidOperationException(Secret);
        fixture.Balances.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserBalance, bool>>>())).ThrowsAsync(failure);
        var balance = await fixture.Controller.GetBalanceByUserId(1);
        Assert.Equal(400, balance.StatusCode);
        Assert.Equal("Coin.BalanceQueryRejected", balance.Code);
        Assert.Equal(Secret, balance.MessageInfo);
        capture.AssertSingle("coin.balance_query_rejected", "Warning");
        capture.Clear();
        fixture.Users.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>>())).ThrowsAsync(failure);
        var transactions = await fixture.Controller.AdminGetTransactions(1);
        Assert.Equal(400, transactions.StatusCode);
        Assert.Equal("Coin.TransactionQueryRejected", transactions.Code);
        capture.AssertSingle("coin.transaction_query_rejected", "Warning");
        capture.Clear();
        var adjustment = await fixture.Controller.AdminAdjustBalance(AdjustmentRequest());
        Assert.Equal(400, adjustment.StatusCode);
        Assert.Equal("Coin.AdminAdjustRejected", adjustment.Code);
        Assert.Equal(Secret, adjustment.MessageInfo);
        capture.AssertSingle("coin.adjustment_rejected", "Warning");
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task CoinAdjustment_ShouldKeepConflictsQuietAndLogConsumedServerFailure(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var fixture = new Fixture();
        var request = AdjustmentRequest();
        request.ExpectedVersion = 2;
        var rejected = await fixture.Controller.AdminAdjustBalance(request);
        Assert.Equal(409, rejected.StatusCode);
        Assert.Equal(CoinErrorCodes.AdminAdjustVersionConflict, rejected.Code);
        fixture.Transactions.Verify(r => r.AddAsync(It.IsAny<CoinTransaction>()), Times.Never);
        capture.AssertQuiet();
        request.ExpectedVersion = 3;
        request.DeltaAmount = 0;
        Assert.Equal(400, (await fixture.Controller.AdminAdjustBalance(request)).StatusCode);
        capture.AssertQuiet();
        request.DeltaAmount = -23;
        fixture.Idempotency.Setup(s => s.BeginAsync(It.IsAny<OperationIdempotencyBeginRequest>()))
            .ThrowsAsync(new BusinessException(Secret, 503, Secret, Secret));
        var failed = await fixture.Controller.AdminAdjustBalance(request);
        Assert.Equal(503, failed.StatusCode);
        Assert.Equal(Secret, failed.Code);
        capture.AssertSingle("http.failed", "Error");
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task CoinAdjustment_ShouldRollbackAndLeaveOneErrorToHttpBoundary(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var fixture = new Fixture();
        var failure = new IOException(Secret);
        fixture.Transactions.Setup(r => r.AddAsync(It.IsAny<CoinTransaction>())).ThrowsAsync(failure);
        var unit = new Mock<IUnitOfWorkManage>();
        var proxy = new ProxyGenerator().CreateInterfaceProxyWithTarget<ICoinService>(fixture.Coin, new TranAop(unit.Object));
        var controller = CreateController(proxy);
        await AssertHttpFailureAsync(() => controller.AdminAdjustBalance(AdjustmentRequest()), capture.Logger);
        unit.Verify(u => u.RollbackTran(It.IsAny<MethodInfo>()), Times.Once);
        unit.Verify(u => u.CommitTran(It.IsAny<MethodInfo>()), Times.Never);
        fixture.Idempotency.Verify(s => s.CompleteSuccessAsync(It.IsAny<OperationIdempotencyCompletionRequest>()), Times.Never);
        capture.AssertSingle("http.failed", "Error");
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ExperienceAdjustment_ShouldKeepAuditClampAndReplayQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var fixture = new Fixture();
        var result = await fixture.Experience.AdminAdjustExperienceAsync(1, -120, Secret, 9, Secret, 3, Secret);
        Assert.Equal(0, result.VoExperience.VoTotalExp);
        Assert.Equal(4, result.VoExperience.VoVersion);
        fixture.Governance.Verify(r => r.ApplyAdjustmentAsync(It.Is<ExperienceAdjustmentMutationCommand>(c =>
            c.ExpectedVersion == 3 && c.Transaction.ExpAmount == -80 && c.Transaction.ExpType == "PENALTY" &&
            c.Transaction.Remark == Secret && c.Transaction.CreateBy == Secret && c.Transaction.CreateId == 9)), Times.Once);
        fixture.Idempotency.Verify(s => s.CompleteSuccessAsync(It.IsAny<OperationIdempotencyCompletionRequest>()), Times.Once);
        fixture.Idempotency.Setup(s => s.BeginAsync(It.IsAny<OperationIdempotencyBeginRequest>()))
            .ReturnsAsync(new OperationIdempotencyBeginResult { Status = OperationIdempotencyBeginStatus.Succeeded, ResponsePayload = Secret });
        fixture.Idempotency.Setup(s => s.DeserializeResponse<AdminExperienceAdjustmentResultVo>(Secret)).Returns(result);
        var replay = await fixture.Experience.AdminAdjustExperienceAsync(1, -120, Secret, 9, Secret, 3, Secret);
        Assert.True(replay.VoReplayed);
        Assert.Same(result, replay);
        fixture.Governance.Verify(r => r.ApplyAdjustmentAsync(It.IsAny<ExperienceAdjustmentMutationCommand>()), Times.Once);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ExperienceFreeze_ShouldKeepVersionedAuditAndConflictContractQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var fixture = new Fixture();
        var until = DateTime.UtcNow.AddDays(7);
        var frozen = await fixture.Experience.FreezeExperienceAsync(1, until, Secret, 9, Secret, 3);
        Assert.True(frozen.VoExperience.VoExpFrozen);
        Assert.Equal(4, frozen.VoExperience.VoVersion);
        fixture.Governance.Verify(r => r.ApplyGovernanceActionAsync(It.Is<ExperienceGovernanceMutationCommand>(c =>
            c.ExpFrozen && c.FrozenUntil == until && c.FrozenReason == Secret && c.ExpectedVersion == 3 &&
            c.Action.ActionType == (int)ExperienceGovernanceActionTypeEnum.Freeze && c.Action.Remark == Secret &&
            c.Action.CreateBy == Secret && c.Action.CreateId == 9)), Times.Once);
        var unfrozen = await fixture.Experience.UnfreezeExperienceAsync(1, Secret, 9, Secret, 4);
        Assert.False(unfrozen.VoExperience.VoExpFrozen);
        Assert.Equal(5, unfrozen.VoExperience.VoVersion);
        fixture.Governance.Verify(r => r.ApplyGovernanceActionAsync(It.Is<ExperienceGovernanceMutationCommand>(c =>
            !c.ExpFrozen && c.FrozenUntil == null && c.FrozenReason == "" && c.ExpectedVersion == 4 &&
            c.Action.ActionType == (int)ExperienceGovernanceActionTypeEnum.Unfreeze && c.Action.Remark == Secret &&
            c.Action.CreateBy == Secret && c.Action.CreateId == 9)), Times.Once);
        var stale = await Assert.ThrowsAsync<BusinessException>(() => fixture.Experience.FreezeExperienceAsync(1, until, Secret, 9, Secret, 4));
        Assert.Equal(409, stale.StatusCode);
        fixture.Governance.Setup(r => r.ApplyGovernanceActionAsync(It.IsAny<ExperienceGovernanceMutationCommand>()))
            .ThrowsAsync(new ExperienceGovernanceStateConflictException());
        Assert.Equal(409, (await Assert.ThrowsAsync<BusinessException>(() => fixture.Experience.FreezeExperienceAsync(1, until, Secret, 9, Secret, 5))).StatusCode);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ExperienceGovernance_ShouldPropagateFailuresToOneSafeHttpError(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var fixture = new Fixture();
        var failure = new IOException(Secret);
        fixture.Governance.Setup(r => r.ApplyAdjustmentAsync(It.IsAny<ExperienceAdjustmentMutationCommand>())).ThrowsAsync(failure);
        fixture.Governance.Setup(r => r.ApplyGovernanceActionAsync(It.IsAny<ExperienceGovernanceMutationCommand>())).ThrowsAsync(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => fixture.Experience.AdminAdjustExperienceAsync(1, -23, Secret, 9, Secret, 3, Secret)));
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => fixture.Experience.FreezeExperienceAsync(1, null, Secret, 9, Secret, 3)));
        fixture.ExperienceAccount.ExpFrozen = true;
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => fixture.Experience.UnfreezeExperienceAsync(1, Secret, 9, Secret, 3)));
        capture.AssertQuiet();
        await AssertHttpFailureAsync(() => fixture.Experience.UnfreezeExperienceAsync(1, Secret, 9, Secret, 3), capture.Logger);
        capture.AssertSingle("http.failed", "Error");
    }

    private static AdminAdjustBalanceDto AdjustmentRequest() => new()
    {
        UserId = 1, DeltaAmount = -23, Reason = Secret, ExpectedVersion = 3, IdempotencyKey = Secret
    };

    private static CoinController CreateController(ICoinService coin)
    {
        var current = new Mock<ICurrentUserAccessor>();
        current.SetupGet(a => a.Current).Returns(new CurrentUser { UserId = 9, UserName = Secret });
        var localizer = new Mock<IStringLocalizer<Errors>>();
        localizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => new LocalizedString(key, key, true));
        return new CoinController(coin, current.Object, localizer.Object);
    }

    private static async Task AssertHttpFailureAsync(Func<Task> action, Serilog.ILogger logger)
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
        Assert.Equal(500, context.Response.StatusCode);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly IConfiguration _previous = AppSettingsTool.Configuration;
        public Mock<IBaseRepository<User>> Users { get; } = new();
        public Mock<IUserBalanceRepository> Balances { get; } = new();
        public Mock<IBaseRepository<CoinTransaction>> Transactions { get; } = new();
        public Mock<IBaseRepository<BalanceChangeLog>> Changes { get; } = new();
        public Mock<IOperationIdempotencyService> Idempotency { get; } = new();
        public Mock<IExperienceGovernanceRepository> Governance { get; } = new();
        public UserExperience ExperienceAccount { get; } = new() { Id = 2, UserId = 1, CurrentLevel = 1, TotalExp = 80, Version = 3 };
        public CoinService Coin { get; }
        public CoinController Controller { get; }
        public ExperienceService Experience { get; }

        public Fixture()
        {
            AppSettingsTool.Configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["ExperienceCalculator:EnableCache"] = "false" }).Build();
            var user = new User { Id = 1, UserName = Secret };
            Users.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync(user);
            Users.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync([user]);
            var balance = new UserBalance { UserId = 1, Balance = 100, Version = 3 };
            Balances.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserBalance, bool>>>())).ReturnsAsync(balance);
            Balances.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<UserBalance, bool>>>())).ReturnsAsync([balance]);
            Balances.Setup(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<UserBalance, UserBalance>>>(), It.IsAny<Expression<Func<UserBalance, bool>>>())).ReturnsAsync(1);
            var transaction = new CoinTransaction { Id = 7, ToUserId = 1, TransactionNo = Secret, Amount = 23, Status = "SUCCESS", CreateTime = DateTime.Now };
            Transactions.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<CoinTransaction, bool>>>())).ReturnsAsync(transaction);
            Transactions.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<CoinTransaction, bool>>>())).ReturnsAsync([transaction]);
            Transactions.Setup(r => r.QueryPageAsync(It.IsAny<Expression<Func<CoinTransaction, bool>>>(), 1, 20,
                It.IsAny<Expression<Func<CoinTransaction, object>>>(), OrderByType.Desc,
                It.IsAny<Expression<Func<CoinTransaction, object>>>(), OrderByType.Desc)).ReturnsAsync((new List<CoinTransaction> { transaction }, 1));
            Transactions.Setup(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<CoinTransaction, CoinTransaction>>>(), It.IsAny<Expression<Func<CoinTransaction, bool>>>())).ReturnsAsync(1);
            Idempotency.Setup(s => s.NormalizeKey(Secret)).Returns(Secret);
            Idempotency.Setup(s => s.CreateRequestSnapshot(It.IsAny<IReadOnlyDictionary<string, object?>>())).Returns(new OperationIdempotencyRequestSnapshot { RequestHash = Secret, RequestSummary = Secret });
            Idempotency.Setup(s => s.BeginAsync(It.IsAny<OperationIdempotencyBeginRequest>())).ReturnsAsync(new OperationIdempotencyBeginResult { Status = OperationIdempotencyBeginStatus.Started, RecordId = 3 });
            var mapper = new Mock<IMapper>();
            mapper.Setup(m => m.Map<UserBalanceVo>(It.IsAny<object>())).Returns((object o) => new UserBalanceVo { VoUserId = ((UserBalance)o).UserId, VoBalance = ((UserBalance)o).Balance });
            mapper.Setup(m => m.Map<CoinTransactionVo>(It.IsAny<object>())).Returns((object o) => new CoinTransactionVo { VoTransactionNo = ((CoinTransaction)o).TransactionNo, VoToUserId = 1 });
            mapper.Setup(m => m.Map<List<CoinTransactionVo>>(It.IsAny<object>())).Returns((object o) => ((List<CoinTransaction>)o).Select(t => mapper.Object.Map<CoinTransactionVo>(t)).ToList());
            mapper.Setup(m => m.Map<UserExperienceVo>(It.IsAny<object>())).Returns((object o) =>
            {
                var e = (UserExperience)o;
                return new UserExperienceVo { VoUserId = e.UserId, VoVersion = e.Version, VoCurrentLevel = e.CurrentLevel, VoTotalExp = e.TotalExp, VoExpFrozen = e.ExpFrozen };
            });
            mapper.Setup(m => m.Map<ExpTransactionVo>(It.IsAny<object>())).Returns((object o) => new ExpTransactionVo { VoId = ((ExpTransaction)o).Id });
            Coin = new CoinService(mapper.Object, Balances.Object, Users.Object, Transactions.Object, Changes.Object, Mock.Of<IPaymentPasswordService>(), Idempotency.Object);
            Controller = CreateController(Coin);
            var experiences = new Mock<IBaseRepository<UserExperience>>();
            experiences.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserExperience, bool>>>())).ReturnsAsync(ExperienceAccount);
            var levels = new Mock<IBaseRepository<LevelConfig>>();
            levels.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<LevelConfig, bool>>>())).ReturnsAsync([]);
            Governance.Setup(r => r.ApplyAdjustmentAsync(It.IsAny<ExperienceAdjustmentMutationCommand>())).ReturnsAsync((ExperienceAdjustmentMutationCommand c) =>
            {
                ExperienceAccount.TotalExp = c.TotalExp;
                ExperienceAccount.CurrentLevel = c.CurrentLevel;
                ExperienceAccount.Version++;
                return new ExperienceAdjustmentMutationResult(ExperienceAccount, c.Transaction);
            });
            Governance.Setup(r => r.ApplyGovernanceActionAsync(It.IsAny<ExperienceGovernanceMutationCommand>())).ReturnsAsync((ExperienceGovernanceMutationCommand c) =>
            {
                ExperienceAccount.ExpFrozen = c.ExpFrozen;
                ExperienceAccount.FrozenUntil = c.FrozenUntil;
                ExperienceAccount.FrozenReason = c.FrozenReason;
                ExperienceAccount.Version++;
                c.Action.ExpectedVersion = c.ExpectedVersion;
                c.Action.ResultVersion = ExperienceAccount.Version;
                return new ExperienceGovernanceMutationResult(ExperienceAccount, c.Action);
            });
            var clock = TimeProvider.System;
            Experience = new ExperienceService(mapper.Object, experiences.Object, Mock.Of<IBaseRepository<ExpTransaction>>(), levels.Object,
                Mock.Of<IBaseRepository<UserExpDailyStats>>(), Users.Object, Governance.Object, Mock.Of<IExperienceCalculator>(), Coin,
                Mock.Of<IAttachmentUrlResolver>(), Mock.Of<INotificationService>(), Mock.Of<ICaching>(), clock,
                new BusinessCalendar(clock, Options.Create(new TimeOptions { DefaultTimeZoneId = "Asia/Shanghai" })), operationIdempotencyService: Idempotency.Object);
        }

        public void Dispose() => AppSettingsTool.Configuration = _previous;
    }

    private sealed class Capture : IDisposable
    {
        private readonly Serilog.ILogger _previous = Log.Logger;
        public StringWriter Output { get; } = new();
        public Serilog.Core.Logger Logger { get; }
        public Capture(bool candidate, string environment)
        {
            var config = new LoggerConfiguration().MinimumLevel.Verbose();
            if (candidate) RuntimeLoggingConfiguration.Configure(config, new ConfigurationBuilder().Build(), environment, "api", Output, Output);
            else config.Enrich.FromLogContext().WriteTo.Sink(new LegacySink(Output));
            Logger = config.CreateLogger();
            Log.Logger = Logger;
        }
        public void AssertQuiet() => Assert.Equal("", Output.ToString());
        public void AssertSingle(string code, string level)
        {
            Assert.Single(Output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries));
            Assert.Contains(code, Output.ToString());
            Assert.Contains(level, Output.ToString());
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
