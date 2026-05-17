using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using Radish.IRepository.Base;
using Radish.Model;
using Radish.Model.Models;
using Radish.Model.ViewModels;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests.Services;

public class FileAccessTokenServiceTest
{
    [Fact]
    public async Task CreateTokenAsync_Should_Create_Token_For_AttachmentOwner()
    {
        var attachmentRepository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        attachmentRepository
            .Setup(r => r.QueryByIdAsync(1001))
            .ReturnsAsync(new Attachment { Id = 1001, UploaderId = 42 });

        FileAccessToken? addedToken = null;
        var tokenRepository = new Mock<IBaseRepository<FileAccessToken>>(MockBehavior.Strict);
        tokenRepository
            .Setup(r => r.AddAsync(It.IsAny<FileAccessToken>()))
            .Callback<FileAccessToken>(token => addedToken = token)
            .ReturnsAsync(9001);

        var service = CreateService(tokenRepository, attachmentRepository);

        var result = await service.CreateTokenAsync(new CreateFileAccessTokenDto
        {
            AttachmentId = 1001,
            AuthorizedIp = "127.0.0.1",
            MaxAccessCount = 3,
            ValidHours = 24
        }, userId: 42, baseUrl: "https://radish.example");

        Assert.NotNull(addedToken);
        Assert.Equal(1001, addedToken.AttachmentId);
        Assert.Equal(42, addedToken.CreatedBy);
        Assert.Equal("127.0.0.1", addedToken.AuthorizedIp);
        Assert.Equal(3, addedToken.MaxAccessCount);
        Assert.False(addedToken.IsRevoked);
        Assert.Equal(addedToken.Token, result.VoToken);
        Assert.Contains(addedToken.Token, result.VoAccessUrl, StringComparison.Ordinal);

        attachmentRepository.VerifyAll();
        tokenRepository.VerifyAll();
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1001, -1, 1)]
    [InlineData(1001, 1, 0)]
    [InlineData(1001, 1, 169)]
    public async Task CreateTokenAsync_Should_Reject_Invalid_Request(long attachmentId, int maxAccessCount, int validHours)
    {
        var attachmentRepository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        var tokenRepository = new Mock<IBaseRepository<FileAccessToken>>(MockBehavior.Strict);
        var service = CreateService(tokenRepository, attachmentRepository);

        await Assert.ThrowsAsync<Exception>(() => service.CreateTokenAsync(new CreateFileAccessTokenDto
        {
            AttachmentId = attachmentId,
            MaxAccessCount = maxAccessCount,
            ValidHours = validHours
        }, userId: 42, baseUrl: "https://radish.example"));

        attachmentRepository.Verify(r => r.QueryByIdAsync(It.IsAny<long>()), Times.Never);
        tokenRepository.Verify(r => r.AddAsync(It.IsAny<FileAccessToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateTokenAsync_Should_Reject_Invalid_AuthorizedUserId(long authorizedUserId)
    {
        var attachmentRepository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        var tokenRepository = new Mock<IBaseRepository<FileAccessToken>>(MockBehavior.Strict);
        var service = CreateService(tokenRepository, attachmentRepository);

        await Assert.ThrowsAsync<Exception>(() => service.CreateTokenAsync(new CreateFileAccessTokenDto
        {
            AttachmentId = 1001,
            MaxAccessCount = 1,
            ValidHours = 1,
            AuthorizedUserId = authorizedUserId
        }, userId: 42, baseUrl: "https://radish.example"));

        attachmentRepository.Verify(r => r.QueryByIdAsync(It.IsAny<long>()), Times.Never);
        tokenRepository.Verify(r => r.AddAsync(It.IsAny<FileAccessToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateTokenAsync_Should_Reject_Invalid_AuthorizedIp()
    {
        var attachmentRepository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        var tokenRepository = new Mock<IBaseRepository<FileAccessToken>>(MockBehavior.Strict);
        var service = CreateService(tokenRepository, attachmentRepository);

        var exception = await Assert.ThrowsAsync<Exception>(() => service.CreateTokenAsync(new CreateFileAccessTokenDto
        {
            AttachmentId = 1001,
            MaxAccessCount = 1,
            ValidHours = 1,
            AuthorizedIp = "not-an-ip"
        }, userId: 42, baseUrl: "https://radish.example"));

        Assert.Equal("授权 IP 地址无效", exception.Message);
        attachmentRepository.Verify(r => r.QueryByIdAsync(It.IsAny<long>()), Times.Never);
        tokenRepository.Verify(r => r.AddAsync(It.IsAny<FileAccessToken>()), Times.Never);
    }

    [Fact]
    public async Task ValidateAndUseTokenAsync_Should_Update_Access_Count_When_Token_Is_Valid()
    {
        var rawToken = "abcdef0123456789abcdef0123456789";
        var tokenEntity = new FileAccessToken
        {
            Token = rawToken,
            AttachmentId = 1001,
            CreatedBy = 42,
            AuthorizedUserId = 77,
            MaxAccessCount = 2,
            AccessCount = 0,
            ExpiresAt = DateTime.Now.AddHours(1),
            IsRevoked = false
        };

        var tokenRepository = new Mock<IBaseRepository<FileAccessToken>>(MockBehavior.Strict);
        tokenRepository
            .Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<FileAccessToken, bool>>>()))
            .ReturnsAsync(tokenEntity);
        tokenRepository
            .Setup(r => r.UpdateAsync(tokenEntity))
            .ReturnsAsync(true);

        var attachmentRepository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        var service = CreateService(tokenRepository, attachmentRepository);

        var attachmentId = await service.ValidateAndUseTokenAsync(rawToken, userId: 77, ipAddress: "127.0.0.1");

        Assert.Equal(1001, attachmentId);
        Assert.Equal(1, tokenEntity.AccessCount);
        Assert.NotNull(tokenEntity.LastAccessedAt);
        Assert.NotNull(tokenEntity.ModifyTime);

        tokenRepository.VerifyAll();
    }

    [Fact]
    public async Task ValidateAndUseTokenAsync_Should_Not_Query_When_Token_Is_Blank()
    {
        var tokenRepository = new Mock<IBaseRepository<FileAccessToken>>(MockBehavior.Strict);
        var attachmentRepository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        var service = CreateService(tokenRepository, attachmentRepository);

        var attachmentId = await service.ValidateAndUseTokenAsync(" ", userId: null, ipAddress: "127.0.0.1");

        Assert.Null(attachmentId);
        tokenRepository.Verify(r => r.QueryFirstAsync(It.IsAny<Expression<Func<FileAccessToken, bool>>>()), Times.Never);
    }

    [Fact]
    public async Task RevokeTokenAsync_Should_Not_Leak_Raw_Token_When_Token_NotFound()
    {
        var rawToken = "abcdef0123456789abcdef0123456789";
        var tokenRepository = new Mock<IBaseRepository<FileAccessToken>>(MockBehavior.Strict);
        tokenRepository
            .Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<FileAccessToken, bool>>>()))
            .ReturnsAsync((FileAccessToken?)null);

        var attachmentRepository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        var service = CreateService(tokenRepository, attachmentRepository);

        var exception = await Assert.ThrowsAsync<Exception>(() => service.RevokeTokenAsync(rawToken, userId: 42));

        Assert.DoesNotContain(rawToken, exception.Message, StringComparison.Ordinal);
        Assert.Equal("令牌不存在", exception.Message);
        tokenRepository.VerifyAll();
    }

    private static FileAccessTokenService CreateService(
        Mock<IBaseRepository<FileAccessToken>> tokenRepository,
        Mock<IBaseRepository<Attachment>> attachmentRepository)
    {
        return new FileAccessTokenService(tokenRepository.Object, attachmentRepository.Object);
    }
}
