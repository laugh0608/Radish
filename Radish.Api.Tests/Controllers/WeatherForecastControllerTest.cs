using System.Threading.Tasks;
using JetBrains.Annotations;
using Radish.Api.Controllers;
using Xunit;

namespace Radish.Api.Tests.Controllers;

[TestSubject(typeof(WeatherForecastController))]
public class WeatherForecastControllerTest
{
    [Fact]
    public async Task GetTest()
    {
        var controller = new WeatherForecastController();
        var res = await controller.Test();

        Assert.NotNull(res);
    }
}
