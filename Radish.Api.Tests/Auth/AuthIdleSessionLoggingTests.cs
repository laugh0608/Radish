using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Radish.Api.Tests.ErrorHandling;
using Radish.Auth.Models;
using Radish.Auth.OpenIddict;
using Xunit;

namespace Radish.Api.Tests.Auth;

[Collection("Runtime logging global state")]
public sealed class AuthIdleSessionLoggingTests
{
    [Theory]
    [MemberData(nameof(AuthAccountLoggingTests.OutputModes), MemberType = typeof(AuthAccountLoggingTests))]
    public async Task Handler_ShouldLogOnlyAfterRejectionWithoutChangingIdlePolicy(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var scope = capture.SensitiveScope();
        foreach (var scenario in new[] { "expired", "disabled", "other-grant", "missing-request", "missing-principal", "missing-activity", "active", "client-active" })
        {
            var context = Context(scenario);
            var observations = 0;
            var logger = new ObservingLogger<ValidateIdleSessionTokenRequestHandler>(capture.Factory.CreateLogger<ValidateIdleSessionTokenRequestHandler>(), () =>
            {
                observations++;
                Assert.True(context.IsRejected);
                Assert.Equal(OpenIddictConstants.Errors.InvalidGrant, context.Error);
            });
            var options = Options(scenario != "disabled");
            var handler = new ValidateIdleSessionTokenRequestHandler(options, logger);
            var original = context.RefreshTokenPrincipal;
            await handler.HandleAsync(context);
            Assert.Same(original, context.RefreshTokenPrincipal);
            Assert.Equal(scenario == "expired", context.IsRejected);
            Assert.Equal(scenario == "expired" ? 1 : 0, observations);
            if (scenario == "expired") Assert.Equal(IdleSessionPolicy.ExpiredErrorDescription, context.ErrorDescription);
            AuthAccountFixture.AssertCodes(capture, scenario == "expired" ? ["auth.idle_session.rejected"] : []);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(AuthAccountLoggingTests.OutputModes), MemberType = typeof(AuthAccountLoggingTests))]
    public async Task Dispatcher_ShouldRetainRejectedStateAlongsideFrameworkDiagnostics(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var fixture = new OpenIddictLoggingFixture(capture);
        using var scope = capture.SensitiveScope();
        var handler = new ValidateIdleSessionTokenRequestHandler(Options(), capture.Factory.CreateLogger<ValidateIdleSessionTokenRequestHandler>());
        var options = new OpenIddictServerOptions();
        options.Handlers.Clear();
        options.Handlers.Add(OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ValidateTokenRequestContext>()
            .UseInlineHandler(context => handler.HandleAsync(context)).Build());
        var monitor = new OpenIddictOptions<OpenIddictServerOptions>(options);
        var dispatcher = new OpenIddictServerDispatcher(fixture.Factory.CreateLogger<OpenIddictServerDispatcher>(), monitor, fixture.Services);
        var context = Context("expired");
        context.Transaction.Options = options;
        await dispatcher.DispatchAsync(context);
        Assert.True(context.IsRejected);
        Assert.Equal(OpenIddictConstants.Errors.InvalidGrant, context.Error);
        Assert.Equal(candidate && environment == "Production" ? new[] { "auth.idle_session.rejected" } :
            new[] { "auth.idle_session.rejected", "oidc.server.handler_returned", "oidc.server.event_rejected" }, capture.Codes);
        AuthAccountFixture.AssertSafe(capture);
    }

    [Theory]
    [MemberData(nameof(AuthAccountLoggingTests.OutputModes), MemberType = typeof(AuthAccountLoggingTests))]
    public async Task PolicyFailure_ShouldPropagateWithoutClaimingRejectedState(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        var failure = new OperationCanceledException(AuthAccountFixture.Secret);
        var monitor = new Mock<IOptionsMonitor<IdleSessionOptions>>();
        monitor.SetupGet(o => o.CurrentValue).Throws(failure);
        var context = Context("expired");
        var handler = new ValidateIdleSessionTokenRequestHandler(monitor.Object, capture.Factory.CreateLogger<ValidateIdleSessionTokenRequestHandler>());
        Assert.Same(failure, await Assert.ThrowsAsync<OperationCanceledException>(async () => await handler.HandleAsync(context)));
        Assert.False(context.IsRejected);
        AuthAccountFixture.AssertCodes(capture);
    }

    internal static IOptionsMonitor<IdleSessionOptions> Options(bool enabled = true)
    {
        var monitor = new Mock<IOptionsMonitor<IdleSessionOptions>>();
        monitor.SetupGet(o => o.CurrentValue).Returns(new IdleSessionOptions { Enable = enabled, IdleTimeoutDays = 7 });
        return monitor.Object;
    }

    internal static OpenIddictServerEvents.ValidateTokenRequestContext Context(string scenario)
    {
        var request = new OpenIddictRequest { GrantType = scenario == "other-grant" ? OpenIddictConstants.GrantTypes.ClientCredentials : OpenIddictConstants.GrantTypes.RefreshToken };
        if (scenario == "client-active") request.SetParameter(IdleSessionPolicy.LastActiveParameter, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
        var principal = new ClaimsPrincipal(new ClaimsIdentity("RefreshToken"));
        principal.SetClaim(OpenIddictConstants.Claims.Subject, AuthAccountFixture.Secret);
        if (scenario != "missing-activity") principal.SetClaim(IdleSessionPolicy.LastActiveClaim,
            DateTimeOffset.UtcNow.AddDays(scenario == "active" ? -1 : -8).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
        return new OpenIddictServerEvents.ValidateTokenRequestContext(new OpenIddictServerTransaction
        {
            Request = scenario == "missing-request" ? null : request, Options = new OpenIddictServerOptions(), Logger = NullLogger.Instance
        }) { RefreshTokenPrincipal = scenario == "missing-principal" ? null : principal };
    }
}

internal sealed class ObservingLogger<T>(ILogger<T> inner, Action beforeWrite) : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);
    public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        beforeWrite();
        inner.Log(logLevel, eventId, state, exception, formatter);
    }
}
