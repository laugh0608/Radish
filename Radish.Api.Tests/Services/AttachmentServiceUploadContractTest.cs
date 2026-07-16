using System;
using System.IO;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Common.CoreTool;
using Radish.Common.Exceptions;
using Radish.Common.OptionTool;
using Radish.Infrastructure.FileStorage;
using Radish.Infrastructure.ImageProcessing;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
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
        fileStorageMock
            .Setup(storage => storage.UploadAsync(
                It.IsAny<Stream>(),
                "test.png",
                "image/png",
                It.IsAny<FileUploadOptionsDto>()))
            .ReturnsAsync(FileUploadResult.Fail(failureKind, "storage detail"));
        var service = CreateService(fileStorageMock.Object);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.UploadFileAsync(
            CreateImageFormFile(),
            CreateUploadOptions(),
            10001,
            "Tester"));

        Assert.Equal(expectedStatus, exception.StatusCode);
        Assert.Equal(expectedCode, exception.ErrorCode);
        Assert.Equal(expectedMessageKey, exception.MessageKey);
        fileStorageMock.VerifyAll();
    }

    [Fact]
    public async Task UploadFileAsync_Should_Preserve_UnexpectedStorageException()
    {
        var fileStorageMock = new Mock<IFileStorage>(MockBehavior.Strict);
        fileStorageMock
            .Setup(storage => storage.UploadAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
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
            var uploadResult = FileUploadResult.Ok("stored.png", "Post/stored.png", 4);
            uploadResult.ThumbnailPath = "Post/stored_thumb.png";

            var fileStorageMock = new Mock<IFileStorage>(MockBehavior.Strict);
            fileStorageMock
                .Setup(storage => storage.UploadAsync(
                    It.IsAny<Stream>(),
                    It.IsAny<string>(),
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

    private static AttachmentService CreateService(
        IFileStorage fileStorage,
        IImageProcessor? imageProcessor = null)
    {
        new ServiceCollection().ConfigureApplication();

        return new AttachmentService(
            Mock.Of<IMapper>(),
            Mock.Of<IBaseRepository<Attachment>>(),
            fileStorage,
            imageProcessor ?? Mock.Of<IImageProcessor>(),
            Mock.Of<IAttachmentUrlResolver>(),
            Mock.Of<IAttachmentReferenceInspector>(),
            Options.Create(new FileStorageOptions()));
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
