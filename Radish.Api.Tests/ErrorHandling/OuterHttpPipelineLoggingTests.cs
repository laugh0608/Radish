using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Radish.Common.Exceptions;
using Serilog.Events;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class OuterHttpPipelineLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task AutomaticDeveloperPage_ShouldOnlyHandleDevelopment_AndHostingShouldDisposeScopes(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        var failure = new IOException(HttpBoundaryCapture.Secret);
        await using var pipeline = new OuterHttpTestPipeline(capture, _ => throw failure);
        using var body = new MemoryStream();
        var request = OuterHttpTestPipeline.Request(body);
        using var activity = new Activity("parent").Start();

        var propagated = await pipeline.Invoke(request);

        Assert.Same(environment == "Development" ? null : failure, propagated);
        Assert.Same(propagated, pipeline.Application.DisposedException);
        Assert.Equal(1, pipeline.Application.DisposeCount);
        Assert.Same(activity, Activity.Current);
        if (environment == "Development")
        {
            Assert.Equal(500, request.Response.StatusCode);
            Assert.Contains(HttpBoundaryCapture.Secret, Encoding.UTF8.GetString(body.ToArray()));
            Assert.Equal(new[] { "http.request_started", "http.developer_exception", "http.request_finished" }, capture.Codes);
        }
        else
        {
            Assert.Empty(body.ToArray());
            Assert.Equal(candidate ? Array.Empty<string>() : new[] { "http.request_started", "http.request_finished" }, capture.Codes);
        }
        capture.AssertSafe();
        if (!candidate)
        {
            capture.Logger.Information("After request scope disposal");
            var last = capture.Sink.Events.Last();
            Assert.False(last.Properties.ContainsKey("RequestPath"));
            Assert.False(last.Properties.ContainsKey("RequestId"));
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task ExplicitDeveloperPage_ShouldKeepTextHtmlAndBadRequestStatus(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        foreach (var html in new[] { false, true })
        {
            var failure = new BadHttpRequestException(HttpBoundaryCapture.Secret, 413);
            await using var pipeline = new OuterHttpTestPipeline(capture, _ => throw failure, explicitDeveloperPage: true);
            using var body = new MemoryStream();
            var request = OuterHttpTestPipeline.Request(body);
            request.Request.Headers.Accept = html ? "text/html" : "text/plain";

            Assert.Null(await pipeline.Invoke(request));

            Assert.Equal(413, request.Response.StatusCode);
            Assert.StartsWith(html ? "text/html" : "text/plain", request.Response.ContentType);
            Assert.Contains(HttpBoundaryCapture.Secret, Encoding.UTF8.GetString(body.ToArray()));
            Assert.Equal("http.developer_exception", Assert.Single(capture.Events, e => !e.Diagnostic).Code);
            capture.AssertSafe();
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task StartedResponse_ShouldPreserveOriginalAndReportEachDeveloperPage(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        var failure = new UnreadableException();
        // Gateway Development 与宿主自动包装共同存在，必须分别保留两个阶段。
        await using var pipeline = new OuterHttpTestPipeline(capture, _ => throw failure, explicitDeveloperPage: true);
        using var body = new MemoryStream();
        body.Write("original body"u8);
        var response = new MutableResponseFeature { StatusCode = 206, Started = true };
        var request = OuterHttpTestPipeline.Request(body, response);
        request.Response.Headers["Original"] = "unchanged";

        Assert.Same(failure, await pipeline.Invoke(request));

        Assert.Equal(206, request.Response.StatusCode);
        Assert.Equal("original body", Encoding.UTF8.GetString(body.ToArray()));
        Assert.Equal("unchanged", request.Response.Headers["Original"]);
        var stages = new[] { "http.developer_exception", "http.developer_response_started" };
        Assert.Equal(environment == "Development" ? stages.Concat(stages) : stages,
            capture.Events.Where(e => !e.Diagnostic).Select(e => e.Code));
        capture.AssertSafe();
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task DeveloperPageWriteFailure_ShouldKeepOriginalAndPartialBody(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        foreach (var partial in new[] { false, true })
        {
            var original = new IOException(HttpBoundaryCapture.Secret);
            await using var pipeline = new OuterHttpTestPipeline(capture, _ => throw original,
                explicitDeveloperPage: environment != "Development");
            using var bytes = new MemoryStream();
            var response = new MutableResponseFeature();
            using var broken = new ControlledResponseStream(async (buffer, token) =>
            {
                if (partial)
                {
                    await bytes.WriteAsync(buffer[..Math.Min(12, buffer.Length)], token);
                    response.Started = true;
                }
                throw new InvalidOperationException(HttpBoundaryCapture.Secret);
            });
            var request = OuterHttpTestPipeline.Request(broken, response);

            Assert.Same(original, await pipeline.Invoke(request));

            Assert.Equal(500, request.Response.StatusCode);
            Assert.Equal(partial ? 12 : 0, bytes.Length);
            Assert.Equal(1, broken.Attempts);
            Assert.Equal(new[] { "http.developer_exception", "http.developer_page_failed" },
                capture.Events.Where(e => !e.Diagnostic).Select(e => e.Code));
            Assert.Equal(new[] { "io", "invalid-operation" }, capture.Events.Where(e => !e.Diagnostic).Select(e => e.FailureKind));
            capture.AssertSafe();
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task DeveloperPageCancellation_ShouldKeepFrameworkStatusAndSeverity(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        foreach (var started in new[] { false, true })
        foreach (var cancelled in new[] { false, true })
        foreach (var kind in new[] { "io", "cancelled", "other" })
        {
            using var cancellation = new CancellationTokenSource();
            if (cancelled) cancellation.Cancel();
            Exception original = kind switch
            {
                "io" => new IOException(HttpBoundaryCapture.Secret),
                "cancelled" => new OperationCanceledException(HttpBoundaryCapture.Secret),
                _ => new InvalidOperationException(HttpBoundaryCapture.Secret)
            };
            await using var pipeline = new OuterHttpTestPipeline(capture, _ => throw original,
                explicitDeveloperPage: environment != "Development");
            using var body = new MemoryStream();
            var request = OuterHttpTestPipeline.Request(body, new MutableResponseFeature { Started = started, StatusCode = 202 });
            request.RequestAborted = cancellation.Token;
            request.Response.Headers["Original"] = "unchanged";
            var aborted = cancelled && kind != "other";

            Assert.Same(started && !aborted ? original : null, await pipeline.Invoke(request));

            Assert.Equal(started ? 202 : aborted ? 499 : 500, request.Response.StatusCode);
            Assert.Equal(started || aborted, request.Response.Headers.ContainsKey("Original"));
            var developerEvents = capture.Events.Where(e => e.Code.StartsWith("http.developer_", StringComparison.Ordinal)).ToArray();
            if (aborted)
            {
                Assert.DoesNotContain(capture.Events, e => !e.Diagnostic);
                if (candidate && environment == "Production") Assert.Empty(developerEvents);
                else
                {
                    var entry = Assert.Single(developerEvents);
                    Assert.Equal("http.developer_request_aborted", entry.Code);
                    Assert.Equal(candidate ? "Info" : "Debug", entry.Level);
                }
            }
            else Assert.Equal(started ? 2 : 1, developerEvents.Length);
            capture.AssertSafe();
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task NormalResponsesAndHandledApiRejections_ShouldOnlyHaveHostingDiagnostics(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        foreach (var scenario in new[] { "success", "unmatched", "api-rejected" })
        {
            RequestDelegate? action = scenario switch
            {
                "success" => c => c.Response.WriteAsync("original success"),
                "api-rejected" => _ => throw new BusinessException("Public rejection", 404),
                _ => null
            };
            await using var pipeline = new OuterHttpTestPipeline(capture, action, api: scenario == "api-rejected");
            using var body = new MemoryStream();
            var request = OuterHttpTestPipeline.Request(body);

            Assert.Null(await pipeline.Invoke(request));

            Assert.Equal(scenario == "success" ? 200 : 404, request.Response.StatusCode);
            Assert.DoesNotContain(capture.Events, e => !e.Diagnostic);
            if (!(candidate && environment == "Production"))
                Assert.Equal(scenario == "unmatched"
                        ? new[] { "http.request_started", "http.request_finished", "http.request_unhandled" }
                        : new[] { "http.request_started", "http.request_finished" }, capture.Codes);
            else Assert.Empty(capture.Events);
            capture.AssertSafe();
            capture.Clear();
        }
    }

    private sealed class UnreadableException : Exception
    {
        public override string Message => throw new InvalidOperationException("Must not read original Message");
        public override string ToString() => throw new InvalidOperationException("Must not render original exception");
    }
}
