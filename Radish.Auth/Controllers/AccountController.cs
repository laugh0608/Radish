using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace Radish.Auth.Controllers;

/// <summary>
/// 最小可用的账号控制器：
/// - 使用内置测试用户完成 Cookie 登录
/// - 为 OIDC 授权端点提供认证会话
/// </summary>
[Route("[controller]/[action]")]
public class AccountController : Controller
{
    private const string TestUserName = "test";
    private const string TestPassword = "P@ssw0rd!";

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        // 返回一个最简单的 HTML 表单，方便在浏览器中直接登录
        var html = $"""
<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="utf-8" />
    <title>Radish.Auth 登录</title>
</head>
<body>
    <h1>Radish.Auth 测试登录</h1>
    <p>使用内置测试账号登录：</p>
    <ul>
        <li>用户名：<code>{TestUserName}</code></li>
        <li>密码：<code>{TestPassword}</code></li>
    </ul>
    <form method="post" action="/Account/Login">
        <input type="hidden" name="returnUrl" value="{(returnUrl ?? string.Empty)}" />
        <div>
            <label>用户名：<input type="text" name="username" value="{TestUserName}" /></label>
        </div>
        <div>
            <label>密码：<input type="password" name="password" value="{TestPassword}" /></label>
        </div>
        <div>
            <button type="submit">登录</button>
        </div>
    </form>
</body>
</html>
""";

        return Content(html, "text/html; charset=utf-8");
    }

    [HttpPost]
    public async Task<IActionResult> Login([FromForm] string username, [FromForm] string password, [FromForm] string? returnUrl = null)
    {
        if (!string.Equals(username, TestUserName, StringComparison.Ordinal) ||
            !string.Equals(password, TestPassword, StringComparison.Ordinal))
        {
            return Unauthorized("Invalid username or password.");
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

        return Ok("Signed in.");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok("Signed out.");
    }
}
