using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Common;
using Radish.Common.CacheTool;
using Radish.Common.Exceptions;
using Radish.Common.OptionTool;
using Radish.Common.TimeTool;
using Radish.Extension.Log;
using Radish.Infrastructure;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Service;
using Serilog;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class RewardLoggingTests
{
    private const string Secret = "REWARD_PRIVATE_SENTINEL";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Coin_ShouldKeepSuccessfulLedgerAndReplay_WithoutPerGrantLogs(bool candidate)
    {
        using var capture = new Capture(candidate);
        using var fixture = new Fixture();
        CoinTransaction? saved = null;
        fixture.CoinTransactions.Setup(r => r.AddAsync(It.IsAny<CoinTransaction>()))
            .Callback<CoinTransaction>(value => saved = value).ReturnsAsync(1);
        var first = await fixture.Coin.GrantCoinOnceAsync(1, 23, Secret, Secret, Secret, 7, Secret);
        Assert.True(first.Granted);
        Assert.NotNull(saved);
        Assert.Equal(23, saved.Amount);
        Assert.Equal(Secret, saved.RewardBusinessKey);
        Assert.Equal(Secret, saved.Remark);
        Assert.Equal(8, saved.TenantId);
        fixture.CoinTransactions.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<CoinTransaction, bool>>>())).ReturnsAsync(saved);
        var replay = await fixture.Coin.GrantCoinOnceAsync(1, 23, Secret, Secret, Secret, 7, Secret);
        Assert.True(replay.AlreadyGranted);
        Assert.Equal(first.TransactionNo, replay.TransactionNo);
        fixture.CoinTransactions.Verify(r => r.AddAsync(It.IsAny<CoinTransaction>()), Times.Once);
        fixture.BalanceChanges.Verify(r => r.AddAsync(It.Is<BalanceChangeLog>(value => value.ChangeAmount == 23 && value.BalanceAfter == 123)), Times.Once);
        Assert.Equal("", capture.Output.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Coin_ShouldKeepFourAttempts_AndLeaveExhaustedErrorToCaller(bool candidate)
    {
        using var capture = new Capture(candidate);
        using var fixture = new Fixture();
        fixture.Balances.Setup(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<UserBalance, UserBalance>>>(), It.IsAny<Expression<Func<UserBalance, bool>>>())).ReturnsAsync(0);
        await Assert.ThrowsAsync<ConcurrencyException>(() => fixture.Coin.GrantCoinOnceAsync(1, 5, Secret, Secret));
        fixture.Balances.Verify(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<UserBalance, UserBalance>>>(), It.IsAny<Expression<Func<UserBalance, bool>>>()), Times.Exactly(4));
        Assert.Equal(3, capture.Lines.Length);
        Assert.All(capture.Lines, line => Assert.Contains("reward.retrying", line));
        Assert.DoesNotContain("reward.failed", capture.Output.ToString());
        if (candidate)
        {
            var delays = capture.Lines.Select(line => { using var json = JsonDocument.Parse(line); return json.RootElement.GetProperty("properties").GetProperty("delayMs").GetInt32(); }).ToArray();
            Assert.Equal(new[] { 100, 200, 400 }, delays);
        }
        capture.AssertSafe();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Experience_ShouldKeepSevenAttempts_AndConsumeExhaustionOnce(bool candidate)
    {
        using var capture = new Capture(candidate);
        using var fixture = new Fixture();
        fixture.Experiences.Setup(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<UserExperience, UserExperience>>>(), It.IsAny<Expression<Func<UserExperience, bool>>>())).ReturnsAsync(0);
        var result = await fixture.Experience.GrantExperienceOnceAsync(1, 5, Secret, Secret);
        Assert.True(result.Skipped);
        Assert.Equal("发放失败", result.Reason);
        fixture.Experiences.Verify(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<UserExperience, UserExperience>>>(), It.IsAny<Expression<Func<UserExperience, bool>>>()), Times.Exactly(7));
        Assert.Equal(6, capture.Lines.Count(line => line.Contains("reward.retrying")));
        Assert.Single(capture.Lines, line => line.Contains("reward.failed"));
        if (candidate)
        {
            var retryLines = capture.Lines.Where(line => line.Contains("reward.retrying")).ToArray();
            for (var i = 0; i < retryLines.Length; i++)
            {
                using var json = JsonDocument.Parse(retryLines[i]);
                var properties = json.RootElement.GetProperty("properties");
                Assert.Equal(i + 1, properties.GetProperty("attempt").GetInt32());
                Assert.InRange(properties.GetProperty("delayMs").GetInt32(), 0, Math.Min(100 * (1 << i), 1000));
            }
        }
        capture.AssertSafe();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Experience_ShouldKeepAuditAndReplay_WithoutPerGrantLogs(bool candidate)
    {
        using var capture = new Capture(candidate);
        using var fixture = new Fixture();
        ExpTransaction? saved = null;
        fixture.ExpTransactions.Setup(r => r.AddAsync(It.IsAny<ExpTransaction>()))
            .Callback<ExpTransaction>(value => saved = value).ReturnsAsync(1);
        Assert.True((await fixture.Experience.GrantExperienceOnceAsync(1, 5, Secret, Secret, Secret, 7, Secret)).Granted);
        Assert.NotNull(saved);
        Assert.Equal(5, saved.ExpAmount);
        Assert.Equal(Secret, saved.RewardBusinessKey);
        Assert.Equal(Secret, saved.Remark);
        Assert.Equal(8, saved.TenantId);
        fixture.ExpTransactions.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<ExpTransaction, bool>>>())).ReturnsAsync(saved);
        Assert.True((await fixture.Experience.GrantExperienceOnceAsync(1, 5, Secret, Secret)).AlreadyGranted);
        fixture.ExpTransactions.Verify(r => r.AddAsync(It.IsAny<ExpTransaction>()), Times.Once);
        Assert.Equal("", capture.Output.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UniqueConflict_ShouldKeepCoinThrowAndExperienceSkip_WhenNoExistingReward(bool candidate)
    {
        using var capture = new Capture(candidate);
        using var fixture = new Fixture();
        var failure = new InvalidOperationException("UNIQUE constraint failed " + Secret);
        fixture.CoinTransactions.SetupSequence(r => r.QueryFirstAsync(It.IsAny<Expression<Func<CoinTransaction, bool>>>()))
            .ThrowsAsync(failure).ReturnsAsync((CoinTransaction?)null);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Coin.GrantCoinOnceAsync(1, 5, Secret, Secret)));
        Assert.Equal("", capture.Output.ToString());
        fixture.ExpTransactions.SetupSequence(r => r.QueryFirstAsync(It.IsAny<Expression<Func<ExpTransaction, bool>>>()))
            .ThrowsAsync(failure).ReturnsAsync((ExpTransaction?)null);
        var result = await fixture.Experience.GrantExperienceOnceAsync(1, 5, Secret, Secret);
        Assert.True(result.Skipped);
        Assert.Equal("奖励业务键冲突", result.Reason);
        capture.AssertSingle("reward.conflict");
    }

    [Fact]
    public async Task UniqueConflict_ShouldReturnExistingQuietly_WhenConcurrentWinnerExists()
    {
        using var capture = new Capture(true);
        using var fixture = new Fixture();
        var failure = new InvalidOperationException("UNIQUE constraint failed " + Secret);
        fixture.CoinTransactions.SetupSequence(r => r.QueryFirstAsync(It.IsAny<Expression<Func<CoinTransaction, bool>>>()))
            .ThrowsAsync(failure).ReturnsAsync(new CoinTransaction { TransactionNo = Secret });
        fixture.ExpTransactions.SetupSequence(r => r.QueryFirstAsync(It.IsAny<Expression<Func<ExpTransaction, bool>>>()))
            .ThrowsAsync(failure).ReturnsAsync(new ExpTransaction());
        Assert.Equal(Secret, (await fixture.Coin.GrantCoinOnceAsync(1, 5, Secret, Secret)).TransactionNo);
        Assert.True((await fixture.Experience.GrantExperienceOnceAsync(1, 5, Secret, Secret)).AlreadyGranted);
        Assert.Equal("", capture.Output.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExperienceInitialization_ShouldLogOnlyUnrecoveredFailure(bool recovered)
    {
        using var capture = new Capture(true);
        using var fixture = new Fixture();
        fixture.Experiences.SetupSequence(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserExperience, bool>>>()))
            .ReturnsAsync((UserExperience?)null).ReturnsAsync((UserExperience?)null).ReturnsAsync((UserExperience?)null)
            .ReturnsAsync(recovered ? new UserExperience { UserId = 1 } : null);
        fixture.Experiences.Setup(r => r.AddAsync(It.IsAny<UserExperience>())).ThrowsAsync(new IOException(Secret));
        Assert.Equal(recovered, await fixture.Experience.GrantExperienceAsync(1, 5, Secret));
        if (recovered) Assert.Equal("", capture.Output.ToString());
        else capture.AssertSingle("reward.failed");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExperienceCache_ShouldRetainDatabaseFallback_WithoutExceptionPayload(bool candidate)
    {
        using var capture = new Capture(candidate);
        using var fixture = new Fixture();
        fixture.Cache.Setup(c => c.GetAsync<List<LevelConfig>>(It.IsAny<string>())).ThrowsAsync(new IOException(Secret));
        fixture.Cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<List<LevelConfig>>(), It.IsAny<TimeSpan>())).ThrowsAsync(new IOException(Secret));
        Assert.True(await fixture.Experience.GrantExperienceAsync(1, 5, Secret));
        Assert.Equal(2, capture.Lines.Length);
        Assert.All(capture.Lines, line => Assert.Contains("reward.cache_fallback", line));
        fixture.Levels.Verify(r => r.QueryAsync(It.IsAny<Expression<Func<LevelConfig, bool>>>()), Times.Once);
        fixture.ExpTransactions.Verify(r => r.AddAsync(It.IsAny<ExpTransaction>()), Times.Once);
        capture.AssertSafe();
    }

    [Fact]
    public async Task Experience_ShouldKeepInvalidFrozenAndLimitedResultsQuiet()
    {
        using var capture = new Capture(true);
        using var fixture = new Fixture();
        Assert.False(await fixture.Experience.GrantExperienceAsync(0, 5, Secret));
        Assert.True((await fixture.Experience.GrantExperienceOnceAsync(1, 0, Secret, Secret)).Skipped);
        fixture.Experiences.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserExperience, bool>>>()))
            .ReturnsAsync(new UserExperience { UserId = 1, ExpFrozen = true });
        Assert.False(await fixture.Experience.GrantExperienceAsync(1, 5, Secret));
        fixture.Experiences.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserExperience, bool>>>()))
            .ReturnsAsync(new UserExperience { UserId = 1 });
        fixture.Stats.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserExpDailyStats, bool>>>()))
            .ReturnsAsync(new UserExpDailyStats { UserId = 1, ExpEarned = 500 });
        Assert.False(await fixture.Experience.GrantExperienceAsync(1, 5, Secret));
        fixture.ExpTransactions.Verify(r => r.AddAsync(It.IsAny<ExpTransaction>()), Times.Never);
        Assert.Equal("", capture.Output.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Batches_ShouldContinueAfterFailure_AndKeepReturnCounts(bool candidate)
    {
        using var capture = new Capture(candidate);
        using var fixture = new Fixture();
        var coins = await fixture.Coin.BatchGrantCoinAsync([
            new CoinGrantInfo { UserId = 1, Amount = -1, TransactionType = Secret },
            new CoinGrantInfo { UserId = 1, Amount = 5, TransactionType = Secret, Remark = Secret }]);
        Assert.Single(coins);
        capture.AssertSingle("reward.batch_failed");
        capture.Clear();
        Assert.Equal(1, await fixture.Experience.BatchGrantExperienceAsync([
            new ExpGrantInfo { UserId = 1, Amount = -1, ExpType = Secret },
            new ExpGrantInfo { UserId = 1, Amount = 5, ExpType = Secret, Remark = Secret }]));
        capture.AssertSingle("reward.batch_completed");
        capture.Clear();
        Assert.Empty(await fixture.Coin.BatchGrantCoinAsync([]));
        Assert.Equal(0, await fixture.Experience.BatchGrantExperienceAsync([]));
        Assert.Equal("", capture.Output.ToString());
    }

    [Fact]
    public async Task Experience_ShouldKeepPreCatchFailurePropagation_AndCaughtFailureResults()
    {
        using var capture = new Capture(true);
        using var fixture = new Fixture();
        var failure = new IOException(Secret);
        fixture.Users.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>>())).ThrowsAsync(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => fixture.Experience.GrantExperienceOnceAsync(1, 5, Secret, Secret)));
        Assert.Equal("", capture.Output.ToString());
        fixture.Experiences.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserExperience, bool>>>())).ThrowsAsync(failure);
        Assert.False(await fixture.Experience.GrantExperienceAsync(1, 5, Secret));
        capture.AssertSingle("reward.failed");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retry_ShouldStopAfterRecovery_WithoutFinalError(bool experience)
    {
        using var capture = new Capture(true);
        using var fixture = new Fixture();
        if (experience)
        {
            fixture.Experiences.SetupSequence(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<UserExperience, UserExperience>>>(), It.IsAny<Expression<Func<UserExperience, bool>>>()))
                .ReturnsAsync(0).ReturnsAsync(1);
            Assert.True((await fixture.Experience.GrantExperienceOnceAsync(1, 5, Secret, Secret)).Granted);
            fixture.Experiences.Verify(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<UserExperience, UserExperience>>>(), It.IsAny<Expression<Func<UserExperience, bool>>>()), Times.Exactly(2));
            fixture.ExpTransactions.Verify(r => r.AddAsync(It.IsAny<ExpTransaction>()), Times.Once);
        }
        else
        {
            fixture.Balances.SetupSequence(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<UserBalance, UserBalance>>>(), It.IsAny<Expression<Func<UserBalance, bool>>>()))
                .ReturnsAsync(0).ReturnsAsync(1);
            Assert.True((await fixture.Coin.GrantCoinOnceAsync(1, 5, Secret, Secret)).Granted);
            fixture.Balances.Verify(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<UserBalance, UserBalance>>>(), It.IsAny<Expression<Func<UserBalance, bool>>>()), Times.Exactly(2));
            fixture.BalanceChanges.Verify(r => r.AddAsync(It.IsAny<BalanceChangeLog>()), Times.Once);
        }
        capture.AssertSingle("reward.retrying");
    }

    [Fact]
    public async Task ExperienceBatch_ShouldNotLogConsumedItemFailureTwice()
    {
        using var capture = new Capture(true);
        using var fixture = new Fixture();
        fixture.Experiences.SetupSequence(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserExperience, bool>>>()))
            .ThrowsAsync(new IOException(Secret)).ReturnsAsync(new UserExperience { UserId = 1 });
        Assert.Equal(1, await fixture.Experience.BatchGrantExperienceAsync([
            new ExpGrantInfo { UserId = 1, Amount = 5, ExpType = Secret },
            new ExpGrantInfo { UserId = 1, Amount = 5, ExpType = Secret }]));
        Assert.Equal(2, capture.Lines.Length);
        Assert.Single(capture.Lines, line => line.Contains("reward.failed"));
        Assert.Single(capture.Lines, line => line.Contains("reward.batch_completed"));
        Assert.DoesNotContain("reward.batch_failed", capture.Output.ToString());
        capture.AssertSafe();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly IConfiguration _previous = AppSettingsTool.Configuration;
        public Mock<IBaseRepository<User>> Users { get; } = new();
        public Mock<IUserBalanceRepository> Balances { get; } = new();
        public Mock<IBaseRepository<CoinTransaction>> CoinTransactions { get; } = new();
        public Mock<IBaseRepository<BalanceChangeLog>> BalanceChanges { get; } = new();
        public Mock<IBaseRepository<UserExperience>> Experiences { get; } = new();
        public Mock<IBaseRepository<ExpTransaction>> ExpTransactions { get; } = new();
        public Mock<IBaseRepository<LevelConfig>> Levels { get; } = new();
        public Mock<IBaseRepository<UserExpDailyStats>> Stats { get; } = new();
        public Mock<ICaching> Cache { get; } = new();
        public CoinService Coin { get; }
        public ExperienceService Experience { get; }
        public Fixture()
        {
            AppSettingsTool.Configuration = new ConfigurationBuilder().Build();
            Users.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync(new User { Id = 1, TenantId = 8 });
            Users.Setup(r => r.QueryExistsAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync(true);
            Balances.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserBalance, bool>>>())).ReturnsAsync(new UserBalance { UserId = 1, Balance = 100, TenantId = 8 });
            Balances.Setup(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<UserBalance, UserBalance>>>(), It.IsAny<Expression<Func<UserBalance, bool>>>())).ReturnsAsync(1);
            Experiences.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserExperience, bool>>>())).ReturnsAsync(new UserExperience { UserId = 1, TenantId = 8 });
            Experiences.Setup(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<UserExperience, UserExperience>>>(), It.IsAny<Expression<Func<UserExperience, bool>>>())).ReturnsAsync(1);
            Stats.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserExpDailyStats, bool>>>())).ReturnsAsync(new UserExpDailyStats { UserId = 1 });
            Levels.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<LevelConfig, bool>>>())).ReturnsAsync([]);
            var clock = TimeProvider.System;
            var calendar = new BusinessCalendar(clock, Options.Create(new TimeOptions { DefaultTimeZoneId = "Asia/Shanghai" }));
            Coin = new CoinService(Mock.Of<IMapper>(), Balances.Object, Users.Object, CoinTransactions.Object,
                BalanceChanges.Object, Mock.Of<IPaymentPasswordService>());
            Experience = new ExperienceService(Mock.Of<IMapper>(), Experiences.Object, ExpTransactions.Object,
                Levels.Object, Stats.Object, Users.Object, Mock.Of<IExperienceGovernanceRepository>(),
                Mock.Of<IExperienceCalculator>(), Mock.Of<ICoinService>(), Mock.Of<IAttachmentUrlResolver>(),
                Mock.Of<INotificationService>(), Cache.Object, clock, calendar);
        }
        public void Dispose() => AppSettingsTool.Configuration = _previous;
    }

    private sealed class Capture : IDisposable
    {
        private readonly Serilog.ILogger _previous = Log.Logger;
        public StringWriter Output { get; } = new();
        private readonly Serilog.Core.Logger _logger;
        public string[] Lines => Output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        public Capture(bool candidate)
        {
            var config = new LoggerConfiguration();
            if (candidate) RuntimeLoggingConfiguration.Configure(config, new ConfigurationBuilder().Build(), "Production", "api", Output, Output);
            else config.Enrich.FromLogContext().WriteTo.Sink(new LegacySink(Output));
            _logger = config.CreateLogger();
            Log.Logger = _logger;
        }
        public void AssertSingle(string code) { Assert.Single(Lines); Assert.Contains(code, Output.ToString()); AssertSafe(); }
        public void AssertSafe() => Assert.DoesNotContain(Secret, Output.ToString());
        public void Clear() => Output.GetStringBuilder().Clear();
        public void Dispose() { Log.Logger = _previous; _logger.Dispose(); Output.Dispose(); }
    }
    private sealed class LegacySink(TextWriter output) : Serilog.Core.ILogEventSink
    {
        private readonly Serilog.Formatting.Display.MessageTemplateTextFormatter _formatter = new("{Level} {Message:lj} {Properties:j} {Exception}{NewLine}");
        public void Emit(Serilog.Events.LogEvent value) => _formatter.Format(value, output);
    }
}
