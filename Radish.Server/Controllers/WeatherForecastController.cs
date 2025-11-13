using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Radish.Common.CacheTool;
using Radish.Common.CoreTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Server.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class WeatherForecastController : ControllerBase
{
    // 属性注入
    public IBaseService<Role, RoleVo>? RoleServiceObj { get; set; }
    
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBaseService<Role, RoleVo> _roleService;
    private readonly ICaching  _caching;

    /// <summary>
    /// 构造函数，注入服务
    /// </summary>
    /// <param name="scopeFactory"></param>
    /// <param name="roleService"></param>
    /// <param name="caching"></param>
    public WeatherForecastController(IServiceScopeFactory scopeFactory, 
        IBaseService<Role, RoleVo> roleService, 
        ICaching caching)
    {
        _scopeFactory = scopeFactory;
        _roleService = roleService;
        _caching = caching;
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
        var roleList4 = await RoleServiceObj?.QueryAsync()!;
        
        var cacheKey = "radish_key";
        var cacheKeys = await _caching.GetAllCacheKeysAsync();
        await Console.Out.WriteLineAsync("全部 keys -->" + JsonConvert.SerializeObject(cacheKeys));
        await Console.Out.WriteLineAsync("添加一个缓存");
        await _caching.SetStringAsync(cacheKey, "hello radish");
        await Console.Out.WriteLineAsync("全部 keys -->" + JsonConvert.SerializeObject(await _caching.GetAllCacheKeysAsync()));
        await Console.Out.WriteLineAsync("当前 key 内容-->" + JsonConvert.SerializeObject(await _caching.GetStringAsync(cacheKey)));
        await Console.Out.WriteLineAsync("删除 key");
        await _caching.RemoveAsync(cacheKey);
        await Console.Out.WriteLineAsync("全部 keys -->" + JsonConvert.SerializeObject(await _caching.GetAllCacheKeysAsync()));
        
        return Ok(new
        {
            roleList1,
            roleList2,
            roleList3,
            roleList4
        });
    }
}