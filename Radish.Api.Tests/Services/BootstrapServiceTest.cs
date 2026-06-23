using System.Threading.Tasks;
using Moq;
using Radish.Common.HelpTool;
using Radish.IRepository;
using Radish.IService;
using Radish.Model.DtoModels;
using Radish.Service;
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

        var service = new BootstrapService(repository.Object, coinService.Object);

        var status = await service.GetStatusAsync();

        Assert.True(status.VoRequiresAdminInitialization);
        Assert.False(status.VoAdministratorExists);
    }

    [Fact]
    public async Task CreateFirstAdministratorAsync_ShouldRejectWeakDefaultPassword()
    {
        var repository = new Mock<IBootstrapRepository>(MockBehavior.Strict);
        var coinService = new Mock<ICoinService>(MockBehavior.Strict);
        var service = new BootstrapService(repository.Object, coinService.Object);

        var result = await service.CreateFirstAdministratorAsync(new BootstrapCreateAdminDto
        {
            DisplayName = "Admin",
            Email = "admin@radish.test",
            Password = "admin123456",
            ConfirmPassword = "admin123456"
        });

        Assert.Equal(BootstrapAdminCreationStatus.InvalidInput, result.Status);
        repository.Verify(
            r => r.TryCreateFirstAdministratorAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateFirstAdministratorAsync_ShouldPassPasswordHashToRepository()
    {
        string? capturedHash = null;
        var repository = new Mock<IBootstrapRepository>(MockBehavior.Strict);
        var coinService = new Mock<ICoinService>(MockBehavior.Strict);
        repository
            .Setup(r => r.TryCreateFirstAdministratorAsync("Owner", It.IsAny<string>(), "owner@radish.test"))
            .Callback<string, string, string>((_, passwordHash, _) => capturedHash = passwordHash)
            .ReturnsAsync(BootstrapAdminCreationResult.Created(9001, "Owner", "owner@radish.test"));
        coinService
            .Setup(service => service.GrantRegistrationRewardAsync(9001))
            .ReturnsAsync("TXN_REGISTER_9001");

        var service = new BootstrapService(repository.Object, coinService.Object);

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
        coinService.Verify(service => service.GrantRegistrationRewardAsync(9001), Times.Once);
    }
}
