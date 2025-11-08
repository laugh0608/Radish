using JetBrains.Annotations;
using Radish.Server.Controllers;
using Xunit;

namespace Radish.Server.Tests.Controllers;

[TestSubject(typeof(WeatherForecastController))]
public class WeatherForecastControllerTest
{

    [Fact]
    public void GetTest()
    {
        var controller = new WeatherForecastController();
        controller.Get();
        
        Assert.NotNull(controller);
    }
}