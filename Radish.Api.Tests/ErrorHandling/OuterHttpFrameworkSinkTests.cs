using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Radish.Extension.Log;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class OuterHttpFrameworkSinkTests
{
    private const string Developer = "Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddleware";
    private const string Hosting = "Microsoft.AspNetCore.Hosting.Diagnostics";
    private const string Kestrel = "Microsoft.AspNetCore.Server.Kestrel";

    private const string Connections = Kestrel + ".Connections";
    private const string BadRequests = Kestrel + ".BadRequests";

    private const string Http2 = Kestrel + ".Http2";
    private const string Http3 = Kestrel + ".Http3";
    private const string Https = "Microsoft.AspNetCore.Server.Kestrel.Https.Internal.HttpsConnectionMiddleware";

    [Theory]
    [InlineData(Developer, 1, "UnhandledException", "http.developer_exception")]
    [InlineData(Developer, 2, "ResponseStarted", "http.developer_response_started")]
    [InlineData(Developer, 3, "DisplayErrorPageException", "http.developer_page_failed")]
    [InlineData(Developer, 4, "RequestAborted", "http.developer_request_aborted")]
    [InlineData(Hosting, 1, null, "http.request_started")]
    [InlineData(Hosting, 2, null, "http.request_finished")]
    [InlineData(Hosting, 16, null, "http.request_unhandled")]
    [InlineData(Hosting, 6, null, "http.host_pipeline_failed")]
    [InlineData(Hosting, 11, null, "http.hosting_startup_failed")]
    [InlineData(Hosting, 15, null, "http.host_binding_overridden")]
    [InlineData(Kestrel, 13, "ApplicationError", "http.server_application_failed")]
    [InlineData(Kestrel, 22, "HeartbeatSlow", "http.server_heartbeat_slow")]
    [InlineData(Kestrel, 23, "ApplicationNeverCompleted", "http.server_application_incomplete")]
    [InlineData(Kestrel, 41, "InvalidResponseHeaderRemoved", "http.server_response_headers_removed")]
    [InlineData(Kestrel, 64, "Http2DisabledWithHttp1AndNoTls", "http.server_protocol_downgraded")]
    [InlineData(Kestrel, 65, "Http3DisabledWithHttp1AndNoTls", "http.server_protocol_downgraded")]
    [InlineData(Kestrel, 66, "RequestAborted", "http.server_request_aborted")]
    [InlineData(Kestrel, 67, "RequestBodyDrainBodyReaderInvalidState", "http.server_body_drain_failed")]
    [InlineData(Developer, 1, null, "http.developer_unclassified")]
    [InlineData(Hosting, 6, "ApplicationError", "http.hosting_unclassified")]
    [InlineData(Hosting, 6, "", "http.hosting_unclassified")]
    [InlineData(Kestrel, 999, "ApplicationError", "http.server_unclassified")]
    [InlineData(Connections, 1, "ConnectionStart", "http.connection_started")]
    [InlineData(Connections, 2, "ConnectionStop", "http.connection_stopped")]
    [InlineData(Connections, 4, "ConnectionPause", "http.connection_paused")]
    [InlineData(Connections, 5, "ConnectionResume", "http.connection_resumed")]
    [InlineData(Connections, 9, "ConnectionKeepAlive", "http.connection_keep_alive")]
    [InlineData(Connections, 10, "ConnectionDisconnect", "http.connection_disconnected")]
    [InlineData(Connections, 16, "NotAllConnectionsClosedGracefully", "http.connection_close_incomplete")]
    [InlineData(Connections, 21, "NotAllConnectionsAborted", "http.connection_abort_incomplete")]
    [InlineData(Connections, 24, "ConnectionRejected", "http.connection_rejected")]
    [InlineData(Connections, 34, "ApplicationAbortedConnection", "http.connection_application_aborted")]
    [InlineData(Connections, 39, "ConnectionAccepted", "http.connection_accepted")]
    [InlineData(BadRequests, 17, "ConnectionBadRequest", "http.bad_request")]
    [InlineData(BadRequests, 20, "RequestProcessingError", "http.request_processing_ended")]
    [InlineData(BadRequests, 27, "RequestBodyMinimumDataRateNotSatisfied", "http.request_rate_insufficient")]
    [InlineData(BadRequests, 28, "ResponseMinimumDataRateNotSatisfied", "http.response_rate_insufficient")]
    [InlineData(BadRequests, 54, "PossibleInvalidHttpVersionDetected", "http.protocol_mismatch")]
    [InlineData(Connections, 24, null, "http.connection_unclassified")]
    [InlineData(Connections, 24, "Wrong", "http.connection_unclassified")]
    [InlineData(BadRequests, 17, "", "http.bad_request_unclassified")]
    [InlineData(BadRequests, 999, "ConnectionBadRequest", "http.bad_request_unclassified")]
    [InlineData(Http2, 29, "Http2ConnectionError", "http.http2_connection_error")]
    [InlineData(Http2, 30, "Http2StreamError", "http.http2_stream_error")]
    [InlineData(Http2, 31, "HPackDecodingError", "http.http2_headers_decode_failed")]
    [InlineData(Http2, 35, "Http2StreamResetAbort", "http.http2_stream_reset")]
    [InlineData(Http2, 36, "Http2ConnectionClosing", "http.http2_connection_closing")]
    [InlineData(Http2, 37, "Http2FrameReceived", "http.http2_frame_received")]
    [InlineData(Http2, 38, "HPackEncodingError", "http.http2_headers_encode_failed")]
    [InlineData(Http2, 40, "Http2MaxConcurrentStreamsReached", "http.http2_stream_limit_reached")]
    [InlineData(Http2, 48, "Http2ConnectionClosed", "http.http2_connection_closed")]
    [InlineData(Http2, 49, "Http2FrameSending", "http.http2_frame_sending")]
    [InlineData(Http2, 60, "Http2QueueOperationsExceeded", "http.http2_output_queue_exceeded")]
    [InlineData(Http2, 61, "Http2UnexpectedDataRemaining", "http.http2_output_data_remaining")]
    [InlineData(Http2, 62, "Http2ConnectionQueueProcessingCompleted", "http.http2_output_queue_completed")]
    [InlineData(Http2, 63, "Http2UnexpectedConnectionQueueError", "http.http2_output_queue_failed")]
    [InlineData(Http2, 64, "Http2TooManyEnhanceYourCalms", "http.http2_reset_limit_exceeded")]
    [InlineData(Http2, 65, "Http2FlowControlQueueOperationsExceeded", "http.http2_flow_queue_exceeded")]
    [InlineData(Http2, 66, "Http2FlowControlQueueMaximumTooLow", "http.http2_flow_queue_adjusted")]
    [InlineData(Http3, 42, "Http3ConnectionError", "http.http3_connection_error")]
    [InlineData(Http3, 43, "Http3ConnectionClosing", "http.http3_connection_closing")]
    [InlineData(Http3, 44, "Http3ConnectionClosed", "http.http3_connection_closed")]
    [InlineData(Http3, 45, "Http3StreamAbort", "http.http3_stream_aborted")]
    [InlineData(Http3, 46, "Http3FrameReceived", "http.http3_frame_received")]
    [InlineData(Http3, 47, "Http3FrameSending", "http.http3_frame_sending")]
    [InlineData(Http3, 50, "Http3OutboundControlStreamError", "http.http3_control_stream_failed")]
    [InlineData(Http3, 51, "QPackDecodingError", "http.http3_headers_decode_failed")]
    [InlineData(Http3, 52, "QPackEncodingError", "http.http3_headers_encode_failed")]
    [InlineData(Http3, 53, "Http3GoAwayHighestOpenedStreamId", "http.http3_goaway")]
    [InlineData(Https, 1, "AuthenticationFailed", "http.tls_handshake_failed")]
    [InlineData(Https, 2, "AuthenticationTimedOut", "http.tls_handshake_timed_out")]
    [InlineData(Https, 3, "HttpsConnectionEstablished", "http.tls_established")]
    [InlineData(Https, 4, "Http2DefaultCiphersInsufficient", "http.tls_protocol_fallback")]
    [InlineData(Https, 5, "LocateCertWithPrivateKey", "http.tls_certificate_search")]
    [InlineData(Https, 6, "FoundCertWithPrivateKey", "http.tls_certificate_found")]
    [InlineData(Https, 7, "FailToLocateCertificate", "http.tls_certificate_search_failed")]
    [InlineData(Https, 8, "FailToOpenStore", "http.tls_certificate_store_failed")]
    [InlineData(Https, 9, "NoSubjectAlternativeName", "http.tls_certificate_missing_san")]
    [InlineData(Http2, 60, null, "http.http2_unclassified")]
    [InlineData(Http2, 60, "Wrong", "http.http2_unclassified")]
    [InlineData(Http3, 53, null, "http.http3_unclassified")]
    [InlineData(Http3, 53, "Wrong", "http.http3_unclassified")]
    [InlineData(Https, 5, null, "http.tls_unclassified")]
    [InlineData(Https, 5, "Wrong", "http.tls_unclassified")]
    public void Adapter_ShouldKeepMetadataAndNeverReadOrMutateOriginalPayload(string source, int id, string? name, string expected)
    {
        var sink = new HttpBoundaryTestSink();
        var original = Event(source, id, new ScalarValue(name));

        new HttpServerFrameworkLogSink(sink).Emit(original);

        var result = Assert.Single(sink.Events);
        Assert.NotSame(original, result);
        Assert.Equal(original.Timestamp, result.Timestamp);
        Assert.Equal(original.Level, result.Level);
        Assert.Equal(original.TraceId, result.TraceId);
        Assert.Equal(original.SpanId, result.SpanId);
        Assert.Equal(expected, ((ScalarValue)result.Properties["EventCode"]).Value);
        Assert.Equal(source, ((ScalarValue)result.Properties["SourceContext"]).Value);
        Assert.Equal("other", ((ScalarValue)result.Properties["failureKind"]).Value);
        Assert.Null(result.Exception);
        Assert.DoesNotContain(HttpBoundaryCapture.Secret, result.RenderMessage() + string.Join(" ", result.Properties));
        Assert.IsType<UnreadableException>(original.Exception);
        Assert.True(original.Properties.ContainsKey("payload"));
    }

    [Fact]
    public void HostingMalformedName_ShouldNotBeTreatedAsMissingName()
    {
        foreach (var name in new LogEventPropertyValue[] { new ScalarValue(6), new ScalarValue(new UnreadableValue()), new SequenceValue(new[] { new ScalarValue("private") }) })
        {
            var sink = new HttpBoundaryTestSink();
            new HttpServerFrameworkLogSink(sink).Emit(Event(Hosting, 6, name));
            Assert.Equal("http.hosting_unclassified", ((ScalarValue)Assert.Single(sink.Events).Properties["EventCode"]).Value);
        }
    }

    [Theory]
    [InlineData("Microsoft.AspNetCore.Server.Kestrel.Connections.Custom")]
    [InlineData("Microsoft.AspNetCore.Server.Kestrel.BadRequests.Custom")]
    [InlineData("Microsoft.AspNetCore.Server.Kestrel.Http2.Custom")]
    [InlineData("Microsoft.AspNetCore.Server.Kestrel.Http3.Custom")]
    [InlineData("Microsoft.Extensions.Hosting.Internal.Host")]
    [InlineData("Microsoft.Hosting.Lifetime")]
    [InlineData("Microsoft.AspNetCore.Hosting.Diagnostics.Custom")]
    [InlineData("Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddleware.Custom")]
    [InlineData("Microsoft.AspNetCore.Server.Kestrel.Https.Internal.HttpsConnectionMiddleware.Custom")]
    [InlineData("Microsoft.AspNetCore.Server.Kestrel.Core.Internal.CertificatePathWatcher")]
    [InlineData("Microsoft.AspNetCore.Server.Kestrel.Transport.Quic")]
    [InlineData(HttpBoundaryCapture.Source)]
    public void OtherSources_ShouldPassThroughSameInstance(string source)
    {
        var sink = new HttpBoundaryTestSink();
        var original = Event(source, 1, new ScalarValue("UnhandledException"));
        new HttpServerFrameworkLogSink(sink).Emit(original);
        Assert.Same(original, Assert.Single(sink.Events));
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void UnknownEvents_ShouldKeepFaultsVisibleAndRequireDiagnosticOptIn(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, diagnostics: false);
        using var scope = capture.SensitiveScope();
        foreach (var (source, code) in new[] { (Developer, "http.developer_unclassified"), (Hosting, "http.hosting_unclassified"), (Kestrel, "http.server_unclassified"), (Connections, "http.connection_unclassified"), (BadRequests, "http.bad_request_unclassified"), (Http2, "http.http2_unclassified"), (Http3, "http.http3_unclassified"), (Https, "http.tls_unclassified") })
        {
            var logger = capture.Factory.CreateLogger(source);
            logger.LogWarning(new EventId(990, "FutureWarning"), new UnreadableException(), "{secret}", HttpBoundaryCapture.Secret);
            logger.LogError(new EventId(991, "FutureError"), new UnreadableException(), "{secret}", HttpBoundaryCapture.Secret);
            logger.LogCritical(new EventId(992, "FutureCritical"), new UnreadableException(), "{secret}", HttpBoundaryCapture.Secret);
            logger.LogInformation(new EventId(993, "FutureInformation"), "{secret}", HttpBoundaryCapture.Secret);
            logger.LogDebug(new EventId(994, "FutureDebug"), "{secret}", HttpBoundaryCapture.Secret);
            Assert.Equal(candidate ? 3 : 5, capture.Events.Length);
            Assert.All(capture.Events, e => Assert.Equal(code, e.Code));
            Assert.Equal(new[] { "Warning", "Error", candidate ? "Error" : "Fatal" }, capture.Events.Take(3).Select(e => e.Level));
            capture.AssertSafe();
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task HostingStartupAssemblyEvent_ShouldNotUseGenericHostStartupFailureFilter(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        var exit = await RuntimeProcess.RunAsync("api", () =>
        {
            capture.Factory.CreateLogger(Hosting).LogCritical(new EventId(11), new UnreadableException(), "{secret}", HttpBoundaryCapture.Secret);
            capture.Factory.CreateLogger("Microsoft.Extensions.Hosting.Internal.Host").LogError(new EventId(11), new UnreadableException(), "{secret}", HttpBoundaryCapture.Secret);
            return Task.CompletedTask;
        }, capture.Emergency);
        Assert.Equal(0, exit);
        var entry = Assert.Single(capture.Events);
        Assert.Equal("http.hosting_startup_failed", entry.Code);
        Assert.Equal(candidate ? "Error" : "Fatal", entry.Level);
        capture.AssertSafe();
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void HostingGenerationMethods_ShouldKeepCriticalAndWarningPhases(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        var owner = Assembly.Load("Microsoft.AspNetCore.Hosting").GetType("Microsoft.AspNetCore.Hosting.HostingLoggerExtensions", true)!;
        var logger = capture.Factory.CreateLogger(Hosting);
        owner.GetMethod("ApplicationError", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, new[] { typeof(ILogger), typeof(Exception) })!
            .Invoke(null, new object[] { logger, new UnreadableException() });
        owner.GetMethod("HostingStartupAssemblyError", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, new[] { typeof(ILogger), typeof(Exception) })!
            .Invoke(null, new object[] { logger, new UnreadableException() });
        owner.GetMethod("PortsOverridenByUrls", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, new[] { typeof(ILogger), typeof(string), typeof(string), typeof(string) })!
            .Invoke(null, new object[] { logger, HttpBoundaryCapture.Secret, HttpBoundaryCapture.Secret, HttpBoundaryCapture.Secret });
        Assert.Equal(new[] { "http.host_pipeline_failed", "http.hosting_startup_failed", "http.host_binding_overridden" }, capture.Codes);
        Assert.Equal(new[] { candidate ? "Error" : "Fatal", candidate ? "Error" : "Fatal", "Warning" }, capture.Events.Select(e => e.Level));
        capture.AssertSafe();
    }

    [Fact]
    public void ConfiguredAuditOutput_ShouldReceiveAllAdaptedSources()
    {
        using var capture = new HttpBoundaryCapture(false, "Production", audit: true);
        using var scope = capture.SensitiveScope();
        foreach (var source in new[] { Developer, Hosting, Kestrel, Connections, BadRequests, Http2, Http3, Https })
            capture.Factory.CreateLogger(source).LogError(new EventId(999, "FutureEvent"), new UnreadableException(), "{secret}", HttpBoundaryCapture.Secret);
        Assert.Equal(new[] { "http.developer_unclassified", "http.hosting_unclassified", "http.server_unclassified", "http.connection_unclassified", "http.bad_request_unclassified", "http.http2_unclassified", "http.http3_unclassified", "http.tls_unclassified" }, capture.Codes);
        capture.AssertSafe();
    }

    private static LogEvent Event(string source, int id, LogEventPropertyValue name) => new(DateTimeOffset.UtcNow, LogEventLevel.Fatal,
        new UnreadableException(), new MessageTemplateParser().Parse(HttpBoundaryCapture.Secret + " {payload}"), new[]
        {
            new LogEventProperty("SourceContext", new ScalarValue(source)),
            new LogEventProperty("EventCode", new ScalarValue("http.failed")),
            new LogEventProperty("EventId", new StructureValue(new[]
            {
                new LogEventProperty("Id", new ScalarValue(id)), new LogEventProperty("Name", name)
            })),
            new LogEventProperty("payload", new ScalarValue(new UnreadableValue()))
        }, ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom());

    private sealed class UnreadableValue
    {
        public override string ToString() => throw new InvalidOperationException("Must not render payload");
    }
    private sealed class UnreadableException : Exception
    {
        public override string Message => throw new InvalidOperationException("Must not read Message");
        public override string ToString() => throw new InvalidOperationException("Must not render exception");
    }
}
