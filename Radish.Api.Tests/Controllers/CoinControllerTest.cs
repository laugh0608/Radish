using Microsoft.Extensions.Localization;
using System.Threading.Tasks;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.Resources;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.Constants;
using Radish.Shared.Security;
using Xunit;

namespace Radish.Api.Tests.Controllers;

public class CoinControllerTest
{
    [Fact]
    public async Task GetTransactionByNo_ShouldReturnStableNotFoundContract()
    {
        var service = new Mock<ICoinService>(MockBehavior.Strict);
        service
            .Setup(item => item.GetTransactionByNoAsync("missing"))
            .ReturnsAsync((CoinTransactionVo?)null);

        var controller = CreateController(service.Object);
        var result = await controller.GetTransactionByNo("missing");

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(CoinErrorCodes.TransactionNotFound, result.Code);
        Assert.Equal("error.coin.transaction_not_found", result.MessageKey);
        service.VerifyAll();
    }

    [Fact]
    public async Task Transfer_ShouldPreservePasscodeUpgradeContract()
    {
        var service = new Mock<ICoinService>(MockBehavior.Strict);
        service
            .Setup(item => item.TransferAsync(10001, 20002, 100, "274958", "test", "transfer-key"))
            .ThrowsAsync(new BusinessException(
                PaymentPasscodeRules.UpgradeRequiredErrorMessage,
                409,
                PaymentPasscodeErrorCodes.UpgradeRequired,
                "error.payment_password.upgrade_required"));

        var controller = CreateController(service.Object);
        var result = await controller.Transfer(new TransferDto
        {
            ToUserId = 20002,
            Amount = 100,
            PaymentPassword = "274958",
            Remark = "test",
            IdempotencyKey = "transfer-key"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(PaymentPasscodeErrorCodes.UpgradeRequired, result.Code);
        Assert.Equal("error.payment_password.upgrade_required", result.MessageKey);
        var payload = Assert.IsType<TransactionResultVo>(result.ResponseData);
        Assert.True(payload.VoRequiresPasscodeUpgrade);
        Assert.Equal(PaymentPasscodeErrorCodes.UpgradeRequired, payload.VoErrorCode);
        service.VerifyAll();
    }

    private static CoinController CreateController(ICoinService coinService)
    {
        var currentUserAccessor = new Mock<ICurrentUserAccessor>();
        currentUserAccessor.SetupGet(accessor => accessor.Current).Returns(new CurrentUser
        {
            UserId = 10001,
            UserName = "Tester",
            TenantId = 0
        });

        return new CoinController(coinService, currentUserAccessor.Object, CreateErrorsLocalizer());
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
