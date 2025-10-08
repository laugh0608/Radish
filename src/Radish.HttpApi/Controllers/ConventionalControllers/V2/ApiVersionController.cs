using System;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace Radish.Controllers.ConventionalControllers.V2;

[ApiController]
[ApiVersion(2.0)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
public class ApiVersionController : AbpControllerBase
{
    [HttpGet]
    public IActionResult GetApiVersion()
    {
        return Ok(new
        {
            ApiVersion = "V2",
            StatusCode = 0,
            StatusMessage = "获取成功",
            OperatingTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        });
    }
}