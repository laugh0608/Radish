using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Radish.Common.HealthTool;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class HealthResponseLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task StatusHeadersAndStructuredResponse_ShouldRemainUnchanged(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        foreach (var status in new[] { HealthStatus.Healthy, HealthStatus.Degraded, HealthStatus.Unhealthy })
        foreach (var structured in new[] { false, true })
        {
            using var fixture = new HealthFixture(capture, b =>
            {
                b.AddCheck("self", new DelegateHealthCheck(_ => Task.FromResult(new HealthCheckResult(status, HttpBoundaryCapture.Secret, new IOException(HttpBoundaryCapture.Secret)))), tags: new[] { "minimal" });
                b.AddCheck("extended", () => throw new InvalidOperationException("Predicate failed to exclude extended check"), tags: new[] { "extended" });
            });
            var options = new HealthCheckOptions { Predicate = registration => registration.Tags.Contains("minimal") };
            if (structured) options.ResponseWriter = (context, report) => StructuredHealthCheckResponseWriter.WriteJsonAsync(context, report,
                new Dictionary<string, string[]> { ["self"] = new[] { "minimal" } });
            var next = false;
            var middleware = new HealthCheckMiddleware(_ => { next = true; return Task.CompletedTask; }, Options.Create(options), fixture.Health);
            var context = new DefaultHttpContext { RequestServices = fixture.Services };
            using var body = new MemoryStream(); context.Response.Body = body;
            await middleware.InvokeAsync(context);
            Assert.False(next);
            Assert.Equal(status == HealthStatus.Unhealthy ? 503 : 200, context.Response.StatusCode);
            Assert.Equal("no-store, no-cache", context.Response.Headers.CacheControl.ToString());
            Assert.Equal("no-cache", context.Response.Headers.Pragma.ToString());
            var text = Encoding.UTF8.GetString(body.ToArray());
            if (structured)
            {
                Assert.Contains(HttpBoundaryCapture.Secret, text);
                Assert.Contains("\"exception\"", text);
                Assert.Contains("minimal", text);
                Assert.Equal("application/json; charset=utf-8", context.Response.ContentType);
            }
            else Assert.Equal(status.ToString(), text);
            HealthFrameworkEvents.AssertSafe(capture);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task ResponseWriteFailure_ShouldOccurAfterReportCompletion(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        foreach (var cancelled in new[] { false, true })
        {
            using var cancellation = new CancellationTokenSource();
            var failure = cancelled ? (Exception)new OperationCanceledException(cancellation.Token) : new IOException(HttpBoundaryCapture.Secret);
            using var fixture = new HealthFixture(capture, b => b.AddCheck("self", () => HealthCheckResult.Degraded(HttpBoundaryCapture.Secret)));
            var options = new HealthCheckOptions { ResponseWriter = (context, report) => StructuredHealthCheckResponseWriter.WriteJsonAsync(context, report) };
            var middleware = new HealthCheckMiddleware(_ => Task.CompletedTask, Options.Create(options), fixture.Health);
            var context = new DefaultHttpContext { RequestServices = fixture.Services };
            context.RequestAborted = cancellation.Token;
            using var stream = new ControlledResponseStream((_, _) =>
            {
                if (cancelled) cancellation.Cancel();
                return ValueTask.FromException(failure);
            });
            context.Response.Body = stream;
            Exception? thrown = null;
            try { await middleware.InvokeAsync(context); } catch (Exception exception) { thrown = exception; }
            Assert.True(ReferenceEquals(failure, thrown));
            Assert.Equal(200, context.Response.StatusCode);
            Assert.True(stream.Attempts > 0);
            HealthServiceLoggingTests.AssertCodes(capture, new[] { "health.check_completed" },
                "health.run_started", "health.check_started", "health.check_completed", "health.run_completed");
            capture.Clear();
        }
    }
}
