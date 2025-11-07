using System;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace Radish.Controllers.V1;

/// <summary>
/// 示例测试接口（多版本演示）
/// </summary>
[ApiController]
[Authorize] // 允许 Cookie 或 Bearer（Host 已配置转发）
[ApiVersion(1)]
[ApiVersion(2)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
public class ApiTestController : AbpControllerBase
{
    /// <summary>
    /// 测试接口（V1）
    /// </summary>
    /// <remarks>
    /// 返回当前 API 版本与示例响应。
    /// </remarks>
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
    
    /// <summary>
    /// 测试接口（V2）
    /// </summary>
    /// <remarks>
    /// 返回当前 API 版本与示例响应。
    /// </remarks>
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
