using Microsoft.AspNetCore.Mvc;
using Radish.Common;

namespace Radish.Server.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get()
    {
        return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
    }
    
    [HttpGet]
    public async Task<IActionResult> Test()
    {
        await Task.CompletedTask;
        var res1 = AppSettings.App(new []{"Redis", "Enable"});
        var res2 = AppSettings.GetValue("Redis:ConnectionString");
        return Ok(new
        {
            res1,
            res2
        });
    }
}