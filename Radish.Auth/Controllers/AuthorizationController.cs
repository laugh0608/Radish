using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace Radish.Auth.Controllers;

/// <summary>
/// 授权端点控制器：处理 /connect/authorize。
/// - 未登录时跳转到 Account/Login
/// - 已登录时根据请求的 scopes/resources 发出授权码
/// </summary>
public class AuthorizationController : Controller
{
    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize()
    {
        // 如果用户尚未通过 Cookie 登录，则引导到登录页，登录后自动回跳到当前请求
        if (User.Identity is not { IsAuthenticated: true })
        {
            var props = new AuthenticationProperties
            {
                RedirectUri = Request.PathBase + Request.Path + Request.QueryString
            };

            return Challenge(
                authenticationSchemes: CookieAuthenticationDefaults.AuthenticationScheme,
                properties: props);
        }

        // 从 OpenIddict 管道中获取当前请求（包含 client_id、redirect_uri、scope 等）
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request is null)
        {
            throw new InvalidOperationException("无法获取 OpenIddict 授权请求。");
        }

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
