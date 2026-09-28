using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Api.Controllers.v1;
using Radish.Api.ErrorHandling;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Extension.Log;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Service;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;
using Serilog;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class LeaderboardLoggingTests
{
    private const string Secret = "LEADERBOARD_PRIVATE_SENTINEL";
    private static readonly LeaderboardType[] UserTypes = [LeaderboardType.Experience, LeaderboardType.PostCount, LeaderboardType.CommentCount, LeaderboardType.Popularity];
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task UserBoards_ShouldKeepPagingAssemblyAndCurrentUserQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        foreach (var type in UserTypes)
        {
            var page = (await f.Controller.GetLeaderboard((int)type, 2, 20)).ResponseData!;
            var row = Assert.Single(page.Data);
            Assert.Equal(22, row.VoRank);
            Assert.Equal(7, row.VoUserId);
            Assert.Equal(Secret, row.VoUserName);
            Assert.Equal(Secret, row.VoAvatarUrl);
            Assert.Equal(2, row.VoCurrentLevel);
            Assert.Equal("TestLevel", row.VoCurrentLevelName);
            Assert.Equal(10, row.VoPrimaryValue);
            Assert.True(row.VoIsCurrentUser);
            Assert.Equal(41, page.DataCount);
            Assert.Equal(3, page.PageCount);
            Assert.Equal(2, page.Page);
            Assert.Equal(20, page.PageSize);
        }
        foreach (var (input, expected) in new[] { (0, 50), (101, 100) })
        {
            var page = (await f.Controller.GetLeaderboard((int)LeaderboardType.PostCount, -1, input)).ResponseData!;
            Assert.Equal(1, page.Page);
            Assert.Equal(expected, page.PageSize);
            f.Repository.Verify(r => r.GetPostCountRankingAsync(1, expected), Times.Once);
        }
        f.UserId = 0;
        Assert.False(Assert.Single((await f.Controller.GetLeaderboard((int)LeaderboardType.PostCount)).ResponseData!.Data).VoIsCurrentUser);
        Assert.Equal(5, (await f.Controller.GetTypes()).ResponseData!.Count);
        f.Repository.Verify(r => r.GetExperienceRankingAsync(It.Is<DateTime>(d => d.Kind == DateTimeKind.Local), 2, 20), Times.Once);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ProductsAndRanks_ShouldKeepValuesAndNotRankedQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        var product = Assert.Single((await f.Controller.GetLeaderboard((int)LeaderboardType.HotProduct, 2, 20)).ResponseData!.Data);
        Assert.Equal(21, product.VoRank);
        Assert.Equal(99, product.VoProductId);
        Assert.Equal(Secret, product.VoProductName);
        Assert.Equal(Secret, product.VoProductIcon);
        Assert.Equal(12, product.VoPrimaryValue);
        Assert.Equal(30, product.VoProductPrice);
        foreach (var type in UserTypes)
        {
            Assert.Equal(4, (await f.Controller.GetMyRank((int)type)).ResponseData);
            Assert.Equal(0, await f.Service.GetUserRankAsync(type, 0));
        }
        f.Repository.Verify(r => r.GetUserExperienceRankAsync(7, It.Is<DateTime>(d => d.Kind == DateTimeKind.Local)), Times.Once);
        f.Rank = 0;
        Assert.Equal(0, (await f.Controller.GetMyRank((int)LeaderboardType.PostCount)).ResponseData);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Rejections_ShouldKeepCodesAndAvoidRepository(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        foreach (var type in new[] { LeaderboardType.Balance, LeaderboardType.TotalSpent, LeaderboardType.PurchaseCount, (LeaderboardType)999 })
        {
            Assert.Equal(LeaderboardErrorCodes.TypeUnavailable, (await f.Controller.GetLeaderboard((int)type)).Code);
            Assert.Equal(LeaderboardErrorCodes.TypeUnavailable, (await f.Controller.GetMyRank((int)type)).Code);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => f.Service.GetLeaderboardAsync(type, 1));
        }
        Assert.Equal(LeaderboardErrorCodes.UserRankUnavailable, (await f.Controller.GetMyRank((int)LeaderboardType.HotProduct)).Code);
        f.UserId = 0;
        Assert.False((await f.Controller.GetMyRank()).IsSuccess);
        f.Repository.VerifyNoOtherCalls();
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task QueryFailures_ShouldPropagateAndReachFinalHandlerOnce(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var failure = new IOException(Secret);
        var f = new Fixture { QueryFailure = failure };
        foreach (var type in UserTypes.Append(LeaderboardType.HotProduct))
        {
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => f.Service.GetLeaderboardAsync(type, 1)));
            capture.AssertQuiet();
            using var body = await InvokeApiAsync(() => f.Controller.GetLeaderboard((int)type), capture.Logger, 500);
            capture.AssertSingle("http.failed", "Error");
            capture.Clear();
        }
        foreach (var type in UserTypes)
        {
            using var body = await InvokeApiAsync(() => f.Controller.GetMyRank((int)type), capture.Logger, 500);
            capture.AssertSingle("http.failed", "Error");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task AssemblyFailures_ShouldNotBecomePartialSuccessOrDuplicateLogs(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var stage in new[] { "users", "avatar", "experience", "level", "product-icon" })
        {
            var f = new Fixture();
            var failure = new IOException(Secret);
            switch (stage)
            {
                case "users": f.Repository.Setup(r => r.GetEligibleUsersAsync(It.IsAny<IReadOnlyCollection<long>>())).ThrowsAsync(failure); break;
                case "avatar": f.Attachments.Setup(r => r.GetLatestAvatarAssetMapAsync(It.IsAny<IReadOnlyCollection<long>>())).ThrowsAsync(failure); break;
                case "experience": f.Experience.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<UserExperience, bool>>>())).ThrowsAsync(failure); break;
                case "level": f.Levels.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<LevelConfig, bool>>>())).ThrowsAsync(failure); break;
                case "product-icon": f.Urls.Setup(r => r.ResolveAttachmentUrl(1)).Throws(failure); break;
            }
            using var body = await InvokeApiAsync(() => f.Controller.GetLeaderboard((int)(stage == "product-icon" ? LeaderboardType.HotProduct : LeaderboardType.PostCount)), capture.Logger, 500);
            capture.AssertSingle("http.failed", "Error");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task BusinessFailures_ShouldKeep4xxQuietAnd5xxOwnedByApi(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var status in new[] { 400, 409, 503 })
        {
            var f = new Fixture { QueryFailure = new BusinessException(Secret, status, "Leaderboard.Test", "error.leaderboard.test") };
            foreach (var call in new Func<Task>[] { () => f.Controller.GetLeaderboard((int)LeaderboardType.PostCount), () => f.Controller.GetMyRank((int)LeaderboardType.PostCount) })
            {
                using var body = await InvokeApiAsync(call, capture.Logger, status);
                Assert.Equal("Leaderboard.Test", body.RootElement.GetProperty("code").GetString());
                if (status >= 500) capture.AssertSingle("http.failed", "Error");
                else capture.AssertQuiet();
                capture.Clear();
            }
        }
    }

    private sealed class Fixture
    {
        public Mock<ILeaderboardRepository> Repository { get; } = new();
        public Mock<IBaseRepository<UserExperience>> Experience { get; } = new();
        public Mock<IBaseRepository<LevelConfig>> Levels { get; } = new();
        public Mock<IAttachmentService> Attachments { get; } = new();
        public Mock<IAttachmentUrlResolver> Urls { get; } = new();
        public Exception? QueryFailure { get; init; }
        public int Rank { get; set; } = 4;
        public long UserId { get; set; } = 7;
        public LeaderboardService Service { get; }
        public LeaderboardController Controller { get; }
        public Fixture()
        {
            Repository.Setup(r => r.GetExperienceRankingAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>())).Returns(() => Query((new List<UserLeaderboardMetric> { new(8, 20), new(7, 10) }, 41)));
            Repository.Setup(r => r.GetPostCountRankingAsync(It.IsAny<int>(), It.IsAny<int>())).Returns(() => Query((new List<UserLeaderboardMetric> { new(8, 20), new(7, 10) }, 41)));
            Repository.Setup(r => r.GetCommentCountRankingAsync(It.IsAny<int>(), It.IsAny<int>())).Returns(() => Query((new List<UserLeaderboardMetric> { new(8, 20), new(7, 10) }, 41)));
            Repository.Setup(r => r.GetPopularityRankingAsync(It.IsAny<int>(), It.IsAny<int>())).Returns(() => Query((new List<UserLeaderboardMetric> { new(8, 20), new(7, 10) }, 41)));
            Repository.Setup(r => r.GetHotProductRankingAsync(It.IsAny<int>(), It.IsAny<int>())).Returns(() => Query((new List<Product> { new() { Id = 99, Name = Secret, Price = 30, SoldCount = 12, IconAttachmentId = 1 } }, 41)));
            Repository.Setup(r => r.GetUserExperienceRankAsync(7, It.IsAny<DateTime>())).Returns(() => Query(Rank));
            Repository.Setup(r => r.GetUserPostCountRankAsync(7)).Returns(() => Query(Rank));
            Repository.Setup(r => r.GetUserCommentCountRankAsync(7)).Returns(() => Query(Rank));
            Repository.Setup(r => r.GetUserPopularityRankAsync(7)).Returns(() => Query(Rank));
            Repository.Setup(r => r.GetEligibleUsersAsync(It.IsAny<IReadOnlyCollection<long>>())).ReturnsAsync([new() { UserId = 7, UserName = Secret }]);
            Experience.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<UserExperience, bool>>>())).ReturnsAsync([new() { UserId = 7, CurrentLevel = 2 }]);
            Levels.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<LevelConfig, bool>>>())).ReturnsAsync([new() { Level = 2, LevelName = "TestLevel" }]);
            Attachments.Setup(r => r.GetLatestAvatarAssetMapAsync(It.IsAny<IReadOnlyCollection<long>>())).ReturnsAsync(new Dictionary<long, AttachmentAssetDto> { [7] = new() { Url = Secret } });
            Urls.Setup(r => r.ResolveAttachmentUrl(1)).Returns(Secret);
            Service = new(Experience.Object, Levels.Object, Repository.Object, Attachments.Object, Urls.Object);
            var current = new Mock<ICurrentUserAccessor>();
            current.SetupGet(c => c.Current).Returns(() => new CurrentUser { UserId = UserId, UserName = Secret, Roles = [] });
            Controller = new(Service, current.Object);
        }
        private Task<T> Query<T>(T value) => QueryFailure is null ? Task.FromResult(value) : Task.FromException<T>(QueryFailure);
    }

    private static async Task<JsonDocument> InvokeApiAsync(Func<Task> action, Serilog.ILogger logger, int status)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(logger, dispose: false);
        builder.Services.AddSingleton<ApiExceptionHandler>();
        await using var app = builder.Build();
        app.UseApiExceptionHandler();
        app.Run(async _ => await action());
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/Leaderboard/GetLeaderboard";
        context.Request.QueryString = new QueryString("?query=" + Secret);
        using var output = new MemoryStream();
        context.Response.Body = output;
        context.RequestServices = app.Services;
        await ((IApplicationBuilder)app).Build()(context);
        Assert.Equal(status, context.Response.StatusCode);
        output.Position = 0;
        return await JsonDocument.ParseAsync(output, cancellationToken: TestContext.Current.CancellationToken);
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
