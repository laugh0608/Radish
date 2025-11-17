using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Radish.Common;
using Radish.Common.CoreTool;
using Radish.Common.OptionTool;

namespace Radish.Api.Controllers;

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
