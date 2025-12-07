using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Radish.Auth.Models;

namespace Radish.Auth.Controllers;

/// <summary>
/// 授权端点控制器：处理 /connect/authorize。
/// - 未登录时跳转到 Account/Login
/// - 已登录时根据客户端配置和用户选择发出授权码
/// </summary>
public class AuthorizationController : Controller
{
    private readonly IOpenIddictApplicationManager _applicationManager;

    public AuthorizationController(IOpenIddictApplicationManager applicationManager)
    {
        _applicationManager = applicationManager;
    }

    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize()
    {
        // 如果用户尚未通过 Cookie 登录，则引导到登录页，登录后自动回跳到当前请求
        if (User.Identity is not { IsAuthenticated: true })
        {
            // 🌍 提取 culture 参数，在重定向到登录页时保留语言设置
            var culture = Request.Query["culture"].ToString();
            var uiCulture = Request.Query["ui-culture"].ToString();

            var loginUrl = "/Account/Login?ReturnUrl=" + Uri.EscapeDataString(Request.PathBase + Request.Path + Request.QueryString);

            // 将 culture 参数添加到登录页 URL（而不是 ReturnUrl 内部）
            if (!string.IsNullOrEmpty(culture))
            {
                loginUrl += "&culture=" + Uri.EscapeDataString(culture);
            }
            if (!string.IsNullOrEmpty(uiCulture))
            {
                loginUrl += "&ui-culture=" + Uri.EscapeDataString(uiCulture);
            }

            return Redirect(loginUrl);
        }

        // 从 OpenIddict 管道中获取当前请求（包含 client_id、redirect_uri、scope 等）
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request is null)
        {
            throw new InvalidOperationException("无法获取 OpenIddict 授权请求。");
        }

        if (string.IsNullOrEmpty(request.ClientId))
        {
            throw new InvalidOperationException("授权请求缺少 client_id。");
        }

        var application = await _applicationManager.FindByClientIdAsync(request.ClientId, HttpContext.RequestAborted)
                         ?? throw new InvalidOperationException($"未知的客户端：{request.ClientId}。");

        var consentType = await _applicationManager.GetConsentTypeAsync(application, HttpContext.RequestAborted);

        var clientId = request.ClientId;
        var clientName = await _applicationManager.GetDisplayNameAsync(application, HttpContext.RequestAborted)
                          ?? clientId;

        // 约定：radish-client 视为一方应用，默认跳过授权确认；
        // 其他使用 Explicit 的客户端（如第三方应用）需要显式授权。
        var isFirstPartyClient = string.Equals(clientId, "radish-client", StringComparison.OrdinalIgnoreCase);

        string? userDecision = null;
        if (string.Equals(Request.Method, "POST", StringComparison.OrdinalIgnoreCase) &&
            Request.HasFormContentType)
        {
            userDecision = Request.Form["decision"];
        }

        if (!isFirstPartyClient &&
            string.Equals(consentType, OpenIddictConstants.ConsentTypes.Explicit, StringComparison.Ordinal) &&
            string.IsNullOrEmpty(userDecision))
        {
            // 第一次到达 /connect/authorize，且为需要显式授权的第三方客户端：展示授权确认页
            var scopes = request.GetScopes().ToArray();

            var vm = new ConsentViewModel
            {
                ClientId = clientId,
                ClientName = clientName,
                Scope = string.Join(" ", scopes),
                Scopes = scopes
            };

            return View("Consent", vm);
        }

        if (!isFirstPartyClient &&
            string.Equals(consentType, OpenIddictConstants.ConsentTypes.Explicit, StringComparison.Ordinal) &&
            string.Equals(userDecision, "deny", StringComparison.Ordinal))
        {
            // 用户拒绝授权：向客户端返回 access_denied
            var properties = new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.AccessDenied,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user denied the authorization request."
            });

            return Forbid(properties, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        // 走到这里，要么是：
        // - 一方客户端（radish-client）；
        // - 显式授权客户端且用户已点击“同意”；
        // - 或其他 ConsentType（implicit/informed）。

        // 基于当前登录用户构造授权主体
        var identity = new ClaimsIdentity(
            User.Claims,
            authenticationType: CookieAuthenticationDefaults.AuthenticationScheme,
            nameType: OpenIddictConstants.Claims.Name,
            roleType: OpenIddictConstants.Claims.Role);

        // 确保 subject 存在
        var subject = identity.FindFirst(OpenIddictConstants.Claims.Subject)?.Value
                      ?? identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(subject))
        {
            identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, Guid.NewGuid().ToString()));
        }

        var principal = new ClaimsPrincipal(identity);

        // 关键点：只把「请求中」的 scopes 传给 OpenIddict，避免超出客户端允许范围
        principal.SetScopes(request.GetScopes());

        // 对于资源服务器，只暴露 radish-api 这一项
        principal.SetResources("radish-api");

        // 将所有当前 Claims 下发到 access_token，方便资源服务器直接消费
        principal.SetDestinations(static claim =>
            new[] { OpenIddictConstants.Destinations.AccessToken });

        // 由 OpenIddict 生成授权码等票据
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
