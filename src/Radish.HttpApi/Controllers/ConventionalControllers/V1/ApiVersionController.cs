using System;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

/*
 * @luobo 2025.10.8
 * 命名空间级定义 API 版本
 * 1. 需要在控制类上定义 [Route("api/[controller]")] 特性
 * 2. API 的命名空间必须以版本结尾，且和 [ApiVersion(1.0)] 特性一致
 * 3. API 版本必须是 RadishHttpApiHostModule 中 PreConfigureServices 部分定义好的
 * 4. 目前仅支持整数版本号，且小版本号必须为 0
 * 5. 路由特性不建议更改：[Route("api/v{version:apiVersion}/[controller]/[action]")]
 * 6. 自动 API 控制器见 TodoAppService 中的示例（实际上就一个命名空间而已）
 *
 * 设置 Deprecated = true 属性即可在返回体中标识该接口已废弃，例如：[ApiVersion(1.0, Deprecated = true)]
 *
 * 下方示例的 API 路径为：/api/v1/ApiVersion/GetApiVersion
 *
 */

namespace Radish.Controllers.ConventionalControllers.V1;

/// <summary>
/// 版本示例接口（V1）。
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
public class ApiVersionController : AbpControllerBase
{
    /// <summary>
    /// 获取当前接口版本（V1）。
    /// </summary>
    /// <remarks>
    /// 用于演示基于路由的 API 版本控制。
    /// </remarks>
    [HttpGet]
    public IActionResult GetApiVersion()
    {
        return Ok(new
        {
            ApiVersion = "V1",
            StatusCode = 0,
            StatusMessage = "获取成功",
            OperatingTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        });
    }
}
