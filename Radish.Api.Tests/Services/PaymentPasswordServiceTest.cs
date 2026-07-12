using AutoMapper;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Common.HelpTool;
using Radish.IRepository;
using Radish.IService;
using Radish.Model.Models;
using Radish.Model.ViewModels;
using Radish.Service;
using Radish.Shared.Security;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Radish.Api.Tests.Services;

public class PaymentPasswordServiceTest
{
    private static readonly DateTime FixedNow = new(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);

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
        Assert.Empty(savedPasscode.Salt);
        Assert.True(PasswordHasher.VerifyPassword("274958", savedPasscode.PasswordHash));
        Assert.True(savedPasscode.IsEnabled);
        Assert.Equal(0, savedPasscode.FailedAttempts);
        Assert.Equal(PaymentPasscodeRules.CurrentPasscodeVersion, savedPasscode.PasscodeVersion);
        Assert.Equal(FixedNow, savedPasscode.CreateTime);
        Assert.Equal(FixedNow, savedPasscode.LastModifiedTime);
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
        Assert.Empty(updatedPasscode.Salt);
        Assert.True(PasswordHasher.VerifyPassword("274958", updatedPasscode.PasswordHash));
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

    [Fact]
    public async Task VerifyPaymentPasswordAsync_ShouldVerifyCurrentArgon2idPasscode()
    {
        const long userId = 9527;
        var repository = new Mock<IPaymentPasswordRepository>(MockBehavior.Strict);
        repository
            .Setup(store => store.GetByUserIdAsync(userId))
            .ReturnsAsync(new UserPaymentPassword
            {
                UserId = userId,
                PasswordHash = PasswordHasher.HashPassword("274958"),
                Salt = string.Empty,
                PasscodeVersion = PaymentPasscodeRules.CurrentPasscodeVersion,
                IsEnabled = true
            });
        repository
            .Setup(store => store.ResetFailedAttemptsAsync(userId, FixedNow))
            .ReturnsAsync(true);
        repository
            .Setup(store => store.UpdateLastUsedTimeAsync(userId, FixedNow))
            .ReturnsAsync(true);

        var service = CreateService(repository);

        var result = await service.VerifyPaymentPasswordAsync(userId, new VerifyPaymentPasswordRequest
        {
            Password = "274958",
            BusinessType = "ShopPurchase"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.RemainingAttempts);
        repository.VerifyAll();
    }

    [Fact]
    public async Task VerifyPaymentPasswordAsync_ShouldUpgradeSha256Passcode_WhenPasswordCorrect()
    {
        const long userId = 9527;
        UserPaymentPassword? upgradedPasscode = null;
        var (legacyHash, legacySalt) = CreateLegacySha256Passcode("274958");
        var existingPasscode = new UserPaymentPassword
        {
            UserId = userId,
            PasswordHash = legacyHash,
            Salt = legacySalt,
            PasscodeVersion = PaymentPasscodeRules.LegacySha256PasscodeVersion,
            IsEnabled = true
        };

        var repository = new Mock<IPaymentPasswordRepository>(MockBehavior.Strict);
        repository
            .Setup(store => store.GetByUserIdAsync(userId))
            .ReturnsAsync(existingPasscode);
        repository
            .Setup(store => store.UpdateAsync(existingPasscode))
            .Callback<UserPaymentPassword>(entity => upgradedPasscode = entity)
            .ReturnsAsync(true);
        repository
            .Setup(store => store.ResetFailedAttemptsAsync(userId, FixedNow))
            .ReturnsAsync(true);
        repository
            .Setup(store => store.UpdateLastUsedTimeAsync(userId, FixedNow))
            .ReturnsAsync(true);

        var service = CreateService(repository);

        var result = await service.VerifyPaymentPasswordAsync(userId, new VerifyPaymentPasswordRequest
        {
            Password = "274958",
            BusinessType = "CoinTransfer"
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(upgradedPasscode);
        Assert.NotEqual(legacyHash, upgradedPasscode!.PasswordHash);
        Assert.Empty(upgradedPasscode.Salt);
        Assert.Equal(PaymentPasscodeRules.CurrentPasscodeVersion, upgradedPasscode.PasscodeVersion);
        Assert.True(PasswordHasher.VerifyPassword("274958", upgradedPasscode.PasswordHash));
        repository.VerifyAll();
    }

    [Fact]
    public async Task VerifyPaymentPasswordAsync_ShouldNotUpgradeSha256Passcode_WhenPasswordIncorrect()
    {
        const long userId = 9527;
        var (legacyHash, legacySalt) = CreateLegacySha256Passcode("274958");
        var existingPasscode = new UserPaymentPassword
        {
            UserId = userId,
            PasswordHash = legacyHash,
            Salt = legacySalt,
            PasscodeVersion = PaymentPasscodeRules.LegacySha256PasscodeVersion,
            IsEnabled = true
        };

        var repository = new Mock<IPaymentPasswordRepository>(MockBehavior.Strict);
        repository
            .Setup(store => store.GetByUserIdAsync(userId))
            .ReturnsAsync(existingPasscode);
        repository
            .Setup(store => store.UpdateFailedAttemptsAsync(userId, 1, null, FixedNow))
            .ReturnsAsync(true);

        var service = CreateService(repository);

        var result = await service.VerifyPaymentPasswordAsync(userId, new VerifyPaymentPasswordRequest
        {
            Password = "274959",
            BusinessType = "CoinTransfer"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(4, result.RemainingAttempts);
        Assert.Equal(PaymentPasscodeRules.LegacySha256PasscodeVersion, existingPasscode.PasscodeVersion);
        Assert.Equal(legacyHash, existingPasscode.PasswordHash);
        Assert.Equal(legacySalt, existingPasscode.Salt);
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
            new Mock<ILogger<PaymentPasswordService>>(MockBehavior.Loose).Object,
            new FixedTimeProvider(FixedNow));
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        private readonly DateTimeOffset _utcNow = new(utcNow);

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private static (string Hash, string Salt) CreateLegacySha256Passcode(string passcode)
    {
        var saltBytes = Encoding.UTF8.GetBytes("legacy-sha256-test-salt");
        var passwordBytes = Encoding.UTF8.GetBytes(passcode);
        var combinedBytes = new byte[saltBytes.Length + passwordBytes.Length];
        Buffer.BlockCopy(saltBytes, 0, combinedBytes, 0, saltBytes.Length);
        Buffer.BlockCopy(passwordBytes, 0, combinedBytes, saltBytes.Length, passwordBytes.Length);

        using var sha256 = SHA256.Create();
        return (
            Convert.ToBase64String(sha256.ComputeHash(combinedBytes)),
            Convert.ToBase64String(saltBytes));
    }
}
