using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Radish.Auth.Resources;
using Radish.Common.HelpTool;
using Radish.IService;
using System.Linq;

namespace Radish.Auth.Controllers;

/// <summary>
/// 账号控制器：
/// - 使用业务用户表完成 Cookie 登录
/// - 为 OIDC 授权端点提供认证会话
/// - 提供一个基于 Razor View + .resx 文案的登录页面
/// </summary>
[Route("[controller]/[action]")]
public class AccountController : Controller
{
    private readonly IStringLocalizer<Errors> _errorsLocalizer;
    private readonly IUserService _userService;

    public AccountController(IStringLocalizer<Errors> errorsLocalizer, IUserService userService)
    {
        _errorsLocalizer = errorsLocalizer;
        _userService = userService;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null, string? username = null)
    {
        ViewData[nameof(returnUrl)] = returnUrl;
        ViewData[nameof(username)] = username;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([FromForm] string username, [FromForm] string password, [FromForm] string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            TempData["LoginError"] = _errorsLocalizer["auth.login.error.invalidCredentials"].Value;
            return RedirectToAction(nameof(Login), new { returnUrl, username });
        }

        // 1. 查询用户（使用 Service 层）
        var users = await _userService.QueryAsync(u =>
            u.LoginName == username &&
            u.IsDeleted == false &&
            u.IsEnable);

        var user = users.FirstOrDefault();
        if (user is null)
        {
            TempData["LoginError"] = _errorsLocalizer["auth.login.error.invalidCredentials"].Value;
            return RedirectToAction(nameof(Login), new { returnUrl, username });
        }

        // 2. 使用 Argon2id 验证密码
        if (!PasswordHasher.VerifyPassword(password, user.VoLoPwd))
        {
            TempData["LoginError"] = _errorsLocalizer["auth.login.error.invalidCredentials"].Value;
            return RedirectToAction(nameof(Login), new { returnUrl, username });
        }

        // 3. 密码验证成功，生成会话
        var userId = user.Uuid.ToString();
        var tenantId = user.VoTenId.ToString();

        // 查询用户角色字符串（逗号分隔，传递哈希后的密码）
        var roleNamesStr = await _userService.GetUserRoleNameStrAsync(username, user.VoLoPwd);
        var roleNames = roleNamesStr.Split(',', StringSplitOptions.RemoveEmptyEntries);

        var claims = new List<Claim>
        {
            // 标准身份标识
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, username),

            // OIDC 兼容的 sub/name
            new("sub", userId),
            new("name", username),

            // 多租户标识（与 AuthenticationGuide 中的约定一致）
            new("tenant_id", tenantId)
        };

        foreach (var role in roleNames)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        if (!string.IsNullOrEmpty(returnUrl))
        {
            return Redirect(returnUrl);
        }

        // 直接访问 /Account/Login 并登录成功时，返回一个简单的确认信息
        var successMessage = _errorsLocalizer["auth.login.success.message"];
        return Ok(successMessage);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        var message = _errorsLocalizer["auth.logout.success"];

        return Ok(message);
    }
}
