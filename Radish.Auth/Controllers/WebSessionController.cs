using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Radish.Auth.OpenIddict;
using Radish.Auth.WebSessions;
using Radish.Common.HttpContextTool;

namespace Radish.Auth.Controllers;

[Route("web/session")]
public sealed class WebSessionController(IWebSessionLoginService login, IWebSessionAccessService sessions,
    IConfiguration configuration) : Controller
{
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] WebSessionLoginRequest? request, CancellationToken cancellationToken)
    {
        if (CheckRequest(requireOrigin: true) is { } denied) return denied;

        var path = request?.ReturnPath ?? "/";
        if (path.Length > 2048 || !path.StartsWith('/') || !Url.IsLocalUrl(path)) return BadRequest();

        try
        {
            var started = await login.StartAsync(path, cancellationToken);
            Response.Cookies.Append(WebSessionCookieNames.Login, started.BrowserSecret, CookieOptions(started.ExpiresAt));
            return Ok(new WebSessionLoginVo(started.AuthorizeUri.AbsoluteUri));
        }
        catch (Exception error) when (IsInfrastructureFailure(error))
        {
            return Retry();
        }
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string? state, [FromQuery] string? code, CancellationToken cancellationToken)
    {
        if (CheckRequest(requireOrigin: false, isCallback: true) is { } denied) return denied;

        try
        {
            var result = await login.CompleteAsync(state, Request.Cookies[WebSessionCookieNames.Login], code, cancellationToken);
            if (result.Status == WebTokenBackchannelStatus.Unavailable) return Retry();
            if (result is not { Status: WebTokenBackchannelStatus.Success, Cookie: { } value, ExpiresAt: { } expiry }) return BadRequest();

            Response.Cookies.Delete(WebSessionCookieNames.Login, CookieOptions());
            Response.Cookies.Append(WebSessionCookieNames.Session, value, CookieOptions(expiry));
            return LocalRedirect(Url.IsLocalUrl(result.ReturnPath) ? result.ReturnPath! : "/");
        }
        catch (Exception error) when (IsInfrastructureFailure(error))
        {
            return Retry();
        }
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        if (CheckRequest(requireOrigin: false) is { } denied) return denied;
        var cookie = ReadSessionCookie();
        try
        {
            if (cookie is null || !await sessions.IsAuthenticatedAsync(cookie, cancellationToken)) return SignedOut();
            return Ok(new WebSessionStatusVo(WebSessionSecrets.Proof(cookie)));
        }
        catch (Exception error) when (IsInfrastructureFailure(error))
        {
            return Retry();
        }
    }

    [HttpPost("token")]
    public async Task<IActionResult> Token(CancellationToken cancellationToken)
    {
        if (CheckWrite(out var cookie) is { } denied) return denied;

        var result = await sessions.GetTokenAsync(cookie, cancellationToken);
        return result.Status switch
        {
            WebSessionResultStatus.Success => Ok(new WebSessionTokenVo(result.AccessToken!, result.ExpiresAt!.Value)),
            WebSessionResultStatus.Retry => Retry(),
            _ => SignedOut()
        };
    }

    [HttpPost("activity")]
    public async Task<IActionResult> Activity(CancellationToken cancellationToken)
    {
        if (CheckWrite(out var cookie) is { } denied) return denied;

        try
        {
            return await sessions.RecordActivityAsync(cookie, cancellationToken) switch
            {
                WebSessionResultStatus.Success => NoContent(),
                WebSessionResultStatus.Retry => Retry(),
                _ => SignedOut()
            };
        }
        catch (Exception error) when (IsInfrastructureFailure(error))
        {
            return Retry();
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (CheckWrite(out var cookie) is { } denied) return denied;

        try
        {
            if (await sessions.LogoutAsync(cookie, cancellationToken) == WebSessionResultStatus.Retry) return Retry();

            Response.Cookies.Delete(WebSessionCookieNames.Session, CookieOptions());
            var origin = WebAuthClient.GetOrigin(WebAuthClient.GetIssuer(configuration));
            // OpenIddict stores the registered URI's OriginalString and compares it verbatim.
            var redirect = QueryHelpers.AddQueryString(new Uri(origin, "/connect/endsession").AbsoluteUri,
                new Dictionary<string, string?> { ["client_id"] = WebAuthClient.ClientId, ["post_logout_redirect_uri"] = origin.OriginalString });
            return Ok(new WebSessionLogoutVo(redirect));
        }
        catch (Exception error) when (IsInfrastructureFailure(error))
        {
            return Retry();
        }
    }

    private IActionResult? CheckWrite(out string? cookie)
    {
        cookie = null;
        if (CheckRequest(requireOrigin: true) is { } denied) return denied;
        cookie = ReadSessionCookie();
        if (cookie is null) return SignedOut();

        var proof = Request.Headers["X-Radish-Web-Session"];
        return proof.Count == 1 && WebSessionSecrets.VerifyProof(cookie, proof[0]) ? null : StatusCode(403);
    }

    private IActionResult? CheckRequest(bool requireOrigin, bool isCallback = false)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (!WebAuthClient.IsEnabled(configuration))
        {
            Response.Headers["X-Radish-Web-Session-Mode"] = "legacy";
            return NotFound();
        }

        var origin = WebAuthClient.GetOrigin(WebAuthClient.GetIssuer(configuration));
        if (!Request.IsHttps || !string.Equals(Request.Host.ToUriComponent(), origin.Authority, StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(403);
        }

        Response.Headers["X-Radish-Web-Session-Mode"] = "cookie";

        if (isCallback) return null;

        var supplied = Request.Headers.Origin;
        if (supplied.Count > 0 || requireOrigin)
        {
            return supplied.Count == 1 && supplied[0] == origin.GetLeftPart(UriPartial.Authority) ? null : StatusCode(403);
        }

        var fetchSite = Request.Headers["Sec-Fetch-Site"];
        return fetchSite.Count == 0 || fetchSite is ["same-origin"] or ["none"] ? null : StatusCode(403);
    }

    private string? ReadSessionCookie()
    {
        var cookie = Request.Cookies[WebSessionCookieNames.Session];
        return WebSessionSecrets.IsSecret(cookie) ? cookie : null;
    }

    private IActionResult SignedOut()
    {
        if (Request.Cookies.ContainsKey(WebSessionCookieNames.Session))
        {
            Response.Cookies.Delete(WebSessionCookieNames.Session, CookieOptions());
        }

        return Unauthorized();
    }

    private IActionResult Retry()
    {
        Response.Headers.RetryAfter = "2";
        return StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    private static bool IsInfrastructureFailure(Exception error) =>
        error is CryptographicException or JsonException or DbException or DbUpdateException;

    private static CookieOptions CookieOptions(DateTimeOffset? expires = null) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = expires,
        IsEssential = true
    };
}

public sealed record WebSessionLoginRequest(string? ReturnPath);
public sealed record WebSessionLoginVo(string VoAuthorizeUrl);
public sealed record WebSessionStatusVo(string VoCsrfToken);
public sealed record WebSessionTokenVo(string VoAccessToken, DateTimeOffset VoExpiresAt);
public sealed record WebSessionLogoutVo(string VoEndSessionUrl);
