using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Radish.Auth.Resources;

namespace Radish.Auth.Controllers;

/// <summary>
/// 账号控制器：
/// - 使用内置测试用户完成 Cookie 登录
/// - 为 OIDC 授权端点提供认证会话
/// - 提供一个基于 Razor View + .resx 文案的登录页面
/// </summary>
[Route("[controller]/[action]")]
public class AccountController : Controller
{
    private const string TestUserName = "test";
    private const string TestPassword = "P@ssw0rd!";

    private readonly IStringLocalizer<Errors> _errorsLocalizer;

    public AccountController(IStringLocalizer<Errors> errorsLocalizer)
    {
        _errorsLocalizer = errorsLocalizer;
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
        if (!string.Equals(username, TestUserName, StringComparison.Ordinal) ||
            !string.Equals(password, TestPassword, StringComparison.Ordinal))
        {
            // 登录失败：使用本地化错误文案，并回到登录页
            TempData["LoginError"] = _errorsLocalizer["auth.login.error.invalidCredentials"];
            return RedirectToAction(nameof(Login), new { returnUrl, username });
        }

        // 模拟一个固定的用户 Id 与租户 Id（与 DbMigrate 种子保持一致）
        const string userId = "20002";
        const string tenantId = "30000";

        var claims = new List<Claim>
        {
            // 标准身份标识
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, "System"),

            // 为 OIDC 兼容再写一份 sub/name
            new("sub", userId),
            new("name", username),

            // 多租户标识（与 AuthenticationGuide 中的约定一致）
            new("tenant_id", tenantId)
        };

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