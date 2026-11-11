using System;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using Moq;
using Radish.Api.Tests.ErrorHandling;
using Radish.Common.Exceptions;
using Radish.Model;
using Radish.IService;
using Radish.Service;
using Serilog.Events;
using Xunit;
using static Radish.Api.Tests.RewardOutputAssertions;

namespace Radish.Api.Tests;

public sealed partial class RewardLoggingTests
{
    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public async Task FinalOutput_ShouldKeepBatchCountsAndConsumedItemOwnership(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var global = new ApplicationLogScope(capture.Logger);
        using var scope = Pollute();
        using var activity = new Activity("reward-batches").SetIdFormat(ActivityIdFormat.W3C).Start();
        using var f = new Fixture();
        Assert.Empty(await f.Coin.BatchGrantCoinAsync([]));
        Assert.Equal(0, await f.Experience.BatchGrantExperienceAsync([]));
        Assert.Empty(capture.Events);

        Assert.Single(await f.Coin.BatchGrantCoinAsync([
            new CoinGrantInfo { UserId = 1, Amount = -1, TransactionType = Secret },
            new CoinGrantInfo { UserId = 1, Amount = 5, TransactionType = Secret }]));
        Check(capture, typeof(RewardRuntimeLog), "reward.batch_failed", LogEventLevel.Error, "argument",
            ("rewardDomain", "coin"), ("processedCount", 1), ("rejectedCount", 0), ("failedCount", 1), ("outcome", "partial"));
        Assert.Single(await f.Coin.BatchGrantCoinAsync([new CoinGrantInfo { UserId = 1, Amount = 5, TransactionType = Secret }]));
        Check(capture, typeof(RewardRuntimeLog), "reward.batch_completed", LogEventLevel.Information, null,
            ("rewardDomain", "coin"), ("processedCount", 1), ("rejectedCount", 0), ("failedCount", 0), ("outcome", "succeeded"));
        Assert.Empty(await f.Coin.BatchGrantCoinAsync([new CoinGrantInfo { UserId = 1, Amount = -1, TransactionType = Secret }]));
        Check(capture, typeof(RewardRuntimeLog), "reward.batch_failed", LogEventLevel.Error, "argument",
            ("rewardDomain", "coin"), ("processedCount", 0), ("rejectedCount", 0), ("failedCount", 1), ("outcome", "failed"));

        Assert.Equal(1, await f.Experience.BatchGrantExperienceAsync([
            new ExpGrantInfo { UserId = 1, Amount = -1, ExpType = Secret },
            new ExpGrantInfo { UserId = 1, Amount = 5, ExpType = Secret }]));
        Check(capture, typeof(RewardRuntimeLog), "reward.batch_completed", LogEventLevel.Information, null,
            ("rewardDomain", "experience"), ("processedCount", 1), ("rejectedCount", 1), ("failedCount", 0), ("outcome", "partial"));
        Assert.Equal(0, await f.Experience.BatchGrantExperienceAsync([new ExpGrantInfo { UserId = 1, Amount = -1, ExpType = Secret }]));
        Check(capture, typeof(RewardRuntimeLog), "reward.batch_completed", LogEventLevel.Information, null,
            ("rewardDomain", "experience"), ("processedCount", 0), ("rejectedCount", 1), ("failedCount", 0), ("outcome", "failed"));

        f.Experiences.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserExperience, bool>>>())).ThrowsAsync(new IOException(Secret));
        Assert.Equal(0, await f.Experience.BatchGrantExperienceAsync([new ExpGrantInfo { UserId = 1, Amount = 5, ExpType = Secret }]));
        Assert.Equal(new[] { "reward.failed", "reward.batch_completed" }, capture.Codes);
        // 单项已消费异常不计作批次捕获；原计数和返回责任保持不变。
        if (candidate)
        {
            var lines = capture.Output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            capture.Clear();
            capture.Output.WriteLine(lines[0]);
            Check(capture, typeof(RewardRuntimeLog), "reward.failed", LogEventLevel.Error, "io",
                ("rewardDomain", "experience"), ("rewardOperation", "grant"));
            capture.Output.WriteLine(lines[1]);
        }
        else
        {
            Assert.True(capture.Sink.Events.TryDequeue(out var first));
            Assert.True(capture.Sink.Events.TryDequeue(out var second));
            capture.Sink.Events.Enqueue(first!);
            Check(capture, typeof(RewardRuntimeLog), "reward.failed", LogEventLevel.Error, "io",
                ("rewardDomain", "experience"), ("rewardOperation", "grant"));
            capture.Sink.Events.Enqueue(second!);
        }
        Check(capture, typeof(RewardRuntimeLog), "reward.batch_completed", LogEventLevel.Information, null,
            ("rewardDomain", "experience"), ("processedCount", 0), ("rejectedCount", 1), ("failedCount", 0), ("outcome", "failed"));
    }

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public async Task FinalOutput_ShouldKeepRetryRecoveryConflictAndFailureResults(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var global = new ApplicationLogScope(capture.Logger);
        using var scope = Pollute();
        using var activity = new Activity("reward-results").SetIdFormat(ActivityIdFormat.W3C).Start();
        using var f = new Fixture();
        f.Balances.SetupSequence(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<UserBalance, UserBalance>>>(), It.IsAny<Expression<Func<UserBalance, bool>>>())).ReturnsAsync(0).ReturnsAsync(1);
        Assert.True((await f.Coin.GrantCoinOnceAsync(1, 5, Secret, Secret)).Granted);
        Check(capture, typeof(RewardRuntimeLog), "reward.retrying", LogEventLevel.Warning, null,
            ("rewardDomain", "coin"), ("attempt", 1), ("delayMs", 100));
        f.Experiences.SetupSequence(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<UserExperience, UserExperience>>>(), It.IsAny<Expression<Func<UserExperience, bool>>>())).ReturnsAsync(0).ReturnsAsync(1);
        Assert.True((await f.Experience.GrantExperienceOnceAsync(1, 5, Secret, Secret)).Granted);
        Check(capture, typeof(RewardRuntimeLog), "reward.retrying", LogEventLevel.Warning, null,
            ("rewardDomain", "experience"), ("attempt", 1), ("delayMs", (0, 100)));
        f.ExpTransactions.SetupSequence(r => r.QueryFirstAsync(It.IsAny<Expression<Func<ExpTransaction, bool>>>()))
            .ThrowsAsync(new InvalidOperationException("UNIQUE constraint failed " + Secret)).ReturnsAsync((ExpTransaction?)null);
        Assert.Equal("奖励业务键冲突", (await f.Experience.GrantExperienceOnceAsync(1, 5, Secret, Secret)).Reason);
        Check(capture, typeof(RewardRuntimeLog), "reward.conflict", LogEventLevel.Warning, "invalid-operation",
            ("rewardDomain", "experience"), ("rewardOperation", "grant-once"));
        f.ExpTransactions.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<ExpTransaction, bool>>>())).ReturnsAsync((ExpTransaction?)null);
        f.Experiences.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserExperience, bool>>>())).ThrowsAsync(new IOException(Secret));
        Assert.False(await f.Experience.GrantExperienceAsync(1, 5, Secret));
        Check(capture, typeof(RewardRuntimeLog), "reward.failed", LogEventLevel.Error, "io",
            ("rewardDomain", "experience"), ("rewardOperation", "grant"));
        Assert.True((await f.Experience.GrantExperienceOnceAsync(1, 5, Secret, Secret)).Skipped);
        Check(capture, typeof(RewardRuntimeLog), "reward.failed", LogEventLevel.Error, "io",
            ("rewardDomain", "experience"), ("rewardOperation", "grant-once"));

        using var init = new Fixture();
        init.Experiences.SetupSequence(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserExperience, bool>>>()))
            .ReturnsAsync((UserExperience?)null).ReturnsAsync((UserExperience?)null).ReturnsAsync((UserExperience?)null).ReturnsAsync((UserExperience?)null);
        init.Experiences.Setup(r => r.AddAsync(It.IsAny<UserExperience>())).ThrowsAsync(new IOException(Secret));
        Assert.False(await init.Experience.GrantExperienceAsync(1, 5, Secret));
        Check(capture, typeof(RewardRuntimeLog), "reward.failed", LogEventLevel.Error, "io",
            ("rewardDomain", "experience"), ("rewardOperation", "initialize"));
    }

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public async Task FinalOutput_ShouldPreserveOutputFailureOwnership(bool candidate, string environment, bool audit)
    {
        using var writer = new HangfireBrokenWriter();
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit, output: writer);
        using var global = new ApplicationLogScope(capture.Logger);
        using var scope = Pollute();
        var sinkFailure = new IOException(Secret);
        capture.Sink.EmitFailure = sinkFailure;
        using var f = new Fixture();
        var batch = new System.Collections.Generic.List<CoinGrantInfo>
        { new() { UserId = 1, Amount = -1, TransactionType = Secret }, new() { UserId = 1, Amount = 5, TransactionType = Secret } };
        if (audit)
        {
            var caught = await Assert.ThrowsAsync<AggregateException>(() => f.Coin.BatchGrantCoinAsync(batch));
            Assert.Same(sinkFailure, Assert.Single(caught.Flatten().InnerExceptions));
        }
        else Assert.Single(await f.Coin.BatchGrantCoinAsync(batch));
        f.CoinTransactions.Verify(r => r.AddAsync(It.IsAny<CoinTransaction>()), Times.Once);
        f.Experiences.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserExperience, bool>>>())).ThrowsAsync(new IOException("repository"));
        if (audit)
        {
            var caught = await Assert.ThrowsAsync<AggregateException>(() => f.Experience.GrantExperienceAsync(1, 5, Secret));
            Assert.Same(sinkFailure, Assert.Single(caught.Flatten().InnerExceptions));
        }
        else Assert.False(await f.Experience.GrantExperienceAsync(1, 5, Secret));
        f.Cache.Setup(c => c.GetAsync<System.Collections.Generic.List<LevelConfig>>(It.IsAny<string>())).ThrowsAsync(new IOException("cache"));
        if (audit)
        {
            var caught = await Assert.ThrowsAsync<AggregateException>(() => f.Experience.GetLevelConfigsAsync());
            Assert.Same(sinkFailure, Assert.Single(caught.Flatten().InnerExceptions));
            f.Levels.Verify(r => r.QueryAsync(It.IsAny<Expression<Func<LevelConfig, bool>>>()), Times.Never);
        }
        else
        {
            Assert.Empty(await f.Experience.GetLevelConfigsAsync());
            f.Levels.Verify(r => r.QueryAsync(It.IsAny<Expression<Func<LevelConfig, bool>>>()), Times.Once);
        }
        Assert.Empty(capture.Events);
        if (candidate)
        {
            using var json = JsonDocument.Parse(capture.Emergency.ToString());
            Assert.Equal("pipeline.output_failed", json.RootElement.GetProperty("eventCode").GetString());
            Assert.DoesNotContain(Secret, capture.Emergency.ToString());
        }
    }
}
