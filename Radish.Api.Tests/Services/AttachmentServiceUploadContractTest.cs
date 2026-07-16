using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Common.AttributeTool;
using Radish.Common.CoreTool;
using Radish.Common.Exceptions;
using Radish.Common.OptionTool;
using Radish.Infrastructure.FileStorage;
using Radish.Infrastructure.ImageProcessing;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service;
using Radish.Shared.Constants;
using Xunit;

namespace Radish.Api.Tests.Services;

[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class AttachmentServiceAppStateCollection
{
    public const string CollectionName = "Attachment service App state";
}

[Collection(AttachmentServiceAppStateCollection.CollectionName)]
public class AttachmentServiceUploadContractTest
{
    [Theory]
    [InlineData(FileUploadFailureKind.FileTooLarge, 413, AttachmentErrorCodes.FileTooLarge, "error.attachment.file_too_large")]
    [InlineData(FileUploadFailureKind.UnsupportedType, 415, AttachmentErrorCodes.UnsupportedMediaType, "error.attachment.unsupported_media_type")]
    [InlineData(FileUploadFailureKind.ContentMismatch, 415, AttachmentErrorCodes.ContentMismatch, "error.attachment.content_mismatch")]
    [InlineData(FileUploadFailureKind.StorageFailed, 500, AttachmentErrorCodes.StorageFailed, "error.attachment.storage_failed")]
    public async Task UploadFileAsync_Should_Map_StorageFailure_To_StableBusinessContract(
        FileUploadFailureKind failureKind,
        int expectedStatus,
        string expectedCode,
        string expectedMessageKey)
    {
        var fileStorageMock = new Mock<IFileStorage>(MockBehavior.Strict);
        var uploadFailure = failureKind == FileUploadFailureKind.FileTooLarge
            ? FileUploadResult.Fail(failureKind, "storage detail", "5 MB")
            : FileUploadResult.Fail(failureKind, "storage detail");
        fileStorageMock
            .Setup(storage => storage.UploadAsync(
                It.IsAny<Stream>(),
                "test.png",
                It.IsAny<FileUploadOptionsDto>()))
            .ReturnsAsync(uploadFailure);
        var service = CreateService(fileStorageMock.Object);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.UploadFileAsync(
            CreateImageFormFile(),
            CreateUploadOptions(),
            10001,
            "Tester"));

        Assert.Equal(expectedStatus, exception.StatusCode);
        Assert.Equal(expectedCode, exception.ErrorCode);
        Assert.Equal(expectedMessageKey, exception.MessageKey);
        if (failureKind == FileUploadFailureKind.FileTooLarge)
        {
            Assert.Equal(new object[] { "5 MB" }, exception.MessageArguments);
        }
        fileStorageMock.VerifyAll();
    }

    [Fact]
    public async Task UploadFileAsync_Should_Reject_Unknown_BusinessType_Before_Storage()
    {
        var fileStorageMock = new Mock<IFileStorage>(MockBehavior.Strict);
        var service = CreateService(fileStorageMock.Object);
        var options = CreateUploadOptions();
        options.BusinessType = "../../outside";

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.UploadFileAsync(
            CreateImageFormFile(),
            options,
            10001,
            "Tester"));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal(AttachmentErrorCodes.BusinessTypeUnsupported, exception.ErrorCode);
        fileStorageMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UploadFileAsync_Should_Reject_Document_For_ImageOnly_BusinessType()
    {
        var fileStorageMock = new Mock<IFileStorage>(MockBehavior.Strict);
        var service = CreateService(fileStorageMock.Object);
        var options = CreateUploadOptions();
        options.BusinessType = AttachmentBusinessTypes.Avatar;

        var pdfBytes = "%PDF-1.7"u8.ToArray();
        await using var stream = new MemoryStream(pdfBytes);
        var file = new FormFile(stream, 0, stream.Length, "file", "avatar.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.UploadFileAsync(
            file,
            options,
            10001,
            "Tester"));

        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, exception.StatusCode);
        Assert.Equal(AttachmentErrorCodes.UnsupportedMediaType, exception.ErrorCode);
        fileStorageMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UploadFileAsync_Should_Preserve_UnexpectedStorageException()
    {
        var fileStorageMock = new Mock<IFileStorage>(MockBehavior.Strict);
        fileStorageMock
            .Setup(storage => storage.UploadAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<FileUploadOptionsDto>()))
            .ThrowsAsync(new InvalidOperationException("unexpected adapter failure"));
        var service = CreateService(fileStorageMock.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadFileAsync(
            CreateImageFormFile(),
            CreateUploadOptions(),
            10001,
            "Tester"));

        Assert.Equal("unexpected adapter failure", exception.Message);
    }

    [Fact]
    public async Task UploadFileAsync_Should_Return_StableProcessingFailure_When_ThumbnailFails()
    {
        var sourcePath = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(
                sourcePath,
                new byte[] { 1, 2, 3, 4 },
                TestContext.Current.CancellationToken);
            var uploadResult = FileUploadResult.Ok("stored.png", "Post/stored.png", 4, "image/png");
            uploadResult.ThumbnailPath = "Post/stored_thumb.png";

            var fileStorageMock = new Mock<IFileStorage>(MockBehavior.Strict);
            fileStorageMock
                .Setup(storage => storage.UploadAsync(
                    It.IsAny<Stream>(),
                    It.IsAny<string>(),
                    It.IsAny<FileUploadOptionsDto>()))
                .ReturnsAsync(uploadResult);
            fileStorageMock
                .Setup(storage => storage.GetFullPath("Post/stored.png"))
                .Returns(sourcePath);
            fileStorageMock
                .Setup(storage => storage.GetFullPath("Post/stored_thumb.png"))
                .Returns(sourcePath + ".thumb");
            fileStorageMock
                .Setup(storage => storage.DeleteAsync("Post/stored.png"))
                .ReturnsAsync(true);
            fileStorageMock
                .Setup(storage => storage.DeleteAsync("Post/stored_thumb.png"))
                .ReturnsAsync(false);
            fileStorageMock
                .Setup(storage => storage.ExistsAsync("Post/stored_thumb.png"))
                .ReturnsAsync(false);

            var imageProcessorMock = new Mock<IImageProcessor>(MockBehavior.Strict);
            imageProcessorMock
                .Setup(processor => processor.GenerateThumbnailAsync(
                    It.IsAny<Stream>(),
                    sourcePath + ".thumb",
                    150,
                    150,
                    85))
                .ReturnsAsync(ImageProcessResult.Fail("processor detail"));
            var service = CreateService(fileStorageMock.Object, imageProcessorMock.Object);
            var options = CreateUploadOptions();
            options.GenerateThumbnail = true;

            var exception = await Assert.ThrowsAsync<BusinessException>(() => service.UploadFileAsync(
                CreateImageFormFile(),
                options,
                10001,
                "Tester"));

            Assert.Equal(500, exception.StatusCode);
            Assert.Equal(AttachmentErrorCodes.ProcessingFailed, exception.ErrorCode);
            Assert.Equal("error.attachment.processing_failed", exception.MessageKey);
            fileStorageMock.VerifyAll();
            imageProcessorMock.VerifyAll();
        }
        finally
        {
            File.Delete(sourcePath);
            File.Delete(sourcePath + ".thumb");
        }
    }

    [Fact]
    public async Task UploadFileAsync_Should_Cleanup_When_Attachment_Insert_Returns_InvalidId()
    {
        var attachmentRepositoryMock = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        attachmentRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<Attachment>()))
            .ReturnsAsync(0);
        var fileStorageMock = new Mock<IFileStorage>(MockBehavior.Strict);
        fileStorageMock
            .Setup(storage => storage.UploadAsync(
                It.IsAny<Stream>(),
                "test.png",
                It.IsAny<FileUploadOptionsDto>()))
            .ReturnsAsync(FileUploadResult.Ok(
                "stored.png",
                "Post/stored.png",
                4,
                "image/png"));
        fileStorageMock
            .Setup(storage => storage.DeleteAsync("Post/stored.png"))
            .ReturnsAsync(true);
        var service = CreateService(
            fileStorageMock.Object,
            attachmentRepository: attachmentRepositoryMock.Object);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.UploadFileAsync(
            CreateImageFormFile(),
            CreateUploadOptions(),
            10001,
            "Tester"));

        Assert.Equal(StatusCodes.Status500InternalServerError, exception.StatusCode);
        Assert.Equal(AttachmentErrorCodes.ProcessingFailed, exception.ErrorCode);
        attachmentRepositoryMock.VerifyAll();
        fileStorageMock.VerifyAll();
    }

    [Fact]
    public async Task UploadFileAsync_Should_Cleanup_All_PreRegistered_Size_Paths_When_Processor_Throws()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"radish-size-cleanup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var sourceFullPath = Path.Combine(tempDirectory, "stored.png");
        var smallFullPath = Path.Combine(tempDirectory, "stored_small.png");
        await File.WriteAllBytesAsync(
            sourceFullPath,
            new byte[] { 1, 2, 3, 4 },
            TestContext.Current.CancellationToken);

        var expectedPaths = new[]
        {
            "Post/stored.png",
            "Post/stored_thumb.png",
            "Post/stored_small.png",
            "Post/stored_medium.png",
            "Post/stored_large.png"
        };
        var physicalPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Post/stored.png"] = sourceFullPath,
            ["Post/stored_thumb.png"] = Path.Combine(tempDirectory, "stored_thumb.png"),
            ["Post/stored_small.png"] = smallFullPath,
            ["Post/stored_medium.png"] = Path.Combine(tempDirectory, "stored_medium.png"),
            ["Post/stored_large.png"] = Path.Combine(tempDirectory, "stored_large.png")
        };

        try
        {
            var uploadResult = FileUploadResult.Ok(
                "stored.png",
                "Post/stored.png",
                4,
                "image/png");
            uploadResult.ThumbnailPath = "Post/stored_thumb.png";
            var fileStorageMock = new Mock<IFileStorage>(MockBehavior.Strict);
            fileStorageMock
                .Setup(storage => storage.UploadAsync(
                    It.IsAny<Stream>(),
                    It.IsAny<string>(),
                    It.IsAny<FileUploadOptionsDto>()))
                .ReturnsAsync(uploadResult);
            fileStorageMock
                .Setup(storage => storage.GetFullPath("Post/stored.png"))
                .Returns(sourceFullPath);
            foreach (var expectedPath in expectedPaths)
            {
                fileStorageMock
                    .Setup(storage => storage.DeleteAsync(expectedPath))
                    .Callback(() => File.Delete(physicalPaths[expectedPath]))
                    .ReturnsAsync(true);
            }

            var imageProcessorMock = new Mock<IImageProcessor>(MockBehavior.Strict);
            imageProcessorMock
                .Setup(processor => processor.GenerateMultipleSizesAsync(
                    It.IsAny<Stream>(),
                    sourceFullPath,
                    It.IsAny<List<ImageSize>>()))
                .Callback(() => File.WriteAllBytes(smallFullPath, new byte[] { 9, 9 }))
                .ThrowsAsync(new InvalidOperationException("processor interrupted"));
            var service = CreateService(fileStorageMock.Object, imageProcessorMock.Object);
            var options = CreateUploadOptions();
            options.GenerateMultipleSizes = true;

            var exception = await Assert.ThrowsAsync<BusinessException>(() => service.UploadFileAsync(
                CreateImageFormFile(),
                options,
                10001,
                "Tester"));

            Assert.Equal(AttachmentErrorCodes.ProcessingFailed, exception.ErrorCode);
            foreach (var expectedPath in expectedPaths)
            {
                fileStorageMock.Verify(storage => storage.DeleteAsync(expectedPath), Times.Once);
                Assert.False(File.Exists(physicalPaths[expectedPath]));
            }
            fileStorageMock.VerifyAll();
            imageProcessorMock.VerifyAll();
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData(20002, AttachmentBusinessTypes.Post, null, true)]
    [InlineData(10001, AttachmentBusinessTypes.Comment, null, true)]
    [InlineData(10001, AttachmentBusinessTypes.Post, 70001L, true)]
    [InlineData(10001, AttachmentBusinessTypes.Post, null, false)]
    public async Task UploadFileAsync_Should_NotReuse_HashMatch_Outside_CurrentOwnershipScope(
        long candidateUploaderId,
        string candidateBusinessType,
        long? candidateBusinessId,
        bool candidateIsEnabled)
    {
        var fileBytes = new byte[] { 1, 2, 3, 4 };
        var fileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(fileBytes))
            .ToLowerInvariant();
        var candidate = new Attachment
        {
            Id = 41,
            FileHash = fileHash,
            StoragePath = "Post/existing.png",
            TenantId = 0,
            UploaderId = candidateUploaderId,
            BusinessType = candidateBusinessType,
            BusinessId = candidateBusinessId,
            IsEnabled = candidateIsEnabled,
            IsDeleted = false
        };
        var attachmentRepositoryMock = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        attachmentRepositoryMock
            .Setup(repository => repository.QueryFirstAsync(
                It.IsAny<Expression<Func<Attachment, bool>>?>()))
            .ReturnsAsync((Expression<Func<Attachment, bool>>? predicate) =>
                predicate?.Compile()(candidate) == true ? candidate : null);
        attachmentRepositoryMock
            .Setup(repository => repository.AddAsync(It.Is<Attachment>(attachment =>
                attachment.UploaderId == 10001
                && attachment.BusinessType == AttachmentBusinessTypes.Post
                && attachment.BusinessId == null)))
            .ReturnsAsync(42);

        var fileStorageMock = new Mock<IFileStorage>(MockBehavior.Strict);
        fileStorageMock
            .Setup(storage => storage.UploadAsync(
                It.IsAny<Stream>(),
                "test.png",
                It.IsAny<FileUploadOptionsDto>()))
            .ReturnsAsync(FileUploadResult.Ok(
                "stored.png",
                "Post/stored.png",
                fileBytes.Length,
                "image/png"));
        var mapperMock = new Mock<IMapper>(MockBehavior.Strict);
        mapperMock
            .Setup(mapper => mapper.Map<AttachmentVo>(It.Is<Attachment>(attachment => attachment.Id == 42)))
            .Returns(new AttachmentVo { VoId = 42 });
        var service = CreateService(
            fileStorageMock.Object,
            attachmentRepository: attachmentRepositoryMock.Object,
            mapper: mapperMock.Object);
        var options = CreateUploadOptions();
        options.CalculateHash = true;

        var result = await service.UploadFileAsync(
            CreateImageFormFile(),
            options,
            10001,
            "Tester");

        Assert.Equal(42, result?.VoId);
        fileStorageMock.VerifyAll();
        attachmentRepositoryMock.VerifyAll();
        mapperMock.VerifyAll();
    }

    [Fact]
    public async Task UploadFileAsync_Should_Reuse_Only_UnboundEnabledAttachment_In_CurrentOwnershipScope()
    {
        var fileBytes = new byte[] { 1, 2, 3, 4 };
        var candidate = new Attachment
        {
            Id = 41,
            FileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(fileBytes))
                .ToLowerInvariant(),
            StoragePath = "Post/existing.png",
            TenantId = 0,
            UploaderId = 10001,
            BusinessType = AttachmentBusinessTypes.Post,
            BusinessId = null,
            IsEnabled = true,
            IsDeleted = false
        };
        var attachmentRepositoryMock = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        attachmentRepositoryMock
            .Setup(repository => repository.QueryFirstAsync(
                It.IsAny<Expression<Func<Attachment, bool>>?>()))
            .ReturnsAsync((Expression<Func<Attachment, bool>>? predicate) =>
                predicate?.Compile()(candidate) == true ? candidate : null);
        var fileStorageMock = new Mock<IFileStorage>(MockBehavior.Strict);
        fileStorageMock
            .Setup(storage => storage.ExistsAsync(candidate.StoragePath))
            .ReturnsAsync(true);
        var mapperMock = new Mock<IMapper>(MockBehavior.Strict);
        mapperMock
            .Setup(mapper => mapper.Map<AttachmentVo>(candidate))
            .Returns(new AttachmentVo { VoId = candidate.Id });
        var service = CreateService(
            fileStorageMock.Object,
            attachmentRepository: attachmentRepositoryMock.Object,
            mapper: mapperMock.Object);
        var options = CreateUploadOptions();
        options.CalculateHash = true;

        var result = await service.UploadFileAsync(
            CreateImageFormFile(),
            options,
            10001,
            "Tester");

        Assert.Equal(candidate.Id, result?.VoId);
        fileStorageMock.VerifyAll();
        attachmentRepositoryMock.VerifyAll();
        mapperMock.VerifyAll();
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public async Task UploadFileAsync_Should_SkipDedup_When_OutputProcessing_IsRequested(
        bool generateThumbnail,
        bool generateMultipleSizes,
        bool addWatermark,
        bool removeExif)
    {
        var attachmentRepositoryMock = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        var fileStorageMock = new Mock<IFileStorage>(MockBehavior.Strict);
        fileStorageMock
            .Setup(storage => storage.UploadAsync(
                It.IsAny<Stream>(),
                "test.png",
                It.IsAny<FileUploadOptionsDto>()))
            .ReturnsAsync(FileUploadResult.Fail(FileUploadFailureKind.StorageFailed, "storage unavailable"));
        var service = CreateService(
            fileStorageMock.Object,
            attachmentRepository: attachmentRepositoryMock.Object);
        var options = CreateUploadOptions();
        options.CalculateHash = true;
        options.GenerateThumbnail = generateThumbnail;
        options.GenerateMultipleSizes = generateMultipleSizes;
        options.AddWatermark = addWatermark;
        options.RemoveExif = removeExif;

        await Assert.ThrowsAsync<BusinessException>(() => service.UploadFileAsync(
            CreateImageFormFile(),
            options,
            10001,
            "Tester"));

        attachmentRepositoryMock.Verify(
            repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Attachment, bool>>?>()),
            Times.Never);
        attachmentRepositoryMock.VerifyNoOtherCalls();
        fileStorageMock.VerifyAll();
    }

    [Fact]
    public async Task UploadFileAsync_Should_SkipDedup_When_Deduplication_IsDisabled()
    {
        var attachmentRepositoryMock = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        var fileStorageMock = new Mock<IFileStorage>(MockBehavior.Strict);
        fileStorageMock
            .Setup(storage => storage.UploadAsync(
                It.IsAny<Stream>(),
                "test.png",
                It.IsAny<FileUploadOptionsDto>()))
            .ReturnsAsync(FileUploadResult.Fail(FileUploadFailureKind.StorageFailed, "storage unavailable"));
        var fileStorageOptions = new FileStorageOptions();
        fileStorageOptions.Deduplication.Enable = false;
        var service = CreateService(
            fileStorageMock.Object,
            attachmentRepository: attachmentRepositoryMock.Object,
            fileStorageOptions: fileStorageOptions);
        var options = CreateUploadOptions();
        options.CalculateHash = true;

        await Assert.ThrowsAsync<BusinessException>(() => service.UploadFileAsync(
            CreateImageFormFile(),
            options,
            10001,
            "Tester"));

        attachmentRepositoryMock.Verify(
            repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Attachment, bool>>?>()),
            Times.Never);
        attachmentRepositoryMock.VerifyNoOtherCalls();
        fileStorageMock.VerifyAll();
    }

    [Fact]
    public async Task UploadFileAsync_Should_Leave_Avatar_Unbound_Until_ProfileService_Confirms()
    {
        var repository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        repository
            .Setup(item => item.AddAsync(It.Is<Attachment>(attachment =>
                attachment.BusinessType == AttachmentBusinessTypes.Avatar &&
                attachment.BusinessId == null &&
                attachment.UploaderId == 10001)))
            .ReturnsAsync(42);
        var fileStorage = new Mock<IFileStorage>(MockBehavior.Strict);
        fileStorage
            .Setup(storage => storage.UploadAsync(
                It.IsAny<Stream>(),
                "test.png",
                It.Is<FileUploadOptionsDto>(options =>
                    options.BusinessType == AttachmentBusinessTypes.Avatar)))
            .ReturnsAsync(FileUploadResult.Ok(
                "stored.png",
                "Avatar/stored.png",
                4,
                "image/png"));
        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        mapper
            .Setup(item => item.Map<AttachmentVo>(It.Is<Attachment>(attachment =>
                attachment.Id == 42 && attachment.BusinessId == null)))
            .Returns(new AttachmentVo { VoId = 42 });
        var service = CreateService(
            fileStorage.Object,
            attachmentRepository: repository.Object,
            mapper: mapper.Object);
        var options = CreateUploadOptions();
        options.BusinessType = AttachmentBusinessTypes.Avatar;

        var result = await service.UploadFileAsync(
            CreateImageFormFile(),
            options,
            10001,
            "Tester");

        Assert.Equal(42, result?.VoId);
        repository.VerifyAll();
        fileStorage.VerifyAll();
        mapper.VerifyAll();
    }

    [Fact]
    public void SetCurrentAvatarAsync_Should_Keep_Transactional_Boundary()
    {
        var method = typeof(AttachmentService).GetMethod(
            nameof(AttachmentService.SetCurrentAvatarAsync),
            BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(method);
        Assert.NotNull(method.GetCustomAttribute<UseTranAttribute>());
    }

    [Fact]
    public async Task SetCurrentAvatarAsync_Should_Reject_Foreign_Attachment_Before_Clearing_Current_Avatar()
    {
        var attachment = CreateAvatarAttachment(uploaderId: 20002);
        var repository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        repository.Setup(item => item.QueryByIdAsync(attachment.Id)).ReturnsAsync(attachment);
        var service = CreateService(Mock.Of<IFileStorage>(), attachmentRepository: repository.Object);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.SetCurrentAvatarAsync(attachment.Id, 10001, "Tester"));

        Assert.Equal(StatusCodes.Status403Forbidden, exception.StatusCode);
        Assert.Equal(AttachmentErrorCodes.UploadForbidden, exception.ErrorCode);
        repository.Verify(
            item => item.UpdateColumnsAsync(
                It.IsAny<Expression<Func<Attachment, Attachment>>>(),
                It.IsAny<Expression<Func<Attachment, bool>>>()),
            Times.Never);
        repository.Verify(item => item.UpdateAsync(It.IsAny<Attachment>()), Times.Never);
        repository.VerifyAll();
    }

    [Fact]
    public async Task SetCurrentAvatarAsync_Should_Reject_NonRaster_Avatar()
    {
        var attachment = CreateAvatarAttachment();
        attachment.Extension = ".svg";
        attachment.MimeType = "image/svg+xml";
        var repository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        repository.Setup(item => item.QueryByIdAsync(attachment.Id)).ReturnsAsync(attachment);
        var service = CreateService(Mock.Of<IFileStorage>(), attachmentRepository: repository.Object);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.SetCurrentAvatarAsync(attachment.Id, 10001, "Tester"));

        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, exception.StatusCode);
        Assert.Equal(AttachmentErrorCodes.ImageTypeUnsupported, exception.ErrorCode);
        repository.Verify(
            item => item.UpdateColumnsAsync(
                It.IsAny<Expression<Func<Attachment, Attachment>>>(),
                It.IsAny<Expression<Func<Attachment, bool>>>()),
            Times.Never);
        repository.Verify(item => item.UpdateAsync(It.IsAny<Attachment>()), Times.Never);
        repository.VerifyAll();
    }

    [Fact]
    public async Task SetCurrentAvatarAsync_Should_Clear_All_Previous_Target_Associations_Then_Bind_Own_Avatar()
    {
        var attachment = CreateAvatarAttachment();
        attachment.BusinessId = null;
        var repository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        repository.Setup(item => item.QueryByIdAsync(attachment.Id)).ReturnsAsync(attachment);
        repository
            .Setup(item => item.UpdateColumnsAsync(
                It.IsAny<Expression<Func<Attachment, Attachment>>>(),
                It.IsAny<Expression<Func<Attachment, bool>>>()))
            .ReturnsAsync((
                Expression<Func<Attachment, Attachment>> _,
                Expression<Func<Attachment, bool>> where) =>
            {
                var predicate = where.Compile();
                Assert.True(predicate(CreateAvatarAttachment(id: 99, uploaderId: 20002, businessId: 10001)));
                Assert.False(predicate(attachment));
                return 1;
            });
        repository.Setup(item => item.UpdateAsync(attachment)).ReturnsAsync(true);
        var service = CreateService(Mock.Of<IFileStorage>(), attachmentRepository: repository.Object);

        await service.SetCurrentAvatarAsync(attachment.Id, 10001, " Tester ");

        Assert.Equal(10001, attachment.BusinessId);
        Assert.Equal("Tester", attachment.ModifyBy);
        Assert.Equal(10001, attachment.ModifyId);
        repository.VerifyAll();
    }

    [Fact]
    public async Task SetCurrentAvatarAsync_Should_FailClosed_When_Target_Update_Does_Not_Persist()
    {
        var attachment = CreateAvatarAttachment();
        var repository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        repository.Setup(item => item.QueryByIdAsync(attachment.Id)).ReturnsAsync(attachment);
        repository
            .Setup(item => item.UpdateColumnsAsync(
                It.IsAny<Expression<Func<Attachment, Attachment>>>(),
                It.IsAny<Expression<Func<Attachment, bool>>>()))
            .ReturnsAsync(1);
        repository.Setup(item => item.UpdateAsync(attachment)).ReturnsAsync(false);
        var service = CreateService(Mock.Of<IFileStorage>(), attachmentRepository: repository.Object);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.SetCurrentAvatarAsync(attachment.Id, 10001, "Tester"));

        Assert.Equal(AttachmentErrorCodes.ProcessingFailed, exception.ErrorCode);
        repository.VerifyAll();
    }

    [Fact]
    public async Task SetCurrentAvatarAsync_Should_Clear_Target_Associations_Regardless_Of_Historical_Uploader()
    {
        var repository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        repository
            .Setup(item => item.UpdateColumnsAsync(
                It.IsAny<Expression<Func<Attachment, Attachment>>>(),
                It.IsAny<Expression<Func<Attachment, bool>>>()))
            .ReturnsAsync((
                Expression<Func<Attachment, Attachment>> _,
                Expression<Func<Attachment, bool>> where) =>
            {
                Assert.True(where.Compile()(CreateAvatarAttachment(
                    id: 99,
                    uploaderId: 20002,
                    businessId: 10001)));
                return 1;
            });
        var service = CreateService(Mock.Of<IFileStorage>(), attachmentRepository: repository.Object);

        await service.SetCurrentAvatarAsync(0, 10001, "Tester");

        repository.Verify(item => item.QueryByIdAsync(It.IsAny<long>()), Times.Never);
        repository.Verify(item => item.UpdateAsync(It.IsAny<Attachment>()), Times.Never);
        repository.VerifyAll();
    }

    [Theory]
    [InlineData(true, true, AttachmentUrlVariant.Original)]
    [InlineData(true, true, AttachmentUrlVariant.Thumbnail)]
    [InlineData(false, false, AttachmentUrlVariant.Original)]
    [InlineData(false, false, AttachmentUrlVariant.Thumbnail)]
    public async Task GetDownloadStreamAsync_Should_FailClosed_For_Deleted_Or_Disabled_Attachment(
        bool isDeleted,
        bool isEnabled,
        AttachmentUrlVariant variant)
    {
        var attachment = CreateAvatarAttachment();
        attachment.IsDeleted = isDeleted;
        attachment.IsEnabled = isEnabled;
        attachment.StoragePath = "Avatar/avatar.png";
        attachment.ThumbnailPath = "Avatar/avatar_thumb.png";
        var repository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        repository.Setup(item => item.QueryByIdAsync(attachment.Id)).ReturnsAsync(attachment);
        var fileStorage = new Mock<IFileStorage>(MockBehavior.Strict);
        var service = CreateService(fileStorage.Object, attachmentRepository: repository.Object);

        var result = await service.GetDownloadStreamAsync(
            attachment.Id,
            attachment.UploaderId,
            ["User"],
            variant);

        Assert.Null(result.stream);
        Assert.Null(result.attachment);
        repository.Verify(item => item.UpdateAsync(It.IsAny<Attachment>()), Times.Never);
        repository.VerifyAll();
        fileStorage.VerifyNoOtherCalls();
    }

    private static AttachmentService CreateService(
        IFileStorage fileStorage,
        IImageProcessor? imageProcessor = null,
        IBaseRepository<Attachment>? attachmentRepository = null,
        IMapper? mapper = null,
        FileStorageOptions? fileStorageOptions = null)
    {
        new ServiceCollection().ConfigureApplication();

        return new AttachmentService(
            mapper ?? Mock.Of<IMapper>(),
            attachmentRepository ?? Mock.Of<IBaseRepository<Attachment>>(),
            fileStorage,
            imageProcessor ?? Mock.Of<IImageProcessor>(),
            Mock.Of<IAttachmentUrlResolver>(),
            Options.Create(fileStorageOptions ?? new FileStorageOptions()));
    }

    private static FileUploadOptionsDto CreateUploadOptions()
    {
        return new FileUploadOptionsDto
        {
            BusinessType = "Post",
            CalculateHash = false,
            GenerateThumbnail = false,
            GenerateMultipleSizes = false,
            AddWatermark = false,
            RemoveExif = false
        };
    }

    private static Attachment CreateAvatarAttachment(
        long id = 42,
        long uploaderId = 10001,
        long? businessId = 10001)
    {
        return new Attachment
        {
            Id = id,
            TenantId = 0,
            UploaderId = uploaderId,
            BusinessType = AttachmentBusinessTypes.Avatar,
            BusinessId = businessId,
            Extension = ".png",
            MimeType = "image/png",
            IsEnabled = true,
            IsDeleted = false
        };
    }

    private static IFormFile CreateImageFormFile()
    {
        var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        return new FormFile(stream, 0, stream.Length, "file", "test.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };
    }
}
