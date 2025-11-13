using Microsoft.AspNetCore.Mvc;
using Radish.Common.Core;
using Radish.Common.Option;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Server.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class WeatherForecastController : ControllerBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBaseService<Role, RoleVo> _roleService;

    /// <summary>
    /// 构造函数，注入服务
    /// </summary>
    /// <param name="scopeFactory"></param>
    /// <param name="roleService"></param>
    public WeatherForecastController(IServiceScopeFactory scopeFactory, IBaseService<Role, RoleVo> roleService)
    {
        _scopeFactory = scopeFactory;
        _roleService = roleService;
    }

    /// <summary>
    /// 构造函数，无参数
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    public WeatherForecastController()
    {
        throw new NotImplementedException();
    }

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
    public async Task<IActionResult> GetTest()
    {
        // await Task.CompletedTask;
        using var scope = _scopeFactory.CreateScope();
        var dataStatisticService1 = scope.ServiceProvider.GetRequiredService<IBaseService<Role, RoleVo>>();
        var roleList1 = await dataStatisticService1.QueryAsync();
        var dataStatisticService2 = scope.ServiceProvider.GetRequiredService<IBaseService<Role, RoleVo>>();
        var roleList2 = await dataStatisticService2.QueryAsync();
        var dataStatisticService3 = App.GetService<IBaseService<Role, RoleVo>>(false);
        var roleList3 = await dataStatisticService3.QueryAsync();
        
        return Ok(new
        {
            roleList1,
            roleList2,
            roleList3
        });
    }
}