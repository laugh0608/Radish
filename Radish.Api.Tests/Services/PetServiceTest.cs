using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Radish.Common.Exceptions;
using Radish.Extension.AutoMapperExtension.CustomProfiles;
using Radish.IRepository.Base;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Service;
using Radish.Shared.CustomEnum;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Services;

public class PetServiceTest
{
    [Fact]
    public async Task ClaimAsync_Should_Create_Default_Pet_When_NotClaimed()
    {
        var mapper = CreateMapper();
        var petRepository = new Mock<IBaseRepository<PetProfile>>(MockBehavior.Strict);
        var logRepository = CreateLogRepository();

        petRepository
            .Setup(repo => repo.QueryFirstAsync(It.IsAny<Expression<Func<PetProfile, bool>>?>()))
            .ReturnsAsync((PetProfile?)null);
        petRepository
            .Setup(repo => repo.AddAsync(It.Is<PetProfile>(pet =>
                pet.UserId == 1001 &&
                pet.Name == "小萝卜" &&
                pet.TenantId == 9 &&
                pet.CreateBy == "Tester" &&
                pet.CreateId == 1001 &&
                pet.PublicId.StartsWith(PetProfile.PublicIdPrefix, StringComparison.Ordinal))))
            .ReturnsAsync(7001);

        var service = new PetService(mapper, petRepository.Object, logRepository.Object);

        var result = await service.ClaimAsync(1001, "Tester", 9, new PetClaimDto { Name = "   " });

        Assert.Equal(7001, result.VoId);
        Assert.Equal("小萝卜", result.VoName);
        Assert.Equal(4, result.VoCareActions.Count);
        petRepository.Verify(repo => repo.AddAsync(It.IsAny<PetProfile>()), Times.Once);
    }

    [Fact]
    public async Task ClaimAsync_Should_Return_Existing_Pet_When_AlreadyClaimed()
    {
        var pet = CreatePet();
        var mapper = CreateMapper();
        var petRepository = new Mock<IBaseRepository<PetProfile>>(MockBehavior.Strict);
        var logRepository = CreateLogRepository();

        petRepository
            .Setup(repo => repo.QueryFirstAsync(It.IsAny<Expression<Func<PetProfile, bool>>?>()))
            .ReturnsAsync(pet);

        var service = new PetService(mapper, petRepository.Object, logRepository.Object);

        var result = await service.ClaimAsync(1001, "Tester", 0, new PetClaimDto { Name = "新名字" });

        Assert.Equal(pet.Id, result.VoId);
        Assert.Equal("小萝卜", result.VoName);
        petRepository.Verify(repo => repo.AddAsync(It.IsAny<PetProfile>()), Times.Never);
    }

    [Fact]
    public async Task GetMyPetAsync_Should_Return_Action_States_Without_Write()
    {
        var pet = CreatePet();
        var mapper = CreateMapper();
        var petRepository = new Mock<IBaseRepository<PetProfile>>(MockBehavior.Strict);
        var logRepository = CreateLogRepository();

        petRepository
            .Setup(repo => repo.QueryFirstAsync(It.IsAny<Expression<Func<PetProfile, bool>>?>()))
            .ReturnsAsync(pet);

        var service = new PetService(mapper, petRepository.Object, logRepository.Object);

        var result = await service.GetMyPetAsync(1001);

        Assert.NotNull(result);
        Assert.Equal(pet.Id, result.VoId);
        Assert.Equal(4, result.VoCareActions.Count);
        Assert.All(result.VoCareActions, action => Assert.True(action.VoCanUse));
        petRepository.Verify(repo => repo.AddAsync(It.IsAny<PetProfile>()), Times.Never);
        petRepository.Verify(repo => repo.UpdateAsync(It.IsAny<PetProfile>()), Times.Never);
        logRepository.Verify(repo => repo.AddAsync(It.IsAny<PetStatLog>()), Times.Never);
    }

