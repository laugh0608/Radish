// using System;
// using Microsoft.AspNetCore.Mvc;
// using Volo.Abp.AspNetCore.Mvc;
// using Radish.Extensions;
// using static Radish.Extensions.CustomApiVersion;
//
// namespace Radish.Controllers.V1Beta;
//
// // @luobo 2025.10.7 该方法和版本约定已弃用，请看 V1 和 V2 文件夹示例。
//
// /*
//  * @luobo 2025.10.6
//  * 写法二：方法级定义 API 版本
//  * 1. 方法的 HTTP 协议不需要带 API 名称，例如：[HttpGet]
//  * 2. 需要在控制类上定义 [Route("api/[controller]")] 特性
//  * 3. 方法的特性必须加上 ActionName 参数，例如：[CustomRoute(ApiVersions.V1Beta, "GetApiVersion"] 
//  *
//  * 下方示例的 API 路径为：/api/v1beta/ApiVersion/GetApiVersion
//  *
//  * 写法一见 V1 文件夹示例。
//  */
//
// [ApiController]
// [Route("api/[controller]")]
// public class ApiVersionController : AbpControllerBase
// {
//     [HttpGet]
//     [CustomRoute(ApiVersions.V1Beta, "GetApiVersion")]
//     public IActionResult GetApiVersion()
//     {
//         return Ok(new
//         {
//             ApiVersion = ApiVersions.V1Beta,
//             StatusCode = 0,
//             StatusMessage = "获取成功",
//             OperatingTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
//         });
//     }
// }