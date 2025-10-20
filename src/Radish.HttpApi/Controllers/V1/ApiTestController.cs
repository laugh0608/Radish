using System;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace Radish.Controllers.V1;

[ApiController]
[ApiVersion(1)]
[ApiVersion(2)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
public class ApiTestController : AbpControllerBase
{
    [HttpGet]
    [MapToApiVersion(1)]
    public IActionResult TestApiV1()
    {
        return Ok(new
        {
            ApiVersion = "V1",
            StatusCode = 0,
            StatusMessage = "获取成功",
            OperatingTime = DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd HH:mm:ss"),
        });
    }
    
    [HttpGet]
    [MapToApiVersion(2)]
    public IActionResult TestApiV2()
    {
        return Ok(new
        {
            ApiVersion = "V2",
            StatusCode = 0,
            StatusMessage = "获取成功",
            OperatingTime = DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd HH:mm:ss"),
        });
    }
}