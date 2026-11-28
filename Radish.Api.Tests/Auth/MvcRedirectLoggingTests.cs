using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Moq;
using Radish.Api.Tests.ErrorHandling;
using Xunit;

namespace Radish.Api.Tests.Auth;

[Collection("Runtime logging global state")]
public sealed class MvcRedirectLoggingTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ExecutorsKeepLocationAndEmitOnlyCorrelatedRedirectEvents(bool candidate, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, "Development", "auth", audit: audit);
        using var scope = capture.SensitiveScope();
        using var activity = new Activity("login").SetIdFormat(ActivityIdFormat.W3C).Start();
        const string direct = "/connect/authorize?state=state-sample&nonce=nonce-sample&code_challenge=challenge-sample";
        var nested = "/Account/Login?ReturnUrl=" + Uri.EscapeDataString(direct);
        var context = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var urls = new UrlHelper(context);
        var factory = new Mock<IUrlHelperFactory>();
        factory.Setup(f => f.GetUrlHelper(context)).Returns(urls);
        await new RedirectResultExecutor(capture.Factory, factory.Object).ExecuteAsync(context, new RedirectResult(nested));
        AssertLocation(nested);
        await new LocalRedirectResultExecutor(capture.Factory, factory.Object).ExecuteAsync(context, new LocalRedirectResult(direct));
        AssertLocation(direct);

        var actionUrl = new Mock<IUrlHelper>();
        using var account = new AuthAccountFixture(capture);
        var rejected = Assert.IsType<RedirectToActionResult>(
            await account.Controller.Login(AuthAccountFixture.Email, string.Empty, direct));
        Assert.Equal(direct, rejected.RouteValues!["returnUrl"]);
        var retry = nested + "&email=" + Uri.EscapeDataString(AuthAccountFixture.Email);
        actionUrl.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns(retry);
        rejected.UrlHelper = actionUrl.Object;
        await new RedirectToActionResultExecutor(capture.Factory, factory.Object).ExecuteAsync(context, rejected);
        AssertLocation(retry);

        if (!candidate)
            Assert.Equal("Microsoft.AspNetCore.Mvc.RedirectToActionResult",
                ((Serilog.Events.ScalarValue)capture.Sink.Events.Last().Properties["SourceContext"]).Value);
        Assert.Equal(new[] { "mvc.redirect_result", "mvc.local_redirect_result", "auth.account.login_rejected", "mvc.redirect_to_action_result" }, capture.Codes);
        var rendered = candidate ? capture.Output.ToString() :
            string.Join("\n", capture.Sink.Events.Select(e => e.RenderMessage() + string.Join(" ", e.Properties)));
        var messages = candidate ? capture.Output.ToString().TrimEnd().Split('\n') :
            capture.Sink.Events.Select(e => e.RenderMessage()).ToArray();
        Assert.All(messages, message => Assert.Contains(activity.TraceId.ToHexString(), message));
        Assert.Contains("invalid-credentials", rendered);
        foreach (var value in new[] { "state-sample", "nonce-sample", "challenge-sample", "/Account/Login", "/connect/authorize", "ReturnUrl" })
            Assert.DoesNotContain(value, rendered);
        AuthAccountFixture.AssertSafe(capture);

        void AssertLocation(string expected)
        {
            Assert.Equal(StatusCodes.Status302Found, context.HttpContext.Response.StatusCode);
            Assert.Equal(expected, context.HttpContext.Response.Headers.Location.ToString());
        }
    }
}
