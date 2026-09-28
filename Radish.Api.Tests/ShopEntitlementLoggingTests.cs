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
using SqlSugar;
using Radish.Shared.Constants;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class ShopEntitlementLoggingTests
{
    private const string Secret = "SHOP_ENTITLEMENT_PRIVATE_SENTINEL";
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };
    public static IEnumerable<object[]> EffectModes =>
        from candidate in new[] { false, true }
        from environment in new[] { "Production", "Development" }
        from kind in new[] { ConsumableType.CoinCard, ConsumableType.ExpCard, ConsumableType.RenameCard }
        select new object[] { candidate, environment, kind };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task QueriesAndSystemGrant_ShouldKeepStateAndAuditQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        Assert.Single(await f.Benefit.GetUserBenefitsAsync(1));
        Assert.Single(await f.Benefit.GetUserBenefitsByTypeAsync(1, BenefitType.Title));
        Assert.True(await f.Benefit.HasBenefitAsync(1, BenefitType.Title, Secret));
        Assert.Empty(await f.Benefit.GetActiveBenefitsAsync(1));
        f.BenefitRow.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        Assert.Empty(await f.Benefit.GetUserBenefitsAsync(1));
        Assert.Single(await f.Benefit.GetUserBenefitsAsync(1, true));
        Assert.False(await f.Benefit.HasBenefitAsync(1, BenefitType.Title, Secret));
        Assert.Equal(8, await f.Benefit.SystemGrantBenefitAsync(1, BenefitType.Title, Secret, Secret, durationType: DurationType.Days, durationDays: 7));
        f.Benefits.Verify(r => r.AddAsync(It.Is<UserBenefit>(b => b.UserId == 1 && b.BenefitValue == Secret &&
            b.SourceType == "System" && b.CreateBy == "System" && !b.IsActive && b.ExpiresAt == b.EffectiveAt.AddDays(7))), Times.Once);
        Assert.Single(await f.Inventory.GetUserInventoryAsync(1));
        Assert.Single(await f.Inventory.GetUserInventoryByTypeAsync(1, ConsumableType.CoinCard));
        Assert.Equal(3, await f.Inventory.GetItemQuantityAsync(1, ConsumableType.CoinCard, "10"));
        Assert.Equal(0, await f.Inventory.GetItemQuantityAsync(2, ConsumableType.CoinCard, "10"));
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task QueriesAndGrant_ShouldPropagateFailureWithoutDuplicateLogs(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        var failure = new IOException(Secret);
        f.Benefits.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<UserBenefit, bool>>>())).ThrowsAsync(failure);
        f.Benefits.Setup(r => r.AddAsync(It.IsAny<UserBenefit>())).ThrowsAsync(failure);
        f.Items.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<UserInventory, bool>>>())).ThrowsAsync(failure);
        Func<Task>[] calls =
        [
            () => f.Controller.GetMyBenefits(), () => f.Controller.GetMyActiveBenefits(),
            () => f.Benefit.GetUserBenefitsByTypeAsync(1, BenefitType.Title),
            () => f.Benefit.HasBenefitAsync(1, BenefitType.Title),
            () => f.Benefit.SystemGrantBenefitAsync(1, BenefitType.Title, Secret),
            () => f.Controller.GetMyInventory(), () => f.Inventory.GetUserInventoryByTypeAsync(1, ConsumableType.CoinCard),
            () => f.Inventory.GetItemQuantityAsync(1, ConsumableType.CoinCard)
        ];
        foreach (var call in calls)
        {
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(call));
            capture.AssertQuiet();
            await InvokeApiAsync(call, capture.Logger, 500);
            capture.AssertSingle("http.failed", "Error");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Benefits_ShouldKeepChangedResultAndMutationCommandsQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        var active = await f.Benefit.ActivateBenefitAsync(1, 2);
        Assert.True(active.VoChanged);
        Assert.Equal(2, active.VoCurrentBenefitId);
        Assert.Equal(UserBenefitStatus.Active, active.VoStatus);
        f.Changed = false;
        Assert.False((await f.Benefit.ActivateBenefitAsync(1, 2)).VoChanged);
        Assert.False((await f.Benefit.DeactivateBenefitAsync(1, 2)).VoChanged);
        Assert.False((await f.Benefit.RevokeBenefitAsync(2, " " + Secret + " ", 9, Secret)).VoChanged);
        f.BenefitCommands.Verify(r => r.RevokeAsync(2, Secret, 9, Secret, It.IsAny<DateTime>()), Times.Once);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Benefits_ShouldLogConsumedRejectionOrPropagatedFailureOnce(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        f.BenefitFailure = new InvalidOperationException(Secret);
        Assert.Equal(400, (await f.Controller.ActivateBenefit(2)).StatusCode);
        capture.AssertSingle("benefit.activation_rejected", "Warning");
        capture.Clear();
        Assert.Equal(400, (await f.Controller.DeactivateBenefit(2)).StatusCode);
        capture.AssertSingle("benefit.deactivation_rejected", "Warning");
        capture.Clear();
        Assert.Equal(409, (await f.Controller.AdminRevokeBenefit(2, new RevokeUserBenefitDto { Reason = Secret })).StatusCode);
        capture.AssertSingle("benefit.revocation_rejected", "Warning");
        capture.Clear();
        f.BenefitFailure = new IOException(Secret);
        foreach (var call in new Func<Task>[]
        {
            () => f.Controller.ActivateBenefit(2), () => f.Controller.DeactivateBenefit(2),
            () => f.Controller.AdminRevokeBenefit(2, new RevokeUserBenefitDto { Reason = Secret })
        })
        {
            await InvokeApiAsync(call, capture.Logger, 500);
            capture.AssertSingle("http.failed", "Error");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task InventoryMutation_ShouldKeepAmountsReturnsAndFailurePropagation(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        Assert.True(await f.Inventory.DeductItemAsync(1, 3, 1));
        f.InventoryCommands.Setup(r => r.TryDeductItemAsync(1, 3, 1)).ReturnsAsync(new UserInventoryDeductPersistenceResult(false, 0));
        Assert.False(await f.Inventory.DeductItemAsync(1, 3, 1));
        Assert.Equal(3, await f.Inventory.AddItemAsync(1, ConsumableType.CoinCard, "10", Secret, null, 2));
        Assert.Equal(5, f.Item.Quantity);
        f.Items.Verify(r => r.UpdateAsync(It.Is<UserInventory>(i => i.Id == 3 && i.Quantity == 5)), Times.Once);
        Assert.Equal(8, await f.Inventory.AddItemAsync(1, ConsumableType.ExpCard, " 20 ", Secret, null, 2));
        f.Items.Verify(r => r.AddAsync(It.Is<UserInventory>(i => i.ItemValue == " 20 " && i.Quantity == 2 && i.CreateBy == "System")), Times.Once);
        var failure = new IOException(Secret);
        f.Items.Setup(r => r.AddAsync(It.IsAny<UserInventory>())).ThrowsAsync(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => f.Inventory.AddItemAsync(1, ConsumableType.ExpCard, "20", Secret, null)));
        f.InventoryCommands.Setup(r => r.TryDeductItemAsync(1, 3, 1)).ThrowsAsync(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => f.Inventory.DeductItemAsync(1, 3, 1)));
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(EffectModes))]
    public async Task ItemUse_ShouldKeepEffectsLedgerAndPersistedReplayQuiet(bool candidate, string environment, ConsumableType kind)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        f.Item.ConsumableType = kind;
        var first = kind == ConsumableType.RenameCard
            ? await f.Inventory.UseRenameCardAsync(1, RenameRequest())
            : await f.Inventory.UseItemAsync(1, UseRequest());
        Assert.True(first.Success);
        Assert.Equal(2, first.RemainingQuantity);
        Assert.NotNull(f.SavedOperation);
        Assert.Equal(first.OperationId, f.SavedOperation.Id);
        Assert.Equal(Secret, f.SavedOperation.IdempotencyKey);
        Assert.Equal(first.EffectType, f.SavedOperation.EffectType);
        if (kind == ConsumableType.RenameCard)
            f.User.Verify(s => s.ChangeDisplayNameAsync(1, Secret, It.Is<UserDisplayNameChangeContext>(c => c.Source == UserDisplayNameChangeSources.RenameCard)), Times.Once);
        else
            Assert.Equal("10", first.EffectValue);
        f.Item.Quantity = 0;
        f.Operations.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<ShopEntitlementOperation, bool>>>())).ReturnsAsync(f.SavedOperation);
        f.Idempotency.Setup(s => s.DeserializeResponse<UseItemResultDto>(Secret)).Returns(first);
        var replay = kind == ConsumableType.RenameCard
            ? await f.Inventory.UseRenameCardAsync(1, RenameRequest())
            : await f.Inventory.UseItemAsync(1, UseRequest());
        Assert.True(replay.IsIdempotentReplay);
        Assert.Equal(first.OperationId, replay.OperationId);
        f.InventoryCommands.Verify(r => r.TryDeductItemAsync(1, 3, 1), Times.Once);
        f.Operations.Verify(r => r.AddAsync(It.IsAny<ShopEntitlementOperation>()), Times.Once);
        f.Idempotency.Verify(s => s.CompleteSuccessAsync(It.IsAny<OperationIdempotencyCompletionRequest>()), Times.Once);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ItemFailure_ShouldKeepResponseAndLogOnlyFinalConsumption(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        foreach (var rename in new[] { false, true })
        {
            Func<Task<MessageModel<UseItemResultDto>>> call = rename
                ? () => f.Controller.UseRenameCard(RenameRequest()) : () => f.Controller.UseItem(UseRequest());
            var code = rename ? "inventory.rename_failed" : "inventory.use_failed";
            f.Items.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserInventory, bool>>>())).ThrowsAsync(new IOException(Secret));
            var response = await call();
            Assert.Equal(400, response.StatusCode);
            Assert.Equal("使用失败", response.ResponseData!.ErrorMessage);
            capture.AssertSingle(code, "Error");
            capture.Clear();
            f.Items.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserInventory, bool>>>())).ThrowsAsync(new BusinessException(Secret, 503));
            Assert.Equal(400, (await call()).StatusCode);
            capture.AssertSingle(code, "Error");
            capture.Clear();
            f.Items.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserInventory, bool>>>())).ThrowsAsync(new BusinessException(Secret, 409));
            Assert.Equal(400, (await call()).StatusCode);
            capture.AssertQuiet();
        }
        f.InventoryCommands.Verify(r => r.TryDeductItemAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<int>()), Times.Never);
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task RenameAndLedgerFailure_ShouldKeepWrappingAndSafeFinalLogs(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        f.Item.ConsumableType = ConsumableType.RenameCard;
        f.User.Setup(s => s.ChangeDisplayNameAsync(1, Secret, It.IsAny<UserDisplayNameChangeContext>())).ThrowsAsync(new InvalidOperationException(Secret));
        Assert.False((await f.Controller.UseRenameCard(RenameRequest())).IsSuccess);
        capture.AssertSingle("inventory.rename_rejected", "Warning");
        capture.Clear();
        f.User.Setup(s => s.ChangeDisplayNameAsync(1, Secret, It.IsAny<UserDisplayNameChangeContext>())).ThrowsAsync(new ArgumentException(Secret));
        Assert.False((await f.Controller.UseRenameCard(RenameRequest())).IsSuccess);
        capture.AssertQuiet();
        f.User.Setup(s => s.ChangeDisplayNameAsync(1, Secret, It.IsAny<UserDisplayNameChangeContext>())).ThrowsAsync(new IOException(Secret));
        Assert.False((await f.Controller.UseRenameCard(RenameRequest())).IsSuccess);
        capture.AssertSingle("inventory.rename_failed", "Error");
        capture.Clear();
        f.Item.ConsumableType = ConsumableType.CoinCard;
        f.Operations.Setup(r => r.AddAsync(It.IsAny<ShopEntitlementOperation>())).ThrowsAsync(new IOException(Secret));
        Assert.False((await f.Controller.UseItem(UseRequest())).IsSuccess);
        capture.AssertSingle("inventory.use_failed", "Error");
        f.Idempotency.Verify(s => s.CompleteSuccessAsync(It.IsAny<OperationIdempotencyCompletionRequest>()), Times.Never);
    }

    private static UseItemDto UseRequest() => new() { InventoryId = 3, Quantity = 1, IdempotencyKey = Secret };
    private static UseRenameCardDto RenameRequest() => new() { InventoryId = 3, NewDisplayName = Secret, IdempotencyKey = Secret };

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

    private sealed class Fixture
    {
        public Mock<IBaseRepository<UserBenefit>> Benefits { get; } = new();
        public Mock<IUserBenefitRepository> BenefitCommands { get; } = new();
        public Mock<IBaseRepository<UserInventory>> Items { get; } = new();
        public Mock<IUserInventoryRepository> InventoryCommands { get; } = new();
        public Mock<IBaseRepository<ShopEntitlementOperation>> Operations { get; } = new();
        public Mock<IOperationIdempotencyService> Idempotency { get; } = new();
        public Mock<IUserService> User { get; } = new();
        public UserBenefit BenefitRow { get; } = new()
        {
            Id = 2, UserId = 1, BenefitType = BenefitType.Title, BenefitValue = Secret,
            BenefitName = Secret, DurationType = DurationType.Permanent, EffectiveAt = DateTime.UtcNow.AddDays(-1)
        };
        public UserInventory Item { get; } = new() { Id = 3, UserId = 1, Quantity = 3, ConsumableType = ConsumableType.CoinCard, ItemValue = "10", ItemName = Secret };
        public ShopEntitlementOperation? SavedOperation { get; private set; }
        public bool Changed { get; set; } = true;
        public Exception? BenefitFailure { get; set; }
        public UserBenefitService Benefit { get; }
        public UserInventoryService Inventory { get; }
        public ShopController Controller { get; }

        public Fixture()
        {
            Benefits.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<UserBenefit, bool>>>()))
                .ReturnsAsync((Expression<Func<UserBenefit, bool>> p) => new[] { BenefitRow }.Where(p.Compile()).ToList());
            Benefits.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserBenefit, bool>>>()))
                .ReturnsAsync((Expression<Func<UserBenefit, bool>> p) => p.Compile()(BenefitRow) ? BenefitRow : null);
            Benefits.Setup(r => r.AddAsync(It.IsAny<UserBenefit>())).ReturnsAsync(8);
            BenefitCommands.Setup(r => r.GetActiveSelectionsAsync(1)).ReturnsAsync([]);
            BenefitCommands.Setup(r => r.ActivateAsync(1, 2, 1, "User", It.IsAny<DateTime>()))
                .Returns(() => BenefitFailure == null ? Task.FromResult(new UserBenefitPersistenceResult(BenefitRow, Changed, 2, null)) : Task.FromException<UserBenefitPersistenceResult>(BenefitFailure));
            BenefitCommands.Setup(r => r.DeactivateAsync(1, 2, 1, "User", It.IsAny<DateTime>()))
                .Returns(() => BenefitFailure == null ? Task.FromResult(new UserBenefitPersistenceResult(BenefitRow, Changed, null, 2)) : Task.FromException<UserBenefitPersistenceResult>(BenefitFailure));
            BenefitCommands.Setup(r => r.RevokeAsync(2, Secret, It.IsAny<long>(), Secret, It.IsAny<DateTime>()))
                .Returns(() => BenefitFailure == null ? Task.FromResult(new UserBenefitPersistenceResult(BenefitRow, Changed, null, 2)) : Task.FromException<UserBenefitPersistenceResult>(BenefitFailure));
            Items.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<UserInventory, bool>>>()))
                .ReturnsAsync((Expression<Func<UserInventory, bool>> p) => new[] { Item }.Where(p.Compile()).ToList());
            Items.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserInventory, bool>>>()))
                .ReturnsAsync((Expression<Func<UserInventory, bool>> p) => p.Compile()(Item) ? Item : null);
            Items.Setup(r => r.AddAsync(It.IsAny<UserInventory>())).ReturnsAsync(8);
            Items.Setup(r => r.UpdateAsync(It.IsAny<UserInventory>())).ReturnsAsync(true);
            InventoryCommands.Setup(r => r.TryDeductItemAsync(1, 3, 1)).ReturnsAsync(new UserInventoryDeductPersistenceResult(true, 2));
            Operations.Setup(r => r.AddAsync(It.IsAny<ShopEntitlementOperation>()))
                .Callback<ShopEntitlementOperation>(o => SavedOperation = o).ReturnsAsync((ShopEntitlementOperation o) => o.Id);
            Idempotency.Setup(s => s.NormalizeKey(Secret)).Returns(Secret);
            Idempotency.Setup(s => s.CreateRequestSnapshot(It.IsAny<IReadOnlyDictionary<string, object?>>()))
                .Returns(new OperationIdempotencyRequestSnapshot { RequestHash = Secret, RequestSummary = Secret });
            Idempotency.Setup(s => s.BeginAsync(It.IsAny<OperationIdempotencyBeginRequest>()))
                .ReturnsAsync(new OperationIdempotencyBeginResult { Status = OperationIdempotencyBeginStatus.Started, RecordId = 7 });
            Idempotency.Setup(s => s.SerializeResponse(It.IsAny<UseItemResultDto>())).Returns(Secret);
            User.Setup(s => s.ChangeDisplayNameAsync(1, Secret, It.IsAny<UserDisplayNameChangeContext>())).ReturnsAsync(true);
            var coin = new Mock<ICoinService>();
            coin.Setup(s => s.GrantCoinOnceAsync(1, 10, "USE_COIN_CARD", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<long?>(), It.IsAny<string?>()))
                .ReturnsAsync(CoinGrantOnceResult.NewGrant(Secret));
            var experience = new Mock<IExperienceService>();
            experience.Setup(s => s.GrantExperienceOnceAsync(1, 10, "USE_EXP_CARD", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<long?>(), It.IsAny<string?>()))
                .ReturnsAsync(ExperienceGrantOnceResult.NewGrant());
            var mapper = new Mock<IMapper>();
            mapper.Setup(m => m.Map<UserBenefitVo>(It.IsAny<object>())).Returns((object o) =>
            {
                var b = (UserBenefit)o;
                return new UserBenefitVo { VoId = b.Id, VoUserId = b.UserId, VoBenefitValue = b.BenefitValue };
            });
            mapper.Setup(m => m.Map<List<UserBenefitVo>>(It.IsAny<object>()))
                .Returns((object o) => ((IEnumerable<UserBenefit>)o).Select(b => mapper.Object.Map<UserBenefitVo>(b)).ToList());
            mapper.Setup(m => m.Map<List<UserInventoryVo>>(It.IsAny<object>()))
                .Returns((object o) => ((IEnumerable<UserInventory>)o).Select(i => new UserInventoryVo { VoId = i.Id, VoQuantity = i.Quantity }).ToList());
            Benefit = new UserBenefitService(mapper.Object, Benefits.Object, Operations.Object, BenefitCommands.Object, InventoryCommands.Object, Mock.Of<IAttachmentUrlResolver>());
            Inventory = new UserInventoryService(mapper.Object, Items.Object, InventoryCommands.Object, Operations.Object,
                User.Object, coin.Object, Idempotency.Object, Mock.Of<IAttachmentUrlResolver>(), experience.Object);
            var current = new Mock<ICurrentUserAccessor>();
            current.SetupGet(c => c.Current).Returns(new CurrentUser { UserId = 1, UserName = Secret });
            var localizer = new Mock<IStringLocalizer<Errors>>();
            localizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => new LocalizedString(key, key, true));
            Controller = new ShopController(Mock.Of<IProductService>(), Mock.Of<IOrderService>(), Benefit, Inventory,
                Mock.Of<IUserBrowseHistoryService>(), current.Object, localizer.Object, Mock.Of<IProductReviewService>());
        }
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
