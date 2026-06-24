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
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OpenIddict.Abstractions;
using Radish.Auth.Controllers;
using Radish.Auth.Resources;
using Radish.Auth.ViewModels.Account;
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
    public async Task Register_ShouldGrantRegistrationReward_WhenUserCreated()
    {
        const long userId = 123456;
        var userServiceMock = new Mock<IUserService>();
        userServiceMock
            .Setup(service => service.QueryAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(new List<UserVo>());
        userServiceMock
            .Setup(service => service.AddAsync(It.IsAny<User>()))
            .ReturnsAsync(userId);

        var errorsLocalizer = new Mock<IStringLocalizer<Errors>>();
        var applicationManager = new Mock<IOpenIddictApplicationManager>();
        var coinService = new Mock<ICoinService>();
        coinService
            .Setup(service => service.GrantRegistrationRewardAsync(userId))
            .ReturnsAsync("TXN_REGISTER_123456");

        var httpContext = new DefaultHttpContext();
        var controller = new AccountController(
            errorsLocalizer.Object,
            userServiceMock.Object,
            applicationManager.Object,
            coinService.Object,
            CreateSystemSettingProvider())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
        };

        var result = await controller.Register(new RegisterViewModel
        {
            DisplayName = "NewUser",
            Password = "test123456",
            ConfirmPassword = "test123456",
            Email = "NewUser@Radish.TEST"
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirect.ActionName);
        Assert.Equal("newuser@radish.test", redirect.RouteValues?["email"]);
        userServiceMock.Verify(service => service.AddAsync(It.Is<User>(user =>
            user.UserEmail == "newuser@radish.test" &&
            user.UserName == "NewUser")), Times.Once);
        coinService.Verify(service => service.GrantRegistrationRewardAsync(userId), Times.Once);
    }

    [Fact]
    public async Task Register_ShouldRejectDisplayNameShorterThanRule()
    {
        var userServiceMock = new Mock<IUserService>();
        var errorsLocalizer = new Mock<IStringLocalizer<Errors>>();
        var applicationManager = new Mock<IOpenIddictApplicationManager>();
        var coinService = new Mock<ICoinService>();
        var httpContext = new DefaultHttpContext();
        var controller = new AccountController(
            errorsLocalizer.Object,
            userServiceMock.Object,
            applicationManager.Object,
            coinService.Object,
            CreateSystemSettingProvider(displayNameMinLength: 2))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
        };

        var result = await controller.Register(new RegisterViewModel
        {
            DisplayName = "a",
            Password = "test123456",
            ConfirmPassword = "test123456",
            Email = "newuser@radish.test"
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Register", redirect.ActionName);
        Assert.Equal("展示名长度必须在 2-24 个字符之间", controller.TempData["RegisterError"]);
        userServiceMock.Verify(service => service.AddAsync(It.IsAny<User>()), Times.Never);
        coinService.Verify(service => service.GrantRegistrationRewardAsync(It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task Login_ShouldSignInAndRedirect_WhenCredentialsValid()
    {
        // Arrange
        var userServiceMock = new Mock<IUserService>();

        const string email = "test@radish.test";
        const string password = "test123456";
        var hashedPassword = PasswordHasher.HashPassword(password);

        var userVo = new UserVo
        {
            Uuid = 1,
            VoDisplayName = "Tester",
            VoDisplayHandle = "Tester#1000",
            VoUserEmail = email,
            VoLoginPassword = hashedPassword,
            VoTenantId = 0,
            VoIsDeleted = false,
            VoIsEnable = true
        };

        userServiceMock
            .Setup(s => s.GetEnabledUserByEmailAsync(email))
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
        var coinService = new Mock<ICoinService>();
        coinService
            .Setup(service => service.GrantRegistrationRewardAsync(userVo.Uuid))
            .ReturnsAsync("TXN_REGISTER_1");

        var controller = new AccountController(
            errorsLocalizer.Object,
            userServiceMock.Object,
            applicationManager.Object,
            coinService.Object,
            CreateSystemSettingProvider())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        var returnUrl = "/connect/authorize";

        // Act
        var result = await controller.Login(email, password, returnUrl);

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
        Assert.Contains(signInPrincipal.Claims, claim => claim.Type == OpenIddictConstants.Claims.Name && claim.Value == "Tester");
        Assert.Contains(signInPrincipal.Claims, claim => claim.Type == OpenIddictConstants.Claims.PreferredUsername && claim.Value == "Tester#1000");
        Assert.Contains(signInPrincipal.Claims, claim => claim.Type == OpenIddictConstants.Claims.Role && claim.Value == "Admin");
        Assert.Contains(signInPrincipal.Claims, claim => claim.Type == UserClaimTypes.TenantId && claim.Value == "0");

        Assert.DoesNotContain(signInPrincipal.Claims, claim => claim.Type == ClaimTypes.NameIdentifier);
        Assert.DoesNotContain(signInPrincipal.Claims, claim => claim.Type == ClaimTypes.Name);
        Assert.DoesNotContain(signInPrincipal.Claims, claim => claim.Type == ClaimTypes.Role);
        coinService.Verify(service => service.GrantRegistrationRewardAsync(userVo.Uuid), Times.Once);
    }

    private static ISystemSettingProvider CreateSystemSettingProvider(
        int? displayNameMinLength = null,
        int? displayNameMaxLength = null)
    {
        var provider = new Mock<ISystemSettingProvider>();
        provider
            .Setup(item => item.GetInt32Async(SystemConfigDefaults.DisplayNameMinLengthKey))
            .ReturnsAsync(displayNameMinLength ?? int.Parse(SystemConfigDefaults.DefaultDisplayNameMinLength));
        provider
            .Setup(item => item.GetInt32Async(SystemConfigDefaults.DisplayNameMaxLengthKey))
            .ReturnsAsync(displayNameMaxLength ?? int.Parse(SystemConfigDefaults.DefaultDisplayNameMaxLength));
        return provider.Object;
    }
}
