using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Radish.Common;
using Radish.Common.CoreTool;
using Radish.Common.OptionTool;

namespace Radish.Api.Controllers;

/// <summary>
/// 应用配置管理控制器 (v2)
/// </summary>
/// <remarks>
/// 提供应用配置信息查询接口。
/// 此接口为 v2 版本，演示版本控制功能。
/// </remarks>
[ApiController]
[ApiVersion(2)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Authorize(Policy = "RadishAuthPolicy")]
[Tags("系统管理")]
public class AppSettingController : ControllerBase
{
    private readonly IOptions<RedisOptions>  _redisOptions;

    public AppSettingController(IOptions<RedisOptions> redisOptions)
    {
        _redisOptions = redisOptions;
    }
    
    /// <summary>
    /// 获取 Redis 配置信息
    /// </summary>
    /// <returns>Redis 配置详情</returns>
    /// <remarks>
    /// 演示多种获取配置的方式：
    /// - AppSettingsTool.RadishApp
    /// - AppSettingsTool.GetValue
    /// - IOptions 注入
    /// - App.GetOptions
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRedisConfig()
    {
        await Task.CompletedTask;
        // 不同的获取 appsetting 的方式 AppSettingController
        var res1 = AppSettingsTool.RadishApp(new []{"Redis", "Enable"});
        var res2 = AppSettingsTool.GetValue("Redis:ConnectionString");
        // var res3 = JsonConvert.SerializeObject(_redisOptions.Value);
        var res3 = _redisOptions.Value.InstanceName;
        var res4 = App.GetOptions<RedisOptions>();
        return Ok(new
        {
            res1,
            res2,
            res3,
            res4
        });
    }
    
}
