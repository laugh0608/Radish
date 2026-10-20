using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Radish.Api.Tests.ErrorHandling;
using Radish.Auth.ViewModels.Account;
using Radish.Model.OpenIddict;
using Xunit;

namespace Radish.Api.Tests.Auth;

[Collection("Runtime logging global state")]
public sealed class AuthClientLoggingTests
{
    [Theory]
    [MemberData(nameof(AuthAccountLoggingTests.OutputModes), MemberType = typeof(AuthAccountLoggingTests))]
    public async Task ClientLookup_ShouldPreserveQueryPrecedenceAndBothProjectionPaths(bool candidate, string environment)
    {
        foreach (var direct in new[] { true, false })
        foreach (var useRegistration in new[] { true, false })
        {
            using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
            using var scope = capture.SensitiveScope();
            using var f = new AuthAccountFixture(capture);
            using var cancellation = new CancellationTokenSource();
            f.Context.RequestAborted = cancellation.Token;
            f.Context.Request.QueryString = new QueryString("?client_id=fallback");
            var app = direct ? (object)new RadishApplication
            {
                ClientId = AuthAccountFixture.Secret, DisplayName = AuthAccountFixture.Secret,
                Description = AuthAccountFixture.Secret, Logo = AuthAccountFixture.Secret, DeveloperName = AuthAccountFixture.Secret
            } : new object();
            f.Applications.Setup(m => m.FindByClientIdAsync(AuthAccountFixture.Secret, cancellation.Token)).ReturnsAsync(app);
            f.Applications.Setup(m => m.GetClientIdAsync(app, cancellation.Token)).ReturnsAsync(AuthAccountFixture.Secret);
            f.Applications.Setup(m => m.GetDisplayNameAsync(app, cancellation.Token)).ReturnsAsync(AuthAccountFixture.Secret);
            var properties = new Dictionary<string, JsonElement>
            {
                ["description"] = JsonSerializer.SerializeToElement(AuthAccountFixture.Secret),
                ["logo"] = JsonSerializer.SerializeToElement(AuthAccountFixture.Secret),
                ["developerName"] = JsonSerializer.SerializeToElement(AuthAccountFixture.Secret)
            }.ToImmutableDictionary();
            f.Applications.Setup(m => m.GetPropertiesAsync(app, cancellation.Token)).ReturnsAsync(properties);
            var url = AuthAccountFixture.ReturnUrl + "&client_id=second";
            var view = Assert.IsType<ViewResult>(useRegistration ? await f.Controller.Register(url) : await f.Controller.Login(url));
            var summary = useRegistration ? Assert.IsType<RegisterViewModel>(view.Model).Client : Assert.IsType<LoginViewModel>(view.Model).Client;
            Assert.Equal(AuthAccountFixture.Secret, summary.ClientId);
            Assert.Equal(AuthAccountFixture.Secret, summary.DisplayName);
            Assert.Equal(AuthAccountFixture.Secret, summary.Description);
            Assert.Equal(AuthAccountFixture.Secret, summary.Logo);
            Assert.Equal(AuthAccountFixture.Secret, summary.DeveloperName);
            f.Applications.Verify(m => m.FindByClientIdAsync(AuthAccountFixture.Secret, cancellation.Token), Times.Once);
            f.Applications.Verify(m => m.FindByClientIdAsync("fallback", It.IsAny<CancellationToken>()), Times.Never);
            f.Applications.Verify(m => m.GetPropertiesAsync(app, cancellation.Token), direct ? Times.Never() : Times.Once());
            AuthAccountFixture.AssertCodes(capture, "auth.account.client_resolution_returned");
        }
    }

    [Theory]
    [MemberData(nameof(AuthAccountLoggingTests.OutputModes), MemberType = typeof(AuthAccountLoggingTests))]
    public async Task MissingAndUnregisteredClients_ShouldReturnEmptyWithoutWarnings(bool candidate, string environment)
    {
        foreach (var url in new string?[] { null, "", "/no-query", "/?other=value", "/?client_id=", "/?client_id=%20", "/?client_id=%ZZ" })
        foreach (var fallback in new[] { false, true })
        {
            using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
            using var f = new AuthAccountFixture(capture);
            if (fallback) f.Context.Request.QueryString = new QueryString("?client_id=" + AuthAccountFixture.Secret);
            var view = Assert.IsType<ViewResult>(await f.Controller.Login(url));
            Assert.Same(ClientSummaryViewModel.Empty, Assert.IsType<LoginViewModel>(view.Model).Client);
            var parsed = url == "/?client_id=%ZZ";
            f.Applications.Verify(m => m.FindByClientIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                parsed || fallback ? Times.Once() : Times.Never());
            AuthAccountFixture.AssertCodes(capture, "auth.account.client_resolution_returned");
            Assert.All(capture.Events, e => Assert.True(e.Diagnostic));
        }
    }

