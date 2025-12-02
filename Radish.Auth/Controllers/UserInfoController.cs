using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace Radish.Auth.Controllers;

/// <summary>
/// UserInfo 端点实现：返回当前访问令牌对应用户的基本信息。
/// </summary>
public class UserInfoController : Controller
{
    [Authorize(AuthenticationSchemes = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)]
    [HttpGet("~/connect/userinfo")]
    public IActionResult UserInfo()
    {
        var subject = User.FindFirst(OpenIddictConstants.Claims.Subject)?.Value
                      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var name = User.FindFirst(OpenIddictConstants.Claims.Name)?.Value
                   ?? User.Identity?.Name;
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();

        var payload = new Dictionary<string, object?>
        {
            [OpenIddictConstants.Claims.Subject] = subject,
            [OpenIddictConstants.Claims.Name] = name,
            [OpenIddictConstants.Claims.Email] = email,
            [OpenIddictConstants.Claims.Role] = roles
        };

        return Ok(payload);
    }
}
