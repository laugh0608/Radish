using System.Collections.Immutable;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using OpenIddict.Abstractions;
using Radish.Auth.Resources;
using Radish.Auth.ViewModels.Account;
using Radish.Common.HelpTool;
using Radish.IService;
using Radish.Model.OpenIddict;
using System.Linq;
using Microsoft.AspNetCore.WebUtilities;
using Serilog;
using Microsoft.AspNetCore.RateLimiting;

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
    private readonly IOpenIddictApplicationManager _applicationManager;

    public AccountController(IStringLocalizer<Errors> errorsLocalizer, IUserService userService, IOpenIddictApplicationManager applicationManager)
    {
        _errorsLocalizer = errorsLocalizer;
        _userService = userService;
        _applicationManager = applicationManager;
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null, string? username = null)
    {
        var model = new LoginViewModel
        {
            ReturnUrl = returnUrl,
            PrefillUserName = username,
            ErrorMessage = TempData["LoginError"] as string,
            Client = await ResolveClientAsync(returnUrl)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
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
            // 标准身份标识 (ClaimTypes)
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, username),

            // OIDC 标准 claims
            new(OpenIddictConstants.Claims.Subject, userId),
            new(OpenIddictConstants.Claims.Name, username),
            new(OpenIddictConstants.Claims.PreferredUsername, user.VoLoName),

            // 多租户标识（与 AuthenticationGuide 中的约定一致）
            new("tenant_id", tenantId)
        };

        // Email claim (如果存在)
        if (!string.IsNullOrWhiteSpace(user.VoUsEmail))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.VoUsEmail));
            claims.Add(new Claim(OpenIddictConstants.Claims.Email, user.VoUsEmail));
        }

        // 真实姓名 (如果存在)
        if (!string.IsNullOrWhiteSpace(user.VoReNa))
        {
            claims.Add(new Claim(OpenIddictConstants.Claims.GivenName, user.VoReNa));
        }

        // 角色 claims
        foreach (var role in roleNames)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim(OpenIddictConstants.Claims.Role, role));
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

    private async Task<ClientSummaryViewModel> ResolveClientAsync(string? returnUrl)
    {
        var clientId = TryGetClientIdFromReturnUrl(returnUrl);

        if (string.IsNullOrWhiteSpace(clientId))
        {
            var fallback = HttpContext.Request.Query["client_id"].FirstOrDefault();
            clientId = string.IsNullOrWhiteSpace(fallback) ? null : fallback;
        }

        if (string.IsNullOrWhiteSpace(clientId))
        {
            Log.Debug("[ResolveClientAsync] No client_id found in URL or query string");
            return ClientSummaryViewModel.Empty;
        }

        Log.Debug("[ResolveClientAsync] Resolved client_id: {ClientId}", clientId);

        try
        {
            var applicationObject = await _applicationManager.FindByClientIdAsync(clientId, HttpContext.RequestAborted);
            if (applicationObject is null)
            {
                Log.Warning("[ResolveClientAsync] Client '{ClientId}' not found in database", clientId);
                return ClientSummaryViewModel.Empty;
            }

            Log.Debug("[ResolveClientAsync] Found application object, type: {TypeName}", applicationObject.GetType().Name);

            if (applicationObject is RadishApplication application)
            {
                Log.Debug("[ResolveClientAsync] Using RadishApplication: {DisplayName}", application.DisplayName);
                return ClientSummaryViewModel.FromApplication(application);
            }

            var clientIdValue = await _applicationManager.GetClientIdAsync(applicationObject, HttpContext.RequestAborted);
            var displayName = await _applicationManager.GetDisplayNameAsync(applicationObject, HttpContext.RequestAborted);
            var properties = await _applicationManager.GetPropertiesAsync(applicationObject, HttpContext.RequestAborted);

            Log.Debug("[ResolveClientAsync] Client: {ClientId}, DisplayName: {DisplayName}, Properties count: {PropertiesCount}",
                clientIdValue, displayName, properties?.Count ?? 0);

            return ClientSummaryViewModel.FromStoreData(clientIdValue, displayName, properties);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ResolveClientAsync] Failed to resolve client from returnUrl");
            return ClientSummaryViewModel.Empty;
        }
    }

    private static string? TryGetClientIdFromReturnUrl(string? returnUrl)
    {
        Log.Debug("[TryGetClientIdFromReturnUrl] Input returnUrl: {ReturnUrl}", returnUrl);

        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return null;
        }

        try
        {
            // 查找查询字符串的起始位置
            var queryIndex = returnUrl.IndexOf('?');
            if (queryIndex == -1)
            {
                Log.Debug("[TryGetClientIdFromReturnUrl] No query string found in returnUrl");
                return null;
            }

            // 提取查询字符串部分（包括 '?'）
            var queryString = returnUrl.Substring(queryIndex);

            // 使用 QueryHelpers.ParseQuery 解析查询字符串（会自动处理 URL 编码）
            var query = QueryHelpers.ParseQuery(queryString);
            Log.Debug("[TryGetClientIdFromReturnUrl] Parsed {Count} query parameters", query.Count);

            if (!query.TryGetValue("client_id", out var clientIdValues))
            {
                Log.Debug("[TryGetClientIdFromReturnUrl] client_id not found in query parameters");
                return null;
            }

            var clientId = clientIdValues.FirstOrDefault();
            Log.Debug("[TryGetClientIdFromReturnUrl] Found client_id: {ClientId}", clientId);
            return clientId;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[TryGetClientIdFromReturnUrl] Failed to parse returnUrl");
            return null;
        }
    }
}
