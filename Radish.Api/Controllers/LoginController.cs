using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Extension.PermissionExtension;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
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
[ApiVersion(1)]
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
    /// 用户登录获取 JWT Token（旧版接口，已迁移到 OIDC）
    /// </summary>
    /// <param name="name">用户名（明文）</param>
    /// <param name="pass">密码（明文，通过 HTTPS 传输加密）</param>
    /// <returns>包含 JWT Token 信息的响应对象</returns>
    /// <remarks>
    /// <para>注意：本接口为早期基于自签 JWT 的登录实现，当前项目已经迁移到 Radish.Auth 提供的 OIDC/OpenIddict 登录流程。</para>
    /// <para>建议客户端改为使用 OIDC 授权码流或直接调用 /connect/token 获取 Access Token。</para>
    /// <para>本接口保留仅用于兼容与调试，后续版本将移除，请不要在新代码中使用。</para>
    /// </remarks>
    /// <response code="410">接口已废弃，请使用 OIDC 登录</response>
    [HttpGet]
    [AllowAnonymous]
    [Obsolete("登录流程已迁移到 OIDC/OpenIddict，此接口仅为兼容保留，后续将删除。请使用 Radish.Auth 的 OIDC 登录流程。")]
    [ProducesResponseType(typeof(MessageModel<TokenInfoVo>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel<TokenInfoVo>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status500InternalServerError)]
    public async Task<MessageModel<TokenInfoVo>> GetJwtToken(string name = "", string pass = "")
    {
        // 已迁移说明：
        // 1. 当前推荐的登录方式是通过 Radish.Auth（OpenIddict）提供的 OIDC 流程获取 Token；
        // 2. 本接口早期使用 JwtTokenGenerate + 对称密钥手动生成 JWT，与现有基于 Authority 的验证逻辑不再一致；
        // 3. 为避免误用，这里直接返回 410 Gone，提示调用方改用 OIDC 登录；
        // 4. 原始实现保留在下面的注释块中，仅用于参考与历史审计，后续版本会删除本接口及相关逻辑。

        await Task.CompletedTask;
        var message = _errorsLocalizer["error.auth.legacy_login_deprecated"] ?? "旧版登录接口已废弃，请使用 OIDC 登录流程。";
        return new MessageModel<TokenInfoVo>
        {
            StatusCode = 410,
            IsSuccess = false,
            MessageInfo = message
        };

        // 历史 JWT 自签实现已移除；身份语义与 Claim 口径以 Radish.Auth 的 OIDC 流程和专项文档为准。
    }
}
