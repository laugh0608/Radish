using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using Radish.Common.Exceptions;
using Radish.Common.Security;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.Model;
using Radish.Model.Models;
using Radish.Model.ViewModels;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests.Services;

public class FileAccessTokenServiceTest
{
    private static readonly DateTime FixedNow = new(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CreateTokenAsync_ShouldReturnRawTokenOnceAndPersistOnlyHash()
    {
        var attachmentRepository = CreateAttachmentRepository(1001, 42);
        FileAccessToken? addedToken = null;
        var tokenRepository = new Mock<IFileAccessTokenRepository>(MockBehavior.Strict);
        tokenRepository
            .Setup(repository => repository.AddAsync(It.IsAny<FileAccessToken>()))
            .Callback<FileAccessToken>(token => addedToken = token)
            .ReturnsAsync(9001);
        var service = CreateService(tokenRepository, attachmentRepository);

        var result = await service.CreateTokenAsync(new CreateFileAccessTokenDto
        {
            AttachmentId = 1001,
            AuthorizedIp = "127.0.0.1",
            MaxAccessCount = 3,
            ValidHours = 24
        }, 42, false, "https://radish.example");

        Assert.NotNull(addedToken);
        Assert.Equal(9001, result.VoId);
        Assert.Equal(FileAccessTokenHashing.StoredHashLength, addedToken.TokenHash.Length);
        Assert.Equal(addedToken.TokenHash, FileAccessTokenHashing.HashToken(result.VoToken));
        Assert.NotEqual(addedToken.TokenHash, result.VoToken);
        Assert.DoesNotContain(addedToken.TokenHash, result.VoAccessUrl, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString(result.VoToken), result.VoAccessUrl, StringComparison.Ordinal);
        Assert.Equal(FixedNow.AddHours(24), result.VoExpiresAt);
        tokenRepository.VerifyAll();
        attachmentRepository.VerifyAll();
    }

    [Fact]
    public async Task CreateTokenAsync_ShouldAllowAdministratorForForeignAttachment()
    {
        var attachmentRepository = CreateAttachmentRepository(1001, 77);
        var tokenRepository = new Mock<IFileAccessTokenRepository>(MockBehavior.Strict);
        tokenRepository.Setup(repository => repository.AddAsync(It.IsAny<FileAccessToken>())).ReturnsAsync(9002);
        var service = CreateService(tokenRepository, attachmentRepository);

        var result = await service.CreateTokenAsync(new CreateFileAccessTokenDto
        {
            AttachmentId = 1001,
            MaxAccessCount = 1,
            ValidHours = 1
        }, 42, true, "https://radish.example");

        Assert.Equal(9002, result.VoId);
        tokenRepository.VerifyAll();
        attachmentRepository.VerifyAll();
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1001, -1, 1)]
    [InlineData(1001, 1, 0)]
    [InlineData(1001, 1, 169)]
    public async Task CreateTokenAsync_ShouldRejectInvalidRequest(long attachmentId, int maxAccessCount, int validHours)
    {
        var tokenRepository = new Mock<IFileAccessTokenRepository>(MockBehavior.Strict);
        var attachmentRepository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        var service = CreateService(tokenRepository, attachmentRepository);

        await Assert.ThrowsAsync<BusinessException>(() => service.CreateTokenAsync(new CreateFileAccessTokenDto
        {
            AttachmentId = attachmentId,
            MaxAccessCount = maxAccessCount,
            ValidHours = validHours
        }, 42, false, "https://radish.example"));

        attachmentRepository.Verify(repository => repository.QueryByIdAsync(It.IsAny<long>()), Times.Never);
        tokenRepository.Verify(repository => repository.AddAsync(It.IsAny<FileAccessToken>()), Times.Never);
    }

    [Fact]
    public async Task ValidateAndUseTokenAsync_ShouldHashInputAndUseAtomicRepositoryResult()
    {
        const string rawToken = "abcdef0123456789abcdef0123456789";
        var storedHash = FileAccessTokenHashing.HashToken(rawToken);
        var tokenRepository = new Mock<IFileAccessTokenRepository>(MockBehavior.Strict);
        tokenRepository
            .Setup(repository => repository.TryConsumeAsync(storedHash, 77, "127.0.0.1", FixedNow))
            .ReturnsAsync(new FileAccessToken
            {
                Id = 9001,
                TokenHash = storedHash,
                AttachmentId = 1001,
                AccessCount = 1
            });
        var attachmentRepository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        var service = CreateService(tokenRepository, attachmentRepository);

        var attachmentId = await service.ValidateAndUseTokenAsync(rawToken, 77, "127.0.0.1");

        Assert.Equal(1001, attachmentId);
        tokenRepository.VerifyAll();
    }

