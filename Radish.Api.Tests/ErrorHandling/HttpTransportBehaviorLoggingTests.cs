using System;
using System.IO;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class HttpTransportBehaviorLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task LongPolling_ShouldPreservePayloadCompletionCancellationAndFailure(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        foreach (var scenario in new[] { "payload", "complete", "timeout", "abort", "replacement", "failure" })
        {
            var pipe = new Pipe();
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            var token = scenario is "timeout" or "abort" or "replacement" ? canceled.Token : CancellationToken.None;
            var context = new DefaultHttpContext();
            var error = new IOException(HttpBoundaryCapture.Secret);
            context.Response.Body = scenario == "failure" ? new ControlledResponseStream((_, _) => ValueTask.FromException(error)) : new MemoryStream();
            if (scenario == "abort") context.RequestAborted = token;
            var transport = HttpTransportFixture.New("Transports.LongPollingServerTransport", scenario == "timeout" ? token : CancellationToken.None, pipe.Reader, capture.Factory);
            if (scenario is "payload" or "failure") await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(HttpBoundaryCapture.Secret), TestContext.Current.CancellationToken);
            if (scenario == "complete") await pipe.Writer.CompleteAsync();
            try
            {
                if (scenario == "failure")
                    Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => HttpTransportFixture.Run(transport, "ProcessRequestAsync", context, token)));
                else await HttpTransportFixture.Run(transport, "ProcessRequestAsync", context, token);
                Assert.Equal(scenario == "failure" ? 500 : scenario is "complete" or "replacement" ? 204 : 200, context.Response.StatusCode);
                if (scenario == "payload") Assert.Equal(HttpBoundaryCapture.Secret, Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray()));
                if (scenario == "timeout") Assert.Equal(0, context.Response.ContentLength);
                var code = scenario switch
                {
                    "payload" => "writing", "complete" or "replacement" => "completed", "timeout" => "timed_out", "abort" => "disconnected", _ => "terminated"
                };
                if (scenario == "failure")
                {
                    var failure = Assert.Single(capture.Events, e => e.Code == "signalr.long_polling_terminated");
                    Assert.Equal("Error", failure.Level);
                    Assert.Equal("io", failure.FailureKind);
                }
                else HttpConnectionLifecycleLoggingTests.ExpectDiagnostic(capture, "signalr.long_polling_" + code);
                TransportFrameworkLoggingTests.AssertSafe(capture);
            }
            finally
            {
                await pipe.Writer.CompleteAsync();
                await pipe.Reader.CompleteAsync();
                await context.Response.Body.DisposeAsync();
                capture.Clear();
            }
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task LongPollingSendTimeout_ShouldCancelThenRethrowWithoutErrorPromotion(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        using var fixture = new HttpTransportFixture(capture);
        var connection = fixture.CreateConnection(new HttpConnectionDispatcherOptions { TransportSendTimeout = TimeSpan.FromSeconds(1) });
        var pipe = new Pipe();
        await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(HttpBoundaryCapture.Secret), TestContext.Current.CancellationToken);
        OperationCanceledException? failure = null;
        var context = fixture.Context();
        context.Response.Body = new ControlledResponseStream((_, token) =>
        {
            HttpTransportFixture.Call(connection, "TryCancelSend", TimeSpan.MaxValue);
            Assert.True(token.IsCancellationRequested);
            failure = new OperationCanceledException(HttpBoundaryCapture.Secret, token);
            return ValueTask.FromException(failure);
        });
        var transport = HttpTransportFixture.New("Transports.LongPollingServerTransport", CancellationToken.None, pipe.Reader, capture.Factory, connection);
        capture.Clear();
        try
        {
            var propagated = await Assert.ThrowsAsync<OperationCanceledException>(() => HttpTransportFixture.Run(transport, "ProcessRequestAsync", context, CancellationToken.None));
            Assert.Same(failure, propagated);
            Assert.Equal(204, context.Response.StatusCode);
            HttpConnectionLifecycleLoggingTests.ExpectDiagnostic(capture, "signalr.http_context_send_timeout");
            Assert.DoesNotContain("signalr.long_polling_terminated", capture.Codes);
            Assert.DoesNotContain(capture.Events, e => e.Level is "Warning" or "Error" or "Fatal");
            TransportFrameworkLoggingTests.AssertSafe(capture);
        }
        finally
        {
            await pipe.Writer.CompleteAsync();
            await pipe.Reader.CompleteAsync();
            await fixture.DisposeConnection(connection);
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task ServerSentEvents_ShouldPreserveFormattingQuietCancellationAndIoPropagation(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        foreach (var scenario in new[] { "payload", "cancel", "failure" })
        {
            var pipe = new Pipe();
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            var token = scenario == "cancel" ? canceled.Token : CancellationToken.None;
            var error = new IOException(HttpBoundaryCapture.Secret);
            var context = new DefaultHttpContext();
            context.Response.Body = scenario == "failure" ? new ControlledResponseStream((_, _) => ValueTask.FromException(error)) : new MemoryStream();
            var transport = HttpTransportFixture.New("Transports.ServerSentEventsServerTransport", pipe.Reader, HttpBoundaryCapture.Secret, capture.Factory);
            await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(HttpBoundaryCapture.Secret), TestContext.Current.CancellationToken);
            await pipe.Writer.CompleteAsync();
            try
            {
                if (scenario == "failure") Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => HttpTransportFixture.Run(transport, "ProcessRequestAsync", context, token)));
                else await HttpTransportFixture.Run(transport, "ProcessRequestAsync", context, token);
                Assert.Equal(200, context.Response.StatusCode);
                Assert.Equal("text/event-stream", context.Response.ContentType);
                if (scenario == "payload")
                {
                    Assert.Equal(":\r\ndata: " + HttpBoundaryCapture.Secret + "\r\n\r\n", Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray()));
                    HttpConnectionLifecycleLoggingTests.ExpectDiagnostic(capture, "signalr.sse_writing");
                }
                else Assert.Empty(capture.Events);
                TransportFrameworkLoggingTests.AssertSafe(capture);
            }
            finally { await pipe.Reader.CompleteAsync(); await context.Response.Body.DisposeAsync(); capture.Clear(); }
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task WebSocketTransport_ShouldKeepCloseAndPrematureDisconnectDiagnostic(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        using var fixture = new HttpTransportFixture(capture);
        foreach (var premature in new[] { false, true })
        {
            var connection = fixture.CreateConnection();
            HttpTransportFixture.Set(connection, "TransportType", HttpTransportType.WebSockets);
            using var socket = new ClosingWebSocket(premature);
            var context = fixture.Context();
            context.Features.Set<IHttpWebSocketFeature>(new SocketFeature(socket));
            var application = HttpTransportFixture.Get<IDuplexPipe>(connection, "Application");
            var transport = HttpTransportFixture.New("Transports.WebSocketsServerTransport", new WebSocketOptions { CloseTimeout = TimeSpan.FromMilliseconds(50) }, application, connection, capture.Factory);
            capture.Clear();
            var result = (Task<bool>)HttpTransportFixture.Call(transport, "ProcessRequestAsync", context, CancellationToken.None)!;
            Assert.Equal(!premature, await result.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.True(socket.Disposed);
            HttpConnectionLifecycleLoggingTests.ExpectDiagnostic(capture, "signalr.websocket_opened");
            HttpConnectionLifecycleLoggingTests.ExpectDiagnostic(capture, "signalr.websocket_closed");
            if (premature) HttpConnectionLifecycleLoggingTests.ExpectDiagnostic(capture, "signalr.websocket_closed_prematurely");
            Assert.DoesNotContain(capture.Events, e => e.Level is "Warning" or "Error" or "Fatal");
            TransportFrameworkLoggingTests.AssertSafe(capture);
            await fixture.DisposeConnection(connection);
        }
    }

    private sealed class SocketFeature(WebSocket socket) : IHttpWebSocketFeature
    {
        public bool IsWebSocketRequest => true;
        public Task<WebSocket> AcceptAsync(WebSocketAcceptContext context) => Task.FromResult(socket);
    }
    // 抽象 WebSocket 的内存实现，真实框架 ProcessRequest/ProcessSocket 执行，未创建网络 socket。
    private sealed class ClosingWebSocket(bool premature) : WebSocket
    {
        public bool Disposed;
        public override WebSocketCloseStatus? CloseStatus => WebSocketCloseStatus.NormalClosure;
        public override string? CloseStatusDescription => HttpBoundaryCapture.Secret;
        public override WebSocketState State => WebSocketState.CloseReceived;
        public override string? SubProtocol => HttpBoundaryCapture.Secret;
        public override void Abort() { }
        public override void Dispose() => Disposed = true;
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) => premature
            ? Task.FromException<WebSocketReceiveResult>(new WebSocketException(WebSocketError.ConnectionClosedPrematurely, HttpBoundaryCapture.Secret))
            : Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
