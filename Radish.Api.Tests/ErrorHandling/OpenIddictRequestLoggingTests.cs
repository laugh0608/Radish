using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using Xunit;
using static OpenIddict.Server.OpenIddictServerEvents;
using AuthorizationHandlers = OpenIddict.Server.OpenIddictServerHandlers.Authentication;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class OpenIddictRequestLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task HttpExtraction_ShouldKeepRequestsAndProtocolRejections(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var fixture = new OpenIddictLoggingFixture(capture);
        foreach (var mode in new[] { "get", "post", "missing-type", "invalid-type", "method" })
        {
            var transaction = fixture.Transaction();
            var http = OpenIddictLoggingFixture.AttachHttp(transaction);
            http.Request.Method = mode == "get" ? "GET" : mode == "method" ? "PUT" : "POST";
            if (mode == "get") http.Request.QueryString = new QueryString("?client_id=" + HttpBoundaryCapture.Secret + "&state=" + HttpBoundaryCapture.Secret);
            if (mode == "post")
            {
                http.Request.ContentType = "application/x-www-form-urlencoded;charset=UTF-8";
                var body = Encoding.UTF8.GetBytes("client_id=" + HttpBoundaryCapture.Secret + "&state=" + HttpBoundaryCapture.Secret);
                http.Request.Body = new MemoryStream(body); http.Request.ContentLength = body.Length;
            }
            if (mode == "invalid-type") http.Request.ContentType = "application/" + HttpBoundaryCapture.Secret;
            var context = new ProcessRequestContext(transaction);
            await new OpenIddictServerAspNetCoreHandlers.ExtractGetOrPostRequest<ProcessRequestContext>().HandleAsync(context);
            Assert.Equal(mode is not ("get" or "post"), context.IsRejected);
            if (!context.IsRejected)
            {
                Assert.Equal(HttpBoundaryCapture.Secret, transaction.Request!.ClientId);
                Assert.Equal(HttpBoundaryCapture.Secret, transaction.Request.State);
            }
            else Assert.Equal("invalid_request", context.Error);
            var code = mode == "method" ? "oidc.server.http_method_rejected" : mode == "missing-type" ? "oidc.server.http_header_missing" : "oidc.server.http_header_invalid";
            AssertCodes(capture, context.IsRejected ? code : null);
            capture.Clear();
        }
        foreach (var mode in new[] { "valid", "multiple", "bad-base64", "missing-colon" })
        {
            var transaction = fixture.Transaction(new OpenIddictRequest { ClientSecret = mode == "multiple" ? HttpBoundaryCapture.Secret : null });
            var http = OpenIddictLoggingFixture.AttachHttp(transaction);
            http.Request.Headers.Authorization = "Basic " + (mode == "bad-base64" ? "!!!" : Convert.ToBase64String(Encoding.ASCII.GetBytes(mode == "missing-colon" ? "client" : HttpBoundaryCapture.Secret + ":" + HttpBoundaryCapture.Secret)));
            var context = new ProcessRequestContext(transaction);
            await new OpenIddictServerAspNetCoreHandlers.ExtractBasicAuthenticationCredentials<ProcessRequestContext>().HandleAsync(context);
            Assert.Equal(mode != "valid", context.IsRejected);
            if (!context.IsRejected)
            {
                Assert.Equal(HttpBoundaryCapture.Secret, transaction.Request!.ClientId);
                Assert.Equal(HttpBoundaryCapture.Secret, transaction.Request.ClientSecret);
            }
            else Assert.Equal("invalid_request", context.Error);
            AssertCodes(capture, mode == "multiple" ? "oidc.server.multiple_client_credentials_rejected" : null);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task AuthorizationParameters_ShouldKeepClientRedirectAndPkceValidation(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var fixture = new OpenIddictLoggingFixture(capture);
        foreach (var mode in new[] { "valid", "missing-client", "relative-redirect", "fragment", "issuer-query", "missing-pkce", "unsupported-pkce" })
        {
            var request = new OpenIddictRequest
            {
                ClientId = mode == "missing-client" ? null : HttpBoundaryCapture.Secret,
                RedirectUri = mode == "relative-redirect" ? "/" + HttpBoundaryCapture.Secret : "https://client.invalid/" + HttpBoundaryCapture.Secret + (mode == "fragment" ? "#fragment" : mode == "issuer-query" ? "?iss=other" : ""),
                ResponseType = "code", Scope = "openid", CodeChallenge = mode == "missing-pkce" ? null : HttpBoundaryCapture.Secret,
                CodeChallengeMethod = mode == "unsupported-pkce" ? "unknown" : "S256"
            };
            var options = new OpenIddictServerOptions { RequireProofKeyForCodeExchange = true };
            options.CodeChallengeMethods.Add("S256");
            var context = new ValidateAuthorizationRequestContext(fixture.Transaction(request, options));
            await new AuthorizationHandlers.ValidateClientIdParameter().HandleAsync(context);
            if (!context.IsRejected) await new AuthorizationHandlers.ValidateRedirectUriParameter().HandleAsync(context);
            if (!context.IsRejected) await new AuthorizationHandlers.ValidateProofKeyForCodeExchangeParameters().HandleAsync(context);
            Assert.Equal(mode != "valid", context.IsRejected);
            if (context.IsRejected) Assert.Equal("invalid_request", context.Error);
            Assert.Same(request, context.Request);
            var code = mode switch
            {
                "valid" => null,
                "missing-client" or "missing-pkce" => "oidc.server.authorization_parameter_missing",
                "relative-redirect" => "oidc.server.authorization_redirect_uri_invalid",
                "fragment" => "oidc.server.authorization_redirect_fragment_rejected",
                "issuer-query" => "oidc.server.authorization_uri_parameter_forbidden",
                _ => "oidc.server.authorization_pkce_method_unsupported"
            };
            AssertCodes(capture, code);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task ApplicationAndScopeLookups_ShouldKeepResultsAndManagerFailurePropagation(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var fixture = new OpenIddictLoggingFixture(capture);
        var application = new object();
        foreach (var mode in new[] { "valid", "invalid", "default-uri", "manager-failure", "manager-cancel" })
        {
            var failure = mode == "manager-cancel" ? (Exception)new OperationCanceledException(HttpBoundaryCapture.Secret) : new IOException(HttpBoundaryCapture.Secret);
            var manager = new Mock<IOpenIddictApplicationManager>(MockBehavior.Strict);
            manager.Setup(m => m.FindByClientIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(application);
            manager.Setup(m => m.GetRedirectUrisAsync(application, It.IsAny<CancellationToken>())).ReturnsAsync(ImmutableArray.Create("https://client.invalid/" + HttpBoundaryCapture.Secret));
            var validate = manager.Setup(m => m.ValidateRedirectUriAsync(application, It.IsAny<string>(), It.IsAny<CancellationToken>()));
            if (mode.StartsWith("manager-", StringComparison.Ordinal)) validate.Throws(failure);
            else validate.ReturnsAsync(mode == "valid");
            var request = new OpenIddictRequest { ClientId = HttpBoundaryCapture.Secret, RedirectUri = mode == "default-uri" ? null : "https://client.invalid/" + HttpBoundaryCapture.Secret };
            var context = new ValidateAuthorizationRequestContext(fixture.Transaction(request));
            var error = await Record.ExceptionAsync(async () => await new AuthorizationHandlers.ValidateClientRedirectUri(manager.Object).HandleAsync(context));
            if (mode.StartsWith("manager-", StringComparison.Ordinal)) Assert.Same(failure, error);
            else Assert.Null(error);
            Assert.Equal(mode == "invalid", context.IsRejected);
            if (mode == "default-uri") Assert.Equal("https://client.invalid/" + HttpBoundaryCapture.Secret, context.RedirectUri);
            AssertCodes(capture, mode == "invalid" ? "oidc.server.authorization_redirect_uri_rejected" : null);
            capture.Clear();
        }
        var scopeManager = new Mock<IOpenIddictScopeManager>();
        scopeManager.Setup(m => m.FindByNamesAsync(It.IsAny<ImmutableArray<string>>(), It.IsAny<CancellationToken>())).Returns(EmptyScopes());
        foreach (var allowed in new[] { true, false })
        {
            var options = new OpenIddictServerOptions();
            options.Scopes.Add("openid");
            var context = new ValidateAuthorizationRequestContext(fixture.Transaction(new OpenIddictRequest { Scope = allowed ? "openid" : HttpBoundaryCapture.Secret }, options));
            await new AuthorizationHandlers.ValidateScopes(scopeManager.Object).HandleAsync(context);
            Assert.Equal(!allowed, context.IsRejected);
            if (!allowed) Assert.Equal("invalid_scope", context.Error);
            AssertCodes(capture, allowed ? null : "oidc.server.authorization_scopes_invalid");
            capture.Clear();
        }
    }

    internal static void AssertCodes(HttpBoundaryCapture capture, string? code)
    {
        Assert.Equal(code == null || (capture.Candidate && capture.Environment == "Production") ? Array.Empty<string>() : new[] { code }, capture.Codes);
        OpenIddictLoggingFixture.AssertSafe(capture);
    }
    private static async IAsyncEnumerable<object> EmptyScopes() { await Task.CompletedTask; yield break; }
}
