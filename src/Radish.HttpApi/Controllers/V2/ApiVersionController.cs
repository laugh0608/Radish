using System;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using static Radish.Extensions.CustomApiVersion;

namespace Radish.Controllers.V2;

[ApiVersion(2)] // 必须要是 AutoApiVersions 中有的版本号，小版本号为 0 则省略，不建议加小版本号
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[ApiController]
public class ApiVersionController : AbpControllerBase
{
    [HttpGet]
    public IActionResult GetApiVersion()
    {
        return Ok(new
        {
            ApiVersion = ApiVersions.V1,
            StatusCode = 0,
            StatusMessage = "获取成功",
            OperatingTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        });
    }
}