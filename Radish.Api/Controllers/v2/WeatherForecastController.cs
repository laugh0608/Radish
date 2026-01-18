using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Radish.Api.Resources;
using Radish.Common;
using Radish.Common.CacheTool;
using Radish.Common.CoreTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Model.LogModels;
using Serilog;

namespace Radish.Api.Controllers.v2;

[ApiController]
// [Authorize(Roles = "Client")] // 可以写多个
// [Authorize(Policy = "SystemOrAdmin")]
[ApiVersion(2)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Authorize(Policy = "RadishAuthPolicy")]
public class WeatherForecastController : ControllerBase
{
    // 属性注入
    public IBaseService<Role, RoleVo>? RoleServiceObj { get; set; }

    // 常规依赖注入
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBaseService<Role, RoleVo> _roleService;
    private readonly IBaseService<AuditSqlLog, AuditSqlLogVo> _auditSqlLogService;
    private readonly ICaching _caching;
    private readonly ILogger<WeatherForecastController> _logger;

    /// <summary>构造函数，注入服务</summary>
    /// <param name="scopeFactory"></param>
    /// <param name="roleService"></param>
    /// <param name="auditSqlLogService"></param>
    /// <param name="caching"></param>
    /// <param name="logger"></param>
    public WeatherForecastController(IServiceScopeFactory scopeFactory,
        IBaseService<Role, RoleVo> roleService,
        IBaseService<AuditSqlLog, AuditSqlLogVo> auditSqlLogService,
        ICaching caching, ILogger<WeatherForecastController> logger)
    {
        _scopeFactory = scopeFactory;
        _roleService = roleService;
        _auditSqlLogService = auditSqlLogService;
        _caching = caching;
        _logger = logger;
    }

    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    /// <summary>天气测试接口（规范化返回格式）</summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<MessageModel<List<WeatherForecastVo>>> Get()
    {
        // 测试两种日志输出
        Log.Information("Log.Information: Getting weather forecast");
        _logger.LogInformation("_logger.LogInformation: Getting weather forecast");

        var forecasts = await Task.Run(() => Enumerable.Range(1, 5).Select(index => new WeatherForecastVo
            {
                VoDate = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                VoTemperatureC = Random.Shared.Next(-20, 55),
                VoTemperatureF = 32 + (int)(Random.Shared.Next(-20, 55) / 0.5556),
                VoSummary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToList());

        return MessageModel<List<WeatherForecastVo>>.Success("获取成功", forecasts);
    }

    /// <summary>
    /// 标准返回格式示例：使用 MessageModel 包裹天气数据，演示 code + messageKey + messageInfo
    /// </summary>
    /// <param name="fail">为 true 时返回一个模拟的业务错误</param>
    /// <returns>统一格式的天气预报响应</returns>
    [HttpGet]
    public ActionResult<MessageModel<IEnumerable<WeatherForecastVo>>> GetStandard(bool fail = false)
    {
        var localizer = HttpContext.RequestServices.GetRequiredService<IStringLocalizer<Errors>>();

        if (fail)
        {
            var failMessage = localizer["error.weather.load_failed"];
            var failResult = MessageModel<IEnumerable<WeatherForecastVo>>.Failed(
                failMessage,
                code: "Weather.LoadFailed",
                messageKey: "error.weather.load_failed");
            failResult.StatusCode = StatusCodes.Status500InternalServerError;
            return StatusCode(StatusCodes.Status500InternalServerError, failResult);
        }

        var forecasts = Enumerable.Range(1, 5).Select(index => new WeatherForecastVo
            {
                VoDate = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                VoTemperatureC = Random.Shared.Next(-20, 55),
                VoSummary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();

        var successMessage = localizer["info.weather.load_success"];
        var successResult = MessageModel<IEnumerable<WeatherForecastVo>>.Success(
            successMessage,
            forecasts,
            code: "Weather.LoadSuccess",
            messageKey: "info.weather.load_success");
        successResult.StatusCode = StatusCodes.Status200OK;

        return Ok(successResult);
    }

    /// <summary>路径参数测试接口</summary>
    /// <remarks>
    /// <para>如果使用了路径参数，那么在 ApiModule 表中存 URL 的时候必须加上正则匹配：</para>
    /// <para>例如：/api/GetById/\d+</para>
    /// <para>api 前面的根符号别忘了</para>
    /// </remarks>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpGet("{id}")] // 效果：api/WeatherForecast/GetById/1
    public async Task<MessageModel<GetByIdResultVo>> GetById(int id)
    {
        var result = await Task.Run(() => new GetByIdResultVo { Id = id, Name = "Radish" });
        return MessageModel<GetByIdResultVo>.Success("获取成功", result);
    }

    /// <summary>普通依赖注入和对象关系映射测试接口</summary>
    /// <returns></returns>
    /// <example>{"name":"Sample","value":42}</example>
    /// <response code="200">Returns the requested project board.</response>
    /// <response code="404">If the project board is not found.</response>
    [HttpGet]
    public async Task<MessageModel<TestResultVo>> Test()
    {
        // 测试普通依赖注入和对象关系映射
        var roleList5 = await _roleService.QueryAsync();

        var result = new TestResultVo
        {
            RoleList5 = roleList5
        };

        return MessageModel<TestResultVo>.Success("获取成功", result);
    }

    /// <summary>Redis/内存测试接口</summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<MessageModel<CacheTestResultVo>> CacheTest()
    {
        // 测试 Redis/内存 缓存
        const string cacheKey = "radish_test_key";
        var cacheKeysBeforeSet = await _caching.GetAllCacheKeysAsync();

        await _caching.SetStringAsync(cacheKey, "hello radish");
        var cacheKeysAfterSet = await _caching.GetAllCacheKeysAsync();
        var cacheValue = await _caching.GetStringAsync(cacheKey);

        await _caching.RemoveAsync(cacheKey);
        var cacheKeysAfterRemove = await _caching.GetAllCacheKeysAsync();

        var result = new CacheTestResultVo
        {
            CacheKey = cacheKey,
            CacheKeysBeforeSet = cacheKeysBeforeSet,
            CacheKeysAfterSet = cacheKeysAfterSet,
            CacheValue = cacheValue,
            CacheKeysAfterRemove = cacheKeysAfterRemove
        };

        return MessageModel<CacheTestResultVo>.Success("获取成功", result);
    }

    /// <summary>工厂服务测试接口</summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<MessageModel<ScopeTestResultVo>> ScopeTest()
    {
        // 测试工厂服务
        using var scope = _scopeFactory.CreateScope();
        var dataStatisticService1 = scope.ServiceProvider.GetRequiredService<IBaseService<Role, RoleVo>>();
        var roleList1 = await dataStatisticService1.QueryAsync();
        var dataStatisticService2 = scope.ServiceProvider.GetRequiredService<IBaseService<Role, RoleVo>>();
        var roleList2 = await dataStatisticService2.QueryAsync();

        var result = new ScopeTestResultVo
        {
            RoleList1 = roleList1,
            RoleList2 = roleList2
        };

        return MessageModel<ScopeTestResultVo>.Success("获取成功", result);
    }

    /// <summary>App 扩展获取服务测试接口</summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<MessageModel<AppTestResultVo>> AppTest()
    {
        // 测试 App.GetService 获取服务
        var dataStatisticService3 = App.GetService<IBaseService<Role, RoleVo>>(false);
        var roleList3 = await dataStatisticService3.QueryAsync();

        var result = new AppTestResultVo
        {
            RoleList3 = roleList3
        };

        return MessageModel<AppTestResultVo>.Success("获取成功", result);
    }

    /// <summary>属性注入测试接口</summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<MessageModel<ServiceObjTestResultVo>> ServiceObjTest()
    {
        // 测试属性注入
        var roleList4 = await RoleServiceObj?.QueryAsync()!;

        var result = new ServiceObjTestResultVo
        {
            RoleList4 = roleList4
        };

        return MessageModel<ServiceObjTestResultVo>.Success("获取成功", result);
    }

    /// <summary>多库-日志库测试接口</summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<IActionResult> TenantTest()
    {
        // 测试多库之 AuditSqlLog 单独日志库
        var res = await _auditSqlLogService.QueryAsync();

        return Ok(res);
    }

    /// <summary>分表-日志库测试接口</summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<IActionResult> SplitTest()
    {
        var timeSpan = DateTime.Now.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var id = timeSpan.TotalSeconds.ObjToLong();
        await _auditSqlLogService.AddSplitAsync(new AuditSqlLog()
        {
            Id = id,
            // DateTime = Convert.ToDateTime("2023-12-23"),
            DateTime = DateTime.Now
        });

        var res = await _auditSqlLogService.QuerySplitAsync(it => true);

        return Ok(res);
    }
}

public class WeatherForecast
{
    public DateOnly Date { get; set; }

    public int TemperatureC { get; set; }

    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

    public string? Summary { get; set; }
}