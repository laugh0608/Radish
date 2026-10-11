using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIddict.Server;
using Xunit;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class OpenIddictDispatcherLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task RealDispatcher_ShouldPreserveOutcomesAndOriginalExceptions(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var fixture = new OpenIddictLoggingFixture(capture);
        using var scope = capture.SensitiveScope();
        foreach (var outcome in new[] { "continue", "handle", "skip", "reject", "throw", "cancel" })
        {
            using var cancellation = new CancellationTokenSource();
            var failure = outcome == "cancel" ? (Exception)new OperationCanceledException(HttpBoundaryCapture.Secret, cancellation.Token) : new IOException(HttpBoundaryCapture.Secret);
            var options = new OpenIddictServerOptions();
            options.Handlers.Clear();
            var calls = new List<int>();
            options.Handlers.Add(OpenIddictServerHandlerDescriptor.CreateBuilder<ProcessRequestContext>().UseInlineHandler(context =>
            {
                calls.Add(1);
                Assert.Equal(cancellation.Token, context.CancellationToken);
                switch (outcome)
                {
                    case "handle": context.HandleRequest(); break;
                    case "skip": context.SkipRequest(); break;
                    case "reject": context.Reject("invalid_request", HttpBoundaryCapture.Secret); break;
                    case "throw": throw failure;
                    case "cancel": cancellation.Cancel(); throw failure;
                }
                return default;
            }).Build());
            options.Handlers.Add(OpenIddictServerHandlerDescriptor.CreateBuilder<ProcessRequestContext>().UseInlineHandler(_ => { calls.Add(2); return default; }).Build());
            var monitor = new OpenIddictOptions<OpenIddictServerOptions>(options);
            var logger = fixture.Factory.CreateLogger<OpenIddictServerDispatcher>();
            var transaction = await new OpenIddictServerFactory(logger, monitor).CreateTransactionAsync();
            Assert.Same(options, transaction.Options);
            var context = new ProcessRequestContext(transaction) { CancellationToken = cancellation.Token };
            var dispatcher = new OpenIddictServerDispatcher(logger, monitor, fixture.Services);
            if (outcome is "throw" or "cancel")
                Assert.Same(failure, await Record.ExceptionAsync(async () => await dispatcher.DispatchAsync(context)));
            else await dispatcher.DispatchAsync(context);
            Assert.Equal(outcome == "continue" ? new[] { 1, 2 } : new[] { 1 }, calls);
            Assert.Equal(outcome == "handle", context.IsRequestHandled);
            Assert.Equal(outcome == "skip", context.IsRequestSkipped);
            Assert.Equal(outcome == "reject", context.IsRejected);
            if (outcome == "reject") Assert.Equal(HttpBoundaryCapture.Secret, context.ErrorDescription);
            var expected = outcome switch
            {
                "throw" or "cancel" => new[] { "oidc.server.handler_exception_observed" },
                "handle" => new[] { "oidc.server.handler_returned", "oidc.server.event_handled" },
                "skip" => new[] { "oidc.server.handler_returned", "oidc.server.event_skipped" },
                "reject" => new[] { "oidc.server.handler_returned", "oidc.server.event_rejected" },
                _ => new[] { "oidc.server.handler_returned", "oidc.server.handler_returned" }
            };
            Assert.Equal(candidate && environment == "Production" ? Array.Empty<string>() : expected, capture.Codes);
            if (capture.Events.Length > 0 && outcome is "throw" or "cancel")
                Assert.Equal(outcome == "cancel" ? "cancelled" : "io", capture.Events[0].FailureKind);
            OpenIddictLoggingFixture.AssertSafe(capture);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task FilterAndResolutionFailures_ShouldStayOutsideHandlerCatch(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        foreach (var mode in new[] { "disabled", "filter-failure", "missing-filter", "factory-failure", "missing-handler" })
        {
            var failure = new IOException(HttpBoundaryCapture.Secret);
            using var fixture = new OpenIddictLoggingFixture(capture, services =>
            {
                if (mode != "missing-filter") services.AddSingleton(new Gate(mode == "filter-failure" ? failure : null));
                if (mode == "factory-failure") services.AddTransient<NeverCalled>(_ => throw failure);
            });
            var options = new OpenIddictServerOptions();
            options.Handlers.Clear();
            var descriptor = OpenIddictServerHandlerDescriptor.CreateBuilder<ProcessRequestContext>().UseSingletonHandler<NeverCalled>();
            if (mode is "disabled" or "filter-failure" or "missing-filter") descriptor.AddFilter<Gate>();
            options.Handlers.Add(descriptor.Build());
            var dispatcher = new OpenIddictServerDispatcher(fixture.Factory.CreateLogger<OpenIddictServerDispatcher>(), new OpenIddictOptions<OpenIddictServerOptions>(options), fixture.Services);
            var error = await Record.ExceptionAsync(async () => await dispatcher.DispatchAsync(new ProcessRequestContext(fixture.Transaction(options: options))));
            if (mode == "disabled") Assert.Null(error);
            else if (mode is "filter-failure" or "factory-failure") Assert.Same(failure, error);
            else Assert.IsType<InvalidOperationException>(error);
            Assert.Empty(capture.Events); // 没有进入 handler，不伪造 6132 或 handler_returned。
            OpenIddictLoggingFixture.AssertSafe(capture);
        }
    }

    private sealed class Gate(Exception? failure) : IOpenIddictServerHandlerFilter<ProcessRequestContext>
    {
        public ValueTask<bool> IsActiveAsync(ProcessRequestContext context) => failure == null ? new(false) : throw failure;
    }
    private sealed class NeverCalled : IOpenIddictServerHandler<ProcessRequestContext>
    {
        public ValueTask HandleAsync(ProcessRequestContext context) => throw new InvalidOperationException("Handler must not be invoked");
    }
}
