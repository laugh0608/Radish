using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Localization;
using Moq;
using OpenIddict.Abstractions;
using Radish.Api.Tests.ErrorHandling;
using Radish.Common.HttpContextTool;
using Radish.Model;
using Radish.Model.ViewModels;
using Xunit;

namespace Radish.Api.Tests.Auth;

[Collection("Runtime logging global state")]
public sealed class AuthAccountLoggingTests
{
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" }, { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Login_ShouldPreserveClaimsCallOrderAndPreparedResults(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var scope = capture.SensitiveScope();
        using var f = new AuthAccountFixture(capture);
        foreach (var url in new[] { AuthAccountFixture.ReturnUrl, null })
        {
            var result = await f.Controller.Login(AuthAccountFixture.Email.ToUpperInvariant(), AuthAccountFixture.Secret, url);
            if (url != null) Assert.Equal(url, Assert.IsType<LocalRedirectResult>(result).Url);
            else Assert.Equal("auth.login.success.message", Assert.IsType<LocalizedString>(Assert.IsType<OkObjectResult>(result).Value).Value);
            Assert.Equal(new[] { "credentials", "reward", "roles", "sign-in" }, f.Calls);
            Assert.NotNull(f.Principal);
            Assert.Equal("73", f.Principal.FindFirst(OpenIddictConstants.Claims.Subject)!.Value);
            Assert.Equal("17", f.Principal.FindFirst(UserClaimTypes.TenantId)!.Value);
            Assert.Equal(AuthAccountFixture.Secret, f.Principal.FindFirst(OpenIddictConstants.Claims.Role)!.Value);
            Assert.Equal(AuthAccountFixture.Email, f.Principal.FindFirst(OpenIddictConstants.Claims.Email)!.Value);
            AuthAccountFixture.AssertCodes(capture, "auth.account.login_result_prepared");
            Assert.False(Assert.Single(capture.Events).Diagnostic);
            f.Calls.Clear();
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task LoginRejections_ShouldStayDiagnosticAndNeverGrantReward(bool candidate, string environment)
    {
        foreach (var scenario in new[] { "external", "missing-email", "missing-password", "unknown-user", "wrong-password" })
        {
            using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
            using var scope = capture.SensitiveScope();
            using var f = new AuthAccountFixture(capture);
            if (scenario == "unknown-user") f.Users.Setup(s => s.GetEnabledUserCredentialByEmailAsync(It.IsAny<string>())).ReturnsAsync((UserCredentialSnapshot?)null);
            var result = await f.Controller.Login(scenario == "missing-email" ? "invalid" : AuthAccountFixture.Email,
                scenario == "missing-password" ? "" : scenario == "wrong-password" ? "wrong" : AuthAccountFixture.Secret,
                scenario == "external" ? "https://example.invalid/" + AuthAccountFixture.Secret : AuthAccountFixture.ReturnUrl);
            if (scenario == "external") Assert.IsType<BadRequestObjectResult>(result);
            else
            {
                Assert.Equal("Login", Assert.IsType<RedirectToActionResult>(result).ActionName);
                Assert.Equal("auth.login.error.invalidCredentials", f.Controller.TempData["LoginError"]);
            }
            if (scenario is "external" or "missing-email" or "missing-password")
                f.Users.Verify(s => s.GetEnabledUserCredentialByEmailAsync(It.IsAny<string>()), Times.Never);
            f.Coins.Verify(s => s.GrantRegistrationRewardAsync(It.IsAny<long>()), Times.Never);
            Assert.Null(f.Principal);
            AuthAccountFixture.AssertCodes(capture, "auth.account.login_rejected");
            Assert.All(capture.Events, e => Assert.True(e.Diagnostic));
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task LoginFailures_ShouldPropagateOriginalExceptionWithoutLocalCompletion(bool candidate, string environment)
    {
        foreach (var stage in new[] { "credentials", "reward", "roles", "sign-in", "redirect", "ok", "localization", "rejection" })
        {
            using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
            using var f = new AuthAccountFixture(capture);
            var failure = new OperationCanceledException(AuthAccountFixture.Secret);
            switch (stage)
            {
                case "credentials": f.Users.Setup(s => s.GetEnabledUserCredentialByEmailAsync(It.IsAny<string>())).ThrowsAsync(failure); break;
                case "reward": f.Coins.Setup(s => s.GrantRegistrationRewardAsync(73)).ThrowsAsync(failure); break;
                case "roles": f.Users.Setup(s => s.GetUserRoleNamesAsync(73)).ThrowsAsync(failure); break;
                case "sign-in": f.Authentication.Setup(s => s.SignInAsync(It.IsAny<Microsoft.AspNetCore.Http.HttpContext>(), It.IsAny<string>(),
                    It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>())).ThrowsAsync(failure); break;
                case "localization": f.Localizer.Setup(l => l["auth.login.success.message"]).Throws(failure); break;
                default:
                    f.Controller.ResultFailure = failure;
                    f.Controller.FailLocalRedirect = stage == "redirect";
                    f.Controller.FailOk = stage == "ok";
                    f.Controller.FailedAction = stage == "rejection" ? "Login" : null;
                    break;
            }
            Assert.Same(failure, await Assert.ThrowsAsync<OperationCanceledException>(() => f.Controller.Login(
                AuthAccountFixture.Email, stage == "rejection" ? "" : AuthAccountFixture.Secret,
                stage is "ok" or "localization" ? null : AuthAccountFixture.ReturnUrl)));
            AuthAccountFixture.AssertCodes(capture);
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Registration_ShouldKeepUserAndRewardDataAndReportOnlyPreparedOutcome(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var scope = capture.SensitiveScope();
        using var f = new AuthAccountFixture(capture);
        var result = Assert.IsType<RedirectToActionResult>(await f.Controller.Register(f.Registration()));
        Assert.Equal("Login", result.ActionName);
        Assert.Equal(AuthAccountFixture.Email, result.RouteValues!["email"]);
        Assert.Equal(AuthAccountFixture.ReturnUrl, result.RouteValues["returnUrl"]);
        Assert.Equal(new[] { "create", "reward" }, f.Calls);
        f.Users.Verify(s => s.AddAsync(It.Is<User>(u => u.UserName == AuthAccountFixture.DisplayName &&
            u.UserEmail == AuthAccountFixture.Email && u.IsEnable && !u.IsDeleted)), Times.Once);
        AuthAccountFixture.AssertCodes(capture, "auth.account.registration_user_create_returned",
            "auth.account.registration_reward_returned", "auth.account.registration_result_prepared");
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task RegistrationFailures_ShouldDescribeStageAndRetainPartialSideEffects(bool candidate, string environment)
    {
        foreach (var stage in new[] { "validation", "user-lookup", "password-hash", "user-create", "reward", "result-prepare" })
        {
            using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
            using var scope = capture.SensitiveScope();
            using var f = new AuthAccountFixture(capture);
            var failure = new UnreadableAuthException();
            var model = f.Registration();
            switch (stage)
            {
                case "validation": f.Settings.Setup(s => s.GetInt32Async(SystemConfigDefaults.DisplayNameMinLengthKey)).ThrowsAsync(failure); break;
                case "user-lookup": f.Users.Setup(s => s.QueryAsync(It.IsAny<Expression<Func<User, bool>>>())).ThrowsAsync(failure); break;
                case "password-hash": model.Password = ""; break;
                case "user-create": f.Users.Setup(s => s.AddAsync(It.IsAny<User>())).ThrowsAsync(failure); break;
                case "reward": f.Coins.Setup(s => s.GrantRegistrationRewardAsync(73)).ThrowsAsync(failure); break;
                case "result-prepare": f.Controller.ResultFailure = failure; f.Controller.FailedAction = "Login"; break;
            }
            var result = Assert.IsType<RedirectToActionResult>(await f.Controller.Register(model));
            Assert.Equal("Register", result.ActionName);
            Assert.Equal("auth.register.error.unexpected", f.Controller.TempData["RegisterError"]);
            var expected = new List<string>();
            if (stage is "reward" or "result-prepare") expected.Add("auth.account.registration_user_create_returned");
            if (stage == "result-prepare") expected.Add("auth.account.registration_reward_returned");
            expected.Add("auth.account.registration_failed");
            AuthAccountFixture.AssertCodes(capture, expected.ToArray());
            Assert.Equal(stage == "password-hash" ? "argument" : "other", capture.Events.Last().FailureKind);
            if (candidate)
            {
                using var doc = JsonDocument.Parse(capture.Output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Last());
                Assert.Equal(stage, doc.RootElement.GetProperty("properties").GetProperty("authStage").GetString());
            }
            else Assert.Equal(stage, ((Serilog.Events.ScalarValue)capture.Sink.Events.Last().Properties["authStage"]).Value);
            if (stage is "reward" or "result-prepare") Assert.Contains("create", f.Calls);
            if (stage == "result-prepare") Assert.Contains("reward", f.Calls);
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task RegistrationCatchFailure_ShouldNotClaimConsumedOriginalError(bool candidate, string environment)
    {
        foreach (var boundary in new[] { "localizer", "temp-data", "redirect" })
        {
            using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
            using var f = new AuthAccountFixture(capture);
            f.Users.Setup(s => s.AddAsync(It.IsAny<User>())).ThrowsAsync(new UnreadableAuthException());
            var second = new IOException(AuthAccountFixture.Secret);
            if (boundary == "localizer") f.Localizer.Setup(l => l["auth.register.error.unexpected", It.IsAny<object[]>()]).Throws(second);
            if (boundary == "temp-data")
            {
                var temp = new Mock<ITempDataDictionary>();
                temp.SetupSet(t => t["RegisterError"] = It.IsAny<object>()).Throws(second);
                f.Controller.TempData = temp.Object;
            }
            if (boundary == "redirect") { f.Controller.ResultFailure = second; f.Controller.FailedAction = "Register"; }
            Assert.Same(second, await Assert.ThrowsAsync<IOException>(() => f.Controller.Register(f.Registration())));
            AuthAccountFixture.AssertCodes(capture);
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task RegistrationRejectionsAndCancellation_ShouldKeepExistingSemantics(bool candidate, string environment)
    {
        foreach (var reason in new[] { "model-state", "display-name", "reserved", "email", "existing", "cancelled" })
        {
            using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
            using var f = new AuthAccountFixture(capture);
            var model = f.Registration();
            switch (reason)
            {
                case "model-state": f.Controller.ModelState.AddModelError("test", AuthAccountFixture.Secret); break;
                case "display-name": model.DisplayName = "x"; break;
                case "reserved": model.DisplayName = "admin"; break;
                case "email": model.Email = "invalid"; break;
                case "existing": f.Users.Setup(s => s.QueryAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync(new List<UserVo> { new() }); break;
                case "cancelled": f.Users.Setup(s => s.QueryAsync(It.IsAny<Expression<Func<User, bool>>>())).ThrowsAsync(new OperationCanceledException(AuthAccountFixture.Secret)); break;
            }
            Assert.Equal("Register", Assert.IsType<RedirectToActionResult>(await f.Controller.Register(model)).ActionName);
            f.Users.Verify(s => s.AddAsync(It.IsAny<User>()), Times.Never);
            f.Coins.Verify(s => s.GrantRegistrationRewardAsync(It.IsAny<long>()), Times.Never);
            AuthAccountFixture.AssertCodes(capture, reason == "cancelled" ? ["auth.account.registration_failed"] : []);
            if (reason == "cancelled") Assert.Equal("cancelled", Assert.Single(capture.Events).FailureKind);
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task RealCookieHandler_ShouldCoexistWithAccountSummaryAndPreserveTicket(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var scope = capture.SensitiveScope();
        using var auth = new AuthenticationFixture(capture);
        var context = auth.Context("/Account/Login");
        using var f = new AuthAccountFixture(capture, context);
        Assert.IsType<LocalRedirectResult>(await f.Controller.Login(AuthAccountFixture.Email, AuthAccountFixture.Secret, AuthAccountFixture.ReturnUrl));
        var cookie = Assert.Single(context.Response.Headers.SetCookie);
        Assert.Contains("httponly", cookie!);
        Assert.Equal(candidate && environment == "Production"
            ? new[] { "auth.account.login_result_prepared" }
            : new[] { "auth.cookie_signed_in", "auth.account.login_result_prepared" }, capture.Codes);
        AuthAccountFixture.AssertSafe(capture);
        var next = auth.Context("/Account/Profile");
        next.Request.Headers.Cookie = cookie!.Split(';')[0];
        var ticket = await next.AuthenticateAsync(AuthenticationFixture.CookieScheme);
        Assert.True(ticket.Succeeded);
        Assert.Equal("73", ticket.Principal!.FindFirst(OpenIddictConstants.Claims.Subject)!.Value);
        Assert.Equal(AuthAccountFixture.Email, ticket.Principal.FindFirst(OpenIddictConstants.Claims.Email)!.Value);
        AuthAccountFixture.AssertSafe(capture);
    }
}