    [Fact]
    public async Task CareAsync_Should_Update_Pet_And_Write_Log()
    {
        var pet = CreatePet();
        var mapper = CreateMapper();
        var petRepository = new Mock<IBaseRepository<PetProfile>>(MockBehavior.Strict);
        var logRepository = CreateLogRepository();

        petRepository
            .Setup(repo => repo.QueryFirstAsync(It.IsAny<Expression<Func<PetProfile, bool>>?>()))
            .ReturnsAsync(pet);
        petRepository
            .Setup(repo => repo.UpdateAsync(It.Is<PetProfile>(updated =>
                updated.Satiety == 94 &&
                updated.Cleanliness == 70 &&
                updated.Energy == 66 &&
                updated.GrowthValue == 5 &&
                updated.GrowthStage == 1 &&
                updated.ModifyBy == "Tester" &&
                updated.ModifyId == 1001)))
            .ReturnsAsync(true);
        logRepository
            .Setup(repo => repo.AddAsync(It.Is<PetStatLog>(entry =>
                entry.UserId == 1001 &&
                entry.PetProfileId == pet.Id &&
                entry.ActionType == PetCareActionTypes.Feed &&
                entry.BeforeSatiety == 70 &&
                entry.AfterSatiety == 94 &&
                entry.GrowthDelta == 5 &&
                entry.IdempotencyKey == "care-1")))
            .ReturnsAsync(9001);

        var service = new PetService(mapper, petRepository.Object, logRepository.Object);

        var result = await service.CareAsync(1001, "Tester", new PetCareDto
        {
            ActionType = PetCareActionTypes.Feed,
            IdempotencyKey = "care-1"
        });

        Assert.Equal(94, result.VoPet.VoSatiety);
        Assert.Equal(9001, result.VoLog.VoId);
        Assert.Equal(PetCareActionTypes.Feed, result.VoLog.VoActionType);
        petRepository.Verify(repo => repo.UpdateAsync(It.IsAny<PetProfile>()), Times.Once);
        logRepository.Verify(repo => repo.AddAsync(It.IsAny<PetStatLog>()), Times.Once);
    }

    [Fact]
    public async Task CareAsync_Should_Return_Existing_Log_When_Idempotency_Replayed()
    {
        var pet = CreatePet();
        pet.Satiety = 94;
        pet.Energy = 66;
        pet.GrowthValue = 5;
        var existingLog = CreateLog(
            id: 9001,
            pet: pet,
            actionType: PetCareActionTypes.Feed,
            idempotencyKey: "care-1",
            createTime: DateTime.UtcNow.AddMinutes(-5),
            beforeSatiety: 70,
            afterSatiety: 94,
            beforeCleanliness: 70,
            afterCleanliness: 70,
            beforeEnergy: 70,
            afterEnergy: 66,
            growthDelta: 5);
        var mapper = CreateMapper();
        var petRepository = new Mock<IBaseRepository<PetProfile>>(MockBehavior.Strict);
        var logRepository = CreateLogRepository([existingLog]);

        petRepository
            .Setup(repo => repo.QueryFirstAsync(It.IsAny<Expression<Func<PetProfile, bool>>?>()))
            .ReturnsAsync(pet);

        var service = new PetService(mapper, petRepository.Object, logRepository.Object);

        var result = await service.CareAsync(1001, "Tester", new PetCareDto
        {
            ActionType = PetCareActionTypes.Feed,
            IdempotencyKey = "care-1"
        });

        Assert.Equal(9001, result.VoLog.VoId);
        Assert.Equal(94, result.VoPet.VoSatiety);
        Assert.Equal("小萝卜吃饱了一些。", result.VoMessage);
        petRepository.Verify(repo => repo.UpdateAsync(It.IsAny<PetProfile>()), Times.Never);
        logRepository.Verify(repo => repo.AddAsync(It.IsAny<PetStatLog>()), Times.Never);
    }

