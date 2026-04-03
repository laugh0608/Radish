using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Radish.Common.HttpContextTool;

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
        var subject = User.FindFirst(UserClaimTypes.Sub)?.Value
                      ?? User.FindFirst(UserClaimTypes.LegacyNameIdentifier)?.Value;
        var name = User.FindFirst(UserClaimTypes.Name)?.Value
                   ?? User.FindFirst(UserClaimTypes.PreferredUsername)?.Value
                   ?? User.FindFirst(UserClaimTypes.LegacyName)?.Value
                   ?? User.Identity?.Name;
        var email = User.FindFirst(OpenIddictConstants.Claims.Email)?.Value
                    ?? User.FindFirst(ClaimTypes.Email)?.Value;
        var roles = User.FindAll(UserClaimTypes.Role).Select(c => c.Value).ToArray();
        if (roles.Length == 0)
        {
            roles = User.FindAll(UserClaimTypes.LegacyRole).Select(c => c.Value).ToArray();
        }

        var tenantId = User.FindFirst(UserClaimTypes.TenantId)?.Value
                       ?? User.FindFirst(UserClaimTypes.LegacyTenantId)?.Value;

        var payload = new Dictionary<string, object?>
        {
            [OpenIddictConstants.Claims.Subject] = subject,
            [OpenIddictConstants.Claims.Name] = name,
            [OpenIddictConstants.Claims.Email] = email,
            [OpenIddictConstants.Claims.Role] = roles,
            [UserClaimTypes.TenantId] = tenantId
        };

        return Ok(payload);
    }
}
