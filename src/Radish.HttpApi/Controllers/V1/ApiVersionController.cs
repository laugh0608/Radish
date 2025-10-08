// using System;
// using Microsoft.AspNetCore.Mvc;
// using Radish.Extensions;
// using Volo.Abp.AspNetCore.Mvc;
// using static Radish.Extensions.CustomApiVersion;
//
// namespace Radish.Controllers.V1;
//
// /*
//  * @luobo 2025.10.6
//  * 写法一：控制类级定义 API 版本
//  * 1. 方法的 HTTP 协议需要带 API 名称，例如：[HttpGet("GetApiVersion")]
//  * 2. 控制类的 [CustomRoute(ApiVersions.XX)] 特性必须在 [ApiController] 特性上方或前方
//  * 3. 不需要在控制类上定义 [Route("api/[controller]")] 特性，否则会出现两个相同的接口
//  *
//  * 下方示例的 API 路径为：/api/v1/ApiVersion/GetApiVersion
//  *
//  * 写法二见 V1Beta 文件夹示例。
//  */
//
// [CustomRoute(ApiVersions.V1)] // 注：该种类的全局写法 CustomRoute 必须在 ApiController 前方
// [ApiController]
// public class ApiVersionController : AbpControllerBase
// {
//     [HttpGet("GetApiVersion")]
//     public IActionResult GetApiVersion()
//     {
//         return Ok(new
//         {
//             ApiVersion = ApiVersions.V1,
//             StatusCode = 0,
//             StatusMessage = "获取成功",
//             OperatingTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
//         });
//     }
// }