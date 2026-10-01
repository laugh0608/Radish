using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Common.OptionTool;
using Radish.Common.TimeTool;
using Radish.Extension.Log;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Service;
using Radish.Service.Jobs;
using Serilog;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class BusinessJobLoggingTests
{
    private const string Secret = "BUSINESS_JOB_PRIVATE_SENTINEL";
    private static readonly DateTime Now = new(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Shop_ShouldAggregateConsumedFailures_AndPreserveFalseReturnCounting(bool candidate)
    {
        using var capture = new Capture(candidate);
        var orders = new Mock<IBaseRepository<Order>>();
        orders.Setup(r => r.QueryDistinctAsync(It.IsAny<Expression<Func<Order, long>>>(), It.IsAny<Expression<Func<Order, bool>>>()))
            .ReturnsAsync([1, 2, 3, 4]);
        var service = new Mock<IOrderService>();
        service.Setup(s => s.CancelOrderBySystemAsync(1, It.IsAny<string>())).ReturnsAsync(true);
        service.Setup(s => s.CancelOrderBySystemAsync(2, It.IsAny<string>())).ReturnsAsync(false);
        service.Setup(s => s.CancelOrderBySystemAsync(3, It.IsAny<string>())).ThrowsAsync(new InvalidOperationException(Secret));
        service.Setup(s => s.CancelOrderBySystemAsync(4, It.IsAny<string>())).ThrowsAsync(new IOException(Secret));
        Assert.Equal(2, await Shop(orders.Object, service.Object).CancelTimeoutOrdersAsync(0));
        service.Verify(s => s.CancelOrderBySystemAsync(It.IsAny<long>(), "订单超时自动取消（超过 30 分钟未支付）"), Times.Exactly(4));
        capture.AssertSingle("job.batch.failed");
        if (candidate)
        {
            using var value = JsonDocument.Parse(capture.Output.ToString());
            var properties = value.RootElement.GetProperty("properties");
            Assert.Equal(2, properties.GetProperty("processedCount").GetInt32());
            Assert.Equal(1, properties.GetProperty("updatedCount").GetInt32());
            Assert.Equal(1, properties.GetProperty("rejectedCount").GetInt32());
            Assert.Equal(1, properties.GetProperty("failedCount").GetInt32());
        }
    }

    [Fact]
    public async Task Shop_ShouldReleaseLockAfterFailure_AndKeepEmptyRunsQuiet()
    {
        using var capture = new Capture(true);
        var orders = new Mock<IBaseRepository<Order>>();
        orders.SetupSequence(r => r.QueryDistinctAsync(It.IsAny<Expression<Func<Order, long>>>(), It.IsAny<Expression<Func<Order, bool>>>()))
            .ThrowsAsync(new IOException(Secret)).ReturnsAsync([]);
        orders.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<Order, bool>>>())).ReturnsAsync([]);
        var benefits = new Mock<IUserBenefitService>();
        benefits.Setup(s => s.GetDueBenefitIdsAsync(100)).ReturnsAsync([]);
        var job = Shop(orders.Object, Mock.Of<IOrderService>(), benefits.Object);
        Assert.Equal(0, await job.CancelTimeoutOrdersAsync());
        capture.AssertSingle("job.batch.failed");
        capture.Clear();
        Assert.Equal(0, await job.CancelTimeoutOrdersAsync());
        Assert.Equal(0, await job.MarkExpiredBenefitsAsync());
        Assert.Equal(0, (await job.GenerateDailyStatsAsync()).TotalOrders);
        Assert.Equal("", capture.Output.ToString());
        orders.Verify(r => r.QueryDistinctAsync(It.IsAny<Expression<Func<Order, long>>>(), It.IsAny<Expression<Func<Order, bool>>>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Shop_ShouldContinueBenefitsAfterFailure_AndRetainStatsFailureDefault()
    {
        using var capture = new Capture(true);
        var benefits = new Mock<IUserBenefitService>();
        benefits.Setup(s => s.GetDueBenefitIdsAsync(100)).ReturnsAsync([1, 2, 3]);
        benefits.Setup(s => s.ExpireBenefitAsync(1)).ThrowsAsync(new IOException(Secret));
        benefits.Setup(s => s.ExpireBenefitAsync(2)).ReturnsAsync(false);
        benefits.Setup(s => s.ExpireBenefitAsync(3)).ReturnsAsync(true);
        var orders = new Mock<IBaseRepository<Order>>();
        orders.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<Order, bool>>>())).ThrowsAsync(new IOException(Secret));
        var job = Shop(orders.Object, Mock.Of<IOrderService>(), benefits.Object);
        Assert.Equal(1, await job.MarkExpiredBenefitsAsync());
        benefits.VerifyAll();
        capture.AssertSingle("job.batch.failed");
        capture.Clear();
        var stats = await job.GenerateDailyStatsAsync();
        Assert.Equal(Now.Date, stats.Date);
        Assert.Equal(0, stats.TotalRevenue);
        capture.AssertSingle("job.batch.failed");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lottery_ShouldDeduplicatePosts_ClampBatch_AndConsumeItemFailure(bool candidate)
    {
        using var capture = new Capture(candidate);
        using var factory = LoggerFactory.Create(b => b.AddSerilog(capture.Logger));
        var repository = new Mock<IBaseRepository<PostLottery>>();
        repository.Setup(r => r.QueryPageAsync(It.IsAny<Expression<Func<PostLottery, bool>>>(), 1, 100,
            It.IsAny<Expression<Func<PostLottery, object>>>(), OrderByType.Asc))
            .ReturnsAsync((new List<PostLottery> { new() { PostId = 1 }, new() { PostId = 1 }, new() { PostId = 2 } }, 3));
        var service = new Mock<IPostLotteryService>();
        service.Setup(s => s.AutoDrawByPostIdAsync(1)).ThrowsAsync(new IOException(Secret));
        var job = new PostLotteryJob(repository.Object, service.Object, factory.CreateLogger<PostLotteryJob>());
        Assert.Equal(1, await job.ExecuteAutoDrawAsync(999));
        service.Verify(s => s.AutoDrawByPostIdAsync(1), Times.Once);
        service.Verify(s => s.AutoDrawByPostIdAsync(2), Times.Once);
        repository.VerifyAll();
        capture.AssertSingle("job.batch.failed");
    }

    [Fact]
    public async Task Lottery_ShouldPropagateScanFailure_WithoutLocalError()
    {
        using var capture = new Capture(true);
        using var factory = LoggerFactory.Create(b => b.AddSerilog(capture.Logger));
        var repository = new Mock<IBaseRepository<PostLottery>>();
        var failure = new IOException(Secret);
        repository.Setup(r => r.QueryPageAsync(It.IsAny<Expression<Func<PostLottery, bool>>>(), 1, 1,
            It.IsAny<Expression<Func<PostLottery, object>>>(), OrderByType.Asc)).ThrowsAsync(failure);
        var job = new PostLotteryJob(repository.Object, Mock.Of<IPostLotteryService>(), factory.CreateLogger<PostLotteryJob>());
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => job.ExecuteAutoDrawAsync(0)));
        Assert.Equal("", capture.Output.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retention_ShouldKeepThreeWeekCap_ContinueAfterFailure_AndOmitReasons(bool candidate)
    {
        using var capture = new Capture(candidate);
        var repository = new Mock<IBaseRepository<CommentHighlight>>();
        repository.SetupSequence(r => r.QueryAsync(It.IsAny<Expression<Func<CommentHighlight, bool>>>()))
            .ReturnsAsync([new CommentHighlight { Id = 1, AuthorId = 11, CreateTime = DateTime.Now.AddDays(-40) }])
            .ReturnsAsync([new CommentHighlight { Id = 2, AuthorId = 22, CreateTime = DateTime.Now.AddDays(-10) }]);
        var rewards = new Mock<ICoinRewardService>(MockBehavior.Strict);
        rewards.Setup(s => s.GrantRetentionRewardAsync(1, 11, 1, "GodComment")).ReturnsAsync(CoinRewardResult.Success(Secret, 15));
        rewards.Setup(s => s.GrantRetentionRewardAsync(1, 11, 2, "GodComment")).ThrowsAsync(new IOException(Secret));
        rewards.Setup(s => s.GrantRetentionRewardAsync(1, 11, 3, "GodComment")).ReturnsAsync(CoinRewardResult.Failure(Secret));
        rewards.Setup(s => s.GrantRetentionRewardAsync(2, 22, 1, "Sofa")).ReturnsAsync(CoinRewardResult.Success(Secret, 10));
        Assert.Equal((1, 1), await new RetentionRewardJob(repository.Object, rewards.Object).ExecuteAsync());
        rewards.VerifyAll();
        Assert.Equal(4, rewards.Invocations.Count);
        capture.AssertSingle("job.batch.failed");
        if (candidate)
        {
            using var value = JsonDocument.Parse(capture.Output.ToString());
            Assert.Equal(2, value.RootElement.GetProperty("properties").GetProperty("rewardCount").GetInt32());
            Assert.Equal(1, value.RootElement.GetProperty("properties").GetProperty("rejectedCount").GetInt32());
        }
    }

    [Fact]
    public async Task Retention_ShouldKeepDuplicateRewardQuiet_AndContinueAfterPhaseQueryFailure()
    {
        using var capture = new Capture(true);
        var repository = new Mock<IBaseRepository<CommentHighlight>>();
        repository.SetupSequence(r => r.QueryAsync(It.IsAny<Expression<Func<CommentHighlight, bool>>>()))
            .ReturnsAsync([new CommentHighlight { CreateTime = DateTime.Now.AddDays(-10) }]).ReturnsAsync([])
            .ThrowsAsync(new IOException(Secret)).ReturnsAsync([]);
        var rewards = new Mock<ICoinRewardService>();
        rewards.Setup(s => s.GrantRetentionRewardAsync(0, 0, 1, "GodComment"))
            .ReturnsAsync(CoinRewardResult.Failure("已发放过" + Secret));
        var job = new RetentionRewardJob(repository.Object, rewards.Object);
        Assert.Equal((0, 0), await job.ExecuteAsync());
        Assert.Equal("", capture.Output.ToString());
        Assert.Equal((0, 0), await job.ExecuteAsync());
        repository.Verify(r => r.QueryAsync(It.IsAny<Expression<Func<CommentHighlight, bool>>>()), Times.Exactly(4));
        capture.AssertSingle("job.batch.failed");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Highlight_ShouldPreserveSnapshotsAndRewardKeys_AndAggregateRejection(bool candidate, bool sofa)
    {
        using var capture = new Capture(candidate);
        var comments = CommentsWithCandidate(sofa);
        var highlights = new Mock<IBaseRepository<CommentHighlight>>();
        var experience = new Mock<IExperienceService>(MockBehavior.Strict);
        experience.Setup(s => s.GrantExperienceOnceAsync(11, sofa ? 30 : 50, sofa ? "SOFA_COMMENT" : "GOD_COMMENT",
            sofa ? "exp:highlight-base:sofa:author:11:comment:1" : "exp:highlight-base:god-comment:author:11:comment:1",
            "Comment", 1, sofa ? "评论成为沙发" : "评论成为神评"))
            .ReturnsAsync(new ExperienceGrantOnceResult());
        List<CommentHighlight>? saved = null;
        highlights.Setup(r => r.AddRangeAsync(It.IsAny<List<CommentHighlight>>()))
            .Callback<List<CommentHighlight>>(items => saved = items);
        var job = Highlight(comments.Object, highlights.Object, experience.Object);
        Assert.Equal(sofa ? (0, 1) : (1, 0), await job.ExecuteAsync());
        Assert.NotNull(saved);
        Assert.Equal(Secret, Assert.Single(saved).ContentSnapshot);
        Assert.Equal(Now.Date.AddDays(-1), saved[0].StatDate);
        experience.VerifyAll();
        capture.AssertSingle("job.batch.warning");
    }

    [Fact]
    public async Task Highlight_ShouldPropagateRewardException_WithoutLocalErrorOrInsert()
    {
        using var capture = new Capture(true);
        var comments = CommentsWithCandidate();
        var highlights = new Mock<IBaseRepository<CommentHighlight>>();
        var experience = new Mock<IExperienceService>();
        var failure = new IOException(Secret);
        experience.Setup(s => s.GrantExperienceOnceAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<string>())).ThrowsAsync(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => Highlight(comments.Object, highlights.Object, experience.Object).ExecuteAsync()));
        highlights.Verify(r => r.AddRangeAsync(It.IsAny<List<CommentHighlight>>()), Times.Never);
        Assert.Equal("", capture.Output.ToString());
    }

    [Fact]
    public async Task Highlight_ShouldReportRetiredCurrentRows_EvenWithoutNewHighlights()
    {
        using var capture = new Capture(true);
        var comments = CommentsWithCandidate();
        comments.Setup(r => r.QueryCountAsync(It.IsAny<Expression<Func<Comment, bool>>>())).ReturnsAsync(0);
        var highlights = new Mock<IBaseRepository<CommentHighlight>>();
        highlights.Setup(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<CommentHighlight, CommentHighlight>>>(),
            It.IsAny<Expression<Func<CommentHighlight, bool>>>())).ReturnsAsync(2);
        Assert.Equal((0, 0), await Highlight(comments.Object, highlights.Object, Mock.Of<IExperienceService>()).ExecuteAsync());
        capture.AssertSingle("job.batch.completed");
        using var value = JsonDocument.Parse(capture.Output.ToString());
        Assert.Equal(2, value.RootElement.GetProperty("properties").GetProperty("updatedCount").GetInt32());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task RewardService_ShouldLeaveLoggingToCaller_AndKeepGrantContract(bool candidate, bool fails)
    {
        using var capture = new Capture(candidate);
        var coins = new Mock<ICoinService>(MockBehavior.Strict);
        var failure = new IOException(Secret);
        var retention = coins.Setup(s => s.GrantCoinOnceAsync(11, 15, "HIGHLIGHT_REWARD",
            "coin:highlight-retention:god-comment:highlight:1:week:2:author:11", "GodComment_RETENTION_W2", 1, It.IsAny<string>()));
        var bonus = coins.Setup(s => s.GrantCoinOnceAsync(11, 6, "HIGHLIGHT_REWARD",
            "coin:highlight-like-bonus:sofa:highlight:1:to-like:4", "Sofa_LIKE_BONUS", 1, It.IsAny<string>()));
        if (fails)
        {
            retention.ThrowsAsync(failure);
            bonus.ThrowsAsync(failure);
        }
        else
        {
            retention.ReturnsAsync(CoinGrantOnceResult.NewGrant(Secret));
            bonus.ReturnsAsync(CoinGrantOnceResult.Existing(Secret));
        }
        var service = new CoinRewardService(coins.Object, Mock.Of<IBaseRepository<CoinTransaction>>(), Calendar(new FixedClock()));
        if (fails)
        {
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => service.GrantRetentionRewardAsync(1, 11, 2, "GodComment")));
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => service.GrantLikeBonusRewardAsync(1, 11, 2, "Sofa", 4)));
        }
        else
        {
            var reward = await service.GrantRetentionRewardAsync(1, 11, 2, "GodComment");
            Assert.Equal(15, reward.Amount);
            Assert.Equal(Secret, reward.TransactionNo);
            Assert.False((await service.GrantLikeBonusRewardAsync(1, 11, 2, "Sofa", 4)).IsSuccess);
        }
        coins.VerifyAll();
        Assert.Equal("", capture.Output.ToString());
    }

    [Fact]
    public async Task Jobs_ShouldKeepEmptyScansQuiet()
    {
        using var capture = new Capture(true);
        using var factory = LoggerFactory.Create(b => b.AddSerilog(capture.Logger));
        var comments = CommentsWithCandidate();
        comments.Setup(r => r.QueryDistinctAsync(It.IsAny<Expression<Func<Comment, long>>>(), It.IsAny<Expression<Func<Comment, bool>>>())).ReturnsAsync([]);
        Assert.Equal((0, 0), await Highlight(comments.Object, Mock.Of<IBaseRepository<CommentHighlight>>(), Mock.Of<IExperienceService>()).ExecuteAsync());
        var highlights = new Mock<IBaseRepository<CommentHighlight>>();
        highlights.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<CommentHighlight, bool>>>())).ReturnsAsync([]);
        Assert.Equal((0, 0), await new RetentionRewardJob(highlights.Object, Mock.Of<ICoinRewardService>()).ExecuteAsync());
        var lotteries = new Mock<IBaseRepository<PostLottery>>();
        lotteries.Setup(r => r.QueryPageAsync(It.IsAny<Expression<Func<PostLottery, bool>>>(), 1, 20,
            It.IsAny<Expression<Func<PostLottery, object>>>(), OrderByType.Asc)).ReturnsAsync((new List<PostLottery>(), 0));
        Assert.Equal(0, await new PostLotteryJob(lotteries.Object, Mock.Of<IPostLotteryService>(), factory.CreateLogger<PostLotteryJob>()).ExecuteAutoDrawAsync());
        Assert.Equal("", capture.Output.ToString());
    }

    private static Mock<IBaseRepository<Comment>> CommentsWithCandidate(bool sofa = false)
    {
        var comments = new Mock<IBaseRepository<Comment>>();
        comments.Setup(r => r.QueryDistinctAsync(It.IsAny<Expression<Func<Comment, long>>>(), It.IsAny<Expression<Func<Comment, bool>>>())).ReturnsAsync(sofa ? [] : [10]);
        comments.Setup(r => r.QueryDistinctAsync(It.IsAny<Expression<Func<Comment, long?>>>(), It.IsAny<Expression<Func<Comment, bool>>>())).ReturnsAsync(sofa ? [20] : []);
        comments.Setup(r => r.QueryByIdAsync(20)).ReturnsAsync(new Comment { Id = 20, PostId = 10 });
        comments.Setup(r => r.QueryCountAsync(It.IsAny<Expression<Func<Comment, bool>>>())).ReturnsAsync(10);
        comments.Setup(r => r.QueryPageAsync(It.IsAny<Expression<Func<Comment, bool>>>(), 1, 5,
            It.IsAny<Expression<Func<Comment, object>>>(), OrderByType.Desc,
            It.IsAny<Expression<Func<Comment, object>>>(), OrderByType.Desc))
            .ReturnsAsync((new List<Comment> { new() { Id = 1, PostId = 10, AuthorId = 11, Content = Secret, AuthorName = Secret, LikeCount = 4 } }, 1));
        return comments;
    }

    private static CommentHighlightJob Highlight(IBaseRepository<Comment> comments, IBaseRepository<CommentHighlight> highlights, IExperienceService experience)
    {
        var clock = new FixedClock();
        return new CommentHighlightJob(comments, highlights, Mock.Of<ICoinRewardService>(), experience,
            Options.Create(new CommentHighlightOptions()), clock, Calendar(clock));
    }

    private static ShopJob Shop(IBaseRepository<Order> orders, IOrderService service, IUserBenefitService? benefits = null)
    {
        var clock = new FixedClock();
        return new ShopJob(orders, service, benefits ?? Mock.Of<IUserBenefitService>(), clock, Calendar(clock));
    }

    private static BusinessCalendar Calendar(TimeProvider clock) => new(clock, Options.Create(new TimeOptions { DefaultTimeZoneId = "Asia/Shanghai" }));
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Now); }

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
            Logger = config.CreateLogger();
            Log.Logger = Logger;
        }
        public void AssertSingle(string code)
        {
            Assert.Single(Output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries));
            Assert.Contains(code, Output.ToString());
            Assert.DoesNotContain(Secret, Output.ToString());
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
