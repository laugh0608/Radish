using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class HttpExceptionGatewayLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task CompletedErrorPage_ShouldReportNeutralFrameworkExceptionAndRestorePath(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        var failure = new IOException(HttpBoundaryCapture.Secret);
        var paths = new List<string>();
        await using var pipeline = new HttpBoundaryPipeline(capture, async context =>
        {
            paths.Add(context.Request.Path);
            if (context.Request.Path != "/Error") throw failure;
            context.Response.StatusCode = 502;
            context.Response.Headers["ErrorPage"] = "original";
            await context.Response.WriteAsync("original gateway error page");
        }, gateway: true);

        await pipeline.Invoke();

        Assert.Equal(new[] { "/original", "/Error" }, paths);
        Assert.Equal("/original", pipeline.Context.Request.Path);
        Assert.Equal(502, pipeline.Context.Response.StatusCode);
        Assert.Equal("original", pipeline.Context.Response.Headers["ErrorPage"]);
        Assert.Equal("original gateway error page", Encoding.UTF8.GetString(pipeline.Body.ToArray()));
        var logged = Assert.Single(capture.Events);
        Assert.Equal("http.framework_exception", logged.Code);
        Assert.Equal("Error", logged.Level);
        Assert.Equal("io", logged.FailureKind);
        Assert.False(pipeline.Context.Response.Headers.ContainsKey("X-Correlation-ID"));
        capture.AssertSafe();
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task ErrorPageFailure_ShouldPreserveOriginalExceptionAndPartialErrorPage(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        foreach (var partial in new[] { false, true })
        {
            var original = new IOException(HttpBoundaryCapture.Secret);
            HttpBoundaryPipeline? pipeline = null;
            await using (pipeline = new HttpBoundaryPipeline(capture, async context =>
            {
                if (context.Request.Path != "/Error") throw original;
                context.Response.StatusCode = 502;
                if (partial)
                {
                    await context.Response.WriteAsync("partial error page");
                    pipeline!.ResponseFeature.Started = true;
                }
                throw new InvalidOperationException(HttpBoundaryCapture.Secret);
            }, gateway: true))
            {
                Assert.Same(original, await Assert.ThrowsAsync<IOException>(pipeline.Invoke));
                Assert.Equal("/original", pipeline.Context.Request.Path);
                Assert.Equal(502, pipeline.Context.Response.StatusCode);
                Assert.Equal(partial ? "partial error page" : "", Encoding.UTF8.GetString(pipeline.Body.ToArray()));
                Assert.Equal(new[] { "http.exception_handler_failed", "http.framework_exception" }, capture.Codes);
                Assert.Equal(new[] { "invalid-operation", "io" }, capture.Events.Select(e => e.FailureKind));
                capture.AssertSafe();
                capture.Clear();
            }
        }
    }
}
