using System;
using System.Threading.Tasks;
using Moq;
using Radish.Common.HelpTool;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Service;
using Radish.Shared.Constants;
using Xunit;

namespace Radish.Api.Tests.Services;

public class BootstrapServiceTest
{
    [Fact]
    public async Task GetStatusAsync_ShouldRequireInitialization_WhenNoAdministratorExists()
    {
        var repository = new Mock<IBootstrapRepository>(MockBehavior.Strict);
        repository
            .Setup(r => r.AdministratorExistsAsync())
            .ReturnsAsync(false);
        var coinService = new Mock<ICoinService>(MockBehavior.Strict);
        var systemSettingProvider = CreateSystemSettingProvider();

        var service = new BootstrapService(repository.Object, coinService.Object, systemSettingProvider.Object);

        var status = await service.GetStatusAsync();

        Assert.True(status.VoRequiresAdminInitialization);
        Assert.False(status.VoAdministratorExists);
    }

    [Fact]
    public async Task CreateFirstAdministratorAsync_ShouldRejectWeakDefaultPassword()
    {
        var repository = new Mock<IBootstrapRepository>(MockBehavior.Strict);
        var coinService = new Mock<ICoinService>(MockBehavior.Strict);
        var systemSettingProvider = CreateSystemSettingProvider();
        var service = new BootstrapService(repository.Object, coinService.Object, systemSettingProvider.Object);

        var result = await service.CreateFirstAdministratorAsync(new BootstrapCreateAdminDto
        {
            DisplayName = "Admin",
            Email = "admin@radish.test",
            Password = "admin123456",
            ConfirmPassword = "admin123456"
        });

        Assert.Equal(BootstrapAdminCreationStatus.InvalidInput, result.Status);
        Assert.Equal(BootstrapErrorCodes.PasswordWeak, result.Code);
        Assert.Equal("error.bootstrap.password_weak", result.MessageKey);
        repository.Verify(
            r => r.TryCreateFirstAdministratorAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PublicIndexReservationPolicy>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateFirstAdministratorAsync_ShouldPreserveValidationMessageArguments()
    {
        var repository = new Mock<IBootstrapRepository>(MockBehavior.Strict);
        var coinService = new Mock<ICoinService>(MockBehavior.Strict);
        var systemSettingProvider = CreateSystemSettingProvider();
        var service = new BootstrapService(repository.Object, coinService.Object, systemSettingProvider.Object);

        var result = await service.CreateFirstAdministratorAsync(new BootstrapCreateAdminDto
        {
            DisplayName = "A",
            Email = "admin@radish.test",
            Password = "Strong!Pass123",
            ConfirmPassword = "Strong!Pass123"
        });

        Assert.Equal(BootstrapAdminCreationStatus.InvalidInput, result.Status);
        Assert.Equal(BootstrapErrorCodes.DisplayNameLengthInvalid, result.Code);
        Assert.Equal("error.bootstrap.display_name_length_invalid", result.MessageKey);
        Assert.Equal(new object[] { 2, 24 }, result.MessageArguments);
        repository.Verify(
            item => item.TryCreateFirstAdministratorAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PublicIndexReservationPolicy>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateFirstAdministratorAsync_ShouldPassPasswordHashToRepository()
    {
        string? capturedHash = null;
        PublicIndexReservationPolicy? capturedPolicy = null;
        var repository = new Mock<IBootstrapRepository>(MockBehavior.Strict);
        var coinService = new Mock<ICoinService>(MockBehavior.Strict);
        var systemSettingProvider = CreateSystemSettingProvider(reservedIndexes: "[1000]", vanityRules: "{}");
        repository
            .Setup(r => r.TryCreateFirstAdministratorAsync(
                "Owner",
                It.IsAny<string>(),
                "owner@radish.test",
                It.IsAny<PublicIndexReservationPolicy>()))
            .Callback<string, string, string, PublicIndexReservationPolicy>((_, passwordHash, _, policy) =>
            {
                capturedHash = passwordHash;
                capturedPolicy = policy;
            })
            .ReturnsAsync(BootstrapAdminCreationResult.Created(9001, "Owner", "owner@radish.test"));
        coinService
            .Setup(service => service.GrantRegistrationRewardAsync(9001))
            .ReturnsAsync("TXN_REGISTER_9001");

        var service = new BootstrapService(repository.Object, coinService.Object, systemSettingProvider.Object);

        var result = await service.CreateFirstAdministratorAsync(new BootstrapCreateAdminDto
        {
            DisplayName = " Owner ",
            Email = " owner@radish.test ",
            Password = "Strong!Pass123",
            ConfirmPassword = "Strong!Pass123"
        });

        Assert.Equal(BootstrapAdminCreationStatus.Created, result.Status);
        Assert.NotNull(capturedHash);
        Assert.NotEqual("Strong!Pass123", capturedHash);
        Assert.True(PasswordHasher.VerifyPassword("Strong!Pass123", capturedHash!));
        Assert.NotNull(capturedPolicy);
        Assert.True(capturedPolicy!.ShouldReserve(1000));
        Assert.False(capturedPolicy.ShouldReserve(1001));
        coinService.Verify(service => service.GrantRegistrationRewardAsync(9001), Times.Once);
    }

    [Fact]
    public async Task CreateFirstAdministratorAsync_ShouldRemainCreated_WhenRegistrationRewardFailsAfterCommit()
    {
        var repository = new Mock<IBootstrapRepository>(MockBehavior.Strict);
        repository
            .Setup(item => item.TryCreateFirstAdministratorAsync(
                "Owner",
                It.IsAny<string>(),
                "owner@radish.test",
                It.IsAny<PublicIndexReservationPolicy>()))
            .ReturnsAsync(BootstrapAdminCreationResult.Created(9001, "Owner", "owner@radish.test"));
        var coinService = new Mock<ICoinService>(MockBehavior.Strict);
        coinService
            .Setup(service => service.GrantRegistrationRewardAsync(9001))
            .ThrowsAsync(new InvalidOperationException("temporary reward failure"));
        var systemSettingProvider = CreateSystemSettingProvider();
        var service = new BootstrapService(repository.Object, coinService.Object, systemSettingProvider.Object);

        var result = await service.CreateFirstAdministratorAsync(new BootstrapCreateAdminDto
        {
            DisplayName = "Owner",
            Email = "owner@radish.test",
            Password = "Strong!Pass123",
            ConfirmPassword = "Strong!Pass123"
        });

        Assert.Equal(BootstrapAdminCreationStatus.Created, result.Status);
        Assert.Equal(9001, result.UserId);
        coinService.Verify(service => service.GrantRegistrationRewardAsync(9001), Times.Once);
    }

    private static Mock<ISystemSettingProvider> CreateSystemSettingProvider(
        string? reservedIndexes = null,
        string? vanityRules = null)
    {
        var provider = new Mock<ISystemSettingProvider>(MockBehavior.Strict);
        provider
            .Setup(item => item.GetEffectiveValueAsync(SystemConfigDefaults.PublicIndexReservedIndexesKey))
            .ReturnsAsync(reservedIndexes ?? SystemConfigDefaults.DefaultPublicIndexReservedIndexes);
        provider
            .Setup(item => item.GetEffectiveValueAsync(SystemConfigDefaults.PublicIndexVanityRulesKey))
            .ReturnsAsync(vanityRules ?? SystemConfigDefaults.DefaultPublicIndexVanityRules);
        return provider;
    }
}
