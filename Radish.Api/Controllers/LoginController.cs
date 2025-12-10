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
using Microsoft.Extensions.Localization;
using Radish.Api.Resources;

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
    private readonly IStringLocalizer<Errors> _errorsLocalizer;

    public LoginController(IUserService userService, ILogger<LoginController> logger, PermissionRequirement requirement, IStringLocalizer<Errors> errorsLocalizer)
    {
        _userService = userService;
        _logger = logger;
        _requirement = requirement;
        _errorsLocalizer = errorsLocalizer;
    }

    /// <summary>
    /// 用户登录获取 JWT Token
    /// </summary>
    /// <param name="name">用户名（明文）</param>
    /// <param name="pass">密码（明文，通过 HTTPS 传输加密）</param>
    /// <returns>包含 JWT Token 信息的响应对象</returns>
    /// <remarks>
    /// <para>登录流程：</para>
    /// <list type="number">
    /// <item>查询用户名对应的用户记录</item>
    /// <item>使用 Argon2id 验证密码</item>
    /// <item>验证成功后生成包含用户信息和角色的 JWT Token</item>
    /// <item>Token 有效期为 12 小时</item>
    /// </list>
    /// <para>安全说明：</para>
    /// <list type="bullet">
    /// <item>密码通过 HTTPS 传输，由 TLS 层提供加密保护</item>
    /// <item>密码在数据库中使用 Argon2id 算法存储（抗暴力破解）</item>
    /// <item>详细的密码安全策略请参阅：radish.docs/docs/PasswordSecurity.md</item>
    /// </list>
    /// <para>请求示例：</para>
    /// <code>
    /// GET /api/Login/GetJwtToken?name=admin&amp;pass=admin123456
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
        Log.Information($"用户登录尝试 -- {name}");
        _logger.LogInformation($"用户登录尝试 -- {name}");

        // 1. 查询用户（不再在数据库层比对密码）
        var user = await _userService.QueryAsync(d =>
            d.LoginName == name && d.IsDeleted == false);

        if (user.Count == 0)
        {
            var failMessage = _errorsLocalizer["error.auth.invalid_credentials"];
            return MessageModel<TokenInfoVo>.Failed(
                failMessage,
                code: "Auth.InvalidCredentials",
                messageKey: "error.auth.invalid_credentials");
        }

        var firstUser = user.FirstOrDefault();
        if (firstUser == null)
        {
            var failMessage = _errorsLocalizer["error.auth.invalid_credentials"];
            return MessageModel<TokenInfoVo>.Failed(
                failMessage,
                code: "Auth.InvalidCredentials",
                messageKey: "error.auth.invalid_credentials");
        }

        // 2. 使用 Argon2id 验证密码
        if (!PasswordHasher.VerifyPassword(pass, firstUser.VoLoPwd))
        {
            Log.Warning($"用户 {name} 密码验证失败");
            var failMessage = _errorsLocalizer["error.auth.invalid_credentials"];
            return MessageModel<TokenInfoVo>.Failed(
                failMessage,
                code: "Auth.InvalidCredentials",
                messageKey: "error.auth.invalid_credentials");
        }

        // 3. 密码验证成功，生成 Token
        var userId = firstUser.Uuid;
        var tenantId = firstUser.VoTenId;

        // 获取用户角色（注意：这里需要传递用户名和哈希后的密码）
        var userRoles = await _userService.GetUserRoleNameStrAsync(name, firstUser.VoLoPwd);

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

        Log.Information($"用户 {name} 登录成功");
        var successMessage = _errorsLocalizer["error.auth.login_success"];
        return MessageModel<TokenInfoVo>.Success(
            successMessage,
            token,
            code: "Auth.LoginSuccess",
            messageKey: "error.auth.login_success");
    }
}