using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Common.CacheTool;
using Radish.Common;
using Radish.Api.Tests.TestCollections;
using Radish.Common.Exceptions;
using Radish.Common.OptionTool;
using Radish.Common.TimeTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;
using Shouldly;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Services;

[Collection(AppSettingsStateCollection.CollectionName)]
public class ExperienceServiceTest
{
    [Fact]
    public async Task GrantExperienceAsync_ShouldUseBusinessDateAndRespectLoginDailyLimit()
    {
        const long userId = 9527;
        var utcNow = new DateTimeOffset(2026, 5, 9, 16, 30, 0, TimeSpan.Zero);
        var businessDate = new DateOnly(2026, 5, 10);

        var userExpRepository = new Mock<IBaseRepository<UserExperience>>(MockBehavior.Strict);
        var dailyStatsRepository = new Mock<IBaseRepository<UserExpDailyStats>>(MockBehavior.Strict);
        var levelConfigRepository = new Mock<IBaseRepository<LevelConfig>>(MockBehavior.Strict);

        userExpRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<UserExperience, bool>>?>()))
            .ReturnsAsync(new UserExperience
            {
                Id = 7001,
                UserId = userId,
                CurrentLevel = 1,
                CurrentExp = 20,
                TotalExp = 80,
                ExpFrozen = false,
                Version = 2,
                IsDeleted = false
            });

        var dailyStats = new UserExpDailyStats
        {
            Id = 8001,
            UserId = userId,
            StatDate = ToBusinessDateStorage(businessDate),
            ExpEarned = 9,
            ExpFromLogin = 9
        };
        dailyStatsRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<UserExpDailyStats, bool>>?>()))
            .ReturnsAsync((Expression<Func<UserExpDailyStats, bool>>? expression) =>
                expression?.Compile()(dailyStats) == true ? dailyStats : null);

        var service = CreateService(
            userExpRepository: userExpRepository,
            levelConfigRepository: levelConfigRepository,
            dailyStatsRepository: dailyStatsRepository,
            configValues: new Dictionary<string, string?>
            {
                ["ExperienceCalculator:EnableCache"] = "true",
                ["ExperienceCalculator:DailyLimits:EnableDailyLimit"] = "true",
                ["ExperienceCalculator:DailyLimits:MaxDailyExp"] = "10",
                ["ExperienceCalculator:DailyLimits:MaxExpFromPost"] = "10",
                ["ExperienceCalculator:DailyLimits:MaxExpFromComment"] = "10",
                ["ExperienceCalculator:DailyLimits:MaxExpFromLike"] = "10",
                ["ExperienceCalculator:DailyLimits:MaxExpFromHighlight"] = "10",
                ["ExperienceCalculator:DailyLimits:MaxExpFromLogin"] = "10"
            },
            utcNow: utcNow);

        var success = await service.GrantExperienceAsync(userId, 2, "DAILY_LOGIN");

        success.ShouldBeFalse();
        levelConfigRepository.Verify(
            repository => repository.QueryAsync(It.IsAny<Expression<Func<LevelConfig, bool>>?>()),
            Times.Never);
        userExpRepository.Verify(
            repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<UserExperience, UserExperience>>>(),
                It.IsAny<Expression<Func<UserExperience, bool>>>()),
            Times.Never);
    }

    [Fact]
    public async Task GrantExperienceOnceAsync_ShouldReturnExisting_WhenRewardBusinessKeyExists()
    {
        const long userId = 9527;
        const string rewardBusinessKey = "exp:post-create:author:9527:post:7001";

        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Strict);
        userRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>?>()))
            .ReturnsAsync(new User
            {
                Id = userId,
                TenantId = 0,
                IsDeleted = false
            });

        var expTransactionRepository = new Mock<IBaseRepository<ExpTransaction>>(MockBehavior.Strict);
        var existingTransaction = new ExpTransaction
        {
            Id = 9001,
            TenantId = 0,
            UserId = userId,
            ExpType = "POST_CREATE",
            BusinessType = "Post",
            BusinessId = 7001,
            RewardBusinessKey = rewardBusinessKey
        };
        expTransactionRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<ExpTransaction, bool>>?>()))
            .ReturnsAsync((Expression<Func<ExpTransaction, bool>>? expression) =>
                expression?.Compile()(existingTransaction) == true ? existingTransaction : null);

        var userExpRepository = new Mock<IBaseRepository<UserExperience>>(MockBehavior.Strict);
        var service = CreateService(
            userExpRepository: userExpRepository,
            expTransactionRepository: expTransactionRepository,
            userRepository: userRepository);

        var result = await service.GrantExperienceOnceAsync(
            userId,
            20,
            "POST_CREATE",
            rewardBusinessKey,
            "Post",
            7001,
            "发布帖子");

        result.AlreadyGranted.ShouldBeTrue();
        result.Granted.ShouldBeFalse();
        result.Skipped.ShouldBeFalse();
        userExpRepository.Verify(
            repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<UserExperience, UserExperience>>>(),
                It.IsAny<Expression<Func<UserExperience, bool>>>()),
            Times.Never);
        expTransactionRepository.Verify(repository => repository.AddAsync(It.IsAny<ExpTransaction>()), Times.Never);
    }

    [Fact]
    public async Task GetLevelConfigsAsync_ShouldUseCacheBetweenCalls()
    {
        var levelConfigs = new List<LevelConfig>
        {
            new()
            {
                Level = 2,
                LevelName = "筑基",
                ExpRequired = 200,
                ExpCumulative = 100,
                IconAttachmentId = 202,
                BadgeAttachmentId = 302,
                IsEnabled = true,
                Privileges = "[\"灵气外放\"]"
            },
            new()
            {
                Level = 1,
                LevelName = "练气",
                ExpRequired = 100,
                ExpCumulative = 0,
                IconAttachmentId = 201,
                BadgeAttachmentId = 301,
                IsEnabled = true,
                Privileges = "[\"入门徽记\"]"
            }
        };

        var levelConfigRepository = new Mock<IBaseRepository<LevelConfig>>(MockBehavior.Strict);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);

        levelConfigRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<LevelConfig, bool>>?>()))
            .ReturnsAsync((Expression<Func<LevelConfig, bool>>? expression) =>
            {
                var query = levelConfigs.AsEnumerable();
                if (expression != null)
                {
                    query = query.Where(expression.Compile());
                }

                return query.Select(CloneLevelConfig).ToList();
            });

        attachmentUrlResolver
            .Setup(resolver => resolver.ResolveAttachmentUrl(It.IsAny<long>()))
            .Returns<long>(attachmentId => $"https://assets.local/{attachmentId}");

        var service = CreateService(
            levelConfigRepository: levelConfigRepository,
            attachmentUrlResolver: attachmentUrlResolver,
            configValues: new Dictionary<string, string?>
            {
                ["ExperienceCalculator:EnableCache"] = "true",
                ["ExperienceCalculator:CacheExpirationMinutes"] = "30"
            });

        var first = await service.GetLevelConfigsAsync();
        var second = await service.GetLevelConfigsAsync();

        first.Count.ShouldBe(2);
        first[0].VoLevel.ShouldBe(1);
        first[0].VoIconUrl.ShouldBe("https://assets.local/201");
        first[0].VoBadgeUrl.ShouldBe("https://assets.local/301");
        first[1].VoLevel.ShouldBe(2);
        second.Select(item => item.VoLevel).ShouldBe([1, 2]);

        levelConfigRepository.Verify(
            repository => repository.QueryAsync(It.IsAny<Expression<Func<LevelConfig, bool>>?>()),
            Times.Once);
        attachmentUrlResolver.Verify(
            resolver => resolver.ResolveAttachmentUrl(It.IsAny<long>()),
            Times.Exactly(8));
    }

    [Fact]
    public async Task UpdateDailyStatsAsync_ShouldCreateStatsAndAccumulateCounters()
    {
        const long userId = 20001;
        var statDate = new DateOnly(2026, 5, 10);
        var storedStats = new List<UserExpDailyStats>();

        var dailyStatsRepository = new Mock<IBaseRepository<UserExpDailyStats>>(MockBehavior.Strict);
        dailyStatsRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<UserExpDailyStats, bool>>?>()))
            .ReturnsAsync((Expression<Func<UserExpDailyStats, bool>>? expression) =>
            {
                if (expression == null)
                {
                    return storedStats.FirstOrDefault();
                }

                return storedStats.FirstOrDefault(expression.Compile());
            });
        dailyStatsRepository
            .Setup(repository => repository.AddAsync(It.IsAny<UserExpDailyStats>()))
            .Callback<UserExpDailyStats>(stats => storedStats.Add(stats))
            .ReturnsAsync(9001);
        dailyStatsRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<UserExpDailyStats>()))
            .ReturnsAsync(true);

        var service = CreateService(dailyStatsRepository: dailyStatsRepository);

        await service.UpdateDailyStatsAsync(userId, "POST_CREATE", 20, statDate);
        await service.UpdateDailyStatsAsync(userId, "GIVE_LIKE", 2, statDate);

        storedStats.Count.ShouldBe(1);
        var stats = storedStats[0];
        stats.UserId.ShouldBe(userId);
        stats.StatDate.ShouldBe(ToBusinessDateStorage(statDate));
        stats.ExpEarned.ShouldBe(22);
        stats.ExpFromPost.ShouldBe(20);
        stats.PostCount.ShouldBe(1);
        stats.ExpFromLike.ShouldBe(2);
        stats.LikeGivenCount.ShouldBe(1);
        stats.CommentCount.ShouldBe(0);

        dailyStatsRepository.Verify(
            repository => repository.UpdateAsync(It.IsAny<UserExpDailyStats>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task GetDailyStatsAsync_ShouldBackfillWindowAndBuildSummary()
    {
        const long userId = 20002;
        var endDate = new DateOnly(2026, 5, 10);
        var existingStats = new List<UserExpDailyStats>
        {
            new()
            {
                Id = 9101,
                UserId = userId,
                StatDate = ToBusinessDateStorage(endDate),
                ExpEarned = 40,
                ExpFromHighlight = 30,
                ExpFromComment = 10,
                CommentCount = 2
            },
            new()
            {
                Id = 9102,
                UserId = userId,
                StatDate = ToBusinessDateStorage(endDate.AddDays(-2)),
                ExpEarned = 25,
                ExpFromLike = 20,
                ExpFromComment = 5,
                LikeReceivedCount = 10
            }
        };

        var dailyStatsRepository = new Mock<IBaseRepository<UserExpDailyStats>>(MockBehavior.Strict);
        dailyStatsRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<UserExpDailyStats, bool>>?>()))
            .ReturnsAsync((Expression<Func<UserExpDailyStats, bool>>? expression) =>
            {
                var query = existingStats.AsEnumerable();
                if (expression != null)
                {
                    query = query.Where(expression.Compile());
                }

                return query.ToList();
            });

        var service = CreateService(
            dailyStatsRepository: dailyStatsRepository,
            utcNow: new DateTimeOffset(2026, 5, 10, 4, 0, 0, TimeSpan.Zero));

        var result = await service.GetDailyStatsAsync(userId, 3);

        result.VoWindowDays.ShouldBe(3);
        result.VoStats.Count.ShouldBe(3);
        result.VoStats[0].VoStatDate.ShouldBe(endDate);
        result.VoStats[0].VoObservations.Select(item => item.VoLabel).ShouldBe(["高亮驱动", "高亮奖励集中"]);
        result.VoStats[1].VoStatDate.ShouldBe(endDate.AddDays(-1));
        result.VoStats[1].VoExpEarned.ShouldBe(0);
        result.VoStats[1].VoObservations.Select(item => item.VoLabel).ShouldBe(["零增长"]);
        result.VoStats[2].VoStatDate.ShouldBe(endDate.AddDays(-2));
        result.VoStats[2].VoObservations.Select(item => item.VoLabel).ShouldBe(["点赞驱动", "点赞占比偏高"]);

        result.VoSummary.ShouldNotBeNull();
        result.VoSummary.VoTotalExp.ShouldBe(65);
        result.VoSummary.VoAverageExp.ShouldBe(65d / 3d);
        result.VoSummary.VoPeakDayExp.ShouldBe(40);
        result.VoSummary.VoPeakStatDate.ShouldBe(endDate);
        result.VoSummary.VoZeroGainDays.ShouldBe(1);
        result.VoSummary.VoReviewDays.ShouldBe(2);
        result.VoSummary.VoNotices.ShouldBe([
            "其中 1 天经验主要来自点赞，建议结合互动来源复核。",
            "其中 1 天经验主要来自高亮评论，建议确认是否集中触发奖励。"
        ]);
        result.VoRuleSummaries.Select(item => item.VoRuleLabel).ShouldBe(["高亮奖励集中", "点赞占比偏高"]);
        result.VoRecommendation.ShouldNotBeNull();
        result.VoRecommendation.VoLevel.ShouldBe("normal");
        result.VoRecommendation.VoTitle.ShouldBe("正常观察");
    }

    [Fact]
    public async Task GetDailyStatsAsync_ShouldTreatDominantSourceAsContextNotAnomaly()
    {
        const long userId = 20003;
        var endDate = new DateOnly(2026, 5, 10);
        var existingStats = new List<UserExpDailyStats>
        {
            new()
            {
                Id = 9201,
                UserId = userId,
                StatDate = ToBusinessDateStorage(endDate),
                ExpEarned = 10,
                ExpFromLike = 10,
                LikeReceivedCount = 5
            }
        };

        var dailyStatsRepository = new Mock<IBaseRepository<UserExpDailyStats>>(MockBehavior.Strict);
        dailyStatsRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<UserExpDailyStats, bool>>?>()))
            .ReturnsAsync((Expression<Func<UserExpDailyStats, bool>>? expression) =>
            {
                var query = existingStats.AsEnumerable();
                if (expression != null)
                {
                    query = query.Where(expression.Compile());
                }

                return query.ToList();
            });

        var service = CreateService(
            dailyStatsRepository: dailyStatsRepository,
            utcNow: new DateTimeOffset(2026, 5, 10, 4, 0, 0, TimeSpan.Zero));

        var result = await service.GetDailyStatsAsync(userId, 1);

        result.VoStats.Count.ShouldBe(1);
        result.VoStats[0].VoObservations.Select(item => item.VoLabel).ShouldBe(["点赞驱动"]);
        result.VoStats[0].VoObservations[0].VoKind.ShouldBe("context");
        result.VoSummary.ShouldNotBeNull();
        result.VoSummary.VoReviewDays.ShouldBe(0);
        result.VoRuleSummaries.ShouldBeEmpty();
        result.VoRecommendation.ShouldNotBeNull();
        result.VoRecommendation.VoLevel.ShouldBe("normal");
        result.VoRecommendation.VoReason.ShouldContain("未命中异常规则");
    }

    [Fact]
    public async Task GetDailyStatsAsync_ShouldRecommendManualReviewForRepeatedRule()
    {
        const long userId = 20004;
        var endDate = new DateOnly(2026, 5, 10);
        var existingStats = new List<UserExpDailyStats>
        {
            new()
            {
                Id = 9301,
                UserId = userId,
                StatDate = ToBusinessDateStorage(endDate),
                ExpEarned = 30,
                ExpFromLike = 24,
                ExpFromComment = 6,
                LikeReceivedCount = 12
            },
            new()
            {
                Id = 9302,
                UserId = userId,
                StatDate = ToBusinessDateStorage(endDate.AddDays(-1)),
                ExpEarned = 25,
                ExpFromLike = 20,
                ExpFromComment = 5,
                LikeReceivedCount = 10
            }
        };

        var dailyStatsRepository = new Mock<IBaseRepository<UserExpDailyStats>>(MockBehavior.Strict);
        dailyStatsRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<UserExpDailyStats, bool>>?>()))
            .ReturnsAsync((Expression<Func<UserExpDailyStats, bool>>? expression) =>
            {
                var query = existingStats.AsEnumerable();
                if (expression != null)
                {
                    query = query.Where(expression.Compile());
                }

                return query.ToList();
            });

        var service = CreateService(
            dailyStatsRepository: dailyStatsRepository,
            utcNow: new DateTimeOffset(2026, 5, 10, 4, 0, 0, TimeSpan.Zero));

        var result = await service.GetDailyStatsAsync(userId, 7);

        result.VoRuleSummaries.ShouldContain(item =>
            item.VoRuleCode == "LIKE_SHARE_HEAVY"
            && item.VoHitDays == 2
            && item.VoSeverity == "review");
        result.VoRecommendation.ShouldNotBeNull();
        result.VoRecommendation.VoLevel.ShouldBe("review");
        result.VoRecommendation.VoReason.ShouldContain("重复命中");
    }

    [Fact]
    public async Task GetDailyStatsAsync_ShouldSuggestFreezeForRepeatedLimitHitAndSourceConcentration()
    {
        const long userId = 20005;
        var endDate = new DateOnly(2026, 5, 10);
        var existingStats = new List<UserExpDailyStats>
        {
            new()
            {
                Id = 9401,
                UserId = userId,
                StatDate = ToBusinessDateStorage(endDate),
                ExpEarned = 50,
                ExpFromLike = 50,
                LikeReceivedCount = 25
            },
            new()
            {
                Id = 9402,
                UserId = userId,
                StatDate = ToBusinessDateStorage(endDate.AddDays(-2)),
                ExpEarned = 50,
                ExpFromLike = 50,
                LikeReceivedCount = 25
            }
        };

        var dailyStatsRepository = new Mock<IBaseRepository<UserExpDailyStats>>(MockBehavior.Strict);
        dailyStatsRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<UserExpDailyStats, bool>>?>()))
            .ReturnsAsync((Expression<Func<UserExpDailyStats, bool>>? expression) =>
            {
                var query = existingStats.AsEnumerable();
                if (expression != null)
                {
                    query = query.Where(expression.Compile());
                }

                return query.ToList();
            });

        var service = CreateService(
            dailyStatsRepository: dailyStatsRepository,
            utcNow: new DateTimeOffset(2026, 5, 10, 4, 0, 0, TimeSpan.Zero));

        var result = await service.GetDailyStatsAsync(userId, 7);

        result.VoRuleSummaries.ShouldContain(item =>
            item.VoRuleCode == "LIKE_LIMIT_PRESSURE"
            && item.VoHitDays == 2
            && item.VoStrongestSignal == "触达上限");
        result.VoRecommendation.ShouldNotBeNull();
        result.VoRecommendation.VoLevel.ShouldBe("freeze-suggest");
        result.VoRecommendation.VoReason.ShouldContain("冻结建议阈值");
    }

    [Fact]
    public async Task GetTransactionsAsync_Should_Map_UserName_And_Operator_Info()
    {
        const long userId = 31001;
        var transactions = new List<ExpTransaction>
        {
            new()
            {
                Id = 70001,
                UserId = userId,
                ExpType = "ADMIN_ADJUST",
                ExpAmount = 18,
                Remark = "活动补偿",
                ExpBefore = 120,
                ExpAfter = 138,
                LevelBefore = 2,
                LevelAfter = 2,
                CreateBy = "Auditor",
                CreateId = 9001,
                CreateTime = new DateTime(2026, 5, 11, 10, 30, 0)
            }
        };

        var expTransactionRepository = new Mock<IBaseRepository<ExpTransaction>>(MockBehavior.Strict);
        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Strict);

        expTransactionRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<ExpTransaction, bool>>>(),
                1,
                100,
                It.IsAny<Expression<Func<ExpTransaction, object>>>(),
                OrderByType.Desc))
            .ReturnsAsync((transactions, 1));
        userRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<User, bool>>?>()))
            .ReturnsAsync(new List<User>
            {
                new()
                {
                    Id = userId,
                    UserName = "tester-user",
                    IsDeleted = false
                }
            });

        var service = CreateService(
            expTransactionRepository: expTransactionRepository,
            userRepository: userRepository);

        var result = await service.GetTransactionsAsync(userId, 0, 500, "  ADMIN_ADJUST  ");

        result.Page.ShouldBe(1);
        result.PageSize.ShouldBe(100);
        result.DataCount.ShouldBe(1);
        result.Data.Count.ShouldBe(1);
        result.Data[0].VoUserName.ShouldBe("tester-user");
        result.Data[0].VoOperatorName.ShouldBe("Auditor");
        result.Data[0].VoOperatorId.ShouldBe(9001);
        result.Data[0].VoExpTypeDisplay.ShouldBe("管理员调整");
    }

    [Fact]
    public async Task GetTransactionsAsync_Should_Filter_By_Multiple_ExpTypes_And_DateRange()
    {
        const long userId = 31002;
        var startDate = new DateTime(2026, 5, 10, 0, 0, 0);
        var endDate = new DateTime(2026, 5, 10, 23, 59, 59);
        var transactions = new List<ExpTransaction>
        {
            new()
            {
                Id = 80001,
                UserId = userId,
                ExpType = "RECEIVE_LIKE",
                ExpAmount = 2,
                ExpBefore = 100,
                ExpAfter = 102,
                LevelBefore = 2,
                LevelAfter = 2,
                CreateBy = "System",
                CreateId = 0,
                CreateTime = new DateTime(2026, 5, 10, 9, 0, 0)
            },
            new()
            {
                Id = 80002,
                UserId = userId,
                ExpType = "GIVE_LIKE",
                ExpAmount = 1,
                ExpBefore = 102,
                ExpAfter = 103,
                LevelBefore = 2,
                LevelAfter = 2,
                CreateBy = "System",
                CreateId = 0,
                CreateTime = new DateTime(2026, 5, 10, 10, 0, 0)
            },
            new()
            {
                Id = 80003,
                UserId = userId,
                ExpType = "POST_CREATE",
                ExpAmount = 20,
                ExpBefore = 103,
                ExpAfter = 123,
                LevelBefore = 2,
                LevelAfter = 2,
                CreateBy = "System",
                CreateId = 0,
                CreateTime = new DateTime(2026, 5, 10, 11, 0, 0)
            },
            new()
            {
                Id = 80004,
                UserId = userId,
                ExpType = "RECEIVE_LIKE",
                ExpAmount = 2,
                ExpBefore = 90,
                ExpAfter = 92,
                LevelBefore = 1,
                LevelAfter = 1,
                CreateBy = "System",
                CreateId = 0,
                CreateTime = new DateTime(2026, 5, 11, 9, 0, 0)
            }
        };

        var expTransactionRepository = new Mock<IBaseRepository<ExpTransaction>>(MockBehavior.Strict);
        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Strict);

        expTransactionRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<ExpTransaction, bool>>>(),
                1,
                20,
                It.IsAny<Expression<Func<ExpTransaction, object>>>(),
                OrderByType.Desc))
            .ReturnsAsync((Expression<Func<ExpTransaction, bool>> expression, int pageIndex, int pageSize, Expression<Func<ExpTransaction, object>> orderByExpression, OrderByType orderByType) =>
            {
                var filtered = transactions
                    .Where(expression.Compile())
                    .OrderByDescending(item => item.CreateTime)
                    .ToList();
                return (filtered, filtered.Count);
            });
        userRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<User, bool>>?>()))
            .ReturnsAsync(new List<User>
            {
                new()
                {
                    Id = userId,
                    UserName = "filter-user",
                    IsDeleted = false
                }
            });

        var service = CreateService(
            expTransactionRepository: expTransactionRepository,
            userRepository: userRepository);

        var result = await service.GetTransactionsAsync(userId, 1, 20, " RECEIVE_LIKE , GIVE_LIKE ", startDate, endDate);

        result.DataCount.ShouldBe(2);
        result.Data.Select(item => item.VoExpType).ShouldBe(["GIVE_LIKE", "RECEIVE_LIKE"]);
        result.Data.All(item => item.VoUserName == "filter-user").ShouldBeTrue();
    }

    [Fact]
    public async Task RecordGovernanceReviewAsync_Should_Advance_Version_And_Persist_Evidence()
    {
        const long userId = 51001;
        UserExperienceGovernanceAction? capturedAction = null;

        var userExpRepository = ExperienceTargetRepository(userId, 22, 0);
        var userRepository = ExperienceUserRepository(userId, 22, "review-user");
        var governanceRepository = new Mock<IExperienceGovernanceRepository>(MockBehavior.Strict);

        governanceRepository
            .Setup(repository => repository.ApplyGovernanceActionAsync(It.IsAny<ExperienceGovernanceMutationCommand>()))
            .ReturnsAsync((ExperienceGovernanceMutationCommand command) =>
            {
                capturedAction = command.Action;
                command.Action.ExpectedVersion = command.ExpectedVersion;
                command.Action.ResultVersion = command.ExpectedVersion + 1;
                return new ExperienceGovernanceMutationResult(
                    new UserExperience { Id = 91001, UserId = userId, TenantId = 22, Version = 1 },
                    command.Action);
            });

        var service = CreateService(
            userExpRepository: userExpRepository,
            userRepository: userRepository,
            experienceGovernanceRepository: governanceRepository,
            operationIdempotencyService: CreateStartedIdempotencyService(82001),
            configValues: new Dictionary<string, string?> { ["ExperienceCalculator:EnableCache"] = "false" });

        var success = await service.RecordGovernanceReviewAsync(
            new AdminRecordExperienceGovernanceReviewDto
            {
                UserId = userId,
                ReviewResult = "Observe",
                Remark = "已回看对应日期经验流水与目标内容，暂未发现直接异常。",
                WindowDays = 7,
                StatDate = new DateOnly(2026, 5, 10),
                RuleCodes = ["LIKE_SHARE_HEAVY"],
                RuleLabels = ["点赞占比偏高"],
                RecommendationLevel = "review",
                RecommendationReason = "最近 7 天同一规则重复命中",
                ExpectedVersion = 0,
                IdempotencyKey = "review-51001-v0"
            },
            9001,
            "Auditor");

        success.VoExperience.VoVersion.ShouldBe(1);
        capturedAction.ShouldNotBeNull();
        capturedAction.TargetUserId.ShouldBe(userId);
        capturedAction.TargetUserName.ShouldBe("review-user");
        capturedAction.TenantId.ShouldBe(22);
        capturedAction.ActionType.ShouldBe((int)ExperienceGovernanceActionTypeEnum.Review);
        capturedAction.ReviewResult.ShouldBe((int)ExperienceGovernanceReviewResultEnum.Observe);
        capturedAction.Remark.ShouldBe("已回看对应日期经验流水与目标内容，暂未发现直接异常。");
        capturedAction.WindowDays.ShouldBe(7);
        capturedAction.StatDate.ShouldBe(new DateTime(2026, 5, 10));
        var serializedRuleCodes = capturedAction.RuleCodes;
        serializedRuleCodes.ShouldNotBeNull();
        serializedRuleCodes!.ShouldContain("LIKE_SHARE_HEAVY");
        var serializedRuleLabels = capturedAction.RuleLabels;
        serializedRuleLabels.ShouldNotBeNull();
        serializedRuleLabels!.ShouldContain("点赞占比偏高");
        capturedAction.RecommendationLevel.ShouldBe("review");
        capturedAction.RecommendationReason.ShouldBe("最近 7 天同一规则重复命中");
        capturedAction.CreateBy.ShouldBe("Auditor");
        capturedAction.CreateId.ShouldBe(9001);
        capturedAction.ExpectedVersion.ShouldBe(0);
        capturedAction.ResultVersion.ShouldBe(1);
    }

    [Fact]
    public async Task FreezeExperienceAsync_Should_Write_Versioned_Governance_Action()
    {
        const long userId = 51002;
        var nowUtc = new DateTimeOffset(2026, 5, 10, 4, 0, 0, TimeSpan.Zero);
        var frozenUntil = nowUtc.UtcDateTime.AddDays(7);
        UserExperienceGovernanceAction? capturedAction = null;

        var userExpRepository = ExperienceTargetRepository(userId, 33, 4);
        var userRepository = ExperienceUserRepository(userId, 33, "freeze-user");
        var governanceRepository = new Mock<IExperienceGovernanceRepository>(MockBehavior.Strict);

        userExpRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<UserExperience, bool>>?>()))
            .ReturnsAsync(new UserExperience
            {
                Id = 91002,
                UserId = userId,
                TenantId = 33,
                Version = 4,
                IsDeleted = false
            });
        userExpRepository
            .Setup(repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<UserExperience, UserExperience>>>(),
                It.IsAny<Expression<Func<UserExperience, bool>>>()))
            .ReturnsAsync(1);
        userRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>?>()))
            .ReturnsAsync(new User
            {
                Id = userId,
                UserName = "freeze-user",
                TenantId = 33,
                IsDeleted = false
            });
        governanceRepository
            .Setup(repository => repository.ApplyGovernanceActionAsync(It.IsAny<ExperienceGovernanceMutationCommand>()))
            .ReturnsAsync((ExperienceGovernanceMutationCommand command) =>
            {
                capturedAction = command.Action;
                command.Action.ExpectedVersion = 4;
                command.Action.ResultVersion = 5;
                return new ExperienceGovernanceMutationResult(
                    new UserExperience
                    {
                        Id = 91002,
                        UserId = userId,
                        TenantId = 33,
                        Version = 5,
                        ExpFrozen = true,
                        FrozenUntil = frozenUntil,
                        FrozenReason = command.FrozenReason
                    },
                    command.Action);
            });

        var service = CreateService(
            userExpRepository: userExpRepository,
            userRepository: userRepository,
            experienceGovernanceRepository: governanceRepository,
            configValues: new Dictionary<string, string?> { ["ExperienceCalculator:EnableCache"] = "false" },
            utcNow: nowUtc);

        var success = await service.FreezeExperienceAsync(
            userId,
            frozenUntil,
            "经验异常待复核",
            9001,
            "Auditor",
            4);

        success.VoExperience.VoVersion.ShouldBe(5);
        capturedAction.ShouldNotBeNull();
        capturedAction.TargetUserId.ShouldBe(userId);
        capturedAction.TargetUserName.ShouldBe("freeze-user");
        capturedAction.TenantId.ShouldBe(33);
        capturedAction.ActionType.ShouldBe((int)ExperienceGovernanceActionTypeEnum.Freeze);
        capturedAction.ReviewResult.ShouldBeNull();
        capturedAction.Remark.ShouldBe("经验异常待复核");
        capturedAction.FrozenUntil.ShouldBe(frozenUntil);
        capturedAction.CreateTime.ShouldBe(nowUtc.UtcDateTime);
        capturedAction.CreateBy.ShouldBe("Auditor");
        capturedAction.CreateId.ShouldBe(9001);
        capturedAction.ExpectedVersion.ShouldBe(4);
        capturedAction.ResultVersion.ShouldBe(5);
    }

    [Fact]
    public async Task GetUserExperienceAsync_Should_Append_AutoUnfreeze_Action_For_Expired_Freeze()
    {
        const long userId = 51004;
        var nowUtc = new DateTimeOffset(2026, 5, 20, 4, 0, 0, TimeSpan.Zero);
        var expired = new UserExperience
        {
            Id = 91004,
            UserId = userId,
            TenantId = 44,
            Version = 7,
            ExpFrozen = true,
            FrozenUntil = nowUtc.UtcDateTime.AddMinutes(-1),
            FrozenReason = "临时复核冻结",
            IsDeleted = false
        };
        var userExpRepository = new Mock<IBaseRepository<UserExperience>>(MockBehavior.Strict);
        userExpRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<UserExperience, bool>>?>()))
            .ReturnsAsync(expired);
        var userRepository = ExperienceUserRepository(userId, 44, "expired-user");
        var governanceRepository = new Mock<IExperienceGovernanceRepository>(MockBehavior.Strict);
        UserExperienceGovernanceAction? capturedAction = null;
        governanceRepository
            .Setup(repository => repository.ApplyGovernanceActionAsync(It.IsAny<ExperienceGovernanceMutationCommand>()))
            .ReturnsAsync((ExperienceGovernanceMutationCommand command) =>
            {
                capturedAction = command.Action;
                command.Action.ExpectedVersion = 7;
                command.Action.ResultVersion = 8;
                return new ExperienceGovernanceMutationResult(
                    new UserExperience
                    {
                        Id = expired.Id,
                        UserId = userId,
                        TenantId = 44,
                        Version = 8,
                        ExpFrozen = false
                    },
                    command.Action);
            });
        var service = CreateService(
            userExpRepository: userExpRepository,
            userRepository: userRepository,
            experienceGovernanceRepository: governanceRepository,
            configValues: new Dictionary<string, string?> { ["ExperienceCalculator:EnableCache"] = "false" },
            utcNow: nowUtc);

        var result = await service.GetUserExperienceAsync(userId);

        result.ShouldNotBeNull();
        result.VoExpFrozen.ShouldBeFalse();
        result.VoVersion.ShouldBe(8);
        capturedAction.ShouldNotBeNull();
        capturedAction.ActionType.ShouldBe((int)ExperienceGovernanceActionTypeEnum.AutoUnfreeze);
        capturedAction.Remark.ShouldContain("临时经验冻结到期");
        capturedAction.Remark.ShouldContain("临时复核冻结");
        capturedAction.CreateBy.ShouldBe("System");
        capturedAction.CreateId.ShouldBe(0);
        capturedAction.ExpectedVersion.ShouldBe(7);
        capturedAction.ResultVersion.ShouldBe(8);
    }

    [Fact]
    public async Task GetGovernanceActionsAsync_Should_Map_Items_In_Descending_Order()
    {
        const long userId = 51003;
        var actions = new List<UserExperienceGovernanceAction>
        {
            new()
            {
                Id = 93001,
                TargetUserId = userId,
                TargetUserName = "review-user",
                ActionType = (int)ExperienceGovernanceActionTypeEnum.Review,
                ReviewResult = (int)ExperienceGovernanceReviewResultEnum.Observe,
                Remark = "已人工复核，继续观察。",
                WindowDays = 7,
                StatDate = new DateTime(2026, 5, 10),
                RuleCodes = "[\"LIKE_SHARE_HEAVY\"]",
                RuleLabels = "[\"点赞占比偏高\"]",
                RecommendationLevel = "review",
                RecommendationReason = "最近 7 天重复命中",
                CreateTime = new DateTime(2026, 5, 11, 10, 0, 0),
                CreateBy = "Auditor",
                CreateId = 9001
            },
            new()
            {
                Id = 93002,
                TargetUserId = userId,
                TargetUserName = "review-user",
                ActionType = (int)ExperienceGovernanceActionTypeEnum.Freeze,
                Remark = "经验异常待复核",
                FrozenUntil = new DateTime(2026, 5, 20, 12, 0, 0),
                CreateTime = new DateTime(2026, 5, 10, 9, 0, 0),
                CreateBy = "Auditor",
                CreateId = 9001
            }
        };

        var governanceRepository = new Mock<IExperienceGovernanceRepository>(MockBehavior.Strict);
        governanceRepository
            .Setup(repository => repository.QueryActionsPageAsync(
                It.IsAny<ExperienceGovernanceActionPageQuery>()))
            .ReturnsAsync((actions, actions.Count));

        var service = CreateService(
            userExpRepository: ExperienceTargetRepository(userId, 12, 4),
            experienceGovernanceRepository: governanceRepository);

        var result = await service.GetGovernanceActionsAsync(userId, 1, 20);

        result.DataCount.ShouldBe(2);
        result.Data[0].VoActionId.ShouldBe(93001);
        result.Data[0].VoActionType.ShouldBe("Review");
        result.Data[0].VoActionTypeDisplay.ShouldBe("人工复核");
        result.Data[0].VoReviewResult.ShouldBe("Observe");
        result.Data[0].VoReviewResultDisplay.ShouldBe("已复核，继续观察");
        result.Data[0].VoRuleCodes.ShouldBe(["LIKE_SHARE_HEAVY"]);
        result.Data[0].VoRuleLabels.ShouldBe(["点赞占比偏高"]);
        result.Data[0].VoStatDate.ShouldBe(new DateOnly(2026, 5, 10));
        var evidenceSummary = result.Data[0].VoEvidenceSummary;
        evidenceSummary.ShouldNotBeNull();
        evidenceSummary!.ShouldContain("窗口 7 天");
        evidenceSummary.ShouldContain("规则 点赞占比偏高");
        result.Data[1].VoActionId.ShouldBe(93002);
        result.Data[1].VoActionType.ShouldBe("Freeze");
        result.Data[1].VoFrozenUntil.ShouldBe(new DateTime(2026, 5, 20, 12, 0, 0));
    }

    [Fact]
    public async Task AdminAdjustExperienceAsync_Should_Clamp_To_Zero_And_Record_Penalty_Transaction()
    {
        const long userId = 41001;
        var nowUtc = new DateTimeOffset(2026, 5, 9, 16, 30, 0, TimeSpan.Zero);
        ExpTransaction? capturedTransaction = null;

        var userExpRepository = new Mock<IBaseRepository<UserExperience>>(MockBehavior.Strict);
        var expTransactionRepository = new Mock<IBaseRepository<ExpTransaction>>(MockBehavior.Strict);
        var levelConfigRepository = new Mock<IBaseRepository<LevelConfig>>(MockBehavior.Strict);
        var governanceRepository = new Mock<IExperienceGovernanceRepository>(MockBehavior.Strict);

        userExpRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<UserExperience, bool>>?>()))
            .ReturnsAsync(new UserExperience
            {
                Id = 8101,
                UserId = userId,
                CurrentLevel = 1,
                CurrentExp = 20,
                TotalExp = 80,
                ExpFrozen = false,
                Version = 3,
                TenantId = 0,
                IsDeleted = false
            });
        userExpRepository
            .Setup(repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<UserExperience, UserExperience>>>(),
                It.IsAny<Expression<Func<UserExperience, bool>>>()))
            .ReturnsAsync(1);
        levelConfigRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<LevelConfig, bool>>?>()))
            .ReturnsAsync(new List<LevelConfig>
            {
                new()
                {
                    Level = 0,
                    LevelName = "凡人",
                    ExpRequired = 50,
                    ExpCumulative = 0,
                    IsEnabled = true
                },
                new()
                {
                    Level = 1,
                    LevelName = "练气",
                    ExpRequired = 100,
                    ExpCumulative = 50,
                    IsEnabled = true
                }
            });
        governanceRepository
            .Setup(repository => repository.ApplyAdjustmentAsync(It.IsAny<ExperienceAdjustmentMutationCommand>()))
            .ReturnsAsync((ExperienceAdjustmentMutationCommand command) =>
            {
                capturedTransaction = command.Transaction;
                return new ExperienceAdjustmentMutationResult(
                    new UserExperience
                    {
                        Id = 8101,
                        UserId = userId,
                        CurrentLevel = command.CurrentLevel,
                        CurrentExp = command.CurrentExp,
                        TotalExp = command.TotalExp,
                        Version = 4
                    },
                    command.Transaction);
            });

        var service = CreateService(
            userExpRepository: userExpRepository,
            expTransactionRepository: expTransactionRepository,
            levelConfigRepository: levelConfigRepository,
            experienceGovernanceRepository: governanceRepository,
            operationIdempotencyService: CreateStartedIdempotencyService(82002),
            configValues: new Dictionary<string, string?>
            {
                ["ExperienceCalculator:EnableCache"] = "false"
            },
            utcNow: nowUtc);

        var success = await service.AdminAdjustExperienceAsync(
            userId,
            -120,
            "回收异常经验",
            9001,
            "Auditor",
            3,
            "adjust-41001-v3");

        success.VoExperience.VoVersion.ShouldBe(4);
        capturedTransaction.ShouldNotBeNull();
        capturedTransaction.ExpType.ShouldBe("PENALTY");
        capturedTransaction.ExpAmount.ShouldBe(-80);
        capturedTransaction.ExpBefore.ShouldBe(80);
        capturedTransaction.ExpAfter.ShouldBe(0);
        capturedTransaction.LevelBefore.ShouldBe(1);
        capturedTransaction.LevelAfter.ShouldBe(0);
        capturedTransaction.CreatedDate.ShouldBe(new DateTime(2026, 5, 10));
        capturedTransaction.CreateTime.ShouldBe(nowUtc.UtcDateTime);
        capturedTransaction.CreateBy.ShouldBe("Auditor");
        capturedTransaction.CreateId.ShouldBe(9001);
    }

    [Fact]
    public async Task AdminAdjustExperienceAsync_Should_Replay_Before_Rejecting_Advanced_Version()
    {
        const long userId = 41002;
        var governanceRepository = new Mock<IExperienceGovernanceRepository>(MockBehavior.Strict);
        var idempotencyService = new Mock<IOperationIdempotencyService>(MockBehavior.Strict);
        var replay = new AdminExperienceAdjustmentResultVo
        {
            VoExperience = new UserExperienceVo
            {
                VoUserId = userId,
                VoTotalExp = 125,
                VoVersion = 4
            },
            VoTransaction = new ExpTransactionVo
            {
                VoId = 82003,
                VoUserId = userId,
                VoExpAmount = 5
            }
        };
        idempotencyService.Setup(service => service.NormalizeKey("adjust-41002-v3"))
            .Returns("adjust-41002-v3");
        idempotencyService.Setup(service => service.CreateRequestSnapshot(
                It.IsAny<IReadOnlyDictionary<string, object?>>()))
            .Returns(new OperationIdempotencyRequestSnapshot
            {
                RequestHash = "hash",
                RequestSummary = "summary"
            });
        idempotencyService.Setup(service => service.BeginAsync(It.IsAny<OperationIdempotencyBeginRequest>()))
            .ReturnsAsync(new OperationIdempotencyBeginResult
            {
                Status = OperationIdempotencyBeginStatus.Succeeded,
                ResponsePayload = "replay"
            });
        idempotencyService.Setup(service =>
                service.DeserializeResponse<AdminExperienceAdjustmentResultVo>("replay"))
            .Returns(replay);

        var service = CreateService(
            userExpRepository: ExperienceTargetRepository(userId, 12, 4, totalExp: 125),
            experienceGovernanceRepository: governanceRepository,
            operationIdempotencyService: idempotencyService,
            configValues: new Dictionary<string, string?> { ["ExperienceCalculator:EnableCache"] = "false" });

        var result = await service.AdminAdjustExperienceAsync(
            userId,
            5,
            "补发活动经验",
            9001,
            "Auditor",
            3,
            "adjust-41002-v3");

        result.ShouldBeSameAs(replay);
        result.VoReplayed.ShouldBeTrue();
        governanceRepository.VerifyNoOtherCalls();
        idempotencyService.VerifyAll();
    }

    [Fact]
    public async Task AdminAdjustExperienceAsync_Should_Reject_Idempotency_Payload_Conflict()
    {
        const long userId = 41003;
        var governanceRepository = new Mock<IExperienceGovernanceRepository>(MockBehavior.Strict);
        var idempotencyService = new Mock<IOperationIdempotencyService>(MockBehavior.Strict);
        idempotencyService.Setup(service => service.NormalizeKey("adjust-41003-v3"))
            .Returns("adjust-41003-v3");
        idempotencyService.Setup(service => service.CreateRequestSnapshot(
                It.IsAny<IReadOnlyDictionary<string, object?>>()))
            .Returns(new OperationIdempotencyRequestSnapshot
            {
                RequestHash = "hash-b",
                RequestSummary = "summary"
            });
        idempotencyService.Setup(service => service.BeginAsync(It.IsAny<OperationIdempotencyBeginRequest>()))
            .ReturnsAsync(new OperationIdempotencyBeginResult
            {
                Status = OperationIdempotencyBeginStatus.Conflict,
                Message = "幂等键已用于其他经验调整内容"
            });
        var service = CreateService(
            userExpRepository: ExperienceTargetRepository(userId, 12, 3, totalExp: 120),
            experienceGovernanceRepository: governanceRepository,
            operationIdempotencyService: idempotencyService,
            configValues: new Dictionary<string, string?> { ["ExperienceCalculator:EnableCache"] = "false" });

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.AdminAdjustExperienceAsync(
                userId,
                5,
                "不同的补发原因",
                9001,
                "Auditor",
                3,
                "adjust-41003-v3"));

        exception.StatusCode.ShouldBe(409);
        exception.ErrorCode.ShouldBe(ExperienceGovernanceErrorCodes.AdjustmentIdempotencyConflict);
        governanceRepository.VerifyNoOtherCalls();
        idempotencyService.VerifyAll();
    }

    private static Mock<IBaseRepository<UserExperience>> ExperienceTargetRepository(
        long userId,
        long tenantId,
        int version,
        int currentLevel = 0,
        long currentExp = 0,
        long totalExp = 0)
    {
        var repository = new Mock<IBaseRepository<UserExperience>>(MockBehavior.Strict);
        repository
            .Setup(instance => instance.QueryFirstAsync(It.IsAny<Expression<Func<UserExperience, bool>>?>()))
            .ReturnsAsync(new UserExperience
            {
                Id = 90000 + userId,
                UserId = userId,
                TenantId = tenantId,
                Version = version,
                CurrentLevel = currentLevel,
                CurrentExp = currentExp,
                TotalExp = totalExp,
                IsDeleted = false
            });
        return repository;
    }

    private static Mock<IBaseRepository<User>> ExperienceUserRepository(
        long userId,
        long tenantId,
        string userName)
    {
        var user = new User
        {
            Id = userId,
            TenantId = tenantId,
            UserName = userName,
            IsDeleted = false
        };
        var repository = new Mock<IBaseRepository<User>>(MockBehavior.Strict);
        repository
            .Setup(instance => instance.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>?>()))
            .ReturnsAsync(user);
        repository
            .Setup(instance => instance.QueryAsync(It.IsAny<Expression<Func<User, bool>>?>()))
            .ReturnsAsync([user]);
        return repository;
    }

    private static Mock<IOperationIdempotencyService> CreateStartedIdempotencyService(long recordId)
    {
        var service = new Mock<IOperationIdempotencyService>(MockBehavior.Strict);
        service.Setup(instance => instance.NormalizeKey(It.IsAny<string?>()))
            .Returns((string? value) => value?.Trim());
        service.Setup(instance => instance.CreateRequestSnapshot(
                It.IsAny<IReadOnlyDictionary<string, object?>>()))
            .Returns(new OperationIdempotencyRequestSnapshot
            {
                RequestHash = "hash",
                RequestSummary = "summary"
            });
        service.Setup(instance => instance.BeginAsync(It.IsAny<OperationIdempotencyBeginRequest>()))
            .ReturnsAsync(new OperationIdempotencyBeginResult
            {
                Status = OperationIdempotencyBeginStatus.Started,
                RecordId = recordId
            });
        service.Setup(instance => instance.SerializeResponse(
                It.IsAny<AdminExperienceAdjustmentResultVo>()))
            .Returns("{}");
        service.Setup(instance => instance.SerializeResponse(
                It.IsAny<AdminExperienceGovernanceResultVo>()))
            .Returns("{}");
        service.Setup(instance => instance.CompleteSuccessAsync(
                It.IsAny<OperationIdempotencyCompletionRequest>()))
            .Returns(Task.CompletedTask);
        return service;
    }

    private static ExperienceService CreateService(
        Mock<IBaseRepository<UserExperience>>? userExpRepository = null,
        Mock<IBaseRepository<ExpTransaction>>? expTransactionRepository = null,
        Mock<IBaseRepository<LevelConfig>>? levelConfigRepository = null,
        Mock<IBaseRepository<UserExpDailyStats>>? dailyStatsRepository = null,
        Mock<IBaseRepository<User>>? userRepository = null,
        Mock<IExperienceGovernanceRepository>? experienceGovernanceRepository = null,
        Mock<IExperienceCalculator>? experienceCalculator = null,
        Mock<ICoinService>? coinService = null,
        Mock<IAttachmentUrlResolver>? attachmentUrlResolver = null,
        Mock<INotificationService>? notificationService = null,
        Mock<IOperationIdempotencyService>? operationIdempotencyService = null,
        ICaching? caching = null,
        Dictionary<string, string?>? configValues = null,
        DateTimeOffset? utcNow = null,
        string timeZoneId = "Asia/Shanghai")
    {
        var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        InitializeAppSettings(configValues);
        TimeProvider timeProvider = utcNow.HasValue
            ? new FixedTimeProvider(utcNow.Value)
            : TimeProvider.System;
        var businessCalendar = new BusinessCalendar(
            timeProvider,
            Options.Create(new TimeOptions { DefaultTimeZoneId = timeZoneId }));
        var resolvedLevelConfigRepository = levelConfigRepository
            ?? new Mock<IBaseRepository<LevelConfig>>(MockBehavior.Loose);
        if (levelConfigRepository == null)
        {
            resolvedLevelConfigRepository
                .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<LevelConfig, bool>>?>()))
                .ReturnsAsync([]);
        }

        var resolvedUserRepository = userRepository
            ?? new Mock<IBaseRepository<User>>(MockBehavior.Loose);
        if (userRepository == null)
        {
            resolvedUserRepository
                .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<User, bool>>?>()))
                .ReturnsAsync([]);
        }

        return new ExperienceService(
            CreateMapper(),
            (userExpRepository ?? new Mock<IBaseRepository<UserExperience>>(MockBehavior.Loose)).Object,
            (expTransactionRepository ?? new Mock<IBaseRepository<ExpTransaction>>(MockBehavior.Loose)).Object,
            resolvedLevelConfigRepository.Object,
            (dailyStatsRepository ?? new Mock<IBaseRepository<UserExpDailyStats>>(MockBehavior.Loose)).Object,
            resolvedUserRepository.Object,
            (experienceGovernanceRepository ?? new Mock<IExperienceGovernanceRepository>(MockBehavior.Loose)).Object,
            (experienceCalculator ?? new Mock<IExperienceCalculator>(MockBehavior.Loose)).Object,
            (coinService ?? new Mock<ICoinService>(MockBehavior.Loose)).Object,
            (attachmentUrlResolver ?? new Mock<IAttachmentUrlResolver>(MockBehavior.Loose)).Object,
            (notificationService ?? new Mock<INotificationService>(MockBehavior.Loose)).Object,
            caching ?? new Caching(distributedCache),
            timeProvider,
            businessCalendar,
            operationIdempotencyService: operationIdempotencyService?.Object);
    }

    private static IMapper CreateMapper()
    {
        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        mapper
            .Setup(service => service.Map<List<LevelConfigVo>>(It.IsAny<object>()))
            .Returns((object source) =>
            {
                var levelConfigs = (source as IEnumerable<LevelConfig>)?.ToList() ?? [];
                return levelConfigs.Select(MapLevelConfig).ToList();
            });
        mapper
            .Setup(service => service.Map<List<UserExpDailyStatsVo>>(It.IsAny<object>()))
            .Returns((object source) =>
            {
                var dailyStats = (source as IEnumerable<UserExpDailyStats>)?.ToList() ?? [];
                return dailyStats.Select(MapDailyStats).ToList();
            });
        mapper
            .Setup(service => service.Map<List<ExpTransactionVo>>(It.IsAny<object>()))
            .Returns((object source) =>
            {
                var transactions = (source as IEnumerable<ExpTransaction>)?.ToList() ?? [];
                return transactions.Select(MapExpTransaction).ToList();
            });
        mapper
            .Setup(service => service.Map<LevelConfigVo>(It.IsAny<object>()))
            .Returns((object source) => MapLevelConfig((LevelConfig)source));
        mapper
            .Setup(service => service.Map<UserExperienceVo>(It.IsAny<object>()))
            .Returns((object source) =>
            {
                var item = (UserExperience)source;
                return new UserExperienceVo
                {
                    VoUserId = item.UserId,
                    VoCurrentLevel = item.CurrentLevel,
                    VoCurrentExp = item.CurrentExp,
                    VoTotalExp = item.TotalExp,
                    VoLevelUpAt = item.LevelUpAt,
                    VoExpFrozen = item.ExpFrozen,
                    VoFrozenUntil = item.FrozenUntil,
                    VoFrozenReason = item.FrozenReason,
                    VoVersion = item.Version
                };
            });
        mapper
            .Setup(service => service.Map<ExpTransactionVo>(It.IsAny<object>()))
            .Returns((object source) => MapExpTransaction((ExpTransaction)source));
        return mapper.Object;
    }

    private static LevelConfig CloneLevelConfig(LevelConfig source)
    {
        return new LevelConfig
        {
            Level = source.Level,
            LevelName = source.LevelName,
            ExpRequired = source.ExpRequired,
            ExpCumulative = source.ExpCumulative,
            ThemeColor = source.ThemeColor,
            IconAttachmentId = source.IconAttachmentId,
            BadgeAttachmentId = source.BadgeAttachmentId,
            Description = source.Description,
            Privileges = source.Privileges,
            IsEnabled = source.IsEnabled,
            SortOrder = source.SortOrder,
            CreateTime = source.CreateTime,
            CreateBy = source.CreateBy,
            CreateId = source.CreateId,
            ModifyTime = source.ModifyTime,
            ModifyBy = source.ModifyBy,
            ModifyId = source.ModifyId
        };
    }

    private static void InitializeAppSettings(Dictionary<string, string?>? configValues)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues ?? new Dictionary<string, string?>())
            .Build();
        _ = new AppSettingsTool(configuration);
    }

    private static LevelConfigVo MapLevelConfig(LevelConfig source)
    {
        return new LevelConfigVo
        {
            VoLevel = source.Level,
            VoLevelName = source.LevelName,
            VoExpRequired = source.ExpRequired,
            VoExpCumulative = source.ExpCumulative,
            VoThemeColor = source.ThemeColor,
            VoIconAttachmentId = source.IconAttachmentId,
            VoBadgeAttachmentId = source.BadgeAttachmentId,
            VoDescription = source.Description,
            VoPrivileges = [],
            VoIsEnabled = source.IsEnabled,
            VoSortOrder = source.SortOrder
        };
    }

    private static UserExpDailyStatsVo MapDailyStats(UserExpDailyStats source)
    {
        return new UserExpDailyStatsVo
        {
            VoId = source.Id,
            VoUserId = source.UserId,
            VoStatDate = DateOnly.FromDateTime(source.StatDate),
            VoExpEarned = source.ExpEarned,
            VoExpFromPost = source.ExpFromPost,
            VoExpFromComment = source.ExpFromComment,
            VoExpFromLike = source.ExpFromLike,
            VoExpFromHighlight = source.ExpFromHighlight,
            VoExpFromLogin = source.ExpFromLogin,
            VoPostCount = source.PostCount,
            VoCommentCount = source.CommentCount,
            VoLikeGivenCount = source.LikeGivenCount,
            VoLikeReceivedCount = source.LikeReceivedCount,
            VoObservations = []
        };
    }

    private static ExpTransactionVo MapExpTransaction(ExpTransaction source)
    {
        return new ExpTransactionVo
        {
            VoId = source.Id,
            VoUserId = source.UserId,
            VoExpType = source.ExpType,
            VoExpTypeDisplay = source.ExpType switch
            {
                "ADMIN_ADJUST" => "管理员调整",
                "PENALTY" => "惩罚扣除",
                _ => source.ExpType
            },
            VoExpAmount = source.ExpAmount,
            VoBusinessType = source.BusinessType,
            VoBusinessId = source.BusinessId,
            VoRemark = source.Remark,
            VoExpBefore = source.ExpBefore,
            VoExpAfter = source.ExpAfter,
            VoLevelBefore = source.LevelBefore,
            VoLevelAfter = source.LevelAfter,
            VoOperatorId = source.CreateId,
            VoOperatorName = source.CreateBy,
            VoCreateTime = source.CreateTime
        };
    }

    private static DateTime ToBusinessDateStorage(DateOnly date)
    {
        return date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
