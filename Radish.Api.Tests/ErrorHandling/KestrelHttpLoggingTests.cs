using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Serilog.Events;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class KestrelHttpLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task RealHttp1ApplicationFailure_ShouldKeepServerResponseAndOwnership(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        var original = new IOException(HttpBoundaryCapture.Secret);
        await using var pipeline = new OuterHttpTestPipeline(capture, _ => throw original);
        await using var server = new MemoryKestrelFixture(pipeline, capture);
        await server.Start();

        var response = await server.Request();

        Assert.StartsWith("HTTP/1.1 500", response);
        Assert.Equal(1, pipeline.Application.DisposeCount);
        if (environment == "Development")
        {
            Assert.Contains(HttpBoundaryCapture.Secret, response);
            Assert.Null(pipeline.Application.DisposedException);
            Assert.Equal(new[] { "http.developer_exception" }, FailureCodes(capture));
        }
        else
        {
            Assert.DoesNotContain(HttpBoundaryCapture.Secret, response);
            Assert.Same(original, pipeline.Application.DisposedException);
            Assert.Equal(new[] { "http.server_application_failed" }, FailureCodes(capture));
        }
        AssertSafeTargetEvents(capture);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task RealHttp1Callbacks_ShouldPreserveOrderingAndCompletionBehavior(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        foreach (var beforeResponse in new[] { true, false })
        {
            var original = new IOException(HttpBoundaryCapture.Secret);
            var calls = new List<string>();
            await using var pipeline = new OuterHttpTestPipeline(capture, context =>
            {
                if (beforeResponse)
                {
                    context.Response.OnStarting(() => { calls.Add("skipped"); return Task.CompletedTask; });
                    context.Response.OnStarting(() => { calls.Add("failed"); throw original; });
                }
                else
                {
                    context.Response.OnCompleted(() => { calls.Add("continued"); return Task.CompletedTask; });
                    context.Response.OnCompleted(() => { calls.Add("failed"); throw original; });
                }
                return Task.CompletedTask;
            });
            await using var server = new MemoryKestrelFixture(pipeline, capture);
            await server.Start();

            var response = await server.Request();

            Assert.StartsWith(beforeResponse ? "HTTP/1.1 500" : "HTTP/1.1 200", response);
            Assert.DoesNotContain(HttpBoundaryCapture.Secret, response);
            Assert.Equal(beforeResponse ? new[] { "failed" } : new[] { "failed", "continued" }, calls);
            Assert.Equal(new[] { "http.server_application_failed" }, FailureCodes(capture));
            Assert.Same(beforeResponse ? original : null, pipeline.Application.DisposedException);
            AssertSafeTargetEvents(capture);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task RealHttp1StartedResponse_ShouldKeepPartialBytesAndAllFailureStages(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        var original = new IOException(HttpBoundaryCapture.Secret);
        await using var pipeline = new OuterHttpTestPipeline(capture, async context =>
        {
            context.Response.StatusCode = 206;
            context.Response.Headers["Original"] = "unchanged";
            await context.Response.WriteAsync("partial bytes");
            throw original;
        }, api: true);
        await using var server = new MemoryKestrelFixture(pipeline, capture);
        await server.Start();

        var response = await server.Request();

        Assert.StartsWith("HTTP/1.1 206", response);
        Assert.Contains("partial bytes", response);
        Assert.Contains("Original: unchanged", response);
        Assert.DoesNotContain(HttpBoundaryCapture.Secret, response);
        var expected = new List<string> { "http.response_started", "http.framework_exception" };
        if (environment == "Development") expected.AddRange(new[] { "http.developer_exception", "http.developer_response_started" });
        expected.Add("http.server_application_failed");
        Assert.Equal(expected, FailureCodes(capture));
        Assert.Same(original, pipeline.Application.DisposedException);
        AssertSafeTargetEvents(capture);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task RealHttp1Abort_ShouldKeepCancellationDiagnosticWithoutApplicationError(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        await using var pipeline = new OuterHttpTestPipeline(capture, context =>
        {
            context.Abort();
            throw new IOException(HttpBoundaryCapture.Secret);
        });
        await using var server = new MemoryKestrelFixture(pipeline, capture);
        await server.Start();

        var response = await server.Request();

        Assert.Empty(response);
        Assert.Empty(FailureCodes(capture));
        Assert.Null(pipeline.Application.DisposedException);
        if (!(candidate && environment == "Production"))
        {
            Assert.Contains(capture.Events, e => e.Code is "http.server_request_aborted" or "http.developer_request_aborted");
            var aborted = Assert.Single(capture.Events, e => e.Code == "http.connection_application_aborted");
            Assert.Equal(candidate ? "Info" : "Information", aborted.Level);
            Assert.True(aborted.Diagnostic);
        }
        AssertSafeTargetEvents(capture);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task RealHttp1NonApiRejection_ShouldKeepInnerAndOuterOwnership(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        var original = new IOException(HttpBoundaryCapture.Secret);
        await using var pipeline = new OuterHttpTestPipeline(capture, _ => throw original, api: true);
        await using var server = new MemoryKestrelFixture(pipeline, capture);
        await server.Start();

        var response = await server.Request("/protocol");

        Assert.StartsWith("HTTP/1.1 500", response);
        Assert.DoesNotContain("X-Correlation-ID", response);
        Assert.DoesNotContain("messageInfo", response);
        Assert.Equal(new[] { "http.exception_handler_failed", "http.framework_exception",
            environment == "Development" ? "http.developer_exception" : "http.server_application_failed" }, FailureCodes(capture));
        Assert.Same(environment == "Development" ? null : original, pipeline.Application.DisposedException);
        AssertSafeTargetEvents(capture);
    }

    private static string[] FailureCodes(HttpBoundaryCapture capture) => capture.Events
        .Where(e => e.Code.StartsWith("http.", StringComparison.Ordinal) && !e.Diagnostic).Select(e => e.Code).ToArray();

    internal static void AssertSafeTargetEvents(HttpBoundaryCapture capture)
    {
        Assert.Equal("", capture.Emergency.ToString());
        Assert.DoesNotContain(HttpBoundaryCapture.Secret, capture.Output.ToString());
        foreach (var e in capture.Sink.Events)
        {
            if (!e.Properties.TryGetValue("SourceContext", out var source) || source is not ScalarValue { Value: string category } ||
                category is not ("Microsoft.AspNetCore.Server.Kestrel.Connections" or "Microsoft.AspNetCore.Server.Kestrel.BadRequests" or "Microsoft.AspNetCore.Server.Kestrel" or "Microsoft.AspNetCore.Hosting.Diagnostics" or
                    "Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddleware" or HttpBoundaryCapture.Source)) continue;
            Assert.Null(e.Exception);
            Assert.DoesNotContain(HttpBoundaryCapture.Secret, e.RenderMessage() + string.Join(" ", e.Properties));
            Assert.All(e.Properties.Keys, key => Assert.Contains(key,
                new[] { "EventCode", "SourceCategory", "SourceContext", "Diagnostic", "failureKind" }));
        }
        // HTTP/2 / HTTP/3 / TLS 等类别仍在本断言与治理范围之外。
    }
}
