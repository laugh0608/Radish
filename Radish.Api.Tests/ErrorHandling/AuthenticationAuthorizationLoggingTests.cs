using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class AuthenticationAuthorizationLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task ActualMiddleware_ShouldKeepSuccessChallengeAndForbid(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        using var fixture = new AuthenticationFixture(capture);
        foreach (var role in new[] { "admin", "member", "missing" })
        {
            var context = fixture.Context();
            context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AuthorizeAttribute("admin")), HttpBoundaryCapture.Secret));
            if (role != "missing") context.Request.Headers.Authorization = "Bearer " + fixture.Token(role);
            var calls = 0;
            var app = new ApplicationBuilder(fixture.Services);
            app.UseAuthentication();
            app.UseAuthorization();
            app.Run(c => { calls++; c.Response.StatusCode = 204; return Task.CompletedTask; });
            await app.Build()(context);
            Assert.Equal(role == "admin" ? 1 : 0, calls);
            Assert.Equal(role == "admin" ? 204 : role == "member" ? 403 : 401, context.Response.StatusCode);
            Assert.Equal(role != "missing", context.User.Identity?.IsAuthenticated == true);
            if (role == "admin") fixture.AssertCodes("auth.jwt_validation_succeeded", "auth.jwt_authenticated", "auth.jwt_authenticated", "auth.authorization_succeeded");
            else if (role == "member") fixture.AssertCodes("auth.jwt_validation_succeeded", "auth.jwt_authenticated", "auth.jwt_authenticated", "auth.authorization_failed", "auth.jwt_forbidden");
            else fixture.AssertCodes("auth.jwt_not_authenticated", "auth.jwt_not_authenticated", "auth.authorization_middleware_unknown", "auth.authorization_failed", "auth.jwt_challenge_observed", "auth.jwt_challenged");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task AllowAnonymousAndDownstreamFailure_ShouldKeepExistingPropagation(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        using var fixture = new AuthenticationFixture(capture);
        var context = fixture.Context();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthorizeAttribute("admin"), new AllowAnonymousAttribute()), HttpBoundaryCapture.Secret));
        var failure = new IOException(HttpBoundaryCapture.Secret);
        var app = new ApplicationBuilder(fixture.Services);
        app.UseAuthentication();
        app.UseAuthorization();
        app.Run(_ => throw failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => app.Build()(context)));
        fixture.AssertCodes("auth.jwt_not_authenticated", "auth.jwt_not_authenticated");
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task BeforeLoggerFormattingFailure_ShouldRemainOutsideAdapterBoundary(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        var messageFailure = new InvalidOperationException("message accessor failed");
        using var fixture = new AuthenticationFixture(capture, jwt: options => options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context => { context.Fail(new FailingMessageException(messageFailure)); return Task.CompletedTask; }
        });
        Assert.Same(messageFailure, await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Context().AuthenticateAsync()));
        fixture.AssertCodes();

        var authorization = fixture.Services.GetRequiredService<IAuthorizationService>();
        var requirement = new FailingRequirement();
        Assert.Same(requirement.Failure, await Assert.ThrowsAsync<InvalidOperationException>(() =>
            authorization.AuthorizeAsync(new ClaimsPrincipal(), null, new[] { requirement })));
        fixture.AssertCodes();
    }

    private sealed class FailingMessageException(Exception failure) : Exception
    {
        public override string Message => throw failure;
    }

    private sealed class FailingRequirement : IAuthorizationRequirement
    {
        internal readonly InvalidOperationException Failure = new("requirement formatting failed");
        public override string ToString() => throw Failure;
    }
}
