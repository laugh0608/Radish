using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Radish.Common.Exceptions;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class HttpExceptionPipelineLoggingTests
{
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" }, { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ApiFailure_ShouldWaitForErrorResponseWriteBeforeLogging(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        foreach (var business in new[] { false, true })
        {
            Exception failure = business ? new BusinessException("Public service failure", 503) : new IOException(HttpBoundaryCapture.Secret);
            await using var pipeline = new HttpBoundaryPipeline(capture, _ => throw failure);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var bytes = new MemoryStream();
            using var stream = new ControlledResponseStream(async (buffer, cancellation) =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(cancellation);
                await bytes.WriteAsync(buffer, cancellation);
            });
            pipeline.Context.Response.Body = stream;
            var invoke = pipeline.Invoke();
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
                Assert.Empty(capture.Events);
                Assert.False(invoke.IsCompleted);
            }
            finally { release.TrySetResult(); }
            await invoke.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Equal("http.failed", Assert.Single(capture.Events).Code);
            using var json = JsonDocument.Parse(bytes.ToArray());
            Assert.Equal(business ? 503 : 500, json.RootElement.GetProperty("statusCode").GetInt32());
            Assert.False(json.RootElement.GetProperty("isSuccess").GetBoolean());
            Assert.Equal("boundary-correlation", json.RootElement.GetProperty("traceId").GetString());
            Assert.Equal("boundary-correlation", pipeline.Context.Response.Headers["X-Correlation-ID"]);
            Assert.DoesNotContain(HttpBoundaryCapture.Secret, json.RootElement.GetRawText());
            capture.AssertSafe();
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ResponseWriteFailure_ShouldKeepOriginalExceptionAndBothFrameworkStages(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        foreach (var status in new[] { 0, 404, 503 })
        foreach (var stage in new[] { "before", "partial", "cancel" })
        {
            Exception original = status == 0 ? new IOException(HttpBoundaryCapture.Secret)
                : new BusinessException("Public failure", status);
            using var cancellation = new CancellationTokenSource();
            await using var pipeline = new HttpBoundaryPipeline(capture, _ => throw original);
            pipeline.Context.RequestAborted = cancellation.Token;
            using var partial = new MemoryStream();
            using var stream = new ControlledResponseStream(async (buffer, token) =>
            {
                Assert.Empty(capture.Events);
                if (stage == "partial")
                {
                    await partial.WriteAsync(buffer[..Math.Min(16, buffer.Length)], token);
                    pipeline.ResponseFeature.Started = true;
                }
                if (stage == "cancel")
                {
                    cancellation.Cancel();
                    throw new OperationCanceledException(HttpBoundaryCapture.Secret, cancellation.Token);
                }
                throw new IOException(HttpBoundaryCapture.Secret);
            });
            pipeline.Context.Response.Body = stream;

            Assert.Same(original, await Assert.ThrowsAnyAsync<Exception>(pipeline.Invoke));

            Assert.Equal(1, stream.Attempts);
            Assert.Equal(status == 0 ? 500 : status, pipeline.Context.Response.StatusCode);
            Assert.Equal(stage == "partial", pipeline.Context.Response.HasStarted);
            Assert.Equal(stage == "partial" ? 16 : 0, partial.Length);
            Assert.Equal(new[] { "http.exception_handler_failed", "http.framework_exception" }, capture.Codes);
            Assert.Equal(new[] { stage == "cancel" ? "cancelled" : "io", status == 0 ? "io" : "other" },
                capture.Events.Select(e => e.FailureKind));
            Assert.All(capture.Events, e => Assert.Equal("Error", e.Level));
            capture.AssertSafe();
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ErrorResponseConstructionFailure_ShouldNotReadOriginalMessageAgainInLogs(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        var failure = new BrokenBusinessMessage();
        await using var pipeline = new HttpBoundaryPipeline(capture, _ => throw failure);

        await AssertOriginalFailure(pipeline.Invoke, failure);

        Assert.Equal(1, failure.MessageReads);
        Assert.Empty(pipeline.Body.ToArray());
        Assert.Equal(500, pipeline.Context.Response.StatusCode);
        Assert.Equal(new[] { "http.exception_handler_failed", "http.framework_exception" }, capture.Codes);
        Assert.Equal(new[] { "invalid-operation", "other" }, capture.Events.Select(e => e.FailureKind));
        capture.AssertSafe();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task StartedResponse_ShouldKeepStatusHeadersBodyAndOriginalException(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        var failure = new UnreadableException();
        await using var pipeline = new HttpBoundaryPipeline(capture, _ => throw failure);
        pipeline.Context.Response.StatusCode = 206;
        pipeline.Context.Response.Headers["Original"] = "unchanged";
        pipeline.Body.Write("original bytes"u8);
        pipeline.ResponseFeature.Started = true;

        await AssertOriginalFailure(pipeline.Invoke, failure);

        Assert.Equal(206, pipeline.Context.Response.StatusCode);
        Assert.Equal("unchanged", pipeline.Context.Response.Headers["Original"]);
        Assert.Equal("original bytes", Encoding.UTF8.GetString(pipeline.Body.ToArray()));
        Assert.False(pipeline.Context.Response.Headers.ContainsKey("X-Correlation-ID"));
        Assert.Equal(new[] { "http.response_started", "http.framework_exception" }, capture.Codes);
        Assert.Equal(new[] { "Warning", "Error" }, capture.Events.Select(e => e.Level));
        capture.AssertSafe();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task AbortedRequests_ShouldOnlyConsumeIoOrCancellationWithCancelledToken(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        foreach (var started in new[] { false, true })
        foreach (var cancelled in new[] { false, true })
        foreach (var kind in new[] { "io", "cancelled", "other" })
        {
            using var cancellation = new CancellationTokenSource();
            if (cancelled) cancellation.Cancel();
            Exception failure = kind switch
            {
                "io" => new IOException(HttpBoundaryCapture.Secret),
                "cancelled" => new OperationCanceledException(HttpBoundaryCapture.Secret),
                _ => new UnreadableException()
            };
            await using var pipeline = new HttpBoundaryPipeline(capture, _ => throw failure);
            // 非 API 委托拒绝处理确保未取消分支继续传播，避免用成功 JSON 掩盖取消判断。
            pipeline.Context.Request.Path = "/protocol";
            pipeline.Context.RequestAborted = cancellation.Token;
            pipeline.Context.Response.StatusCode = 202;
            pipeline.Context.Response.Headers["Original"] = "unchanged";
            pipeline.Body.Write("original"u8);
            pipeline.ResponseFeature.Started = started;
            var aborted = cancelled && kind != "other";

            if (aborted) await pipeline.Invoke();
            else await AssertOriginalFailure(pipeline.Invoke, failure);

            Assert.Equal(started ? 202 : aborted ? 499 : 500, pipeline.Context.Response.StatusCode);
            Assert.Equal(started || aborted, pipeline.Context.Response.Headers.ContainsKey("Original"));
            Assert.Equal(started || aborted ? "original" : "", Encoding.UTF8.GetString(pipeline.Body.ToArray()));
            if (aborted)
            {
                if (candidate && environment == "Production") Assert.Empty(capture.Events);
                else
                {
                    var entry = Assert.Single(capture.Events);
                    Assert.Equal("http.request_aborted", entry.Code);
                    Assert.Equal(candidate ? "Info" : "Debug", entry.Level);
                    Assert.True(entry.Diagnostic);
                    Assert.Null(entry.FailureKind);
                }
            }
            else Assert.Equal(new[] { started ? "http.response_started" : "http.exception_handler_failed", "http.framework_exception" }, capture.Codes);
            capture.AssertSafe();
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task NonApiPropagation_ShouldRetainExistingResponseClearingWithoutMessageModel(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        foreach (var path in new[] { "/hub/chat", "/connect/authorize", "/api/health", "/api/health/live", "/healthz", "/scalar" })
        {
            var failure = new IOException(HttpBoundaryCapture.Secret);
            await using var pipeline = new HttpBoundaryPipeline(capture, _ => throw failure);
            pipeline.Context.Request.Path = path;
            pipeline.Context.Response.StatusCode = 418;
            pipeline.Context.Response.Headers["Original"] = "cleared";
            pipeline.Body.Write("old body"u8);

            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(pipeline.Invoke));

            Assert.Equal(path, pipeline.Context.Request.Path);
            Assert.Equal(500, pipeline.Context.Response.StatusCode);
            Assert.Empty(pipeline.Context.Response.Headers);
            Assert.Empty(pipeline.Body.ToArray());
            Assert.Equal(new[] { "http.exception_handler_failed", "http.framework_exception" }, capture.Codes);
            capture.AssertSafe();
            capture.Clear();
        }
    }

    private static async Task AssertOriginalFailure(Func<Task> action, Exception expected)
    {
        Exception? observed = null;
        try { await action(); }
        catch (Exception exception) { observed = exception; }
        // xUnit 的 Throws 会读取 Message；这里专门验证禁止读取异常文本的日志边界。
        Assert.True(ReferenceEquals(expected, observed), "Pipeline must rethrow the original exception instance");
    }

    private sealed class BrokenBusinessMessage() : BusinessException("unused", 503)
    {
        public int MessageReads;
        public override string Message { get { MessageReads++; throw new InvalidOperationException(HttpBoundaryCapture.Secret); } }
        public override string ToString() => throw new InvalidOperationException("Must not render original exception");
    }
    private sealed class UnreadableException : Exception
    {
        public override string Message => throw new InvalidOperationException("Must not read exception Message");
        public override string ToString() => throw new InvalidOperationException("Must not render exception");
    }
}
