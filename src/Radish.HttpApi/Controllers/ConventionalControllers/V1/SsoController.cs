using System;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Users;
using Microsoft.Extensions.Configuration;

namespace Radish.Controllers.ConventionalControllers.V1;

/// <summary>
/// SSO 实用接口（示例）：撤销当前用户的令牌，可选按客户端筛选。
/// 注意：本控制器未集成到现有登出流程，仅供需要时手动调用。
/// </summary>
[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/sso/[action]")]
public class SsoController : AbpControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IOpenIddictTokenManager _tokenManager;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IConfiguration _configuration;

    public SsoController(
        ICurrentUser currentUser,
        IOpenIddictTokenManager tokenManager,
        IOpenIddictApplicationManager applicationManager,
        IConfiguration configuration)
    {
        _currentUser = currentUser;
        _tokenManager = tokenManager;
        _applicationManager = applicationManager;
        _configuration = configuration;
    }

    /// <summary>
    /// 撤销当前用户的刷新令牌（可选：同时撤销访问令牌；可选：仅限某个 ClientId）。
    /// </summary>
    /// <param name="clientId">可选：限定只撤销该客户端颁发的令牌（如 Radish_Console）。</param>
    /// <param name="includeAccessTokens">是否同时撤销访问令牌（默认仅撤销刷新令牌）。</param>
    /// <returns>撤销的令牌数量。</returns>
    [HttpPost]
    public async Task<IActionResult> RevokeMyTokens([FromQuery] string? clientId = null, [FromQuery] bool includeAccessTokens = false)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Unauthorized();
        }

        // 已经过 IsAuthenticated 判断，GetId() 可安全取得非空 Guid
        var subject = _currentUser.GetId().ToString();
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Unauthorized();
        }

        // 如果传入了 clientId，则解析出目标应用的 Id（用于对齐 token 的 ApplicationId）
        object? app = null;
        string? appId = null;
        if (!string.IsNullOrWhiteSpace(clientId))
        {
            app = await _applicationManager.FindByClientIdAsync(clientId!);
            if (app == null)
            {
                return NotFound(new { message = $"Client '{clientId}' was not found." });
            }
            appId = await _applicationManager.GetIdAsync(app);
        }

        var count = 0;
        await foreach (var token in _tokenManager.FindBySubjectAsync(subject))
        {
            // 只撤销刷新令牌，必要时可带上访问令牌
            // OpenIddict v4 使用 TokenTypeHints 常量（access_token / refresh_token）
            var isRefresh = await _tokenManager.HasTypeAsync(token, OpenIddictConstants.TokenTypeHints.RefreshToken);
            var isAccess = includeAccessTokens && await _tokenManager.HasTypeAsync(token, OpenIddictConstants.TokenTypeHints.AccessToken);
            if (!isRefresh && !isAccess)
            {
                continue;
            }

            if (appId != null)
            {
                var tokenAppId = await _tokenManager.GetApplicationIdAsync(token);
                if (!string.Equals(appId, tokenAppId, StringComparison.Ordinal))
                {
                    continue;
                }
            }

            var status = await _tokenManager.GetStatusAsync(token);
            if (string.Equals(status, OpenIddictConstants.Statuses.Revoked, StringComparison.Ordinal))
            {
                continue; // 已撤销
            }

            var ok = await _tokenManager.TryRevokeAsync(token);
            if (ok)
            {
                count++;
            }
        }

        return Ok(new { revoked = count });
    }

    /// <summary>
    /// 构造 OIDC End-Session（前通道登出）URL，便于前端重定向。
    /// </summary>
    /// <param name="redirectUri">登出后回跳地址（默认取 App:AngularUrl）。</param>
    /// <param name="state">可选透传状态。</param>
    /// <returns>{ url: string }</returns>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult GetEndSessionUrl([FromQuery] string? redirectUri = null, [FromQuery] string? state = null)
    {
        var authority = _configuration["AuthServer:Authority"]; // e.g. https://localhost:44342
        if (string.IsNullOrWhiteSpace(authority))
        {
            authority = $"{Request.Scheme}://{Request.Host}";
        }

        var endSession = authority!.TrimEnd('/') + "/connect/logout";
        var postLogout = redirectUri ?? _configuration["App:AngularUrl"] ?? "/";

        var url = endSession + "?post_logout_redirect_uri=" + Uri.EscapeDataString(postLogout);
        if (!string.IsNullOrEmpty(state))
        {
            url += "&state=" + Uri.EscapeDataString(state);
        }

        return Ok(new { url, authority, postLogoutRedirectUri = postLogout });
    }
}
