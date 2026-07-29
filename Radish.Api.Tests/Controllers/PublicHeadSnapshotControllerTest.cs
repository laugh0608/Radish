using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Radish.Api.Controllers;
using Radish.IService;
using Radish.Model.ViewModels;
using Xunit;

namespace Radish.Api.Tests.Controllers;

public class PublicHeadSnapshotControllerTest
{
    [Fact]
    public async Task GetStaticRoute_Should_Use_Forwarded_Public_Base_Url_When_Config_Is_Missing()
    {
        var capturedPublicBaseUrl = string.Empty;
        var service = new Mock<IPublicHeadSnapshotService>(MockBehavior.Strict);
        service
            .Setup(snapshotService => snapshotService.GetStaticRouteSnapshotAsync("discover", It.IsAny<string>()))
            .Callback<string, string>((_, publicBaseUrl) => capturedPublicBaseUrl = publicBaseUrl)
            .ReturnsAsync(new PublicHeadSnapshotVo
            {
                VoTitle = "社区发现 - Radish",
                VoCanonicalUrl = "https://localhost:5000/discover"
            });

        var controller = new PublicHeadSnapshotController(service.Object, new ConfigurationBuilder().Build())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.Request.Scheme = "http";
        controller.Request.Host = new HostString("localhost", 5100);
        controller.Request.Headers["X-Forwarded-Proto"] = "https";
        controller.Request.Headers["X-Forwarded-Host"] = "localhost:5000";

        var result = await controller.GetStaticRoute("discover");

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("https://localhost:5000", capturedPublicBaseUrl);
    }

    [Fact]
    public async Task GetForumPost_Should_Use_Forwarded_Public_Base_Url_When_Config_Is_Missing()
    {
        var capturedPublicBaseUrl = string.Empty;
        var service = new Mock<IPublicHeadSnapshotService>(MockBehavior.Strict);
        service
            .Setup(snapshotService => snapshotService.GetForumPostSnapshotAsync("pst_test", It.IsAny<string>()))
            .Callback<string, string>((_, publicBaseUrl) => capturedPublicBaseUrl = publicBaseUrl)
            .ReturnsAsync(new PublicHeadSnapshotVo
            {
                VoTitle = "测试帖子",
                VoCanonicalUrl = "https://localhost:5000/forum/post/pst_test"
            });

        var controller = new PublicHeadSnapshotController(service.Object, new ConfigurationBuilder().Build())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.Request.Scheme = "http";
        controller.Request.Host = new HostString("localhost", 5100);
        controller.Request.Headers["X-Forwarded-Proto"] = "https";
        controller.Request.Headers["X-Forwarded-Host"] = "localhost:5000";

        var result = await controller.GetForumPost("pst_test");

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("https://localhost:5000", capturedPublicBaseUrl);
    }

    [Fact]
    public async Task GetForumTag_Should_Return_Tag_Snapshot()
    {
        var service = new Mock<IPublicHeadSnapshotService>(MockBehavior.Strict);
        service
            .Setup(snapshotService => snapshotService.GetForumTagSnapshotAsync("csharp", It.IsAny<string>()))
            .ReturnsAsync(new PublicHeadSnapshotVo
            {
                VoTitle = "C# - Radish 论坛",
                VoCanonicalUrl = "https://localhost:5000/forum/tag/csharp"
            });

        var controller = new PublicHeadSnapshotController(service.Object, new ConfigurationBuilder().Build())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.Request.Scheme = "https";
        controller.Request.Host = new HostString("localhost", 5000);

        var result = await controller.GetForumTag("csharp");

        Assert.IsType<OkObjectResult>(result);
        service.Verify(
            snapshotService => snapshotService.GetForumTagSnapshotAsync(
                "csharp",
                "https://localhost:5000"),
            Times.Once);
    }
}
