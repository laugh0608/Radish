using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Api.Services;
using Radish.Common.OptionTool;
using Radish.Common.TimeTool;
using Radish.Extension.Log;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Repository;
using Radish.Service;
using Serilog;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class CoinRewardEntryLoggingTests
{
    private const string Secret = "REWARD_ENTRY_PRIVATE_SENTINEL";
    private static readonly DateTime RewardDate = new(2026, 9, 28);
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };
    public static IEnumerable<object[]> TaskModes =>
        from candidate in new[] { false, true }
        from environment in new[] { "Production", "Development" }
        from kind in new[] { "post-like", "comment-like", "comment", "reply", "god-comment", "sofa" }
        select new object[] { candidate, environment, kind };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Entries_ShouldKeepAmountsKeysAndReplayQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        Assert.Equal(3, (await f.Rewards.GrantLikeRewardAsync(7, 1, 2)).Amount);
        Assert.Equal(3, (await f.Rewards.GrantCommentLikeRewardAsync(7, 1, 2)).Amount);
        Assert.Equal(1, (await f.Rewards.GrantCommentRewardAsync(7, 1, 9)).Amount);
        Assert.Equal(1, (await f.Rewards.GrantCommentReplyRewardAsync(7, 1, 8)).Amount);
        Assert.Equal(23, (await f.Rewards.GrantGodCommentRewardAsync(7, 1, 3)).Amount);
        Assert.Equal(14, (await f.Rewards.GrantSofaRewardAsync(7, 1, 3)).Amount);
        Assert.Equal(new[]
        {
            "coin:post-like:author:1:post:7:day:20260928",
            "coin:post-like:giver:2:post:7:day:20260928",
            "coin:comment-like:author:1:comment:7:day:20260928",
            "coin:comment-like:giver:2:comment:7:day:20260928",
            "coin:comment-create:author:1:comment:7",
            "coin:comment-reply:author:1:comment:7:day:20260928",
            "coin:highlight-base:god-comment:author:1:comment:7",
            "coin:highlight-base:sofa:author:1:comment:7"
        }, f.Grants.Select(g => g.Key));
        Assert.Equal(new long[] { 2, 1, 2, 1, 1, 1, 23, 14 }, f.Grants.Select(g => g.Amount));
        Assert.All(f.Grants, g => Assert.False(string.IsNullOrEmpty(g.Remark)));
        f.Replay = true;
        Assert.Equal("今日已发放过点赞奖励", (await f.Rewards.GrantLikeRewardAsync(7, 1, 2)).FailureReason);
        Assert.Equal("今日已发放过评论点赞奖励", (await f.Rewards.GrantCommentLikeRewardAsync(7, 1, 2)).FailureReason);
        Assert.Equal("评论奖励已发放过", (await f.Rewards.GrantCommentRewardAsync(7, 1, 9)).FailureReason);
        Assert.Equal("今日已发放过评论被回复奖励", (await f.Rewards.GrantCommentReplyRewardAsync(7, 1, 8)).FailureReason);
        Assert.Equal("该评论已发放过神评奖励", (await f.Rewards.GrantGodCommentRewardAsync(7, 1, 3)).FailureReason);
        Assert.Equal("该评论已发放过沙发奖励", (await f.Rewards.GrantSofaRewardAsync(7, 1, 3)).FailureReason);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task LikeLimit_ShouldKeepBusinessDateAndSkipOnlyGiverAtFifty(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        Expression<Func<CoinTransaction, bool>>? predicate = null;
        f.Transactions.Setup(r => r.QuerySumAsync(It.IsAny<Expression<Func<CoinTransaction, long>>>(), It.IsAny<Expression<Func<CoinTransaction, bool>>>()))
            .Callback((Expression<Func<CoinTransaction, long>> _, Expression<Func<CoinTransaction, bool>> p) => predicate = p)
            .ReturnsAsync(50);
        Assert.Equal(2, (await f.Rewards.GrantLikeRewardAsync(7, 1, 2, RewardDate)).Amount);
        Assert.Equal(2, (await f.Rewards.GrantCommentLikeRewardAsync(7, 1, 2, RewardDate)).Amount);
        Assert.Equal(2, f.Grants.Count);
        Assert.All(f.Grants, g => Assert.Equal(1, g.UserId));
        Assert.NotNull(predicate);
        var item = new CoinTransaction { ToUserId = 2, TransactionType = "LIKE_REWARD", BusinessType = "POST_LIKE_ACTION", Status = "SUCCESS", CreateTime = new DateTime(2026, 9, 27, 16, 0, 0, DateTimeKind.Utc) };
        Assert.True(predicate.Compile()(item));
        item.CreateTime = item.CreateTime.AddTicks(-1);
        Assert.False(predicate.Compile()(item));
        item.CreateTime = new DateTime(2026, 9, 28, 16, 0, 0, DateTimeKind.Utc);
        Assert.False(predicate.Compile()(item));
        f.Transactions.Setup(r => r.QuerySumAsync(It.IsAny<Expression<Func<CoinTransaction, long>>>(), It.IsAny<Expression<Func<CoinTransaction, bool>>>())).ReturnsAsync(49);
        Assert.Equal(3, (await f.Rewards.GrantLikeRewardAsync(7, 1, 2, RewardDate)).Amount);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Likes_ShouldKeepPartialGrantResultAndPropagateOriginalFailure(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        f.Coin.Setup(s => s.GrantCoinOnceAsync(1, 2, "LIKE_REWARD", It.IsAny<string>(), It.IsAny<string?>(), 7, It.IsAny<string?>()))
            .ReturnsAsync(CoinGrantOnceResult.Existing(Secret + "_AUTHOR"));
        var result = await f.Rewards.GrantLikeRewardAsync(7, 1, 2, RewardDate);
        Assert.Equal(1, result.Amount);
        Assert.Equal(Secret, result.TransactionNo);
        var failure = new IOException(Secret);
        f.FailBusinessType = "POST_LIKE_ACTION";
        f.Failure = failure;
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => f.Rewards.GrantLikeRewardAsync(7, 1, 2, RewardDate)));
        f.FailBusinessType = null;
        f.Transactions.Setup(r => r.QuerySumAsync(It.IsAny<Expression<Func<CoinTransaction, long>>>(), It.IsAny<Expression<Func<CoinTransaction, bool>>>())).ThrowsAsync(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => f.Rewards.CheckDailyLikeRewardLimitAsync(2)));
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ExistenceCheck_ShouldKeepFilteringAndFailClosedWithOneSafeError(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        var row = new CoinTransaction { BusinessType = Secret, BusinessId = 7, ToUserId = 1, Status = "SUCCESS", CreateTime = RewardDate };
        f.Transactions.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<CoinTransaction, bool>>>()))
            .ReturnsAsync((Expression<Func<CoinTransaction, bool>> p) => new[] { row }.Where(p.Compile()).ToList());
        Assert.True(await f.Rewards.CheckRewardExistsAsync(Secret, 7, 1));
        Assert.True(await f.Rewards.CheckRewardExistsAsync(Secret, 7, 1, RewardDate));
        Assert.False(await f.Rewards.CheckRewardExistsAsync(Secret, 7, 1, RewardDate.AddDays(-1)));
        Assert.False(await f.Rewards.CheckRewardExistsAsync(Secret, 8, 1));
        capture.AssertQuiet();
        f.Transactions.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<CoinTransaction, bool>>>())).ThrowsAsync(new IOException(Secret));
        Assert.True(await f.Rewards.CheckRewardExistsAsync(Secret, 7, 1, RewardDate));
        capture.AssertSingle("reward.existence_check_failed", "Error");
        f.Coin.Verify(s => s.GrantCoinOnceAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<long?>(), It.IsAny<string?>()), Times.Never);
    }

    [Theory]
    [MemberData(nameof(TaskModes))]
    public async Task Outbox_ShouldOwnRetryAndDeadLetterForActualRewardEntry(bool candidate, string environment, string kind)
    {
        using var db = CreateDatabase();
        var repository = new ReliableOutboxRepository(db);
        var outbox = new ReliableOutboxService(repository);
        var f = new Fixture();
        var (taskType, payload, failingBusinessType) = CreateTask(kind);
        f.FailBusinessType = failingBusinessType;
        var now = DateTime.UtcNow;
        var id = await repository.AddAsync(new ReliableOutboxDraft("main", 0, taskType, 1, Secret, "Comment", Secret,
            System.Text.Json.JsonSerializer.Serialize(payload), now, MaxAttempts: 2));
        await repository.ClaimDueAsync("main", 1, Secret, now, TimeSpan.FromMinutes(5));
        var processor = CreateProcessor(f);
        var job = new ReliableOutboxExecutionJob(outbox, processor, Mock.Of<IContentModerationCaseRepository>());
        using var capture = new Capture(candidate, environment);
        var snapshot = (await repository.QueryByIdAsync("main", id))!;
        Assert.Same(f.Failure, await Assert.ThrowsAsync<IOException>(() => processor.ProcessAsync(snapshot, TestContext.Current.CancellationToken)));
        capture.AssertQuiet();
        await job.ExecuteAsync("main", id, TestContext.Current.CancellationToken);
        Assert.Equal(ReliableOutboxStatuses.Pending, (await repository.QueryByIdAsync("main", id))!.Status);
        capture.AssertSingle("outbox.retrying", "Warning");
        await job.ExecuteAsync("main", id, TestContext.Current.CancellationToken);
        capture.AssertSingle("outbox.retrying", "Warning");
        capture.Clear();
        await repository.ClaimDueAsync("main", 1, Secret, now.AddHours(1), TimeSpan.FromMinutes(5));
        await job.ExecuteAsync("main", id, TestContext.Current.CancellationToken);
        var final = (await repository.QueryByIdAsync("main", id))!;
        Assert.Equal(ReliableOutboxStatuses.DeadLetter, final.Status);
        Assert.Equal(2, final.AttemptCount);
        capture.AssertSingle("outbox.dead_letter", "Error");
    }

    [Theory]
    [MemberData(nameof(TaskModes))]
    public async Task Outbox_ShouldCompleteReplayedRewardQuietly(bool candidate, string environment, string kind)
    {
        using var db = CreateDatabase();
        var repository = new ReliableOutboxRepository(db);
        var outbox = new ReliableOutboxService(repository);
        var f = new Fixture { Replay = true };
        var (taskType, payload, _) = CreateTask(kind);
        var now = DateTime.UtcNow;
        var id = await outbox.AddAsync("main", 0, taskType, Secret, "Comment", Secret, payload, now);
        await repository.ClaimDueAsync("main", 1, Secret, now, TimeSpan.FromMinutes(5));
        var job = new ReliableOutboxExecutionJob(outbox, CreateProcessor(f), Mock.Of<IContentModerationCaseRepository>());
        using var capture = new Capture(candidate, environment);
        await job.ExecuteAsync("main", id, TestContext.Current.CancellationToken);
        Assert.Equal(ReliableOutboxStatuses.Succeeded, (await repository.QueryByIdAsync("main", id))!.Status);
        Assert.NotEmpty(f.Grants);
        capture.AssertQuiet();
    }

    private static (string taskType, object payload, string businessType) CreateTask(string kind) => kind switch
    {
        "post-like" => (ReliableTaskTypes.PostLiked, new LikeEffectsTaskPayload(10, 7, 9, 1, 2, Secret, Secret, Secret, "20260928", Secret), "POST_LIKE"),
        "comment-like" => (ReliableTaskTypes.CommentLiked, new LikeEffectsTaskPayload(10, 7, 9, 1, 2, Secret, Secret, Secret, "20260928", Secret), "COMMENT_LIKE"),
        "comment" => (ReliableTaskTypes.CommentPublished, new CommentPublishedTaskPayload(7, 9, 1, Secret, Secret, "20260928", null, null, null, 10, 11), "COMMENT_POST"),
        "reply" => (ReliableTaskTypes.CommentPublished, new CommentPublishedTaskPayload(7, 9, 1, Secret, Secret, "20260928", 8, 8, 2, 10, 11), "COMMENT_REPLY"),
        "god-comment" => (ReliableTaskTypes.HighlightBaseReward, new HighlightBaseRewardTaskPayload(10, 7, 1, 1, 3), "GOD_COMMENT"),
        "sofa" => (ReliableTaskTypes.HighlightBaseReward, new HighlightBaseRewardTaskPayload(10, 7, 1, 2, 3), "SOFA"),
        _ => throw new ArgumentException(nameof(kind))
    };

    private static ReliableTaskProcessor CreateProcessor(Fixture f)
    {
        var comments = new Mock<IBaseRepository<Comment>>();
        comments.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<Comment, bool>>>())).ReturnsAsync([]);
        return new ReliableTaskProcessor(f.Rewards, f.Coin.Object, Mock.Of<IExperienceService>(), Mock.Of<INotificationService>(),
            Mock.Of<IChatAttachmentBindingService>(), Mock.Of<IBaseRepository<Post>>(), comments.Object);
    }

    private static SqlSugarScope CreateDatabase()
    {
        var db = new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "main", ConnectionString = "Data Source=:memory:", DbType = DbType.Sqlite,
            IsAutoCloseConnection = false, InitKeyType = InitKeyType.Attribute
        });
        db.CodeFirst.InitTables<ReliableOutboxMessage>();
        return db;
    }

    private sealed class Fixture
    {
        public Mock<ICoinService> Coin { get; } = new();
        public Mock<IBaseRepository<CoinTransaction>> Transactions { get; } = new();
        public List<(long UserId, long Amount, string Key, string? Remark)> Grants { get; } = [];
        public bool Replay { get; set; }
        public string? FailBusinessType { get; set; }
        public IOException Failure { get; set; } = new(Secret);
        public CoinRewardService Rewards { get; }
        public Fixture()
        {
            Coin.Setup(s => s.GrantCoinOnceAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<long?>(), It.IsAny<string?>()))
                .Returns((long user, long amount, string type, string key, string? business, long? id, string? remark) =>
                {
                    Grants.Add((user, amount, key, remark));
                    return business == FailBusinessType
                        ? Task.FromException<CoinGrantOnceResult>(Failure)
                        : Task.FromResult(Replay ? CoinGrantOnceResult.Existing(Secret) : CoinGrantOnceResult.NewGrant(Secret));
                });
            var clock = new FixedClock();
            Rewards = new CoinRewardService(Coin.Object, Transactions.Object,
                new BusinessCalendar(clock, Options.Create(new TimeOptions { DefaultTimeZoneId = "Asia/Shanghai" })));
        }
    }
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 27, 16, 30, 0, TimeSpan.Zero);
    }
    private sealed class Capture : IDisposable
    {
        private readonly Serilog.ILogger _previous = Log.Logger;
        public StringWriter Output { get; } = new();
        private readonly Serilog.Core.Logger _logger;
        public Capture(bool candidate, string environment)
        {
            var config = new LoggerConfiguration().MinimumLevel.Verbose();
            if (candidate) RuntimeLoggingConfiguration.Configure(config, new ConfigurationBuilder().Build(), environment, "api", Output, Output);
            else config.Enrich.FromLogContext().WriteTo.Sink(new LegacySink(Output));
            _logger = config.CreateLogger(); Log.Logger = _logger;
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
        public void Dispose() { Log.Logger = _previous; _logger.Dispose(); Output.Dispose(); }
    }
    private sealed class LegacySink(TextWriter output) : Serilog.Core.ILogEventSink
    {
        private readonly Serilog.Formatting.Display.MessageTemplateTextFormatter _formatter = new("{Level} {Message:lj} {Properties:j} {Exception}{NewLine}");
        public void Emit(Serilog.Events.LogEvent value) => _formatter.Format(value, output);
    }
}
