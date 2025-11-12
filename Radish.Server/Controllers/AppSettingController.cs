using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Radish.Common;
using Radish.Common.Option;

namespace Radish.Server.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class AppSettingController : ControllerBase
{
    private readonly IOptions<RedisOptions>  _redisOptions;

    public AppSettingController(IOptions<RedisOptions> redisOptions)
    {
        _redisOptions = redisOptions;
    }
    
    [HttpGet]
    public async Task<IActionResult> GetRedisConfig()
    {
        await Task.CompletedTask;
        // 三种不同的获取 appsetting 的方式 AppSettingController
        var res1 = AppSettings.RadishApp(new []{"Redis", "Enable"});
        var res2 = AppSettings.GetValue("Redis:ConnectionString");
        // var res3 = JsonConvert.SerializeObject(_redisOptions.Value);
        var res3 = _redisOptions.Value.InstanceName;
        return Ok(new
        {
            res1,
            res2,
            res3
        });
    }
    
}