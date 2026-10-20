using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class KestrelConnectionLoggingTests
{
    private const string Secret = HttpBoundaryCapture.Secret;
    private const string Member = nameof(HttpExceptionPipelineLoggingTests.OutputModes);

    [Theory]
    [MemberData(Member, MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task RealHttp1KeepAlive_ShouldProcessBothRequestsAndReleaseConnection(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        var calls = 0;
        await using var pipeline = new OuterHttpTestPipeline(capture, context =>
        {
            calls++;
            context.Response.ContentLength = 0;
            return Task.CompletedTask;
        });
        await using var server = new MemoryKestrelFixture(pipeline, capture);
        await server.Start();
        var disposed = 0;

        var response = await server.RequestRaw(Request(close: false) + Request(), onDisposed: () => disposed++);

        Assert.Equal(2, calls);
        Assert.Equal(2, pipeline.Application.DisposeCount);
        Assert.Equal(2, response.Split("HTTP/1.1 200").Length - 1);
        Assert.Equal(1, disposed);
        if (DiagnosticsVisible(candidate, environment))
        {
            Assert.Contains("http.connection_keep_alive", capture.Codes);
            Assert.Equal(1, capture.Codes.Count(code => code == "http.connection_started"));
            Assert.Equal(1, capture.Codes.Count(code => code == "http.connection_stopped"));
        }
        Assert.DoesNotContain(capture.Events, e => !e.Diagnostic);
        KestrelHttpLoggingTests.AssertSafeTargetEvents(capture);
    }

    [Theory]
    [MemberData(Member, MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task RealHttp1ParserRejection_ShouldKeep400AndSkipApplication(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        foreach (var request in new[]
        {
            "INVALID " + Secret + "\r\n\r\n",
            "GET / HTTP/1.1\r\nHost: example.test\r\n" + Secret + "\r\n\r\n",
            "GET /" + Secret + " HTTP/1.1\r\nConnection: close\r\n\r\n"
        })
        {
            var calls = 0;
            await using var pipeline = new OuterHttpTestPipeline(capture, _ => { calls++; return Task.CompletedTask; });
            await using var server = new MemoryKestrelFixture(pipeline, capture);
            await server.Start();
            var disposed = 0;

            var response = await server.RequestRaw(request, onDisposed: () => disposed++);

            Assert.StartsWith("HTTP/1.1 400", response);
            Assert.DoesNotContain(Secret, response);
            Assert.Equal(0, calls);
            Assert.Equal(0, pipeline.Application.DisposeCount);
            Assert.Equal(1, disposed);
            Assert.DoesNotContain(capture.Events, e => !e.Diagnostic || e.Code == "http.request_started");
            if (DiagnosticsVisible(candidate, environment))
                Assert.Single(capture.Events, e => e.Code == "http.bad_request" && e.Level == (candidate ? "Info" : "Debug"));
            KestrelHttpLoggingTests.AssertSafeTargetEvents(capture);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(Member, MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task RealInputFailure_ShouldEndProcessingAndReleaseConnection(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        var calls = 0;
        await using var pipeline = new OuterHttpTestPipeline(capture, _ => { calls++; return Task.CompletedTask; });
        await using var server = new MemoryKestrelFixture(pipeline, capture);
        await server.Start();
        var disposed = 0;

        var response = await server.RequestRaw(null, new IOException(Secret), () => disposed++);

        Assert.Empty(response);
        Assert.Equal(0, calls);
        Assert.Equal(1, disposed);
        Assert.DoesNotContain(capture.Events, e => !e.Diagnostic);
        if (DiagnosticsVisible(candidate, environment))
            Assert.Single(capture.Events, e => e.Code == "http.request_processing_ended" && e.FailureKind == "io");
        KestrelHttpLoggingTests.AssertSafeTargetEvents(capture);
    }

    [Theory]
    [MemberData(Member, MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task RealConnectionLimit_ShouldKeepWarningWithoutHttpResponseWhenDiagnosticsOff(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, diagnostics: false);
        var calls = 0;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var pipeline = new OuterHttpTestPipeline(capture, async context =>
        {
            calls++;
            entered.TrySetResult();
            await release.Task;
            context.Response.ContentLength = 0;
        });
        await using var server = new MemoryKestrelFixture(pipeline, capture, options => options.Limits.MaxConcurrentConnections = 1);
        await server.Start();
        var disposed = 0;
        var first = server.Request();
        string firstResponse;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            var response = await server.RequestRaw(null, onDisposed: () => disposed++);

            Assert.Empty(response);
            Assert.Equal(1, calls); // 只有占用配额的首个请求进入应用。
            Assert.Equal(0, pipeline.Application.DisposeCount);
            Assert.Equal(1, disposed);
            var rejected = Assert.Single(capture.Events, e => !e.Diagnostic);
            Assert.Equal("http.connection_rejected", rejected.Code);
            Assert.Equal("Warning", rejected.Level);
        }
        finally
        {
            release.TrySetResult();
            firstResponse = await first;
        }
        Assert.StartsWith("HTTP/1.1 200", firstResponse);
        Assert.Equal(1, pipeline.Application.DisposeCount);
        KestrelHttpLoggingTests.AssertSafeTargetEvents(capture);
    }

    [Theory]
    [MemberData(Member, MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task RealConnectionFailures_ShouldKeepMainCategoryAndContinueCallbacksBeforeDisposal(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        foreach (var failDelegate in new[] { true, false })
        {
            var order = new List<string>();
            var calls = 0;
            await using var pipeline = new OuterHttpTestPipeline(capture, context =>
            {
                calls++;
                context.Response.ContentLength = 0;
                return Task.CompletedTask;
            });
            await using var server = new MemoryKestrelFixture(pipeline, capture, middleware: next => async connection =>
            {
                var completion = connection.Features.Get<IConnectionCompleteFeature>()!;
                completion.OnCompleted(_ => { order.Add("continued"); return Task.CompletedTask; }, connection);
                completion.OnCompleted(async _ => { await Task.Yield(); order.Add("async-failed"); throw new IOException(Secret); }, connection);
                completion.OnCompleted(_ => { order.Add("sync-failed"); throw new IOException(Secret); }, connection);
                if (failDelegate) throw new IOException(Secret);
                await next(connection);
            });
            await server.Start();

            var response = await server.RequestRaw(failDelegate ? null : Request(), onDisposed: () => order.Add("disposed"));

            if (failDelegate) Assert.Empty(response);
            else Assert.StartsWith("HTTP/1.1 200", response);
            Assert.Equal(failDelegate ? 0 : 1, calls);
            Assert.Equal(new[] { "sync-failed", "async-failed", "continued", "disposed" }, order);
            Assert.Equal(failDelegate ? 3 : 2, capture.Events.Count(e => !e.Diagnostic));
            Assert.All(capture.Events.Where(e => !e.Diagnostic), e =>
            {
                Assert.Equal("http.server_unclassified", e.Code);
                Assert.Equal("Error", e.Level);
                Assert.Equal("io", e.FailureKind);
            });
            if (DiagnosticsVisible(candidate, environment)) Assert.Contains("http.connection_stopped", capture.Codes);
            KestrelHttpLoggingTests.AssertSafeTargetEvents(capture);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(Member, MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void RealGenerationMethods_ShouldKeepRateAndShutdownDiagnostics(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        var owner = Assembly.Load("Microsoft.AspNetCore.Server.Kestrel.Core")
            .GetType("Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure.KestrelTrace", true)!;
        var trace = Activator.CreateInstance(owner, capture.Factory)!;
        var expected = new List<string>();
        foreach (var (method, code) in new[]
        {
            ("ConnectionPause", "http.connection_paused"), ("ConnectionResume", "http.connection_resumed"),
            ("NotAllConnectionsClosedGracefully", "http.connection_close_incomplete"),
            ("NotAllConnectionsAborted", "http.connection_abort_incomplete"),
            ("RequestBodyMinimumDataRateNotSatisfied", "http.request_rate_insufficient"),
            ("ResponseMinimumDataRateNotSatisfied", "http.response_rate_insufficient"),
            ("PossibleInvalidHttpVersionDetected", "http.protocol_mismatch")
        })
        {
            var generator = owner.GetMethod(method)!;
            var arguments = generator.GetParameters().Select(p => p.ParameterType == typeof(string) ? (object)Secret :
                p.ParameterType == typeof(double) ? 1d : p.ParameterType.IsEnum ? Enum.ToObject(p.ParameterType, 0) :
                throw new InvalidOperationException("Unexpected framework signature")).ToArray();
            generator.Invoke(trace, arguments);
            expected.Add(code);
        }
        Assert.Equal(DiagnosticsVisible(candidate, environment) ? expected : Array.Empty<string>(), capture.Codes);
        Assert.All(capture.Events, e => { Assert.True(e.Diagnostic); Assert.Equal(candidate ? "Info" : "Debug", e.Level); });
        capture.AssertSafe();
    }

    private static bool DiagnosticsVisible(bool candidate, string environment) => !candidate || environment == "Development";
    private static string Request(bool close = true) => "GET /" + Secret + "?token=" + Secret +
        " HTTP/1.1\r\nHost: example.test\r\nAuthorization: Bearer " + Secret +
        (close ? "\r\nConnection: close" : "") + "\r\n\r\n";
}
