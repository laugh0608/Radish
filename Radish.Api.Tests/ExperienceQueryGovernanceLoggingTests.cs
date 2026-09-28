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
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Radish.Api.Controllers.v1;
using Radish.Api.ErrorHandling;
using Radish.Api.Resources;
using Radish.Common;
using Radish.Common.CacheTool;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Common.OptionTool;
using Radish.Common.TimeTool;
using Radish.Extension.Log;
using Radish.Extension.ExperienceExtension;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service;
using Serilog;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class ExperienceQueryGovernanceLoggingTests
{
    private const string Secret = "EXPERIENCE_QUERY_PRIVATE_SENTINEL";
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Queries_ShouldPreserveResultsAndQuietBusinessReturns(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        Assert.Equal(80, (await f.Service.GetUserExperienceAsync(1))!.VoTotalExp);
        Assert.Equal(80, (await f.Service.GetUserExperiencesAsync([1]))[1].VoTotalExp);
        Assert.Null(await f.Service.GetUserExperienceAsync(0));
        Assert.Equal(Secret, Assert.Single(await f.Service.GetLevelConfigsAsync()).VoLevelName);
        Assert.Equal(Secret, (await f.Service.GetLevelConfigAsync(1))!.VoLevelName);
        Assert.Null(await f.Service.GetLevelConfigAsync(9));
        Assert.Equal((1, 80L), await f.Service.CalculateLevelAsync(80));
        Assert.Equal(7, (await f.Service.GetDailyStatsAsync(1, 0)).VoStats.Count);
        Assert.Equal(30, (await f.Service.GetDailyStatsAsync(1, 99)).VoStats.Count);
        var page = await f.Service.GetTransactionsAsync(1, 0, 0);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(Secret, Assert.Single(page.Data).VoRemark);
        Assert.Equal(4, await f.Service.GetUserRankAsync(1));
        Assert.Equal(0, await f.Service.GetUserRankAsync(0));
        f.Account.ExpFrozen = true;
        Assert.Equal(0, await f.Service.GetUserRankAsync(1));
        await f.Service.UpdateDailyStatsAsync(0, Secret, 1, new DateOnly(2026, 9, 28));
        await f.Service.UpdateDailyStatsAsync(1, Secret, 0, new DateOnly(2026, 9, 28));
        f.Stats.Verify(r => r.AddAsync(It.IsAny<UserExpDailyStats>()), Times.Never);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Queries_ShouldPropagateOriginalFailureAndLetApiLogOnce(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        var failure = new IOException(Secret);
        f.Experiences.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserExperience, bool>>>())).ThrowsAsync(failure);
        f.Experiences.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<UserExperience, bool>>>())).ThrowsAsync(failure);
        f.Experiences.Setup(r => r.QueryPageAsync(It.IsAny<Expression<Func<UserExperience, bool>>>(), 1, 50,
            It.IsAny<Expression<Func<UserExperience, object>>>(), OrderByType.Desc)).ThrowsAsync(failure);
        f.Levels.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<LevelConfig, bool>>>())).ThrowsAsync(failure);
        f.Stats.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<UserExpDailyStats, bool>>>())).ThrowsAsync(failure);
        f.Transactions.Setup(r => r.QueryPageAsync(It.IsAny<Expression<Func<ExpTransaction, bool>>>(), 1, 20,
            It.IsAny<Expression<Func<ExpTransaction, object>>>(), OrderByType.Desc)).ThrowsAsync(failure);
        Func<Task>[] queries =
        [
            () => f.Controller.GetUserExperience(1), () => f.Service.GetUserExperiencesAsync([1]),
            () => f.Controller.GetUserTransactions(1), () => f.Controller.GetUserDailyStats(1),
            () => f.Controller.GetUserGovernanceActions(1), () => f.Controller.GetLevelConfigs(),
            () => f.Controller.GetLevelConfig(1), () => f.Controller.GetLeaderboard()
        ];
        foreach (var query in queries)
        {
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(query));
            capture.AssertQuiet();
            await AssertHttpFailureAsync(query, capture.Logger);
            capture.AssertSingle("http.failed", "Error");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task RankFailure_ShouldRemainZeroAndHaveOneSafeError(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        f.Experiences.Setup(r => r.QueryCountAsync(It.IsAny<Expression<Func<UserExperience, bool>>>())).ThrowsAsync(new IOException(Secret));
        Assert.Equal(0, (await f.Controller.GetMyRank()).ResponseData);
        capture.AssertSingle("experience.rank_query_failed", "Error");
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Leaderboard_ShouldAggregateMissingUsersAndKeepPagination(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        f.Experiences.Setup(r => r.QueryPageAsync(It.IsAny<Expression<Func<UserExperience, bool>>>(), 1, 50,
            It.IsAny<Expression<Func<UserExperience, object>>>(), OrderByType.Desc))
            .ReturnsAsync((new List<UserExperience> { new() { UserId = 2 }, f.Account, new() { UserId = 3 } }, 3));
        var page = await f.Service.GetLeaderboardAsync(0, 0, 1);
        Assert.Equal(3, page.DataCount);
        Assert.Equal(50, page.PageSize);
        var row = Assert.Single(page.Data);
        Assert.Equal(1, row.VoRank);
        Assert.True(row.VoIsCurrentUser);
        Assert.Equal(Secret, row.VoUserName);
        capture.AssertSingle("experience.leaderboard_incomplete", "Warning");
        Assert.Matches("\"skippedCount\"\\s*:\\s*2", capture.Output.ToString());
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task GovernanceSnapshot_ShouldKeepFallbackAndReportSafeWarning(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        f.Governance.Setup(r => r.QueryActionsPageAsync(It.IsAny<ExperienceGovernanceActionPageQuery>()))
            .ReturnsAsync((new List<UserExperienceGovernanceAction> { new() { RuleCodes = "[\"" + Secret, RuleLabels = "[\"safe\"]", Remark = Secret } }, 1));
        var page = await f.Service.GetGovernanceActionsAsync(1, 0, 0);
        Assert.Empty(Assert.Single(page.Data).VoRuleCodes);
        Assert.Equal("safe", Assert.Single(page.Data[0].VoRuleLabels));
        Assert.Equal(Secret, page.Data[0].VoRemark);
        capture.AssertSingle("experience.governance_snapshot_invalid", "Warning");
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Review_ShouldKeepAuditVersionAndReplayQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        var result = await f.Service.RecordGovernanceReviewAsync(ReviewRequest(), 9, Secret);
        Assert.Equal(4, result.VoExperience.VoVersion);
        Assert.Equal(Secret, result.VoAction.VoRemark);
        Assert.Equal(Secret, Assert.Single(result.VoAction.VoRuleCodes));
        f.Governance.Verify(r => r.ApplyGovernanceActionAsync(It.Is<ExperienceGovernanceMutationCommand>(c =>
            c.ExpectedVersion == 3 && c.ActorUserId == 9 && c.ActorName == Secret && c.Action.Remark == Secret)), Times.Once);
        f.Idempotency.Verify(s => s.CompleteSuccessAsync(It.IsAny<OperationIdempotencyCompletionRequest>()), Times.Once);
        f.Idempotency.Setup(s => s.BeginAsync(It.IsAny<OperationIdempotencyBeginRequest>()))
            .ReturnsAsync(new OperationIdempotencyBeginResult { Status = OperationIdempotencyBeginStatus.Succeeded, ResponsePayload = Secret });
        f.Idempotency.Setup(s => s.DeserializeResponse<AdminExperienceGovernanceResultVo>(Secret)).Returns(result);
        Assert.True((await f.Service.RecordGovernanceReviewAsync(ReviewRequest(), 9, Secret)).VoReplayed);
        f.Governance.Verify(r => r.ApplyGovernanceActionAsync(It.IsAny<ExperienceGovernanceMutationCommand>()), Times.Once);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task GovernanceFailure_ShouldReachApiAndConflictsRemainQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        f.Governance.Setup(r => r.ApplyGovernanceActionAsync(It.IsAny<ExperienceGovernanceMutationCommand>()))
            .ThrowsAsync(new ExperienceGovernanceStateConflictException());
        var conflict = await Assert.ThrowsAsync<BusinessException>(() => f.Controller.AdminRecordGovernanceReview(ReviewRequest()));
        Assert.Equal(409, conflict.StatusCode);
        capture.AssertQuiet();
        f.Governance.Setup(r => r.ApplyGovernanceActionAsync(It.IsAny<ExperienceGovernanceMutationCommand>())).ThrowsAsync(new IOException(Secret));
        await AssertHttpFailureAsync(() => f.Controller.AdminRecordGovernanceReview(ReviewRequest()), capture.Logger);
        capture.AssertSingle("http.failed", "Error");
        capture.Clear();
        var preview = await f.Service.PreviewLevelConfigRecalculationAsync();
        var request = new RecalculateLevelConfigsDto { Reason = Secret, ExpectedFingerprint = preview.VoFingerprint };
        f.Governance.Setup(r => r.ApplyLevelRecalculationAsync(It.IsAny<ExperienceLevelRecalculationCommand>()))
            .ThrowsAsync(new ExperienceLevelRecalculationPreviewConflictException());
        Assert.Equal(409, (await Assert.ThrowsAsync<BusinessException>(() => f.Controller.RecalculateLevelConfigs(request))).StatusCode);
        capture.AssertQuiet();
        f.Governance.Setup(r => r.ApplyLevelRecalculationAsync(It.IsAny<ExperienceLevelRecalculationCommand>())).ThrowsAsync(new IOException(Secret));
        await AssertHttpFailureAsync(() => f.Controller.RecalculateLevelConfigs(request), capture.Logger);
        capture.AssertSingle("http.failed", "Error");
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Recalculation_ShouldKeepAuditAndCacheFailureBoundary(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        var preview = await f.Service.PreviewLevelConfigRecalculationAsync();
        var request = new RecalculateLevelConfigsDto { Reason = Secret, ExpectedFingerprint = preview.VoFingerprint };
        var result = await f.Service.RecalculateLevelConfigsAsync(request, 9, Secret);
        Assert.Equal(1, result.VoAudit.VoChangedLevelCount);
        Assert.Equal(Secret, result.VoAudit.VoReason);
        Assert.Equal(preview.VoFingerprint, result.VoAudit.VoPreviewFingerprint);
        Assert.Equal(200, Assert.Single(result.VoLevels).VoExpRequired);
        f.Calculator.Verify(c => c.ClearCache(), Times.Once);
        capture.AssertQuiet();
        f.Cache.Setup(c => c.RemoveAsync(It.IsAny<string>())).ThrowsAsync(new IOException(Secret));
        Assert.Equal(1, (await f.Service.RecalculateLevelConfigsAsync(request, 9, Secret)).VoAudit.VoChangedLevelCount);
        capture.AssertSingle("reward.cache_fallback", "Warning");
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public void Calculator_ShouldKeepFormulaAndSafeCacheFallbacks(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var options = Options.Create(new ExperienceCalculatorOptions
        {
            EnableCache = true, MaxLevel = 2, FormulaType = "Hybrid", BaseExp = 100,
            Exponent = 1, ScaleFactor = 1, MinExpRequired = 1
        });
        var memory = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var calculator = new ExperienceCalculator(options, memory);
        var first = calculator.CalculateAllLevels();
        Assert.Equal((100L, 0L), first[0]);
        Assert.Equal((200L, 100L), first[1]);
        Assert.Equal((0L, 300L), first[2]);
        Assert.Equal(0, calculator.CalculateExpRequired(-1));
        Assert.Equal(0, calculator.CalculateExpRequired(3));
        Assert.Equal(new[] { 0, 1, 2 }, calculator.CalculateAllLevels().Keys);
        calculator.ClearCache();
        capture.AssertQuiet();

        var cache = new Mock<IDistributedCache>();
        var failure = new IOException(Secret);
        cache.Setup(c => c.Get(It.IsAny<string>())).Throws(failure);
        calculator = new ExperienceCalculator(options, cache.Object);
        Assert.Equal(first, calculator.CalculateAllLevels());
        capture.AssertSingle("experience.calculator_cache_fallback", "Warning");
        Assert.Contains("cache-read", capture.Output.ToString());
        capture.Clear();
        cache.Setup(c => c.Get(It.IsAny<string>())).Returns((byte[]?)null);
        cache.Setup(c => c.Set(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>())).Throws(failure);
        Assert.Equal(first, calculator.CalculateAllLevels());
        capture.AssertSingle("experience.calculator_cache_fallback", "Warning");
        Assert.Contains("cache-write", capture.Output.ToString());
        capture.Clear();
        cache.Setup(c => c.Remove(It.IsAny<string>())).Throws(failure);
        calculator.ClearCache();
        capture.AssertSingle("experience.calculator_cache_fallback", "Warning");
        Assert.Contains("cache-invalidate", capture.Output.ToString());
    }

    private static AdminRecordExperienceGovernanceReviewDto ReviewRequest() => new()
    {
        UserId = 1, ExpectedVersion = 3, IdempotencyKey = Secret, ReviewResult = "Observe",
        Remark = Secret, RuleCodes = [Secret], RuleLabels = [Secret], WindowDays = 7
    };

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
        public Mock<IBaseRepository<UserExperience>> Experiences { get; } = new();
        public Mock<IBaseRepository<ExpTransaction>> Transactions { get; } = new();
        public Mock<IBaseRepository<UserExpDailyStats>> Stats { get; } = new();
        public Mock<IBaseRepository<LevelConfig>> Levels { get; } = new();
        public Mock<IExperienceGovernanceRepository> Governance { get; } = new();
        public Mock<IOperationIdempotencyService> Idempotency { get; } = new();
        public Mock<IExperienceCalculator> Calculator { get; } = new();
        public Mock<ICaching> Cache { get; } = new();
        public UserExperience Account { get; } = new() { Id = 2, UserId = 1, CurrentLevel = 1, TotalExp = 80, Version = 3 };
        public ExperienceService Service { get; }
        public ExperienceController Controller { get; }

        public Fixture()
        {
            AppSettingsTool.Configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["ExperienceCalculator:EnableCache"] = "false" }).Build();
            Experiences.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserExperience, bool>>>())).ReturnsAsync(Account);
            Experiences.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<UserExperience, bool>>>())).ReturnsAsync([Account]);
            Experiences.Setup(r => r.QueryCountAsync(It.IsAny<Expression<Func<UserExperience, bool>>>())).ReturnsAsync(3);
            var users = new Mock<IBaseRepository<User>>();
            var user = new User { Id = 1, UserName = Secret, PublicId = Secret, PublicIndex = User.PublicIndexStart };
            users.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync(user);
            users.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync([user]);
            var level = new LevelConfig { Level = 1, LevelName = Secret, ExpRequired = 100, ExpCumulative = 0, IsEnabled = true };
            Levels.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<LevelConfig, bool>>>())).ReturnsAsync([level]);
            Stats.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<UserExpDailyStats, bool>>>())).ReturnsAsync([]);
            Transactions.Setup(r => r.QueryPageAsync(It.IsAny<Expression<Func<ExpTransaction, bool>>>(), 1, 20,
                It.IsAny<Expression<Func<ExpTransaction, object>>>(), OrderByType.Desc))
                .ReturnsAsync((new List<ExpTransaction> { new() { UserId = 1, Remark = Secret } }, 1));
            Idempotency.Setup(s => s.NormalizeKey(Secret)).Returns(Secret);
            Idempotency.Setup(s => s.CreateRequestSnapshot(It.IsAny<IReadOnlyDictionary<string, object?>>()))
                .Returns(new OperationIdempotencyRequestSnapshot { RequestHash = Secret, RequestSummary = Secret });
            Idempotency.Setup(s => s.BeginAsync(It.IsAny<OperationIdempotencyBeginRequest>()))
                .ReturnsAsync(new OperationIdempotencyBeginResult { Status = OperationIdempotencyBeginStatus.Started, RecordId = 3 });
            Governance.Setup(r => r.ApplyGovernanceActionAsync(It.IsAny<ExperienceGovernanceMutationCommand>()))
                .ReturnsAsync((ExperienceGovernanceMutationCommand c) =>
                {
                    Account.Version++;
                    c.Action.ExpectedVersion = c.ExpectedVersion;
                    c.Action.ResultVersion = Account.Version;
                    return new ExperienceGovernanceMutationResult(Account, c.Action);
                });
            Governance.Setup(r => r.QueryLevelConfigsAsync()).ReturnsAsync([level]);
            Calculator.Setup(c => c.CalculateAllLevels()).Returns(new Dictionary<int, (long, long)> { [1] = (200, 0) });
            Calculator.Setup(c => c.GetFormulaType()).Returns(Secret);
            Calculator.Setup(c => c.GetConfigSummary()).Returns(Secret);
            Governance.Setup(r => r.ApplyLevelRecalculationAsync(It.IsAny<ExperienceLevelRecalculationCommand>()))
                .ReturnsAsync((ExperienceLevelRecalculationCommand c) => new ExperienceLevelRecalculationMutationResult(
                    c.Targets.Select(t => new LevelConfig { Level = t.Level, ExpRequired = t.ExpRequired, ExpCumulative = t.ExpCumulative }).ToList(),
                    new ExperienceLevelRecalculationAudit { Id = 8, Reason = c.Reason, PreviewFingerprint = c.ExpectedFingerprint,
                        FormulaType = c.FormulaType, FormulaSummary = c.FormulaSummary, ChangedLevelCount = 1, CreateId = c.ActorUserId, CreateBy = c.ActorName }));
            var mapper = new Mock<IMapper>();
            mapper.Setup(m => m.Map<UserExperienceVo>(It.IsAny<object>())).Returns((object o) =>
            {
                var e = (UserExperience)o;
                return new UserExperienceVo { VoUserId = e.UserId, VoVersion = e.Version, VoCurrentLevel = e.CurrentLevel, VoTotalExp = e.TotalExp };
            });
            mapper.Setup(m => m.Map<LevelConfigVo>(It.IsAny<object>())).Returns((object o) =>
            {
                var e = (LevelConfig)o;
                return new LevelConfigVo { VoLevel = e.Level, VoLevelName = e.LevelName, VoExpRequired = e.ExpRequired, VoExpCumulative = e.ExpCumulative };
            });
            mapper.Setup(m => m.Map<List<LevelConfigVo>>(It.IsAny<object>()))
                .Returns((object o) => ((IEnumerable<LevelConfig>)o).Select(e => mapper.Object.Map<LevelConfigVo>(e)).ToList());
            mapper.Setup(m => m.Map<List<UserExpDailyStatsVo>>(It.IsAny<object>())).Returns([]);
            mapper.Setup(m => m.Map<List<ExpTransactionVo>>(It.IsAny<object>()))
                .Returns((object o) => ((IEnumerable<ExpTransaction>)o).Select(e => new ExpTransactionVo { VoUserId = e.UserId, VoRemark = e.Remark }).ToList());
            var clock = TimeProvider.System;
            Service = new ExperienceService(mapper.Object, Experiences.Object, Transactions.Object, Levels.Object, Stats.Object,
                users.Object, Governance.Object, Calculator.Object, Mock.Of<ICoinService>(), Mock.Of<IAttachmentUrlResolver>(),
                Mock.Of<INotificationService>(), Cache.Object, clock,
                new BusinessCalendar(clock, Options.Create(new TimeOptions { DefaultTimeZoneId = "Asia/Shanghai" })),
                operationIdempotencyService: Idempotency.Object);
            var current = new Mock<ICurrentUserAccessor>();
            current.SetupGet(a => a.Current).Returns(new CurrentUser { UserId = 9, UserName = Secret });
            var localizer = new Mock<IStringLocalizer<Errors>>();
            localizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => new LocalizedString(key, key, true));
            Controller = new ExperienceController(Service, current.Object, localizer.Object);
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
