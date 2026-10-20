using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using OpenIddict.Abstractions;
using Radish.Api.Tests.ErrorHandling;
using Radish.Auth.Controllers;
using Radish.Auth.Resources;
using Radish.Auth.ViewModels.Account;
using Radish.Common.HelpTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.Constants;
using Xunit;

namespace Radish.Api.Tests.Auth;

internal sealed class AuthAccountFixture : IDisposable
{
    internal const string Secret = HttpBoundaryCapture.Secret;
    internal const string DisplayName = "AuthSecretSentinel";
    internal const string Email = "authsecretsentinel@example.invalid";
    internal const string ReturnUrl = "/connect/authorize?client_id=" + Secret + "&code=" + Secret;
    internal static readonly Lazy<string> PasswordHash = new(() => PasswordHasher.HashPassword(Secret));
    internal readonly Mock<IUserService> Users = new();
    internal readonly Mock<ICoinService> Coins = new();
    internal readonly Mock<IOpenIddictApplicationManager> Applications = new();
    internal readonly Mock<ISystemSettingProvider> Settings = new();
    internal readonly Mock<IStringLocalizer<Errors>> Localizer = new();
    internal readonly Mock<IAuthenticationService> Authentication = new();
    internal readonly ServiceProvider Services;
    internal readonly DefaultHttpContext Context;
    internal readonly FaultingAccountController Controller;
    internal readonly List<string> Calls = [];
    internal ClaimsPrincipal? Principal;

    internal AuthAccountFixture(HttpBoundaryCapture capture, DefaultHttpContext? context = null)
    {
        Localizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => new LocalizedString(key, key));
        Localizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] values) => new LocalizedString(key, key));
        Settings.Setup(s => s.GetInt32Async(SystemConfigDefaults.DisplayNameMinLengthKey)).ReturnsAsync(2);
        Settings.Setup(s => s.GetInt32Async(SystemConfigDefaults.DisplayNameMaxLengthKey)).ReturnsAsync(24);
        Users.Setup(s => s.GetEnabledUserCredentialByEmailAsync(It.IsAny<string>()))
            .Callback(() => Calls.Add("credentials"))
            .ReturnsAsync(new UserCredentialSnapshot(73, DisplayName, Secret, Email, PasswordHash.Value, 17));
        Users.Setup(s => s.GetUserRoleNamesAsync(73)).Callback(() => Calls.Add("roles")).ReturnsAsync(new List<string> { Secret });
        Users.Setup(s => s.QueryAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync(new List<UserVo>());
        Users.Setup(s => s.AddAsync(It.IsAny<User>())).Callback(() => Calls.Add("create")).ReturnsAsync(73);
        Coins.Setup(s => s.GrantRegistrationRewardAsync(73)).Callback(() => Calls.Add("reward")).ReturnsAsync(Secret);
        Authentication.Setup(s => s.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()))
            .Callback<HttpContext, string?, ClaimsPrincipal, AuthenticationProperties?>((_, _, principal, _) =>
            {
                Calls.Add("sign-in");
                Principal = principal;
            }).Returns(Task.CompletedTask);
        Services = new ServiceCollection().AddSingleton(Authentication.Object).BuildServiceProvider();
        Context = context ?? new DefaultHttpContext { RequestServices = Services };
        Context.Request.Scheme = "https";
        Context.Request.Host = new HostString("auth.example.invalid");
        Context.TraceIdentifier = Secret;
        Context.Response.Body = new MemoryStream();
        var actionContext = new ActionContext(Context, new RouteData(), new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor(), new ModelStateDictionary());
        Controller = new FaultingAccountController(Localizer.Object, Users.Object, Applications.Object, Coins.Object, Settings.Object,
            capture.Factory.CreateLogger<AccountController>())
        {
            ControllerContext = new ControllerContext(actionContext),
            TempData = new TempDataDictionary(Context, Mock.Of<ITempDataProvider>()),
            Url = new UrlHelper(actionContext)
        };
    }

    internal RegisterViewModel Registration() => new()
    {
        DisplayName = DisplayName, Email = Email, Password = Secret, ConfirmPassword = Secret, ReturnUrl = ReturnUrl
    };

    internal static void AssertSafe(HttpBoundaryCapture capture)
    {
        capture.AssertSafe();
        var text = capture.Output.ToString() + string.Join("\n", capture.Sink.Events.Select(e => e.RenderMessage() +
            string.Join(" ", e.Properties.Select(p => p.Value.ToString()))));
        Assert.DoesNotContain(DisplayName, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Email, text, StringComparison.OrdinalIgnoreCase);
        foreach (var entry in capture.Sink.Events.Where(e => e.Properties.TryGetValue("EventCode", out var code) &&
                     (code.ToString().Contains("auth.account.") || code.ToString().Contains("auth.idle_session."))))
            Assert.All(entry.Properties.Keys, key => Assert.Contains(key, new[]
                { "EventCode", "SourceCategory", "SourceContext", "Diagnostic", "failureKind", "authStage", "authReason", "authClientOutcome", "durationMs" }));
    }

    internal static void AssertCodes(HttpBoundaryCapture capture, params string[] expected)
    {
        var visible = capture.Candidate && capture.Environment == "Production"
            ? expected.Where(code => code is "auth.account.login_result_prepared" or "auth.account.registration_result_prepared" or
                "auth.account.registration_failed" or "auth.account.client_resolution_failed" or "auth.account.return_url_parse_failed" or "auth.idle_session.rejected")
            : expected;
        Assert.Equal(visible, capture.Codes);
        AssertSafe(capture);
    }

    public void Dispose() => Services.Dispose();
}

/// <summary>在真实结果构造边界注入故障；不替换登录 / 注册流程。</summary>
internal sealed class FaultingAccountController(
    IStringLocalizer<Errors> localizer, IUserService users, IOpenIddictApplicationManager applications,
    ICoinService coins, ISystemSettingProvider settings, ILogger<AccountController> logger)
    : AccountController(localizer, users, applications, coins, settings, logger)
{
    internal Exception? ResultFailure;
    internal string? FailedAction;
    internal bool FailLocalRedirect;
    internal bool FailOk;

    public override RedirectToActionResult RedirectToAction(string? actionName, string? controllerName, object? routeValues, string? fragment)
    {
        if (ResultFailure != null && actionName == FailedAction) throw ResultFailure;
        return base.RedirectToAction(actionName, controllerName, routeValues, fragment);
    }

    public override LocalRedirectResult LocalRedirect(string localUrl)
    {
        if (FailLocalRedirect && ResultFailure != null) throw ResultFailure;
        return base.LocalRedirect(localUrl);
    }

    public override OkObjectResult Ok(object? value)
    {
        if (FailOk && ResultFailure != null) throw ResultFailure;
        return base.Ok(value);
    }
}

internal sealed class UnreadableAuthException : Exception
{
    public override string Message => throw new InvalidOperationException("Exception text must not be read.");
    public override string ToString() => throw new InvalidOperationException("Exception text must not be rendered.");
}
