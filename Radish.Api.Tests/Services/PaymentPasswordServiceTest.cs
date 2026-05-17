using AutoMapper;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.IRepository;
using Radish.IService;
using Radish.Model.Models;
using Radish.Model.ViewModels;
using Radish.Service;
using Radish.Shared.Security;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Radish.Api.Tests.Services;

public class PaymentPasswordServiceTest
{
    [Fact]
    public async Task SetPaymentPasswordAsync_ShouldRejectRepeatedDigitPasscode()
    {
        var repository = new Mock<IPaymentPasswordRepository>(MockBehavior.Strict);
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.SetPaymentPasswordAsync(9527, new SetPaymentPasswordRequest
        {
            NewPassword = "111111",
            ConfirmPassword = "111111"
        }));

        Assert.Equal("支付口令不能为6个相同数字", exception.Message);
        repository.Verify(repository => repository.GetByUserIdAsync(It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task SetPaymentPasswordAsync_ShouldPersistValidSixDigitPasscode()
    {
        const long userId = 9527;
        UserPaymentPassword? savedPasscode = null;

        var repository = new Mock<IPaymentPasswordRepository>(MockBehavior.Strict);
        repository
            .Setup(store => store.GetByUserIdAsync(userId))
            .ReturnsAsync((UserPaymentPassword?)null);
        repository
            .Setup(store => store.AddAsync(It.IsAny<UserPaymentPassword>()))
            .Callback<UserPaymentPassword>(entity => savedPasscode = entity)
            .ReturnsAsync(7001);

        var service = CreateService(repository);

        var result = await service.SetPaymentPasswordAsync(userId, new SetPaymentPasswordRequest
        {
            NewPassword = "274958",
            ConfirmPassword = "274958"
        });

        Assert.True(result);
        Assert.NotNull(savedPasscode);
        Assert.Equal(userId, savedPasscode!.UserId);
        Assert.Equal(5, savedPasscode.StrengthLevel);
        Assert.NotEmpty(savedPasscode.PasswordHash);
        Assert.NotEmpty(savedPasscode.Salt);
        Assert.True(savedPasscode.IsEnabled);
        Assert.Equal(0, savedPasscode.FailedAttempts);
        Assert.Equal(PaymentPasscodeRules.CurrentPasscodeVersion, savedPasscode.PasscodeVersion);
        repository.VerifyAll();
    }

    [Fact]
    public async Task SetPaymentPasswordAsync_ShouldResetLegacyPasscodeToCurrentVersion()
    {
        const long userId = 9527;
        UserPaymentPassword? updatedPasscode = null;
        var existingPasscode = new UserPaymentPassword
        {
            Id = 7002,
            UserId = userId,
            PasswordHash = "legacy-hash",
            Salt = "legacy-salt",
            PasscodeVersion = null,
            StrengthLevel = 5,
            IsEnabled = true
        };

        var repository = new Mock<IPaymentPasswordRepository>(MockBehavior.Strict);
        repository
            .Setup(store => store.GetByUserIdAsync(userId))
            .ReturnsAsync(existingPasscode);
        repository
            .Setup(store => store.UpdateAsync(existingPasscode))
            .Callback<UserPaymentPassword>(entity => updatedPasscode = entity)
            .ReturnsAsync(true);

        var service = CreateService(repository);

        var result = await service.SetPaymentPasswordAsync(userId, new SetPaymentPasswordRequest
        {
            NewPassword = "274958",
            ConfirmPassword = "274958"
        });

        Assert.True(result);
        Assert.NotNull(updatedPasscode);
        Assert.Equal(PaymentPasscodeRules.CurrentPasscodeVersion, updatedPasscode!.PasscodeVersion);
        Assert.NotEqual("legacy-hash", updatedPasscode.PasswordHash);
        Assert.NotEqual("legacy-salt", updatedPasscode.Salt);
        repository.VerifyAll();
    }

    [Fact]
    public async Task VerifyPaymentPasswordAsync_ShouldRequireUpgrade_WhenStoredPasscodeIsLegacy()
    {
        const long userId = 9527;
        var repository = new Mock<IPaymentPasswordRepository>(MockBehavior.Strict);
        repository
            .Setup(store => store.GetByUserIdAsync(userId))
            .ReturnsAsync(new UserPaymentPassword
            {
                UserId = userId,
                PasswordHash = "legacy-hash",
                Salt = "legacy-salt",
                PasscodeVersion = null,
                IsEnabled = true
            });

        var service = CreateService(repository);

        var result = await service.VerifyPaymentPasswordAsync(userId, new VerifyPaymentPasswordRequest
        {
            Password = "274958",
            BusinessType = "ShopPurchase"
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.RequiresPasscodeUpgrade);
        Assert.Equal(PaymentPasscodeErrorCodes.UpgradeRequired, result.ErrorCode);
        Assert.Equal(PaymentPasscodeRules.UpgradeRequiredErrorMessage, result.ErrorMessage);
        repository.VerifyAll();
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("12", 0)]
    [InlineData("111111", 1)]
    [InlineData("123456", 2)]
    [InlineData("112233", 4)]
    [InlineData("274958", 5)]
    public void CheckPasswordStrength_ShouldMatchPasscodeRules(string password, int expectedStrength)
    {
        var repository = new Mock<IPaymentPasswordRepository>(MockBehavior.Loose);
        var service = CreateService(repository);

        Assert.Equal(expectedStrength, service.CheckPasswordStrength(password));
    }

    private static PaymentPasswordService CreateService(Mock<IPaymentPasswordRepository> repository)
    {
        return new PaymentPasswordService(
            repository.Object,
            new Mock<IAuditLogService>(MockBehavior.Loose).Object,
            new Mock<IMapper>(MockBehavior.Loose).Object,
            new Mock<ILogger<PaymentPasswordService>>(MockBehavior.Loose).Object);
    }
}
