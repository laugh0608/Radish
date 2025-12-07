using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Common;
using Radish.Common.HelpTool;
using Radish.Extension;
using Radish.Extension.PermissionExtension;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Serilog;

namespace Radish.Api.Controllers;

/// <summary>
/// 登录认证控制器
/// </summary>
/// <remarks>
/// 提供用户登录、获取 Token 等认证相关接口。
/// 所有接口均返回统一的 MessageModel 格式。
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("认证管理")]
public class LoginController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<LoginController> _logger;
    private readonly PermissionRequirement _requirement;

    public LoginController(IUserService userService, ILogger<LoginController> logger, PermissionRequirement requirement)
    {
        _userService = userService;
        _logger = logger;
        _requirement = requirement;
    }

    /// <summary>
    /// 用户登录获取 JWT Token
    /// </summary>
    /// <param name="name">用户名（明文）</param>
    /// <param name="pass">密码（明文，传输时建议使用 RSA 加密）</param>
    /// <returns>包含 JWT Token 信息的响应对象</returns>
    /// <remarks>
    /// <para>登录流程：</para>
    /// <list type="number">
    /// <item>密码使用 MD5 加密后与数据库比对</item>
    /// <item>验证成功后生成包含用户信息和角色的 JWT Token</item>
    /// <item>Token 有效期为 12 小时</item>
    /// </list>
    /// <para>请求示例：</para>
    /// <code>
    /// GET /api/Login/GetJwtToken?name=admin&amp;pass=123456
    /// </code>
    /// <para>成功响应示例：</para>
    /// <code>
    /// {
    ///   "statusCode": 200,
    ///   "isSuccess": true,
    ///   "messageInfo": "获取成功",
    ///   "responseData": {
    ///     "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    ///     "expires": 43200
    ///   }
    /// }
    /// </code>
    /// <para>失败响应示例：</para>
    /// <code>
    /// {
    ///   "statusCode": 401,
    ///   "isSuccess": false,
    ///   "messageInfo": "认证失败",
    ///   "responseData": null
    /// }
    /// </code>
    /// </remarks>
    /// <response code="200">登录成功，返回 Token 信息</response>
    /// <response code="401">认证失败，用户名或密码错误</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel<TokenInfoVo>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel<TokenInfoVo>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status500InternalServerError)]
    public async Task<MessageModel<TokenInfoVo>> GetJwtToken(string name = "", string pass = "")
    {
        // string jwtStr = string.Empty;

        // 用 32 位 MD5 加密密码
        pass = Md5Helper.Md5Encrypt32(pass);

        Log.Information($"自定义日志 -- {name}-{pass}");
        _logger.LogInformation($"自定义日志 -- {name}-{pass}");

        var user = await _userService.QueryAsync(d =>
            d.LoginName == name && d.LoginPassword == pass && d.IsDeleted == false);
        if (user.Count > 0)
        {
            var userRoles = await _userService.GetUserRoleNameStrAsync(name, pass);
            var firstUser = user.FirstOrDefault();
            var userId = firstUser?.Uuid ?? 0;
            var tenantId = firstUser?.VoTenId ?? 0;

            var claims = new List<Claim>
            {
                // 统一身份标识：优先使用 OIDC 风格的 sub/name/tenant_id
                new Claim("sub", userId.ToString()),
                new Claim("name", name),
                new Claim("tenant_id", tenantId.ToString()),

                // 兼容旧版：Name/Uuid/TenantId 仍然保留，方便历史代码解析
                new Claim(ClaimTypes.Name, name),
                new Claim(JwtRegisteredClaimNames.Jti, userId.ToString()),
                new Claim("TenantId", tenantId.ToString()),

                new Claim(JwtRegisteredClaimNames.Iat, DateTime.Now.DateToTimeStamp()),
                new Claim(ClaimTypes.Expiration,
                    DateTime.Now.AddSeconds(60 * 60 * 12).ToString()) // Token 有效期，单位 s
            };
            // 如果是基于用户的授权策略，这里要添加用户，如果是基于角色的授权策略，这里要添加角色
            claims.AddRange(userRoles.Split(',').Select(s => new Claim(ClaimTypes.Role, s)));
            
            var token = JwtTokenGenerate.BuildJwtToken(claims.ToArray(), _requirement);
            
            return MessageModel<TokenInfoVo>.Success("获取成功", token);
        }

        return MessageModel<TokenInfoVo>.Failed("认证失败");
    }
}