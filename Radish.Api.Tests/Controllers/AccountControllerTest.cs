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
using Radish.Auth.Controllers;
using Radish.Auth.Resources;
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
            VoLoName = username,
            VoLoPwd = hashedPassword,
            VoTenId = 0,
            VoIsDeleted = false,
            VoIsEnable = true
        };

        userServiceMock
            .Setup(s => s.QueryAsync(It.IsAny<Expression<Func<User, bool>>>() ))
            .ReturnsAsync(new List<UserVo> { userVo });

        userServiceMock
            .Setup(s => s.GetUserRoleNameStrAsync(username, hashedPassword))
            .ReturnsAsync("Admin");

        var errorsLocalizer = new Mock<IStringLocalizer<Errors>>();

        var authServiceMock = new Mock<IAuthenticationService>();
        authServiceMock
            .Setup(s => s.SignInAsync(
                It.IsAny<HttpContext>(),
                CookieAuthenticationDefaults.AuthenticationScheme,
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<AuthenticationProperties>()))
            .Returns(Task.CompletedTask);

        var httpContext = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService>(authServiceMock.Object);
        httpContext.RequestServices = services.BuildServiceProvider();

        var controller = new AccountController(errorsLocalizer.Object, userServiceMock.Object)
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
    }
}
