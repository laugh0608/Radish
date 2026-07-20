using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.Filters;
using Radish.Api.Resources;
using Radish.IService;
using Radish.Model.DtoModels;
using Radish.Shared.Constants;
using Xunit;

namespace Radish.Api.Tests.Controllers;

public class BootstrapControllerTest
{
    public static TheoryData<BootstrapAdminCreationStatus, int, string, string> FailureCases => new()
    {
        {
            BootstrapAdminCreationStatus.InvalidInput,
            StatusCodes.Status400BadRequest,
            BootstrapErrorCodes.DisplayNameRequired,
            "error.bootstrap.display_name_required"
        },
        {
            BootstrapAdminCreationStatus.AlreadyInitialized,
            StatusCodes.Status409Conflict,
            BootstrapErrorCodes.AlreadyInitialized,
            "error.bootstrap.already_initialized"
        },
        {
            BootstrapAdminCreationStatus.EmailTaken,
            StatusCodes.Status409Conflict,
            BootstrapErrorCodes.EmailTaken,
            "error.bootstrap.email_taken"
        },
        {
            BootstrapAdminCreationStatus.ConcurrentInitialization,
            StatusCodes.Status409Conflict,
            BootstrapErrorCodes.ConcurrentInitialization,
            "error.bootstrap.concurrent_initialization"
        },
        {
            BootstrapAdminCreationStatus.Failed,
            StatusCodes.Status500InternalServerError,
            BootstrapErrorCodes.InitializationFailed,
            "error.bootstrap.initialization_failed"
        }
    };

    [Fact]
    public void Controller_ShouldOptIntoApiErrorContract()
    {
        var attribute = typeof(BootstrapController).GetCustomAttribute<ApiErrorContractAttribute>();

        Assert.NotNull(attribute);
    }

    [Theory]
    [MemberData(nameof(FailureCases))]
    public async Task CreateFirstAdministrator_ShouldMapFailureToStableContract(
        BootstrapAdminCreationStatus status,
        int expectedStatusCode,
        string expectedCode,
        string expectedMessageKey)
    {
        var service = new Mock<IBootstrapService>(MockBehavior.Strict);
        service
            .Setup(item => item.CreateFirstAdministratorAsync(It.IsAny<BootstrapCreateAdminDto>()))
            .ReturnsAsync(BootstrapAdminCreationResult.Failed(
                status,
                "fallback",
                expectedCode));
        var controller = new BootstrapController(service.Object, CreateMissingResourceLocalizer());

        var response = await controller.CreateFirstAdministrator(new BootstrapCreateAdminDto());

        Assert.False(response.IsSuccess);
        Assert.Equal(expectedStatusCode, response.StatusCode);
        Assert.Equal(expectedCode, response.Code);
        Assert.Equal(expectedMessageKey, response.MessageKey);
        Assert.Equal("fallback", response.MessageInfo);
    }

    [Fact]
    public async Task CreateFirstAdministrator_ShouldUseLocalizedMessageAndFormattingArguments()
    {
        const string messageKey = "error.bootstrap.display_name_length_invalid";
        var service = new Mock<IBootstrapService>(MockBehavior.Strict);
        service
            .Setup(item => item.CreateFirstAdministratorAsync(It.IsAny<BootstrapCreateAdminDto>()))
            .ReturnsAsync(BootstrapAdminCreationResult.Failed(
                BootstrapAdminCreationStatus.InvalidInput,
                "展示名长度必须为 2-24 位",
                BootstrapErrorCodes.DisplayNameLengthInvalid,
                2,
                24));
        var localizer = new Mock<IStringLocalizer<Errors>>(MockBehavior.Strict);
        localizer
            .Setup(item => item[messageKey, It.IsAny<object[]>()])
            .Returns((string _, object[] arguments) => new LocalizedString(
                messageKey,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The display name must contain {0}-{1} characters.",
                    arguments),
                resourceNotFound: false));
        var controller = new BootstrapController(service.Object, localizer.Object);

        var response = await controller.CreateFirstAdministrator(new BootstrapCreateAdminDto());

        Assert.Equal("The display name must contain 2-24 characters.", response.MessageInfo);
    }

    private static IStringLocalizer<Errors> CreateMissingResourceLocalizer()
    {
        var localizer = new Mock<IStringLocalizer<Errors>>();
        localizer
            .Setup(item => item[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] _) => new LocalizedString(key, key, resourceNotFound: true));
        return localizer.Object;
    }
}
