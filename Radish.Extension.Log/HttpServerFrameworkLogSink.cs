using System.Diagnostics;
using Radish.Common.LogTool;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Radish.Extension.Log;

/// <summary>适配外层 HTTP 与 Kestrel 协议 / HTTPS 的精确日志来源，不改变服务器与开发异常页行为。</summary>
public sealed class HttpServerFrameworkLogSink(ILogEventSink next) : ILogEventSink
{
    private const string DeveloperPage = "Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddleware";
    private const string Hosting = "Microsoft.AspNetCore.Hosting.Diagnostics";
    private const string Kestrel = "Microsoft.AspNetCore.Server.Kestrel";
    private const string Connections = Kestrel + ".Connections";
    private const string BadRequests = Kestrel + ".BadRequests";
    private const string Http2 = Kestrel + ".Http2";
    private const string Http3 = Kestrel + ".Http3";
    private const string Https = "Microsoft.AspNetCore.Server.Kestrel.Https.Internal.HttpsConnectionMiddleware";
    private static readonly MessageTemplate Template = new MessageTemplateParser().Parse("HTTP server event {EventCode}.");
    private static readonly MessageTemplate FailureTemplate = new MessageTemplateParser().Parse("HTTP server event {EventCode}; kind={failureKind}.");

    public void Emit(LogEvent logEvent)
    {
        if (!logEvent.Properties.TryGetValue("SourceContext", out var source) ||
            source is not ScalarValue { Value: string category } ||
            category is not (DeveloperPage or Hosting or Kestrel or Connections or BadRequests or Http2 or Http3 or Https))
        {
            next.Emit(logEvent);
            return;
        }

        var (id, name) = Identity(logEvent);
        var code = (category, id, name) switch
        {
            (DeveloperPage, 1, "UnhandledException") => "http.developer_exception",
            (DeveloperPage, 2, "ResponseStarted") => "http.developer_response_started",
            (DeveloperPage, 3, "DisplayErrorPageException") => "http.developer_page_failed",
            (DeveloperPage, 4, "RequestAborted") => "http.developer_request_aborted",
            // Hosting 使用整数 EventId，Name 为 null；不能用方法名猜测，也不能跨来源匹配 11。
            (Hosting, 1, null) => "http.request_started",
            (Hosting, 2, null) => "http.request_finished",
            (Hosting, 16, null) => "http.request_unhandled",
            (Hosting, 6, null) => "http.host_pipeline_failed",
            (Hosting, 11, null) => "http.hosting_startup_failed",
            (Hosting, 15, null) => "http.host_binding_overridden",
            // 应用错误也包含 OnStarting / OnCompleted 等回调，不表示响应一定失败。
            (Kestrel, 13, "ApplicationError") => "http.server_application_failed",
            (Kestrel, 22, "HeartbeatSlow") => "http.server_heartbeat_slow",
            (Kestrel, 23, "ApplicationNeverCompleted") => "http.server_application_incomplete",
            (Kestrel, 41, "InvalidResponseHeaderRemoved") => "http.server_response_headers_removed",
            (Kestrel, 64, "Http2DisabledWithHttp1AndNoTls") or
            (Kestrel, 65, "Http3DisabledWithHttp1AndNoTls") => "http.server_protocol_downgraded",
            (Kestrel, 66, "RequestAborted") => "http.server_request_aborted",
            (Kestrel, 67, "RequestBodyDrainBodyReaderInvalidState") => "http.server_body_drain_failed",
            (Connections, 1, "ConnectionStart") => "http.connection_started",
            (Connections, 2, "ConnectionStop") => "http.connection_stopped",
            (Connections, 4, "ConnectionPause") => "http.connection_paused",
            (Connections, 5, "ConnectionResume") => "http.connection_resumed",
            (Connections, 9, "ConnectionKeepAlive") => "http.connection_keep_alive",
            (Connections, 10, "ConnectionDisconnect") => "http.connection_disconnected",
            (Connections, 16, "NotAllConnectionsClosedGracefully") => "http.connection_close_incomplete",
            (Connections, 21, "NotAllConnectionsAborted") => "http.connection_abort_incomplete",
            (Connections, 24, "ConnectionRejected") => "http.connection_rejected",
            (Connections, 34, "ApplicationAbortedConnection") => "http.connection_application_aborted",
            (Connections, 39, "ConnectionAccepted") => "http.connection_accepted",
            (BadRequests, 17, "ConnectionBadRequest") => "http.bad_request",
            (BadRequests, 20, "RequestProcessingError") => "http.request_processing_ended",
            (BadRequests, 27, "RequestBodyMinimumDataRateNotSatisfied") => "http.request_rate_insufficient",
            (BadRequests, 28, "ResponseMinimumDataRateNotSatisfied") => "http.response_rate_insufficient",
            (BadRequests, 54, "PossibleInvalidHttpVersionDetected") => "http.protocol_mismatch",
            (Http2, 29, "Http2ConnectionError") => "http.http2_connection_error",
            (Http2, 30, "Http2StreamError") => "http.http2_stream_error",
            (Http2, 31, "HPackDecodingError") => "http.http2_headers_decode_failed",
            (Http2, 35, "Http2StreamResetAbort") => "http.http2_stream_reset",
            (Http2, 36, "Http2ConnectionClosing") => "http.http2_connection_closing",
            (Http2, 37, "Http2FrameReceived") => "http.http2_frame_received",
            (Http2, 38, "HPackEncodingError") => "http.http2_headers_encode_failed",
            (Http2, 40, "Http2MaxConcurrentStreamsReached") => "http.http2_stream_limit_reached",
            (Http2, 48, "Http2ConnectionClosed") => "http.http2_connection_closed",
            (Http2, 49, "Http2FrameSending") => "http.http2_frame_sending",
            (Http2, 60, "Http2QueueOperationsExceeded") => "http.http2_output_queue_exceeded",
            (Http2, 61, "Http2UnexpectedDataRemaining") => "http.http2_output_data_remaining",
            (Http2, 62, "Http2ConnectionQueueProcessingCompleted") => "http.http2_output_queue_completed",
            (Http2, 63, "Http2UnexpectedConnectionQueueError") => "http.http2_output_queue_failed",
            (Http2, 64, "Http2TooManyEnhanceYourCalms") => "http.http2_reset_limit_exceeded",
            (Http2, 65, "Http2FlowControlQueueOperationsExceeded") => "http.http2_flow_queue_exceeded",
            (Http2, 66, "Http2FlowControlQueueMaximumTooLow") => "http.http2_flow_queue_adjusted",
            (Http3, 42, "Http3ConnectionError") => "http.http3_connection_error",
            (Http3, 43, "Http3ConnectionClosing") => "http.http3_connection_closing",
            (Http3, 44, "Http3ConnectionClosed") => "http.http3_connection_closed",
            (Http3, 45, "Http3StreamAbort") => "http.http3_stream_aborted",
            (Http3, 46, "Http3FrameReceived") => "http.http3_frame_received",
            (Http3, 47, "Http3FrameSending") => "http.http3_frame_sending",
            (Http3, 50, "Http3OutboundControlStreamError") => "http.http3_control_stream_failed",
            (Http3, 51, "QPackDecodingError") => "http.http3_headers_decode_failed",
            (Http3, 52, "QPackEncodingError") => "http.http3_headers_encode_failed",
            (Http3, 53, "Http3GoAwayHighestOpenedStreamId") => "http.http3_goaway",
            (Https, 1, "AuthenticationFailed") => "http.tls_handshake_failed",
            (Https, 2, "AuthenticationTimedOut") => "http.tls_handshake_timed_out",
            (Https, 3, "HttpsConnectionEstablished") => "http.tls_established",
            (Https, 4, "Http2DefaultCiphersInsufficient") => "http.tls_protocol_fallback",
            (Https, 5, "LocateCertWithPrivateKey") => "http.tls_certificate_search",
            (Https, 6, "FoundCertWithPrivateKey") => "http.tls_certificate_found",
            (Https, 7, "FailToLocateCertificate") => "http.tls_certificate_search_failed",
            (Https, 8, "FailToOpenStore") => "http.tls_certificate_store_failed",
            (Https, 9, "NoSubjectAlternativeName") => "http.tls_certificate_missing_san",
            (Http2, _, _) => "http.http2_unclassified",
            (Http3, _, _) => "http.http3_unclassified",
            (Https, _, _) => "http.tls_unclassified",
            (Connections, _, _) => "http.connection_unclassified",
            (BadRequests, _, _) => "http.bad_request_unclassified",
            (DeveloperPage, _, _) => "http.developer_unclassified",
            (Hosting, _, _) => "http.hosting_unclassified",
            _ => "http.server_unclassified"
        };
        var properties = new List<LogEventProperty>
        {
            new("EventCode", new ScalarValue(code)),
            new("SourceCategory", new ScalarValue("http")),
            new("SourceContext", new ScalarValue(category)),
            new("Diagnostic", new ScalarValue(logEvent.Level < LogEventLevel.Warning))
        };
        if (logEvent.Exception != null)
            properties.Add(new LogEventProperty("failureKind", new ScalarValue(RuntimeFailureSummary.Classify(logEvent.Exception))));
        next.Emit(new LogEvent(logEvent.Timestamp, logEvent.Level, null,
            logEvent.Exception == null ? Template : FailureTemplate, properties,
            logEvent.TraceId ?? Activity.Current?.TraceId ?? default,
            logEvent.SpanId ?? Activity.Current?.SpanId ?? default));
    }

    private static (int?, string?) Identity(LogEvent logEvent)
    {
        if (!logEvent.Properties.TryGetValue("EventId", out var value) || value is not StructureValue details)
            return (null, null);
        int? id = null;
        string? name = null;
        foreach (var property in details.Properties)
        {
            if (property.Name == "Id")
            {
                if (property.Value is not ScalarValue { Value: int number }) return (null, null);
                id = number;
            }
            if (property.Name == "Name")
            {
                if (property.Value is not ScalarValue scalar || scalar.Value is not (null or string)) return (null, null);
                name = scalar.Value as string;
            }
        }
        return (id, name);
    }
}
