using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Radish.Common.CacheTool;
using Radish.Common.CoreTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.LogModels;
using Radish.Model.ViewModels;

namespace Radish.Api.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class WeatherForecastController : ControllerBase
{
    // 属性注入
    public IBaseService<Role, RoleVo>? RoleServiceObj { get; set; }

    // 常规依赖注入
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBaseService<Role, RoleVo> _roleService;
    private readonly IBaseService<AuditSqlLog, AuditSqlLogVo> _auditSqlLogService;
    private readonly ICaching _caching;

    /// <summary>构造函数，注入服务</summary>
    /// <param name="scopeFactory"></param>
    /// <param name="roleService"></param>
    /// <param name="auditSqlLogService"></param>
    /// <param name="caching"></param>
    public WeatherForecastController(IServiceScopeFactory scopeFactory,
        IBaseService<Role, RoleVo> roleService,
        IBaseService<AuditSqlLog, AuditSqlLogVo> auditSqlLogService,
        ICaching caching)
    {
        _scopeFactory = scopeFactory;
        _roleService = roleService;
        _auditSqlLogService = auditSqlLogService;
        _caching = caching;
    }

    /// <summary>构造函数，无参数</summary>
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

    [HttpGet("{id}")] // 效果：api/WeatherForecast/GetById/1
    public async Task<IActionResult> GetById(int id) {
        var res = await Task.Run(() => new { Id = id, Name = "Radish"});
        return Ok(res);
    }

    [HttpGet]
    public async Task<IActionResult> Test()
    {
        // await Task.CompletedTask;
        // 测试工厂服务
        using var scope = _scopeFactory.CreateScope();
        var dataStatisticService1 = scope.ServiceProvider.GetRequiredService<IBaseService<Role, RoleVo>>();
        var roleList1 = await dataStatisticService1.QueryAsync();
        var dataStatisticService2 = scope.ServiceProvider.GetRequiredService<IBaseService<Role, RoleVo>>();
        var roleList2 = await dataStatisticService2.QueryAsync();
        // 测试 App.GetService 获取服务
        var dataStatisticService3 = App.GetService<IBaseService<Role, RoleVo>>(false);
        var roleList3 = await dataStatisticService3.QueryAsync();
        // 测试属性注入
        var roleList4 = await RoleServiceObj?.QueryAsync()!;
        // 测试 Redis/内存 缓存
        const string cacheKey = "radish_test_key";
        var cacheKeys = await _caching.GetAllCacheKeysAsync();
        await Console.Out.WriteLineAsync("全部 keys -->" + JsonConvert.SerializeObject(cacheKeys));
        await Console.Out.WriteLineAsync("添加一个缓存");
        await _caching.SetStringAsync(cacheKey, "hello radish");
        await Console.Out.WriteLineAsync("全部 keys -->" +
                                         JsonConvert.SerializeObject(await _caching.GetAllCacheKeysAsync()));
        await Console.Out.WriteLineAsync("当前 key 内容-->" +
                                         JsonConvert.SerializeObject(await _caching.GetStringAsync(cacheKey)));
        await Console.Out.WriteLineAsync("删除 key");
        await _caching.RemoveAsync(cacheKey);
        await Console.Out.WriteLineAsync("全部 keys -->" +
                                         JsonConvert.SerializeObject(await _caching.GetAllCacheKeysAsync()));
        // 测试普通依赖注入和对象关系映射
        var roleList5 = await _roleService.QueryAsync();
        // 测试多库之 AuditSqlLog 单独日志库
        var res = _auditSqlLogService.QueryAsync();

        return Ok(new
        {
            //roleList1,
            //roleList2,
            //roleList3,
            //roleList4,
            //roleList5,
            res
        });
    }
}