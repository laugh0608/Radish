using JetBrains.Annotations;
using Microsoft.Extensions.Options;
using Radish.Api.Controllers;
using Xunit;

namespace Radish.Api.Tests.Controllers;

[TestSubject(typeof(WeatherForecastController))]
public class WeatherForecastControllerTest
{
    [Fact]
    public void GetTest()
    {
        var controller = new WeatherForecastController();
        controller.Test();

        Assert.NotNull(controller);
    }
}
