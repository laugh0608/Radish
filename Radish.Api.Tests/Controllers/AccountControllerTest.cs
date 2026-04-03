using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OpenIddict.Abstractions;
using Radish.Auth.Controllers;
using Radish.Auth.Resources;
using Radish.Common.HttpContextTool;
using Radish.Common.HelpTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Xunit;

namespace Radish.Api.Tests.Controllers;

[TestSubject(typeof(AccountController))]
public class AccountControllerTest
{
    [Fact]
    public async Task Login_ShouldSignInAndRedirect_WhenCredentialsValid()
    {
        // Arrange
        var userServiceMock = new Mock<IUserService>();

        const string username = "test";
        const string password = "test123456";
        var hashedPassword = PasswordHasher.HashPassword(password);

        var userVo = new UserVo
        {
            Uuid = 1,
            VoLoginName = username,
            VoLoginPassword = hashedPassword,
            VoTenantId = 0,
            VoIsDeleted = false,
            VoIsEnable = true
        };

        userServiceMock
            .Setup(s => s.GetEnabledUserByLoginNameAsync(username))
            .ReturnsAsync(userVo);

        userServiceMock
            .Setup(s => s.GetUserRoleNamesAsync(userVo.Uuid))
            .ReturnsAsync(new List<string> { "Admin" });

        var errorsLocalizer = new Mock<IStringLocalizer<Errors>>();

        ClaimsPrincipal? signInPrincipal = null;
        var authServiceMock = new Mock<IAuthenticationService>();
        authServiceMock
            .Setup(s => s.SignInAsync(
                It.IsAny<HttpContext>(),
                CookieAuthenticationDefaults.AuthenticationScheme,
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<AuthenticationProperties>()))
            .Callback<HttpContext, string?, ClaimsPrincipal, AuthenticationProperties?>((_, _, principal, _) =>
            {
                signInPrincipal = principal;
            })
            .Returns(Task.CompletedTask);

        var httpContext = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService>(authServiceMock.Object);
        httpContext.RequestServices = services.BuildServiceProvider();

        var applicationManager = new Mock<IOpenIddictApplicationManager>();
        applicationManager.Setup(m => m.FindByClientIdAsync(It.IsAny<string>(), default)).ReturnsAsync((object?)null);

        var controller = new AccountController(errorsLocalizer.Object, userServiceMock.Object, applicationManager.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        var returnUrl = "/connect/authorize";

        // Act
        var result = await controller.Login(username, password, returnUrl);

        // Assert
        authServiceMock.Verify(
            s => s.SignInAsync(
                httpContext,
                CookieAuthenticationDefaults.AuthenticationScheme,
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<AuthenticationProperties>()),
            Times.Once);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal(returnUrl, redirect.Url);

        Assert.NotNull(signInPrincipal);
        Assert.Contains(signInPrincipal!.Claims, claim => claim.Type == OpenIddictConstants.Claims.Subject && claim.Value == "1");
        Assert.Contains(signInPrincipal.Claims, claim => claim.Type == OpenIddictConstants.Claims.Name && claim.Value == username);
        Assert.Contains(signInPrincipal.Claims, claim => claim.Type == OpenIddictConstants.Claims.PreferredUsername && claim.Value == username);
        Assert.Contains(signInPrincipal.Claims, claim => claim.Type == OpenIddictConstants.Claims.Role && claim.Value == "Admin");
        Assert.Contains(signInPrincipal.Claims, claim => claim.Type == UserClaimTypes.TenantId && claim.Value == "0");

        Assert.DoesNotContain(signInPrincipal.Claims, claim => claim.Type == ClaimTypes.NameIdentifier);
        Assert.DoesNotContain(signInPrincipal.Claims, claim => claim.Type == ClaimTypes.Name);
        Assert.DoesNotContain(signInPrincipal.Claims, claim => claim.Type == ClaimTypes.Role);
    }
}
