using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Api.Tests.ErrorHandling;
using Radish.Common;
using Radish.Extension.ExperienceExtension;
using Radish.Model;
using Radish.IRepository;
using Radish.Model.DtoModels;
using Radish.Service;
using Serilog.Events;
using SqlSugar;
using Xunit;
using static Radish.Api.Tests.RewardOutputAssertions;

namespace Radish.Api.Tests;

public sealed partial class ExperienceQueryGovernanceLoggingTests
{
    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public async Task FinalOutput_ShouldKeepRankPaginationAndSnapshotFallbacks(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var global = new ApplicationLogScope(capture.Logger);
        using var scope = Pollute();
        using var activity = new Activity("experience-queries").SetIdFormat(ActivityIdFormat.W3C).Start();
        using var f = new Fixture();
        f.Experiences.Setup(r => r.QueryCountAsync(It.IsAny<Expression<Func<UserExperience, bool>>>())).ThrowsAsync(new IOException(Secret));
        Assert.Equal(0, await f.Service.GetUserRankAsync(1));
        Check(capture, typeof(ExperienceService), "experience.rank_query_failed", LogEventLevel.Error, "io");
        f.Experiences.Setup(r => r.QueryPageAsync(It.IsAny<Expression<Func<UserExperience, bool>>>(), 1, 50,
            It.IsAny<Expression<Func<UserExperience, object>>>(), OrderByType.Desc))
            .ReturnsAsync((new List<UserExperience> { new() { UserId = 2 }, f.Account, new() { UserId = 3 } }, 3));
        var page = await f.Service.GetLeaderboardAsync(0, 0, 1);
        Assert.Equal(3, page.DataCount);
        Assert.Equal(1, Assert.Single(page.Data).VoRank);
        Check(capture, typeof(ExperienceService), "experience.leaderboard_incomplete", LogEventLevel.Warning, null, ("skippedCount", 2));
        f.Governance.Setup(r => r.QueryActionsPageAsync(It.IsAny<ExperienceGovernanceActionPageQuery>()))
            .ReturnsAsync((new List<UserExperienceGovernanceAction> { new() { RuleCodes = "[\"" + Secret, RuleLabels = "[\"safe\"]" } }, 1));
        var actions = await f.Service.GetGovernanceActionsAsync(1, 0, 0);
        Assert.Empty(Assert.Single(actions.Data).VoRuleCodes);
        Assert.Equal("safe", Assert.Single(actions.Data[0].VoRuleLabels));
        Check(capture, typeof(ExperienceService), "experience.governance_snapshot_invalid", LogEventLevel.Warning, "other");
    }

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public async Task FinalOutput_ShouldKeepCacheOperationsAndRecalculationAudit(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var global = new ApplicationLogScope(capture.Logger);
        using var scope = Pollute();
        using var activity = new Activity("experience-cache").SetIdFormat(ActivityIdFormat.W3C).Start();
        using var f = new Fixture();
        AppSettingsTool.Configuration = new ConfigurationBuilder().Build();
        f.Cache.Setup(c => c.GetAsync<List<LevelConfig>>(It.IsAny<string>())).ThrowsAsync(new IOException(Secret));
        Assert.Single(await f.Service.GetLevelConfigsAsync());
        Check(capture, typeof(RewardRuntimeLog), "reward.cache_fallback", LogEventLevel.Warning, "io",
            ("rewardDomain", "experience"), ("rewardOperation", "cache-read"));
        f.Cache.Setup(c => c.GetAsync<List<LevelConfig>>(It.IsAny<string>())).ReturnsAsync((List<LevelConfig>)null!);
        f.Cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<List<LevelConfig>>(), It.IsAny<TimeSpan>())).ThrowsAsync(new IOException(Secret));
        Assert.Single(await f.Service.GetLevelConfigsAsync());
        Check(capture, typeof(RewardRuntimeLog), "reward.cache_fallback", LogEventLevel.Warning, "io",
            ("rewardDomain", "experience"), ("rewardOperation", "cache-write"));
        f.Cache.Setup(c => c.RemoveAsync(It.IsAny<string>())).ThrowsAsync(new IOException(Secret));
        var preview = await f.Service.PreviewLevelConfigRecalculationAsync();
        var result = await f.Service.RecalculateLevelConfigsAsync(new RecalculateLevelConfigsDto { Reason = Secret, ExpectedFingerprint = preview.VoFingerprint }, 9, Secret);
        Assert.Equal(1, result.VoAudit.VoChangedLevelCount);
        Check(capture, typeof(RewardRuntimeLog), "reward.cache_fallback", LogEventLevel.Warning, "io",
            ("rewardDomain", "experience"), ("rewardOperation", "cache-invalidate"));

        var cache = new Mock<IDistributedCache>();
        var calculator = new ExperienceCalculator(Options.Create(new ExperienceCalculatorOptions
        { EnableCache = true, MaxLevel = 2, FormulaType = "Hybrid", BaseExp = 100, Exponent = 1, ScaleFactor = 1, MinExpRequired = 1 }), cache.Object);
        cache.Setup(c => c.Get(It.IsAny<string>())).Throws(new IOException(Secret));
        Assert.Equal((200L, 100L), calculator.CalculateAllLevels()[1]);
        Check(capture, typeof(ExperienceCalculator), "experience.calculator_cache_fallback", LogEventLevel.Warning, "io", ("rewardOperation", "cache-read"));
        cache.Setup(c => c.Get(It.IsAny<string>())).Returns((byte[]?)null);
        cache.Setup(c => c.Set(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>())).Throws(new IOException(Secret));
        Assert.Equal((200L, 100L), calculator.CalculateAllLevels()[1]);
        Check(capture, typeof(ExperienceCalculator), "experience.calculator_cache_fallback", LogEventLevel.Warning, "io", ("rewardOperation", "cache-write"));
        cache.Setup(c => c.Remove(It.IsAny<string>())).Throws(new IOException(Secret));
        calculator.ClearCache();
        Check(capture, typeof(ExperienceCalculator), "experience.calculator_cache_fallback", LogEventLevel.Warning, "io", ("rewardOperation", "cache-invalidate"));
    }
}
