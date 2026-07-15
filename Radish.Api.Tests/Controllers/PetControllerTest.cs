using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Localization;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.Resources;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model.DtoModels;
using Xunit;

namespace Radish.Api.Tests.Controllers;

[TestSubject(typeof(PetController))]
public class PetControllerTest
{
    [Fact]
    public async Task UpdateProfile_Should_Return_Stable_Error_When_ModelState_Invalid()
    {
        var serviceMock = CreateServiceMock();
        var controller = CreateController(serviceMock.Object);
        controller.ModelState.AddModelError(nameof(UpdatePetProfileDto.Name), "invalid");

        var result = await controller.UpdateProfile(new UpdatePetProfileDto { Name = string.Empty });

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Pet.InvalidRequest", result.Code);
        Assert.Equal("error.pet.invalid_request", result.MessageKey);
    }

    [Fact]
    public async Task Care_Should_Forward_BusinessException_Contract()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(service => service.CareAsync(
                10001,
                "Tester",
                It.IsAny<PetCareDto>()))
            .ThrowsAsync(new BusinessException(
                "照顾动作仍在冷却中",
                400,
                "Pet.CareCooldown",
                "error.pet.care_cooldown"));
        var controller = CreateController(serviceMock.Object);

        var result = await controller.Care(new PetCareDto { ActionType = "feed" });

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Pet.CareCooldown", result.Code);
        Assert.Equal("error.pet.care_cooldown", result.MessageKey);
    }

    private static PetController CreateController(IPetService petService)
    {
        var currentUserAccessorMock = new Mock<ICurrentUserAccessor>();
        currentUserAccessorMock.SetupGet(accessor => accessor.Current).Returns(new CurrentUser
        {
            UserId = 10001,
            UserName = "Tester",
            TenantId = 0
        });

        return new PetController(
            petService,
            currentUserAccessorMock.Object,
            CreateErrorsLocalizer());
    }

    private static Mock<IPetService> CreateServiceMock()
    {
        return new Mock<IPetService>(MockBehavior.Strict);
    }

    private static IStringLocalizer<Errors> CreateErrorsLocalizer()
    {
        var localizerMock = new Mock<IStringLocalizer<Errors>>();
        localizerMock
            .Setup(localizer => localizer[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key, resourceNotFound: true));
        return localizerMock.Object;
    }
}
