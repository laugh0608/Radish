using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.Resources;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Common.OptionTool;
using Radish.Common.PermissionTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.Constants;
using Xunit;

namespace Radish.Api.Tests.Controllers;

[TestSubject(typeof(AttachmentController))]
public class AttachmentControllerTest
{
    [Fact]
    public async Task UploadImage_Should_Return_Forbidden_When_Sticker_WithoutPermission()
    {
        var attachmentServiceMock = CreateAttachmentServiceMock();
        var userServiceMock = new Mock<IUserService>(MockBehavior.Strict);
        userServiceMock
            .Setup(s => s.GetPermissionKeysByRolesAsync(It.Is<IReadOnlyCollection<string>>(roles => roles.Count == 1)))
            .ReturnsAsync(new List<string>());

        var controller = CreateController(
            attachmentServiceMock.Object,
            userServiceMock.Object,
            new[] { "Operator" });

        var result = await controller.UploadImage(CreateImageFormFile(), "Sticker");

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("当前账号暂无该附件上传权限", result.MessageInfo);
        Assert.Equal(AttachmentErrorCodes.UploadForbidden, result.Code);
        Assert.Equal("error.attachment.upload_forbidden", result.MessageKey);
        attachmentServiceMock.Verify(
            s => s.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<FileUploadOptionsDto>(), It.IsAny<long>(), It.IsAny<string>()),
            Times.Never);
    }

    [Theory]
    [InlineData(AttachmentBusinessTypes.CategoryIcon)]
    [InlineData(AttachmentBusinessTypes.CategoryCover)]
    [InlineData(AttachmentBusinessTypes.ProductIcon)]
    [InlineData(AttachmentBusinessTypes.ProductCover)]
    [InlineData(AttachmentBusinessTypes.SiteFavicon)]
    public async Task UploadImage_Should_Reject_ConsoleOnlyBusinessType_WithoutPermission(string businessType)
    {
        var attachmentServiceMock = CreateAttachmentServiceMock();
        var userServiceMock = new Mock<IUserService>(MockBehavior.Strict);
        userServiceMock
            .Setup(service => service.GetPermissionKeysByRolesAsync(
                It.Is<IReadOnlyCollection<string>>(roles => roles.Count == 1)))
            .ReturnsAsync(new List<string>());
        var controller = CreateController(
            attachmentServiceMock.Object,
            userServiceMock.Object,
            new[] { "Operator" });

        var result = await controller.UploadImage(CreateImageFormFile(), businessType);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(AttachmentErrorCodes.UploadForbidden, result.Code);
        attachmentServiceMock.Verify(
            service => service.UploadFileAsync(
                It.IsAny<IFormFile>(),
                It.IsAny<FileUploadOptionsDto>(),
                It.IsAny<long>(),
                It.IsAny<string>()),
            Times.Never);
        userServiceMock.VerifyAll();
    }

    [Fact]
    public async Task UploadImage_Should_Allow_CategoryAsset_With_EditPermission()
    {
        var attachmentServiceMock = CreateAttachmentServiceMock();
        attachmentServiceMock
            .Setup(service => service.UploadFileAsync(
                It.IsAny<IFormFile>(),
                It.Is<FileUploadOptionsDto>(options =>
                    options.BusinessType == AttachmentBusinessTypes.CategoryIcon),
                10001,
                "Tester"))
            .ReturnsAsync(new AttachmentVo
            {
                VoId = 12,
                VoBusinessType = AttachmentBusinessTypes.CategoryIcon
            });
        var userServiceMock = new Mock<IUserService>(MockBehavior.Strict);
        userServiceMock
            .Setup(service => service.GetPermissionKeysByRolesAsync(
                It.Is<IReadOnlyCollection<string>>(roles => roles.Count == 1)))
            .ReturnsAsync(new List<string> { ConsolePermissions.CategoriesEdit });
        var controller = CreateController(
            attachmentServiceMock.Object,
            userServiceMock.Object,
            new[] { "Operator" });

        var result = await controller.UploadImage(
            CreateImageFormFile(),
            AttachmentBusinessTypes.CategoryIcon);

        Assert.True(result.IsSuccess);
        Assert.Equal(AttachmentBusinessTypes.CategoryIcon,
            Assert.IsType<AttachmentVo>(result.ResponseData).VoBusinessType);
        attachmentServiceMock.VerifyAll();
        userServiceMock.VerifyAll();
    }

    [Fact]
    public async Task UploadImage_Should_Return_StableContract_When_File_IsEmpty()
    {
        var controller = CreateController(
            CreateAttachmentServiceMock().Object,
            new Mock<IUserService>(MockBehavior.Strict).Object,
            new[] { "Operator" });
        var emptyFile = new FormFile(new MemoryStream(), 0, 0, "file", "empty.png");

        var result = await controller.UploadImage(emptyFile);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(AttachmentErrorCodes.FileEmpty, result.Code);
        Assert.Equal("error.attachment.file_empty", result.MessageKey);
    }

    [Fact]
    public async Task UploadImage_Should_Return_UnsupportedMediaType_For_NonImageExtension()
    {
        var controller = CreateController(
            CreateAttachmentServiceMock().Object,
            new Mock<IUserService>(MockBehavior.Strict).Object,
            new[] { "Operator" });
        var file = CreateFormFile("archive.zip", "application/zip");

        var result = await controller.UploadImage(file);

        Assert.False(result.IsSuccess);
        Assert.Equal(415, result.StatusCode);
        Assert.Equal(AttachmentErrorCodes.ImageTypeUnsupported, result.Code);
        Assert.Equal("error.attachment.image_type_unsupported", result.MessageKey);
    }

    [Fact]
    public async Task UploadDocument_Should_Return_UnsupportedMediaType_For_NonDocumentExtension()
    {
        var controller = CreateController(
            CreateAttachmentServiceMock().Object,
            new Mock<IUserService>(MockBehavior.Strict).Object,
            new[] { "Operator" });
        var file = CreateFormFile("image.png", "image/png");

        var result = await controller.UploadDocument(file);

        Assert.False(result.IsSuccess);
        Assert.Equal(415, result.StatusCode);
        Assert.Equal(AttachmentErrorCodes.DocumentTypeUnsupported, result.Code);
        Assert.Equal("error.attachment.document_type_unsupported", result.MessageKey);
    }

    [Fact]
    public async Task UploadDocument_Should_Reject_ConsoleOnlyBusinessType_WithoutPermission()
    {
        var attachmentServiceMock = CreateAttachmentServiceMock();
        var userServiceMock = new Mock<IUserService>(MockBehavior.Strict);
        userServiceMock
            .Setup(service => service.GetPermissionKeysByRolesAsync(
                It.Is<IReadOnlyCollection<string>>(roles => roles.Count == 1)))
            .ReturnsAsync(new List<string>());
        var controller = CreateController(
            attachmentServiceMock.Object,
            userServiceMock.Object,
            new[] { "Operator" });

        var result = await controller.UploadDocument(
            CreateFormFile("report.txt", "text/plain"),
            AttachmentBusinessTypes.CategoryIcon);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(AttachmentErrorCodes.UploadForbidden, result.Code);
        attachmentServiceMock.Verify(
            service => service.UploadFileAsync(
                It.IsAny<IFormFile>(),
                It.IsAny<FileUploadOptionsDto>(),
                It.IsAny<long>(),
                It.IsAny<string>()),
            Times.Never);
        userServiceMock.VerifyAll();
    }

    [Fact]
    public async Task UploadDocument_Should_Reject_ImageOnly_BusinessType_Before_Reservation()
    {
        var attachmentServiceMock = CreateAttachmentServiceMock();
        var userServiceMock = new Mock<IUserService>(MockBehavior.Strict);
        var controller = CreateController(
            attachmentServiceMock.Object,
            userServiceMock.Object,
            new[] { "Operator" });

        var result = await controller.UploadDocument(
            CreateFormFile("avatar.pdf", "application/pdf"),
            AttachmentBusinessTypes.Avatar);

        Assert.False(result.IsSuccess);
        Assert.Equal(415, result.StatusCode);
        Assert.Equal(AttachmentErrorCodes.UnsupportedMediaType, result.Code);
        attachmentServiceMock.Verify(
            service => service.UploadFileAsync(
                It.IsAny<IFormFile>(),
                It.IsAny<FileUploadOptionsDto>(),
                It.IsAny<long>(),
                It.IsAny<string>()),
            Times.Never);
        userServiceMock.Verify(
            service => service.GetPermissionKeysByRolesAsync(It.IsAny<IReadOnlyCollection<string>>()),
            Times.Never);
    }

    [Fact]
    public async Task UploadImage_Should_Return_StableRateLimitContract()
    {
        var rateLimitServiceMock = new Mock<IUploadRateLimitService>(MockBehavior.Strict);
        rateLimitServiceMock
            .Setup(service => service.AcquireUploadAsync(
                10001,
                It.IsAny<string>(),
                4,
                It.IsAny<TimeSpan?>()))
            .ReturnsAsync(UploadRateLimitCheckResult.Rejected(
                "本分钟上传次数已达上限",
                UploadRateLimitFailureKind.UploadFrequency,
                5,
                5));
        var controller = CreateController(
            CreateAttachmentServiceMock().Object,
            new Mock<IUserService>(MockBehavior.Strict).Object,
            new[] { "Operator" },
            rateLimitServiceMock.Object,
            enableRateLimit: true,
            errorsLocalizer: CreateFormattingLocalizer(
                "error.attachment.upload_frequency_limit_reached",
                arguments => $"Uploaded {arguments[0]} of {arguments[1]} files."));

        var result = await controller.UploadImage(CreateImageFormFile());

        Assert.False(result.IsSuccess);
        Assert.Equal(429, result.StatusCode);
        Assert.Equal(AttachmentErrorCodes.UploadFrequencyLimitReached, result.Code);
        Assert.Equal("error.attachment.upload_frequency_limit_reached", result.MessageKey);
        Assert.Equal("Uploaded 5 of 5 files.", result.MessageInfo);
        Assert.Equal(new object[] { 5, 5 }, result.MessageArguments);
        rateLimitServiceMock.VerifyAll();
    }

    [Fact]
    public async Task UploadImage_Should_Reject_Unknown_BusinessType()
    {
        var attachmentServiceMock = CreateAttachmentServiceMock();
        var controller = CreateController(
            attachmentServiceMock.Object,
            new Mock<IUserService>(MockBehavior.Strict).Object,
            new[] { "Operator" });

        var result = await controller.UploadImage(CreateImageFormFile(), "../../outside");

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(AttachmentErrorCodes.BusinessTypeUnsupported, result.Code);
        Assert.Equal("error.attachment.business_type_unsupported", result.MessageKey);
        attachmentServiceMock.Verify(
            service => service.UploadFileAsync(
                It.IsAny<IFormFile>(),
                It.IsAny<FileUploadOptionsDto>(),
                It.IsAny<long>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task UploadImage_Should_Return_Success_When_Accounting_Fails_After_Persistence()
    {
        var attachmentServiceMock = CreateAttachmentServiceMock();
        attachmentServiceMock
            .Setup(service => service.UploadFileAsync(
                It.IsAny<IFormFile>(),
                It.IsAny<FileUploadOptionsDto>(),
                10001,
                "Tester"))
            .ReturnsAsync(new AttachmentVo { VoId = 42, VoBusinessType = "General" });
        var rateLimitServiceMock = new Mock<IUploadRateLimitService>(MockBehavior.Strict);
        rateLimitServiceMock
            .Setup(service => service.AcquireUploadAsync(
                10001,
                It.IsAny<string>(),
                4,
                It.IsAny<TimeSpan?>()))
            .ReturnsAsync(UploadRateLimitCheckResult.Allowed());
        rateLimitServiceMock
            .Setup(service => service.CompleteUploadAsync(10001, It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));
        var controller = CreateController(
            attachmentServiceMock.Object,
            new Mock<IUserService>(MockBehavior.Strict).Object,
            new[] { "Operator" },
            rateLimitServiceMock.Object,
            enableRateLimit: true);

        var result = await controller.UploadImage(CreateImageFormFile());

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(42, Assert.IsType<AttachmentVo>(result.ResponseData).VoId);
        rateLimitServiceMock.Verify(
            service => service.FailUploadAsync(It.IsAny<long>(), It.IsAny<string>()),
            Times.Never);
        attachmentServiceMock.VerifyAll();
        rateLimitServiceMock.VerifyAll();
    }

    [Fact]
    public async Task UploadImage_Should_Return_StableProcessingFailure_When_Service_ReturnsNull()
    {
        var attachmentServiceMock = CreateAttachmentServiceMock();
        attachmentServiceMock
            .Setup(service => service.UploadFileAsync(
                It.IsAny<IFormFile>(),
                It.IsAny<FileUploadOptionsDto>(),
                10001,
                "Tester"))
            .ReturnsAsync((AttachmentVo?)null);
        var controller = CreateController(
            attachmentServiceMock.Object,
            new Mock<IUserService>(MockBehavior.Strict).Object,
            new[] { "Operator" });

        var result = await controller.UploadImage(CreateImageFormFile());

        Assert.False(result.IsSuccess);
        Assert.Equal(500, result.StatusCode);
        Assert.Equal(AttachmentErrorCodes.ProcessingFailed, result.Code);
        Assert.Equal("error.attachment.processing_failed", result.MessageKey);
        attachmentServiceMock.VerifyAll();
    }

    [Fact]
    public async Task UploadImage_Should_Localize_DirectFailure_From_MessageKey()
    {
        var controller = CreateController(
            CreateAttachmentServiceMock().Object,
            new Mock<IUserService>(MockBehavior.Strict).Object,
            new[] { "Operator" },
            errorsLocalizer: CreateLocalizer(
                "error.attachment.image_type_unsupported",
                "This image format is not supported."));

        var result = await controller.UploadImage(CreateFormFile("archive.zip", "application/zip"));

        Assert.False(result.IsSuccess);
        Assert.Equal("This image format is not supported.", result.MessageInfo);
        Assert.Equal(AttachmentErrorCodes.ImageTypeUnsupported, result.Code);
        Assert.Equal("error.attachment.image_type_unsupported", result.MessageKey);
    }

    [Fact]
    public async Task UploadImage_Should_Localize_Expected_ServiceFailure()
    {
        var attachmentServiceMock = CreateAttachmentServiceMock();
        attachmentServiceMock
            .Setup(service => service.UploadFileAsync(
                It.IsAny<IFormFile>(),
                It.IsAny<FileUploadOptionsDto>(),
                10001,
                "Tester"))
            .ThrowsAsync(new BusinessException(
                "文件处理失败，请稍后重试",
                500,
                AttachmentErrorCodes.ProcessingFailed,
                "error.attachment.processing_failed"));
        var controller = CreateController(
            attachmentServiceMock.Object,
            new Mock<IUserService>(MockBehavior.Strict).Object,
            new[] { "Operator" },
            errorsLocalizer: CreateLocalizer(
                "error.attachment.processing_failed",
                "The file could not be processed. Try again later."));

        var result = await controller.UploadImage(CreateImageFormFile());

        Assert.False(result.IsSuccess);
        Assert.Equal(500, result.StatusCode);
        Assert.Equal("The file could not be processed. Try again later.", result.MessageInfo);
        Assert.Equal(AttachmentErrorCodes.ProcessingFailed, result.Code);
        Assert.Equal("error.attachment.processing_failed", result.MessageKey);
        attachmentServiceMock.VerifyAll();
    }

    [Fact]
    public async Task UploadImage_Should_Return_Success_When_Sticker_WithPermission()
    {
        var attachmentServiceMock = CreateAttachmentServiceMock();
        attachmentServiceMock
            .Setup(s => s.UploadFileAsync(
                It.IsAny<IFormFile>(),
                It.Is<FileUploadOptionsDto>(options => options.BusinessType == "Sticker"),
                10001,
                "Tester"))
            .ReturnsAsync(new AttachmentVo
            {
                VoId = 1,
                VoBusinessType = "Sticker",
                VoUrl = "https://localhost/files/sticker.png"
            });

        var userServiceMock = new Mock<IUserService>(MockBehavior.Strict);
        userServiceMock
            .Setup(s => s.GetPermissionKeysByRolesAsync(It.Is<IReadOnlyCollection<string>>(roles => roles.Count == 1)))
            .ReturnsAsync(new List<string> { ConsolePermissions.StickersBatchUpload });

        var controller = CreateController(
            attachmentServiceMock.Object,
            userServiceMock.Object,
            new[] { "Operator" });

        var result = await controller.UploadImage(CreateImageFormFile(), "Sticker");

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        var payload = Assert.IsType<AttachmentVo>(result.ResponseData);
        Assert.Equal("Sticker", payload.VoBusinessType);
        attachmentServiceMock.VerifyAll();
    }

    [Fact]
    public async Task UploadImage_Should_Not_Require_StickerPermission_For_Avatar()
    {
        var attachmentServiceMock = CreateAttachmentServiceMock();
        attachmentServiceMock
            .Setup(s => s.UploadFileAsync(
                It.IsAny<IFormFile>(),
                It.Is<FileUploadOptionsDto>(options => options.BusinessType == "Avatar"),
                10001,
                "Tester"))
            .ReturnsAsync(new AttachmentVo
            {
                VoId = 2,
                VoBusinessType = "Avatar",
                VoUrl = "https://localhost/files/avatar.png"
            });

        var userServiceMock = new Mock<IUserService>(MockBehavior.Strict);

        var controller = CreateController(
            attachmentServiceMock.Object,
            userServiceMock.Object,
            new[] { "Operator" });

        var result = await controller.UploadImage(CreateImageFormFile(), "Avatar");

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        var payload = Assert.IsType<AttachmentVo>(result.ResponseData);
        Assert.Equal("Avatar", payload.VoBusinessType);
        userServiceMock.Verify(
            s => s.GetPermissionKeysByRolesAsync(It.IsAny<IReadOnlyCollection<string>>()),
            Times.Never);
        attachmentServiceMock.VerifyAll();
    }

    [Fact]
    public async Task DownloadByToken_Should_Return_NotFound_When_Attachment_State_Is_Blocked()
    {
        var attachmentServiceMock = CreateAttachmentServiceMock();
        attachmentServiceMock
            .Setup(service => service.GetDownloadStreamAsync(
                42,
                10001,
                It.Is<List<string>>(roles => roles.SequenceEqual(new[] { "Operator" })),
                AttachmentUrlVariant.Original))
            .ReturnsAsync(((Stream?)null, (AttachmentAssetDto?)null));
        var tokenService = new Mock<IFileAccessTokenService>(MockBehavior.Strict);
        tokenService
            .Setup(service => service.ValidateAndUseTokenAsync("valid-token", 10001, It.IsAny<string>()))
            .ReturnsAsync(42);
        var controller = CreateController(
            attachmentServiceMock.Object,
            new Mock<IUserService>(MockBehavior.Strict).Object,
            new[] { "Operator" },
            fileAccessTokenService: tokenService.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = await controller.DownloadByToken("valid-token");

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var payload = Assert.IsType<MessageModel>(notFound.Value);
        Assert.False(payload.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, payload.StatusCode);
        attachmentServiceMock.VerifyAll();
        tokenService.VerifyAll();
    }

    private static AttachmentController CreateController(
        IAttachmentService attachmentService,
        IUserService userService,
        IReadOnlyList<string> roles,
        IUploadRateLimitService? rateLimitService = null,
        bool enableRateLimit = false,
        IStringLocalizer<Errors>? errorsLocalizer = null,
        IFileAccessTokenService? fileAccessTokenService = null)
    {
        var currentUserAccessorMock = new Mock<ICurrentUserAccessor>();
        currentUserAccessorMock.SetupGet(x => x.Current).Returns(new CurrentUser
        {
            IsAuthenticated = true,
            UserId = 10001,
            UserName = "Tester",
            TenantId = 0,
            Roles = roles
        });

        return new AttachmentController(
            attachmentService,
            currentUserAccessorMock.Object,
            userService,
            rateLimitService ?? new Mock<IUploadRateLimitService>(MockBehavior.Strict).Object,
            Options.Create(new UploadRateLimitOptions { Enable = enableRateLimit }),
            fileAccessTokenService ?? new Mock<IFileAccessTokenService>(MockBehavior.Strict).Object,
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            errorsLocalizer ?? CreateMissingResourceLocalizer(),
            Mock.Of<ILogger<AttachmentController>>());
    }

    private static IStringLocalizer<Errors> CreateMissingResourceLocalizer()
    {
        var localizer = new Mock<IStringLocalizer<Errors>>();
        localizer
            .Setup(item => item[It.IsAny<string>()])
            .Returns((string name) => new LocalizedString(name, name, resourceNotFound: true));
        localizer
            .Setup(item => item[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string name, object[] _) => new LocalizedString(name, name, resourceNotFound: true));
        return localizer.Object;
    }

    private static IStringLocalizer<Errors> CreateLocalizer(string messageKey, string localizedMessage)
    {
        var localizer = new Mock<IStringLocalizer<Errors>>(MockBehavior.Strict);
        localizer
            .Setup(item => item[messageKey])
            .Returns(new LocalizedString(messageKey, localizedMessage));
        return localizer.Object;
    }

    private static IStringLocalizer<Errors> CreateFormattingLocalizer(
        string messageKey,
        Func<object[], string> format)
    {
        var localizer = new Mock<IStringLocalizer<Errors>>(MockBehavior.Strict);
        localizer
            .Setup(item => item[messageKey, It.IsAny<object[]>()])
            .Returns((string key, object[] arguments) => new LocalizedString(key, format(arguments)));
        return localizer.Object;
    }

    private static Mock<IAttachmentService> CreateAttachmentServiceMock()
    {
        return new Mock<IAttachmentService>(MockBehavior.Strict);
    }

    private static IFormFile CreateImageFormFile()
    {
        return CreateFormFile("test.png", "image/png");
    }

    private static IFormFile CreateFormFile(string fileName, string contentType)
    {
        var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
