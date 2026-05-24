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
}