    [Fact]
    public async Task CareAsync_Should_Reject_When_DailyLimit_Reached()
    {
        var pet = CreatePet();
        var mapper = CreateMapper();
        var petRepository = new Mock<IBaseRepository<PetProfile>>(MockBehavior.Strict);
        var today = DateTime.UtcNow.Date;
        var logs = new List<PetStatLog>
        {
            CreateLog(8001, pet, PetCareActionTypes.Feed, "feed-1", today.AddHours(1)),
            CreateLog(8002, pet, PetCareActionTypes.Feed, "feed-2", today.AddHours(2)),
            CreateLog(8003, pet, PetCareActionTypes.Feed, "feed-3", today.AddHours(3))
        };
        var logRepository = CreateLogRepository(logs);

        petRepository
            .Setup(repo => repo.QueryFirstAsync(It.IsAny<Expression<Func<PetProfile, bool>>?>()))
            .ReturnsAsync(pet);

        var service = new PetService(mapper, petRepository.Object, logRepository.Object);

        var error = await Assert.ThrowsAsync<BusinessException>(() => service.CareAsync(1001, "Tester", new PetCareDto
        {
            ActionType = PetCareActionTypes.Feed,
            IdempotencyKey = "care-limit"
        }));

        Assert.Equal("Pet.DailyLimitReached", error.ErrorCode);
        petRepository.Verify(repo => repo.UpdateAsync(It.IsAny<PetProfile>()), Times.Never);
        logRepository.Verify(repo => repo.AddAsync(It.IsAny<PetStatLog>()), Times.Never);
    }

    [Fact]
    public async Task CareAsync_Should_Reject_When_Cooldown_Active()
    {
        var pet = CreatePet();
        var mapper = CreateMapper();
        var petRepository = new Mock<IBaseRepository<PetProfile>>(MockBehavior.Strict);
        var logs = new List<PetStatLog>
        {
            CreateLog(8001, pet, PetCareActionTypes.Feed, "feed-1", DateTime.UtcNow.AddMinutes(-10))
        };
        var logRepository = CreateLogRepository(logs);

        petRepository
            .Setup(repo => repo.QueryFirstAsync(It.IsAny<Expression<Func<PetProfile, bool>>?>()))
            .ReturnsAsync(pet);

        var service = new PetService(mapper, petRepository.Object, logRepository.Object);

        var error = await Assert.ThrowsAsync<BusinessException>(() => service.CareAsync(1001, "Tester", new PetCareDto
        {
            ActionType = PetCareActionTypes.Feed,
            IdempotencyKey = "care-cooldown"
        }));

        Assert.Equal("Pet.CareCooldown", error.ErrorCode);
        petRepository.Verify(repo => repo.UpdateAsync(It.IsAny<PetProfile>()), Times.Never);
        logRepository.Verify(repo => repo.AddAsync(It.IsAny<PetStatLog>()), Times.Never);
    }

    [Fact]
    public async Task CareAsync_Should_Clamp_Stats_And_Update_Stage_And_Mood()
    {
        var pet = CreatePet();
        pet.Satiety = 90;
        pet.Energy = 2;
        pet.GrowthValue = 118;
        var mapper = CreateMapper();
        var petRepository = new Mock<IBaseRepository<PetProfile>>(MockBehavior.Strict);
        var logRepository = CreateLogRepository();

        petRepository
            .Setup(repo => repo.QueryFirstAsync(It.IsAny<Expression<Func<PetProfile, bool>>?>()))
            .ReturnsAsync(pet);
        petRepository
            .Setup(repo => repo.UpdateAsync(It.Is<PetProfile>(updated =>
                updated.Satiety == 100 &&
                updated.Cleanliness == 70 &&
                updated.Energy == 0 &&
                updated.GrowthValue == 123 &&
                updated.GrowthStage == 3 &&
                updated.Mood == PetMoodTypes.Tired)))
            .ReturnsAsync(true);
        logRepository
            .Setup(repo => repo.AddAsync(It.Is<PetStatLog>(entry =>
                entry.BeforeSatiety == 90 &&
                entry.AfterSatiety == 100 &&
                entry.BeforeEnergy == 2 &&
                entry.AfterEnergy == 0 &&
                entry.GrowthDelta == 5)))
            .ReturnsAsync(9002);

        var service = new PetService(mapper, petRepository.Object, logRepository.Object);

        var result = await service.CareAsync(1001, "Tester", new PetCareDto
        {
            ActionType = PetCareActionTypes.Feed,
            IdempotencyKey = "care-clamp"
        });

        Assert.Equal(100, result.VoPet.VoSatiety);
        Assert.Equal(0, result.VoPet.VoEnergy);
        Assert.Equal(3, result.VoPet.VoGrowthStage);
        Assert.Equal(PetMoodTypes.Tired, result.VoPet.VoMood);
    }

