using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Radish.Api.Security;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class AuthenticationJwtLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task JwtValidation_ShouldPreservePrincipalFailureAndCachedResult(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        using var fixture = new AuthenticationFixture(capture);
        var context = fixture.Context();
        context.Request.Headers.Authorization = "Bearer " + fixture.Token();
        var result = await context.AuthenticateAsync();
        Assert.True(result.Succeeded);
        Assert.Equal(HttpBoundaryCapture.Secret, result.Principal!.Identity!.Name);
        Assert.True(result.Principal.IsInRole("admin"));
        var repeated = await context.AuthenticateAsync();
        Assert.Same(result.Principal, repeated.Principal);
        Assert.Same(result.Properties, repeated.Properties);
        fixture.AssertCodes("auth.jwt_validation_succeeded", "auth.jwt_authenticated", "auth.jwt_authenticated");
        capture.Clear();

        var rejected = fixture.Context();
        rejected.Request.Headers.Authorization = "Bearer " + fixture.Token(expired: true);
        var failure = await rejected.AuthenticateAsync();
        Assert.False(failure.Succeeded);
        Assert.IsType<Microsoft.IdentityModel.Tokens.SecurityTokenExpiredException>(failure.Failure);
        fixture.AssertCodes("auth.jwt_validation_failed", "auth.jwt_failure_observed", "auth.jwt_authentication_failed");
        capture.Clear();
        Assert.True((await fixture.Context().AuthenticateAsync()).None);
        fixture.AssertCodes("auth.jwt_not_authenticated");
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task QueryToken_ShouldRemainLimitedToExistingHubSegments(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        using var fixture = new AuthenticationFixture(capture);
        foreach (var path in new[] { "/hub/notification", "/hub/chat/negotiate", "/hub/comment", "/api/private", "/hub/chat-other" })
        {
            var context = fixture.Context(path);
            context.Request.QueryString = new QueryString("?access_token=" + fixture.Token());
            var allowed = path is "/hub/notification" or "/hub/chat/negotiate" or "/hub/comment";
            var result = await context.AuthenticateAsync();
            Assert.Equal(allowed, result.Succeeded);
            Assert.Equal(!allowed, result.None);
            fixture.AssertCodes(allowed ? new[] { "auth.jwt_validation_succeeded", "auth.jwt_authenticated" } : new[] { "auth.jwt_not_authenticated" });
            capture.Clear();
        }
        var headerFallback = fixture.Context("/hub/chat");
        headerFallback.Request.QueryString = new QueryString("?access_token=%20");
        headerFallback.Request.Headers.Authorization = "Bearer " + fixture.Token();
        Assert.True((await headerFallback.AuthenticateAsync()).Succeeded);
        fixture.AssertCodes("auth.jwt_validation_succeeded", "auth.jwt_authenticated");
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task ChallengeAndForbid_ShouldKeepApiJsonAndNonApiHeaders(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        using var fixture = new AuthenticationFixture(capture);
        foreach (var path in new[] { "/api/private", "/api/health", "/hub/chat" })
        {
            var api = path == "/api/private";
            var challenge = fixture.Context(path);
            await challenge.ChallengeAsync();
            Assert.Equal(401, challenge.Response.StatusCode);
            Assert.Equal(api ? "" : "Bearer", challenge.Response.Headers.WWWAuthenticate.ToString());
            Assert.Equal(api ? HttpBoundaryCapture.Secret : "", challenge.Response.Headers["X-Correlation-ID"].ToString());
            var body = System.Text.Encoding.UTF8.GetString(((MemoryStream)challenge.Response.Body).ToArray());
            if (api) Assert.Contains("error.auth.unauthorized", body);
            else Assert.Empty(body);
            fixture.AssertCodes("auth.jwt_challenge_observed", "auth.jwt_challenged");
            capture.Clear();

            var forbidden = fixture.Context(path);
            await forbidden.ForbidAsync();
            Assert.Equal(403, forbidden.Response.StatusCode);
            body = System.Text.Encoding.UTF8.GetString(((MemoryStream)forbidden.Response.Body).ToArray());
            if (api) Assert.Contains("error.auth.forbidden", body);
            else Assert.Empty(body);
            fixture.AssertCodes("auth.jwt_forbidden");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task ProcessingFailure_ShouldPropagateOriginalExceptionAfterObservation(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        var failure = new UnreadableAuthenticationException();
        using var fixture = new AuthenticationFixture(capture, jwt: options =>
            options.Events.OnTokenValidated = _ => throw failure);
        var context = fixture.Context();
        context.Request.Headers.Authorization = "Bearer " + fixture.Token();
        Exception? observed = null;
        try { await context.AuthenticateAsync(); }
        catch (Exception exception) { observed = exception; }
        Assert.True(ReferenceEquals(failure, observed), observed?.GetType().FullName ?? "No exception");
        fixture.AssertCodes("auth.jwt_validation_succeeded", "auth.jwt_processing_failed", "auth.jwt_failure_observed");
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task EventResults_ShouldRemainAuthoritativeWithoutExtraFailureObservation(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        foreach (var rejected in new[] { false, true })
        {
            var failure = new IOException(HttpBoundaryCapture.Secret);
            using var fixture = new AuthenticationFixture(capture, jwt: options => options.Events.OnTokenValidated = context =>
            {
                if (rejected) context.Fail(failure);
                else context.NoResult();
                return Task.CompletedTask;
            });
            var context = fixture.Context();
            context.Request.Headers.Authorization = "Bearer " + fixture.Token();
            var result = await context.AuthenticateAsync();
            Assert.False(result.Succeeded);
            Assert.Equal(!rejected, result.None);
            if (rejected) Assert.Same(failure, result.Failure);
            fixture.AssertCodes("auth.jwt_validation_succeeded", rejected ? "auth.jwt_authentication_failed" : "auth.jwt_not_authenticated");
            capture.Clear();
        }

        // 框架允许应用在异常回调中消费异常并返回 NoResult；日志适配不改变该返回边界。
        using var recovered = new AuthenticationFixture(capture, jwt: options => options.Events = new JwtBearerEvents
        {
            OnTokenValidated = _ => throw new IOException(HttpBoundaryCapture.Secret),
            OnAuthenticationFailed = context => { context.NoResult(); return Task.CompletedTask; }
        });
        var recoveredContext = recovered.Context();
        recoveredContext.Request.Headers.Authorization = "Bearer " + recovered.Token();
        Assert.True((await recoveredContext.AuthenticateAsync()).None);
        recovered.AssertCodes("auth.jwt_validation_succeeded", "auth.jwt_processing_failed", "auth.jwt_not_authenticated");
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task CallbackWriteFailureAndCancellation_ShouldNotClaimCompletedChallenge(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        using var fixture = new AuthenticationFixture(capture);
        foreach (var cancel in new[] { false, true })
        {
            using var cancellation = new CancellationTokenSource();
            if (cancel) cancellation.Cancel();
            var failure = cancel ? (Exception)new OperationCanceledException(cancellation.Token) : new IOException(HttpBoundaryCapture.Secret);
            var context = fixture.Context();
            context.RequestAborted = cancellation.Token;
            using var stream = new ControlledResponseStream((_, token) =>
            {
                // StreamPipeWriter 可包装 token；只验证取消状态，不要求底层 token 身份。
                Assert.Equal(cancel, token.IsCancellationRequested);
                Assert.Contains("auth.jwt_challenge_observed", capture.Codes);
                return ValueTask.FromException(failure);
            });
            context.Response.Body = stream;
            var thrown = await Record.ExceptionAsync(() => context.ChallengeAsync());
            // WriteAsJsonAsync 在调用方传入已取消 token 时仍沿用框架的取消传播。
            Assert.Same(failure, thrown);
            Assert.Equal(1, stream.Attempts);
            fixture.AssertCodes("auth.jwt_challenge_observed");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task ForbidAfterResponseStarted_ShouldKeepStatusAndEmitFrameworkDiagnostic(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        using var fixture = new AuthenticationFixture(capture);
        var context = fixture.Context("/hub/chat");
        context.Features.Set<IHttpResponseFeature>(new MutableResponseFeature { Started = true, StatusCode = 202 });
        await context.ForbidAsync();
        Assert.Equal(202, context.Response.StatusCode);
        fixture.AssertCodes("auth.jwt_forbidden_response_started", "auth.jwt_forbidden");
    }
}
