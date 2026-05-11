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
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service;
using Shouldly;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Services;

public class ExperienceServiceTest
{
    [Fact]
    public async Task GrantExperienceAsync_ShouldRespectConfiguredDailyLimit()
    {
        const long userId = 9527;

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

        dailyStatsRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<UserExpDailyStats, bool>>?>()))
            .ReturnsAsync(new UserExpDailyStats
            {
                Id = 8001,
                UserId = userId,
                StatDate = DateTime.Today,
                ExpEarned = 9,
                ExpFromPost = 4
            });

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
            });

        var success = await service.GrantExperienceAsync(userId, 2, "POST_CREATE");

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
        var statDate = new DateTime(2026, 5, 10, 9, 30, 0);
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
        await service.UpdateDailyStatsAsync(userId, "GIVE_LIKE", 2, statDate.AddHours(2));

        storedStats.Count.ShouldBe(1);
        var stats = storedStats[0];
        stats.UserId.ShouldBe(userId);
        stats.StatDate.ShouldBe(statDate.Date);
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
        var endDate = DateTime.Today;
        var existingStats = new List<UserExpDailyStats>
        {
            new()
            {
                Id = 9101,
                UserId = userId,
                StatDate = endDate,
                ExpEarned = 40,
                ExpFromHighlight = 30,
                ExpFromComment = 10,
                CommentCount = 2
            },
            new()
            {
                Id = 9102,
                UserId = userId,
                StatDate = endDate.AddDays(-2),
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

        var service = CreateService(dailyStatsRepository: dailyStatsRepository);

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
        var endDate = DateTime.Today;
        var existingStats = new List<UserExpDailyStats>
        {
            new()
            {
                Id = 9201,
                UserId = userId,
                StatDate = endDate,
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

        var service = CreateService(dailyStatsRepository: dailyStatsRepository);

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
        var endDate = DateTime.Today;
        var existingStats = new List<UserExpDailyStats>
        {
            new()
            {
                Id = 9301,
                UserId = userId,
                StatDate = endDate,
                ExpEarned = 30,
                ExpFromLike = 24,
                ExpFromComment = 6,
                LikeReceivedCount = 12
            },
            new()
            {
                Id = 9302,
                UserId = userId,
                StatDate = endDate.AddDays(-1),
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

        var service = CreateService(dailyStatsRepository: dailyStatsRepository);

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
        var endDate = DateTime.Today;
        var existingStats = new List<UserExpDailyStats>
        {
            new()
            {
                Id = 9401,
                UserId = userId,
                StatDate = endDate,
                ExpEarned = 50,
                ExpFromLike = 50,
                LikeReceivedCount = 25
            },
            new()
            {
                Id = 9402,
                UserId = userId,
                StatDate = endDate.AddDays(-2),
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

        var service = CreateService(dailyStatsRepository: dailyStatsRepository);

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
    public async Task AdminAdjustExperienceAsync_Should_Clamp_To_Zero_And_Record_Penalty_Transaction()
    {
        const long userId = 41001;
        ExpTransaction? capturedTransaction = null;

        var userExpRepository = new Mock<IBaseRepository<UserExperience>>(MockBehavior.Strict);
        var expTransactionRepository = new Mock<IBaseRepository<ExpTransaction>>(MockBehavior.Strict);
        var levelConfigRepository = new Mock<IBaseRepository<LevelConfig>>(MockBehavior.Strict);

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
        expTransactionRepository
            .Setup(repository => repository.AddAsync(It.IsAny<ExpTransaction>()))
            .Callback<ExpTransaction>(transaction => capturedTransaction = transaction)
            .ReturnsAsync(90001);

        var service = CreateService(
            userExpRepository: userExpRepository,
            expTransactionRepository: expTransactionRepository,
            levelConfigRepository: levelConfigRepository,
            configValues: new Dictionary<string, string?>
            {
                ["ExperienceCalculator:EnableCache"] = "false"
            });

        var success = await service.AdminAdjustExperienceAsync(userId, -120, "回收异常经验", 9001, "Auditor");

        success.ShouldBeTrue();
        capturedTransaction.ShouldNotBeNull();
        capturedTransaction.ExpType.ShouldBe("PENALTY");
        capturedTransaction.ExpAmount.ShouldBe(-80);
        capturedTransaction.ExpBefore.ShouldBe(80);
        capturedTransaction.ExpAfter.ShouldBe(0);
        capturedTransaction.LevelBefore.ShouldBe(1);
        capturedTransaction.LevelAfter.ShouldBe(0);
        capturedTransaction.CreateBy.ShouldBe("Auditor");
        capturedTransaction.CreateId.ShouldBe(9001);
    }

    private static ExperienceService CreateService(
        Mock<IBaseRepository<UserExperience>>? userExpRepository = null,
        Mock<IBaseRepository<ExpTransaction>>? expTransactionRepository = null,
        Mock<IBaseRepository<LevelConfig>>? levelConfigRepository = null,
        Mock<IBaseRepository<UserExpDailyStats>>? dailyStatsRepository = null,
        Mock<IBaseRepository<User>>? userRepository = null,
        Mock<IExperienceCalculator>? experienceCalculator = null,
        Mock<ICoinService>? coinService = null,
        Mock<IAttachmentUrlResolver>? attachmentUrlResolver = null,
        Mock<INotificationService>? notificationService = null,
        ICaching? caching = null,
        Dictionary<string, string?>? configValues = null)
    {
        var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        InitializeAppSettings(configValues);

        return new ExperienceService(
            CreateMapper(),
            (userExpRepository ?? new Mock<IBaseRepository<UserExperience>>(MockBehavior.Loose)).Object,
            (expTransactionRepository ?? new Mock<IBaseRepository<ExpTransaction>>(MockBehavior.Loose)).Object,
            (levelConfigRepository ?? new Mock<IBaseRepository<LevelConfig>>(MockBehavior.Loose)).Object,
            (dailyStatsRepository ?? new Mock<IBaseRepository<UserExpDailyStats>>(MockBehavior.Loose)).Object,
            (userRepository ?? new Mock<IBaseRepository<User>>(MockBehavior.Loose)).Object,
            (experienceCalculator ?? new Mock<IExperienceCalculator>(MockBehavior.Loose)).Object,
            (coinService ?? new Mock<ICoinService>(MockBehavior.Loose)).Object,
            (attachmentUrlResolver ?? new Mock<IAttachmentUrlResolver>(MockBehavior.Loose)).Object,
            (notificationService ?? new Mock<INotificationService>(MockBehavior.Loose)).Object,
            caching ?? new Caching(distributedCache));
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
            VoStatDate = source.StatDate,
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
}