    [Fact]
    public async Task GetMyPetAsync_Should_Report_DailyLimit_And_Cooldown_ActionStates()
    {
        var pet = CreatePet();
        var now = DateTime.UtcNow;
        var cleanCreateTime = now.AddMinutes(-10);
        if (cleanCreateTime.Date != now.Date)
        {
            cleanCreateTime = now.Date;
        }

        var logs = new List<PetStatLog>
        {
            CreateLog(8001, pet, PetCareActionTypes.Feed, "feed-1", now.Date.AddHours(1)),
            CreateLog(8002, pet, PetCareActionTypes.Feed, "feed-2", now.Date.AddHours(2)),
            CreateLog(8003, pet, PetCareActionTypes.Feed, "feed-3", now.Date.AddHours(3)),
            CreateLog(8101, pet, PetCareActionTypes.Clean, "clean-1", cleanCreateTime)
        };
        var mapper = CreateMapper();
        var petRepository = new Mock<IBaseRepository<PetProfile>>(MockBehavior.Strict);
        var logRepository = CreateLogRepository(logs);

        petRepository
            .Setup(repo => repo.QueryFirstAsync(It.IsAny<Expression<Func<PetProfile, bool>>?>()))
            .ReturnsAsync(pet);

        var service = new PetService(mapper, petRepository.Object, logRepository.Object);

        var result = await service.GetMyPetAsync(1001);

        Assert.NotNull(result);
        var feed = result.VoCareActions.Single(action => action.VoActionType == PetCareActionTypes.Feed);
        var clean = result.VoCareActions.Single(action => action.VoActionType == PetCareActionTypes.Clean);
        var play = result.VoCareActions.Single(action => action.VoActionType == PetCareActionTypes.Play);

        Assert.Equal(0, feed.VoRemainingToday);
        Assert.False(feed.VoCanUse);
        Assert.Equal(2, clean.VoRemainingToday);
        Assert.False(clean.VoCanUse);
        Assert.NotNull(clean.VoNextAvailableAt);
        Assert.Equal(3, play.VoRemainingToday);
        Assert.True(play.VoCanUse);
    }

    [Fact]
    public async Task GetMyLogsAsync_Should_Return_Empty_Page_When_NotClaimed()
    {
        var mapper = CreateMapper();
        var petRepository = new Mock<IBaseRepository<PetProfile>>(MockBehavior.Strict);
        var logRepository = new Mock<IBaseRepository<PetStatLog>>(MockBehavior.Strict);

        petRepository
            .Setup(repo => repo.QueryFirstAsync(It.IsAny<Expression<Func<PetProfile, bool>>?>()))
            .ReturnsAsync((PetProfile?)null);

        var service = new PetService(mapper, petRepository.Object, logRepository.Object);

        var result = await service.GetMyLogsAsync(1001, 0, 0);

        Assert.Empty(result.VoItems);
        Assert.Equal(0, result.VoTotal);
        Assert.Equal(1, result.VoPageIndex);
        Assert.Equal(20, result.VoPageSize);
        logRepository.Verify(
            repo => repo.QueryPageAsync(
                It.IsAny<Expression<Func<PetStatLog, bool>>?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Expression<Func<PetStatLog, object>>?>(),
                It.IsAny<OrderByType>(),
                It.IsAny<Expression<Func<PetStatLog, object>>?>(),
                It.IsAny<OrderByType>()),
            Times.Never);
    }

    private static IMapper CreateMapper()
    {
        var configuration = new MapperConfiguration(
            cfg => cfg.AddProfile<PetMappingProfile>(),
            NullLoggerFactory.Instance);
        return configuration.CreateMapper();
    }