    [Fact]
    public async Task ValidateAndUseTokenAsync_ShouldNotQueryWhenTokenIsBlank()
    {
        var tokenRepository = new Mock<IFileAccessTokenRepository>(MockBehavior.Strict);
        var attachmentRepository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        var service = CreateService(tokenRepository, attachmentRepository);

        var attachmentId = await service.ValidateAndUseTokenAsync(" ", null, "127.0.0.1");

        Assert.Null(attachmentId);
        tokenRepository.Verify(repository => repository.TryConsumeAsync(
            It.IsAny<string>(),
            It.IsAny<long?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task RevokeTokenAsync_ShouldNotExposeRawTokenWhenMissing()
    {
        const string rawToken = "abcdef0123456789abcdef0123456789";
        var storedHash = FileAccessTokenHashing.HashToken(rawToken);
        var tokenRepository = new Mock<IFileAccessTokenRepository>(MockBehavior.Strict);
        tokenRepository.Setup(repository => repository.GetByHashAsync(storedHash)).ReturnsAsync((FileAccessToken?)null);
        var attachmentRepository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        var service = CreateService(tokenRepository, attachmentRepository);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.RevokeTokenAsync(rawToken, 42, false));

        Assert.Equal("FileToken.NotFound", exception.ErrorCode);
        Assert.DoesNotContain(rawToken, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(storedHash, exception.Message, StringComparison.Ordinal);
        tokenRepository.VerifyAll();
    }

    [Fact]
    public async Task RevokeTokenAsync_ShouldAllowAttachmentOwnerAndRejectUnrelatedUser()
    {
        var token = new FileAccessToken
        {
            Id = 9001,
            TokenHash = FileAccessTokenHashing.HashToken("managed-token"),
            AttachmentId = 1001,
            CreatedBy = 77
        };
        var tokenRepository = new Mock<IFileAccessTokenRepository>(MockBehavior.Strict);
        tokenRepository.Setup(repository => repository.QueryByIdAsync(9001)).ReturnsAsync(token);
        tokenRepository.Setup(repository => repository.TryRevokeByIdAsync(9001, FixedNow)).ReturnsAsync(true);
        var attachmentRepository = CreateAttachmentRepository(1001, 42);
        var service = CreateService(tokenRepository, attachmentRepository);

        await service.RevokeTokenAsync(9001, 42, false);

        tokenRepository.VerifyAll();
        attachmentRepository.VerifyAll();

        var deniedTokenRepository = new Mock<IFileAccessTokenRepository>(MockBehavior.Strict);
        deniedTokenRepository.Setup(repository => repository.QueryByIdAsync(9001)).ReturnsAsync(token);
        var deniedAttachmentRepository = CreateAttachmentRepository(1001, 88);
        var deniedService = CreateService(deniedTokenRepository, deniedAttachmentRepository);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            deniedService.RevokeTokenAsync(9001, 42, false));
        Assert.Equal(403, exception.StatusCode);
        deniedTokenRepository.Verify(repository => repository.TryRevokeByIdAsync(
            It.IsAny<long>(),
            It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task RevokeTokenAsync_ShouldAllowTokenCreatorWithoutLoadingAttachment()
    {
        var token = new FileAccessToken
        {
            Id = 9001,
            TokenHash = FileAccessTokenHashing.HashToken("creator-token"),
            AttachmentId = 1001,
            CreatedBy = 42
        };
        var tokenRepository = new Mock<IFileAccessTokenRepository>(MockBehavior.Strict);
        tokenRepository.Setup(repository => repository.QueryByIdAsync(9001)).ReturnsAsync(token);
        tokenRepository.Setup(repository => repository.TryRevokeByIdAsync(9001, FixedNow)).ReturnsAsync(true);
        var attachmentRepository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        var service = CreateService(tokenRepository, attachmentRepository);

        await service.RevokeTokenAsync(9001, 42, false);

        attachmentRepository.Verify(repository => repository.QueryByIdAsync(It.IsAny<long>()), Times.Never);
        tokenRepository.VerifyAll();
    }

    [Fact]
    public async Task GetAttachmentTokensAsync_ShouldReturnSummaryWithoutCredential()
    {
        var tokenRepository = new Mock<IFileAccessTokenRepository>(MockBehavior.Strict);
        tokenRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<FileAccessToken, bool>>>()))
            .ReturnsAsync(new List<FileAccessToken>
            {
                new()
                {
                    Id = 9001,
                    TokenHash = FileAccessTokenHashing.HashToken("list-token"),
                    AttachmentId = 1001,
                    CreatedBy = 42,
                    MaxAccessCount = 3,
                    AccessCount = 1,
                    ExpiresAt = FixedNow.AddHours(1),
                    CreateTime = FixedNow
                }
            });
        var attachmentRepository = CreateAttachmentRepository(1001, 42);
        var service = CreateService(tokenRepository, attachmentRepository);

        var summaries = await service.GetAttachmentTokensAsync(1001, 42, false);

        var summary = Assert.Single(summaries);
        Assert.Equal(9001, summary.VoId);
        Assert.Equal("token-9001", summary.VoTokenPreview);
        Assert.IsNotType<FileAccessTokenCreatedVo>(summary);
        tokenRepository.VerifyAll();
        attachmentRepository.VerifyAll();
    }

    private static Mock<IBaseRepository<Attachment>> CreateAttachmentRepository(long attachmentId, long uploaderId)
    {
        var repository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        repository.Setup(item => item.QueryByIdAsync(attachmentId)).ReturnsAsync(new Attachment
        {
            Id = attachmentId,
            UploaderId = uploaderId
        });
        return repository;
    }

    private static FileAccessTokenService CreateService(
        Mock<IFileAccessTokenRepository> tokenRepository,
        Mock<IBaseRepository<Attachment>> attachmentRepository)
    {
        return new FileAccessTokenService(
            tokenRepository.Object,
            attachmentRepository.Object,
            new FixedTimeProvider(FixedNow));
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        private readonly DateTimeOffset _utcNow = new(utcNow);

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
