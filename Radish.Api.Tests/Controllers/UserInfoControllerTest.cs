#nullable enable

using System.Collections.Generic;
using System.Security.Claims;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using Radish.Auth.Controllers;
using Radish.Common.HttpContextTool;
using Xunit;

namespace Radish.Api.Tests.Controllers;

[TestSubject(typeof(UserInfoController))]
public class UserInfoControllerTest
{
    [Fact]
    public void UserInfo_ShouldPreferStandardClaims_WhenStandardAndLegacyBothExist()
    {
        var controller = CreateController(new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(UserClaimTypes.Sub, "100"),
            new Claim(UserClaimTypes.LegacyNameIdentifier, "200"),
            new Claim(UserClaimTypes.Name, "standard-name"),
            new Claim(UserClaimTypes.LegacyName, "legacy-name"),
            new Claim(OpenIddictConstants.Claims.Email, "standard@example.com"),
            new Claim(ClaimTypes.Email, "legacy@example.com"),
            new Claim(UserClaimTypes.Role, "Admin"),
            new Claim(UserClaimTypes.LegacyRole, "LegacyAdmin"),
            new Claim(UserClaimTypes.TenantId, "300"),
            new Claim(UserClaimTypes.LegacyTenantId, "400")
        ], "TestAuth")));

        var result = controller.UserInfo();

        var payload = AssertPayload(result);
        Assert.Equal("100", payload[OpenIddictConstants.Claims.Subject]);
        Assert.Equal("standard-name", payload[OpenIddictConstants.Claims.Name]);
        Assert.Equal("300", payload[UserClaimTypes.TenantId]);

        var roles = Assert.IsType<string[]>(payload[OpenIddictConstants.Claims.Role]);
        Assert.Equal(["Admin"], roles);
    }

    [Fact]
    public void UserInfo_ShouldFallbackToLegacyClaims_WhenStandardClaimsMissing()
    {
        var controller = CreateController(new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(UserClaimTypes.LegacyNameIdentifier, "200"),
            new Claim(UserClaimTypes.LegacyName, "legacy-name"),
            new Claim(ClaimTypes.Email, "legacy@example.com"),
            new Claim(UserClaimTypes.LegacyRole, "LegacyAdmin"),
            new Claim(UserClaimTypes.LegacyTenantId, "400")
        ], "TestAuth")));

        var result = controller.UserInfo();

        var payload = AssertPayload(result);
        Assert.Equal("200", payload[OpenIddictConstants.Claims.Subject]);
        Assert.Equal("legacy-name", payload[OpenIddictConstants.Claims.Name]);
        Assert.Equal("legacy@example.com", payload[OpenIddictConstants.Claims.Email]);
        Assert.Equal("400", payload[UserClaimTypes.TenantId]);

        var roles = Assert.IsType<string[]>(payload[OpenIddictConstants.Claims.Role]);
        Assert.Equal(["LegacyAdmin"], roles);
    }

    private static UserInfoController CreateController(ClaimsPrincipal user)
    {
        return new UserInfoController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user
                }
            }
        };
    }

    private static Dictionary<string, object?> AssertPayload(IActionResult result)
    {
        var okResult = Assert.IsType<OkObjectResult>(result);
        return Assert.IsType<Dictionary<string, object?>>(okResult.Value);
    }
}