    [Theory]
    [MemberData(nameof(AuthAccountLoggingTests.OutputModes), MemberType = typeof(AuthAccountLoggingTests))]
    public async Task ResolutionFailures_ShouldKeepEmptyFallbackIncludingCancellationAndProjectionFailure(bool candidate, string environment)
    {
        foreach (var stage in new[] { "find", "client-id", "display-name", "properties", "projection", "cancelled" })
        {
            using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
            using var scope = capture.SensitiveScope();
            using var f = new AuthAccountFixture(capture);
            var app = new object();
            var failure = stage == "cancelled" ? (Exception)new OperationCanceledException(AuthAccountFixture.Secret) : new UnreadableAuthException();
            f.Applications.Setup(m => m.FindByClientIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(app);
            f.Applications.Setup(m => m.GetClientIdAsync(app, It.IsAny<CancellationToken>())).ReturnsAsync(AuthAccountFixture.Secret);
            f.Applications.Setup(m => m.GetDisplayNameAsync(app, It.IsAny<CancellationToken>())).ReturnsAsync(AuthAccountFixture.Secret);
            f.Applications.Setup(m => m.GetPropertiesAsync(app, It.IsAny<CancellationToken>())).ReturnsAsync(ImmutableDictionary<string, JsonElement>.Empty);
            switch (stage)
            {
                case "find": case "cancelled": f.Applications.Setup(m => m.FindByClientIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure); break;
                case "client-id": f.Applications.Setup(m => m.GetClientIdAsync(app, It.IsAny<CancellationToken>())).ThrowsAsync(failure); break;
                case "display-name": f.Applications.Setup(m => m.GetDisplayNameAsync(app, It.IsAny<CancellationToken>())).ThrowsAsync(failure); break;
                case "properties": f.Applications.Setup(m => m.GetPropertiesAsync(app, It.IsAny<CancellationToken>())).ThrowsAsync(failure); break;
                case "projection":
                    var doc = JsonDocument.Parse("\"" + AuthAccountFixture.Secret + "\"");
                    var disposed = doc.RootElement;
                    doc.Dispose();
                    f.Applications.Setup(m => m.GetPropertiesAsync(app, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(ImmutableDictionary<string, JsonElement>.Empty.Add("logo", disposed));
                    break;
            }
            var view = Assert.IsType<ViewResult>(await f.Controller.Login(AuthAccountFixture.ReturnUrl));
            Assert.Same(ClientSummaryViewModel.Empty, Assert.IsType<LoginViewModel>(view.Model).Client);
            AuthAccountFixture.AssertCodes(capture, "auth.account.client_resolution_failed");
            Assert.Equal("Warning", Assert.Single(capture.Events).Level);
            Assert.Equal(stage == "cancelled" ? "cancelled" : stage == "projection" ? "invalid-operation" : "other", capture.Events[0].FailureKind);
        }
    }

    [Theory]
    [MemberData(nameof(AuthAccountLoggingTests.OutputModes), MemberType = typeof(AuthAccountLoggingTests))]
    public async Task RequestQueryFailure_ShouldRemainOutsideResolverCatch(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var f = new AuthAccountFixture(capture);
        var failure = new InvalidOperationException(AuthAccountFixture.Secret);
        var query = new Mock<IQueryCollection>();
        query.SetupGet(q => q["client_id"]).Throws(failure);
        var feature = new Mock<Microsoft.AspNetCore.Http.Features.IQueryFeature>();
        feature.SetupGet(q => q.Query).Returns(query.Object);
        f.Context.Features.Set(feature.Object);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => f.Controller.Login((string?)null)));
        AuthAccountFixture.AssertCodes(capture);
    }
}
