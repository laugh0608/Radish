using Microsoft.Extensions.Localization;
using System.Threading.Tasks;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.Resources;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model.ViewModels;
using Radish.Shared.Security;
using Xunit;

namespace Radish.Api.Tests.Controllers;

public class PaymentPasswordControllerTest
{
    [Fact]
    public async Task VerifyPassword_ShouldReturnTooManyRequestsContract_WhenLocked()
    {
        var service = new Mock<IPaymentPasswordService>(MockBehavior.Strict);
        service
            .Setup(item => item.VerifyPaymentPasswordAsync(10001, It.IsAny<VerifyPaymentPasswordRequest>()))
            .ReturnsAsync(new PaymentPasswordVerifyResult
            {
                IsSuccess = false,
                IsLocked = true,
                LockedRemainingMinutes = 30,
                RemainingAttempts = 0,
                ErrorCode = PaymentPasscodeErrorCodes.Locked,
                MessageKey = "error.payment_password.locked",
                ErrorMessage = "locked"
            });

        var controller = CreateController(service.Object);
        var result = await controller.VerifyPassword(new VerifyPaymentPasswordRequest
        {
            Password = "274958",
            BusinessType = "CoinTransfer"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(429, result.StatusCode);
        Assert.Equal(PaymentPasscodeErrorCodes.Locked, result.Code);
        Assert.Equal("error.payment_password.locked", result.MessageKey);
        Assert.True(result.ResponseData?.IsLocked);
        service.VerifyAll();
    }

    [Fact]
    public async Task SetPassword_ShouldReturnStableValidationContract()
    {
        var service = new Mock<IPaymentPasswordService>(MockBehavior.Strict);
        service
            .Setup(item => item.SetPaymentPasswordAsync(10001, It.IsAny<SetPaymentPasswordRequest>()))
            .ThrowsAsync(new BusinessException(
                "repeated digits",
                400,
                PaymentPasscodeErrorCodes.RepeatedDigits,
                "error.payment_password.repeated_digits"));

        var controller = CreateController(service.Object);
        var result = await controller.SetPassword(new SetPaymentPasswordRequest
        {
            NewPassword = "111111",
            ConfirmPassword = "111111"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(PaymentPasscodeErrorCodes.RepeatedDigits, result.Code);
        Assert.Equal("error.payment_password.repeated_digits", result.MessageKey);
        service.VerifyAll();
    }

    private static PaymentPasswordController CreateController(IPaymentPasswordService service)
    {
        var currentUserAccessor = new Mock<ICurrentUserAccessor>();
        currentUserAccessor.SetupGet(accessor => accessor.Current).Returns(new CurrentUser
        {
            UserId = 10001,
            UserName = "Tester",
            TenantId = 0
        });

        return new PaymentPasswordController(service, currentUserAccessor.Object, CreateErrorsLocalizer());
    }

    private static IStringLocalizer<Errors> CreateErrorsLocalizer()
    {
        var localizer = new Mock<IStringLocalizer<Errors>>();
        localizer
            .Setup(item => item[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key, resourceNotFound: true));
        return localizer.Object;
    }
}
