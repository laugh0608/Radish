using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Radish.Api.Security;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class AuthenticationCallbackLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task Callbacks_ShouldNotReadExceptionTextOrChangeAuthenticationResult(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        using var fixture = new AuthenticationFixture(capture);
        var events = new ApiJwtBearerEvents();
        var scheme = new AuthenticationScheme("Bearer", null, typeof(JwtBearerHandler));
        var context = fixture.Context("/hub/chat");
        var failure = new UnreadableAuthenticationException();
        var failed = new AuthenticationFailedContext(context, scheme, new JwtBearerOptions()) { Exception = failure };
        await events.AuthenticationFailed(failed);
        Assert.Null(failed.Result);
        Assert.Same(failure, failed.Exception);
        var challenge = new JwtBearerChallengeContext(context, scheme, new JwtBearerOptions(), new AuthenticationProperties())
        {
            Error = HttpBoundaryCapture.Secret, ErrorDescription = HttpBoundaryCapture.Secret,
            AuthenticateFailure = failure
        };
        await events.Challenge(challenge);
        Assert.False(challenge.Handled);
        Assert.Equal(200, context.Response.StatusCode);
        fixture.AssertCodes("auth.jwt_failure_observed", "auth.jwt_challenge_observed");
        Assert.Equal("other", capture.Events[0].FailureKind);
        Assert.All(capture.Events, entry => { Assert.Equal("Warning", entry.Level); Assert.False(entry.Diagnostic); });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfiguredWriteAndAudit_ShouldCoverAllTwentyFourCodes(bool audit)
    {
        using var capture = new HttpBoundaryCapture(false, "Production", audit: audit);
        using var scope = capture.SensitiveScope();
        using var fixture = new AuthenticationFixture(capture);
        foreach (var row in AuthenticationFrameworkEvents.All)
            AuthenticationFrameworkEvents.Generate(row, capture.Factory.CreateLogger(row.Source));
        foreach (var row in AuthenticationFrameworkEvents.Sources)
            capture.Factory.CreateLogger(row.Source).LogWarning(new EventId(991, "Future"), "{private}", HttpBoundaryCapture.Secret);
        var events = new ApiJwtBearerEvents();
        var context = fixture.Context("/hub/chat");
        var scheme = new AuthenticationScheme("Bearer", null, typeof(JwtBearerHandler));
        await events.AuthenticationFailed(new AuthenticationFailedContext(context, scheme, new JwtBearerOptions()) { Exception = new UnreadableAuthenticationException() });
        await events.Challenge(new JwtBearerChallengeContext(context, scheme, new JwtBearerOptions(), new AuthenticationProperties()));
        Assert.Equal(24, capture.Codes.Distinct().Count());
        Assert.Equal(24, capture.Events.Length);
        AuthenticationFrameworkLoggingTests.AssertSafe(capture);
    }
}
