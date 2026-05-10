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
}
