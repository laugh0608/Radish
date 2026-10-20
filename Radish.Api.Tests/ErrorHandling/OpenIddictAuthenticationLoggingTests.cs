using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using Xunit;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class OpenIddictAuthenticationLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task Authenticate_ShouldPreserveResultPropertiesPrincipalsAndCachedDispatch(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var fixture = new OpenIddictLoggingFixture(capture);
        foreach (var result in new[] { "success", "missing", "failure", "handled", "skipped" })
        {
            var transaction = fixture.Transaction();
            transaction.EndpointType = OpenIddictServerEndpointType.UserInfo;
            var http = Context(transaction);
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", HttpBoundaryCapture.Secret) }, "test"));
            var calls = 0;
            var dispatcher = new ControlledDispatcher(context =>
            {
                var authentication = Assert.IsType<ProcessAuthenticationContext>(context);
                calls++;
                switch (result)
                {
                    case "missing": authentication.Reject(OpenIddictConstants.Errors.MissingToken); break;
                    case "failure": authentication.Reject(OpenIddictConstants.Errors.InvalidToken, HttpBoundaryCapture.Secret, "https://error.invalid/" + HttpBoundaryCapture.Secret); break;
                    case "handled": authentication.HandleRequest(); break;
                    case "skipped": authentication.SkipRequest(); break;
                    default:
                        authentication.AccessTokenPrincipal = principal;
                        authentication.AccessToken = HttpBoundaryCapture.Secret;
                        authentication.Properties["private"] = HttpBoundaryCapture.Secret;
                        break;
                }
            });
            var handler = await Handler(fixture, dispatcher, http);
            var first = await handler.AuthenticateAsync();
            Assert.Same(first, await handler.AuthenticateAsync());
            Assert.Equal(1, calls);
            Assert.Equal(result == "success", first.Succeeded);
            Assert.Equal(result is "missing" or "handled" or "skipped", first.None);
            if (result == "success")
            {
                Assert.Same(principal, first.Principal);
                Assert.Equal(HttpBoundaryCapture.Secret, first.Properties!.GetTokenValue(OpenIddictServerAspNetCoreConstants.Tokens.AccessToken));
                Assert.Equal(HttpBoundaryCapture.Secret, first.Properties!.Items["private"]);
            }
            if (result == "failure")
            {
                Assert.NotNull(first.Failure);
                Assert.Equal(HttpBoundaryCapture.Secret, first.Properties!.Items[OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription]);
            }
            var code = result == "success" ? "oidc.handler.authenticated" : result == "failure" ? "oidc.handler.authentication_failed" : "oidc.handler.not_authenticated";
            Assert.Equal(candidate && environment == "Production" ? Array.Empty<string>() : new[] { code, code }, capture.Codes);
            OpenIddictLoggingFixture.AssertSafe(capture);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task RequestChallengeAndForbid_ShouldKeepHandledSkippedRejectedAndThrownBoundaries(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var fixture = new OpenIddictLoggingFixture(capture);
        foreach (var operation in new[] { "request", "challenge", "forbid" })
        foreach (var outcome in new[] { "handled", "skipped", "rejected", "unhandled-error", "throw", "cancel" })
        {
            var transaction = fixture.Transaction();
            var http = Context(transaction);
            using var cancellation = new CancellationTokenSource();
            http.RequestAborted = cancellation.Token;
            var failure = outcome == "cancel" ? (Exception)new OperationCanceledException(cancellation.Token) : new IOException(HttpBoundaryCapture.Secret);
            var errors = 0;
            var dispatcher = new ControlledDispatcher(context =>
            {
                Assert.Equal(cancellation.Token, context.CancellationToken);
                if (context is ProcessErrorContext error)
                {
                    errors++;
                    Assert.Equal("invalid_request", error.Error);
                    Assert.Equal(HttpBoundaryCapture.Secret, error.ErrorDescription);
                    if (outcome != "unhandled-error") error.HandleRequest();
                    return;
                }
                if (outcome is "throw" or "cancel") throw failure;
                var request = Assert.IsAssignableFrom<BaseValidatingContext>(context);
                if (outcome == "handled") request.HandleRequest();
                else if (outcome == "skipped") request.SkipRequest();
                else request.Reject("invalid_request", HttpBoundaryCapture.Secret);
            });
            var handler = await Handler(fixture, dispatcher, http);
            bool? handled = null;
            var error = await Record.ExceptionAsync(async () =>
            {
                if (operation == "request") handled = await handler.HandleRequestAsync();
                else if (operation == "challenge") await handler.ChallengeAsync(new AuthenticationProperties());
                else await handler.ForbidAsync(new AuthenticationProperties());
            });
            if (outcome is "throw" or "cancel") Assert.Same(failure, error);
            else if (outcome == "unhandled-error") Assert.IsType<InvalidOperationException>(error);
            else Assert.Null(error);
            Assert.Equal(outcome is "rejected" or "unhandled-error" ? 1 : 0, errors);
            if (operation == "request" && error == null) Assert.Equal(outcome != "skipped", handled);
            var expected = error == null && operation != "request" && (!candidate || environment == "Development")
                ? new[] { operation == "challenge" ? "oidc.handler.challenged" : "oidc.handler.forbidden" } : Array.Empty<string>();
            Assert.Equal(expected, capture.Codes);
            OpenIddictLoggingFixture.AssertSafe(capture);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task SignInAndOut_ShouldPreservePrincipalPropertiesAndErrorHandling(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var fixture = new OpenIddictLoggingFixture(capture);
        foreach (var signIn in new[] { false, true })
        foreach (var reject in new[] { false, true })
        {
            var transaction = fixture.Transaction();
            var http = Context(transaction);
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", HttpBoundaryCapture.Secret) }));
            var properties = new AuthenticationProperties();
            properties.Items["private"] = HttpBoundaryCapture.Secret;
            var dispatcher = new ControlledDispatcher(context =>
            {
                if (context is ProcessErrorContext error) { Assert.Equal("invalid_request", error.Error); error.HandleRequest(); return; }
                Assert.Same(properties, transaction.Properties[typeof(AuthenticationProperties).FullName!]);
                if (signIn) Assert.Same(principal, Assert.IsType<ProcessSignInContext>(context).Principal);
                else Assert.IsType<ProcessSignOutContext>(context);
                var validating = Assert.IsAssignableFrom<BaseValidatingContext>(context);
                if (reject) validating.Reject("invalid_request", HttpBoundaryCapture.Secret);
                else validating.HandleRequest();
            });
            var handler = await Handler(fixture, dispatcher, http);
            if (signIn) await handler.SignInAsync(principal, properties);
            else await handler.SignOutAsync(properties);
            Assert.Empty(capture.Events); // OpenIddict 自行实现 SignIn/Out，不虚构基类 10 / 11 事件。
            OpenIddictLoggingFixture.AssertSafe(capture);
        }
    }

    private static DefaultHttpContext Context(OpenIddictServerTransaction transaction)
    {
        var http = OpenIddictLoggingFixture.AttachHttp(transaction);
        http.Features.Set(new OpenIddictServerAspNetCoreFeature { Transaction = transaction });
        return http;
    }
    private static async Task<OpenIddictServerAspNetCoreHandler> Handler(OpenIddictLoggingFixture fixture, IOpenIddictServerDispatcher dispatcher, DefaultHttpContext http)
    {
        http.RequestServices = fixture.Services;
        var factory = new OpenIddictServerFactory(fixture.Factory.CreateLogger<OpenIddictServerDispatcher>(), new OpenIddictOptions<OpenIddictServerOptions>(new()));
        var handler = new OpenIddictServerAspNetCoreHandler(dispatcher, factory, new OpenIddictOptions<OpenIddictServerAspNetCoreOptions>(new()), fixture.Factory, UrlEncoder.Default);
        await handler.InitializeAsync(new AuthenticationScheme(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, null, typeof(OpenIddictServerAspNetCoreHandler)), http);
        return handler;
    }
    private sealed class ControlledDispatcher(Action<BaseContext> run) : IOpenIddictServerDispatcher
    {
        public ValueTask DispatchAsync<TContext>(TContext context) where TContext : BaseContext { run(context); return default; }
    }
}
