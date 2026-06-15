using System;
using System.Collections.Generic;
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
    public async Task ClaimAsync_Should_Return_Existing_Pet_When_AlreadyClaimed()
    {
        var pet = CreatePet();
        var mapper = CreateMapper();
        var petRepository = new Mock<IBaseRepository<PetProfile>>(MockBehavior.Strict);
        var logRepository = CreateRelaxedLogRepository();

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
    public async Task CareAsync_Should_Update_Pet_And_Write_Log()
    {
        var pet = CreatePet();
        var mapper = CreateMapper();
        var petRepository = new Mock<IBaseRepository<PetProfile>>(MockBehavior.Strict);
        var logRepository = CreateRelaxedLogRepository();

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
    public async Task CareAsync_Should_Reject_When_DailyLimit_Reached()
    {
        var pet = CreatePet();
        var mapper = CreateMapper();
        var petRepository = new Mock<IBaseRepository<PetProfile>>(MockBehavior.Strict);
        var logRepository = new Mock<IBaseRepository<PetStatLog>>(MockBehavior.Strict);

        petRepository
            .Setup(repo => repo.QueryFirstAsync(It.IsAny<Expression<Func<PetProfile, bool>>?>()))
            .ReturnsAsync(pet);
        logRepository
            .Setup(repo => repo.QueryFirstAsync(It.IsAny<Expression<Func<PetStatLog, bool>>?>()))
            .ReturnsAsync((PetStatLog?)null);
        logRepository
            .Setup(repo => repo.QueryCountAsync(It.IsAny<Expression<Func<PetStatLog, bool>>?>()))
            .ReturnsAsync(3);

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

    private static Mock<IBaseRepository<PetStatLog>> CreateRelaxedLogRepository()
    {
        var logRepository = new Mock<IBaseRepository<PetStatLog>>(MockBehavior.Strict);
        logRepository
            .Setup(repo => repo.QueryFirstAsync(It.IsAny<Expression<Func<PetStatLog, bool>>?>()))
            .ReturnsAsync((PetStatLog?)null);
        logRepository
            .Setup(repo => repo.QueryCountAsync(It.IsAny<Expression<Func<PetStatLog, bool>>?>()))
            .ReturnsAsync(0);
        logRepository
            .Setup(repo => repo.QueryPageAsync(
                It.IsAny<Expression<Func<PetStatLog, bool>>?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Expression<Func<PetStatLog, object>>?>(),
                It.IsAny<OrderByType>(),
                It.IsAny<Expression<Func<PetStatLog, object>>?>(),
                It.IsAny<OrderByType>()))
            .ReturnsAsync((new List<PetStatLog>(), 0));
        return logRepository;
    }
}
