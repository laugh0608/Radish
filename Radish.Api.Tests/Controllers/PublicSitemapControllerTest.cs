using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Radish.Api.Controllers;
using Radish.IService;
using Xunit;

namespace Radish.Api.Tests.Controllers;

public class PublicSitemapControllerTest
{
    [Fact]
    public async Task GetIndex_Should_Use_Forwarded_Public_Base_Url_When_Config_Is_Missing()
    {
        var capturedPublicBaseUrl = string.Empty;
        var service = new Mock<IPublicSitemapService>(MockBehavior.Strict);
        service
            .Setup(sitemapService => sitemapService.GetIndexXmlAsync(It.IsAny<string>()))
            .Callback<string>(publicBaseUrl => capturedPublicBaseUrl = publicBaseUrl)
            .ReturnsAsync("<sitemapindex />");

        var controller = new PublicSitemapController(service.Object, new ConfigurationBuilder().Build())
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

        var result = await controller.GetIndex();

        Assert.IsType<ContentResult>(result);
        Assert.Equal("https://localhost:5000", capturedPublicBaseUrl);
        service.VerifyAll();
    }
}