    private static PetProfile CreatePet()
    {
        return new PetProfile
        {
            Id = 7001,
            UserId = 1001,
            PublicId = "pet_018f6b6f7c7d70008f8f8f8f8f8f8f8f",
            Name = "小萝卜",
            Satiety = 70,
            Cleanliness = 70,
            Energy = 70,
            GrowthValue = 0,
            GrowthStage = 1,
            TenantId = 0,
            IsDeleted = false
        };
    }

    private static PetStatLog CreateLog(
        long id,
        PetProfile pet,
        string actionType,
        string idempotencyKey,
        DateTime createTime,
        int beforeSatiety = 70,
        int afterSatiety = 70,
        int beforeCleanliness = 70,
        int afterCleanliness = 70,
        int beforeEnergy = 70,
        int afterEnergy = 70,
        long growthDelta = 0)
    {
        return new PetStatLog
        {
            Id = id,
            UserId = pet.UserId,
            PetProfileId = pet.Id,
            PetPublicId = pet.PublicId,
            ActionType = actionType,
            Source = "care",
            IdempotencyKey = idempotencyKey,
            BeforeSatiety = beforeSatiety,
            AfterSatiety = afterSatiety,
            BeforeCleanliness = beforeCleanliness,
            AfterCleanliness = afterCleanliness,
            BeforeEnergy = beforeEnergy,
            AfterEnergy = afterEnergy,
            GrowthDelta = growthDelta,
            Message = actionType switch
            {
                PetCareActionTypes.Feed => "小萝卜吃饱了一些。",
                PetCareActionTypes.Clean => "小萝卜变清爽了。",
                PetCareActionTypes.Play => "小萝卜玩得很开心。",
                PetCareActionTypes.Rest => "小萝卜恢复了精力。",
                _ => "状态已变化。"
            },
            TenantId = pet.TenantId,
            CreateTime = createTime,
            CreateBy = "Tester",
            CreateId = pet.UserId
        };
    }

    private static Mock<IBaseRepository<PetStatLog>> CreateLogRepository(IEnumerable<PetStatLog>? logs = null)
    {
        var logItems = logs?.ToList() ?? new List<PetStatLog>();
        var logRepository = new Mock<IBaseRepository<PetStatLog>>(MockBehavior.Strict);
        logRepository
            .Setup(repo => repo.QueryFirstAsync(It.IsAny<Expression<Func<PetStatLog, bool>>?>()))
            .ReturnsAsync((Expression<Func<PetStatLog, bool>>? predicate) =>
                QueryLogs(logItems, predicate).FirstOrDefault());
        logRepository
            .Setup(repo => repo.QueryCountAsync(It.IsAny<Expression<Func<PetStatLog, bool>>?>()))
            .ReturnsAsync((Expression<Func<PetStatLog, bool>>? predicate) =>
                QueryLogs(logItems, predicate).Count);
        logRepository
            .Setup(repo => repo.QueryPageAsync(
                It.IsAny<Expression<Func<PetStatLog, bool>>?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Expression<Func<PetStatLog, object>>?>(),
                It.IsAny<OrderByType>(),
                It.IsAny<Expression<Func<PetStatLog, object>>?>(),
                It.IsAny<OrderByType>()))
            .ReturnsAsync((
                Expression<Func<PetStatLog, bool>>? predicate,
                int pageIndex,
                int pageSize,
                Expression<Func<PetStatLog, object>>? _,
                OrderByType __,
                Expression<Func<PetStatLog, object>>? ___,
                OrderByType ____) =>
            {
                var filtered = QueryLogs(logItems, predicate)
                    .OrderByDescending(log => log.CreateTime)
                    .ThenByDescending(log => log.Id)
                    .ToList();
                var safePageIndex = pageIndex < 1 ? 1 : pageIndex;
                var safePageSize = pageSize <= 0 ? 20 : pageSize;
                var data = filtered
                    .Skip((safePageIndex - 1) * safePageSize)
                    .Take(safePageSize)
                    .ToList();
                return (data, filtered.Count);
            });
        return logRepository;
    }

    private static List<PetStatLog> QueryLogs(
        List<PetStatLog> logs,
        Expression<Func<PetStatLog, bool>>? predicate)
    {
        return predicate == null ? logs : logs.Where(predicate.Compile()).ToList();
    }
}
