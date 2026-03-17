using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Api.Controllers;
using Radish.Common.HttpContextTool;
using Radish.Common.OptionTool;
using Radish.Common.PermissionTool;
using Radish.IService;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
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
        Assert.Equal("当前账号暂无该表情上传权限", result.MessageInfo);
        attachmentServiceMock.Verify(
            s => s.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<FileUploadOptionsDto>(), It.IsAny<long>(), It.IsAny<string>()),
            Times.Never);
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

    private static AttachmentController CreateController(
        IAttachmentService attachmentService,
        IUserService userService,
        IReadOnlyList<string> roles)
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

        var rateLimitServiceMock = new Mock<IUploadRateLimitService>(MockBehavior.Strict);
        var fileAccessTokenServiceMock = new Mock<IFileAccessTokenService>(MockBehavior.Strict);

        return new AttachmentController(
            attachmentService,
            currentUserAccessorMock.Object,
            userService,
            rateLimitServiceMock.Object,
            Options.Create(new UploadRateLimitOptions { Enable = false }),
            fileAccessTokenServiceMock.Object);
    }

    private static Mock<IAttachmentService> CreateAttachmentServiceMock()
    {
        return new Mock<IAttachmentService>(MockBehavior.Strict);
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
