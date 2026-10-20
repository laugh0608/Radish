using Radish.Common.LogTool;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Radish.Extension.Log;

/// <summary>在上下文合并后重建 SignalR Core、协议解析及 Http.Connections 事件，禁止原始异常、模板与 scope 进入输出。</summary>
public sealed class SignalRFrameworkLogSink(ILogEventSink next) : ILogEventSink
{
    private static readonly MessageTemplate Template = new MessageTemplateParser()
        .Parse("SignalR event {EventCode}.");
    private static readonly MessageTemplate FailureTemplate = new MessageTemplateParser()
        .Parse("SignalR event {EventCode}; kind={failureKind}.");

    public void Emit(LogEvent logEvent)
    {
        var source = Scalar(logEvent, "SourceContext") as string;
        var owner = source switch
        {
            "Microsoft.AspNetCore.SignalR.HubConnectionContext" => "connection",
            "Microsoft.AspNetCore.SignalR.HubConnectionHandler" => "handler",
            "Microsoft.AspNetCore.SignalR.Internal.DefaultHubDispatcher" => "dispatcher",
            "Microsoft.AspNetCore.Http.Connections.Internal.HttpConnectionContext" => "http_context",
            "Microsoft.AspNetCore.Http.Connections.Internal.HttpConnectionDispatcher" => "http_dispatcher",
            "Microsoft.AspNetCore.Http.Connections.Internal.HttpConnectionManager" => "http_manager",
            "Microsoft.AspNetCore.Http.Connections.Internal.Transports.LongPollingTransport" => "long_polling",
            "Microsoft.AspNetCore.Http.Connections.Internal.Transports.ServerSentEventsTransport" => "sse",
            "Microsoft.AspNetCore.Http.Connections.Internal.Transports.WebSocketsTransport" => "websocket",
            "Microsoft.AspNetCore.SignalR.Internal.DefaultHubProtocolResolver" => "protocol",
            _ => null
        };
        if (owner == null)
        {
            next.Emit(logEvent);
            return;
        }

        var (id, name) = Identity(logEvent);
        var code = (owner, id, name) switch
        {
            ("handler", 1, "ErrorDispatchingHubEvent") => Scalar(logEvent, "HubMethod") switch
            {
                "OnConnectedAsync" => "signalr.connect_failed",
                "OnDisconnectedAsync" => "signalr.disconnect_failed",
                _ => "signalr.unclassified"
            },
            ("handler", 2, "ErrorProcessingRequest") => "signalr.message_failed",
            ("handler", 4, "ErrorSendingClose") => "signalr.close_failed",
            ("connection", 5, "HandshakeFailed") => "signalr.handshake_failed",
            ("connection", 6, "FailedWritingMessage") => "signalr.write_failed",
            ("dispatcher", 8, "FailedInvokingHubMethod") => "signalr.invocation_failed",
            ("dispatcher", 25, "FailedStreaming") => "signalr.stream_failed",
            ("dispatcher", 3, "UnknownHubMethod") => "signalr.invocation_rejected",
            ("dispatcher", 22, "InvalidHubParameters") => "signalr.invocation_rejected",
            ("http_context", 1, "DisposingConnection") => "signalr.http_context_disposing",
            ("http_context", 2, "WaitingForApplication") => "signalr.http_context_waiting_application",
            ("http_context", 3, "ApplicationComplete") => "signalr.http_context_application_complete",
            ("http_context", 4, "WaitingForTransport") => "signalr.http_context_waiting_transport",
            ("http_context", 5, "TransportComplete") => "signalr.http_context_transport_complete",
            ("http_context", 6, "ShuttingDownTransportAndApplication") => "signalr.http_context_shutting_down",
            ("http_context", 7, "WaitingForTransportAndApplication") => "signalr.http_context_waiting_completion",
            ("http_context", 8, "TransportAndApplicationComplete") => "signalr.http_context_complete",
            ("http_context", 9, "TransportSendTimeout") => "signalr.http_context_send_timeout",
            ("http_dispatcher", 1, "ConnectionDisposed") => "signalr.http_dispatcher_disposed",
            ("http_dispatcher", 2, "ConnectionAlreadyActive") => "signalr.http_dispatcher_already_active",
            ("http_dispatcher", 3, "PollCanceled") => "signalr.http_dispatcher_poll_canceled",
            ("http_dispatcher", 4, "EstablishedConnection") => "signalr.http_dispatcher_established",
            ("http_dispatcher", 5, "ResumingConnection") => "signalr.http_dispatcher_resuming",
            ("http_dispatcher", 6, "ReceivedBytes") => "signalr.http_dispatcher_received",
            ("http_dispatcher", 7, "TransportNotSupported") => "signalr.http_dispatcher_transport_unsupported",
            ("http_dispatcher", 8, "CannotChangeTransport") => "signalr.http_dispatcher_transport_change_rejected",
            ("http_dispatcher", 9, "PostNotAllowedForWebSockets") => "signalr.http_dispatcher_websocket_post_rejected",
            ("http_dispatcher", 10, "NegotiationRequest") => "signalr.http_dispatcher_negotiating",
            ("http_dispatcher", 11, "ReceivedDeleteRequestForUnsupportedTransport") => "signalr.http_dispatcher_delete_unsupported",
            ("http_dispatcher", 12, "TerminatingConection") => "signalr.http_dispatcher_terminating",
            ("http_dispatcher", 13, "ConnectionDisposedWhileWriteInProgress") => "signalr.http_dispatcher_disposed_during_write",
            ("http_dispatcher", 14, "FailedToReadHttpRequestBody") => "signalr.http_dispatcher_request_read_failed",
            ("http_dispatcher", 15, "NegotiateProtocolVersionMismatch") => "signalr.http_dispatcher_negotiate_version_mismatch",
            ("http_dispatcher", 16, "InvalidNegotiateProtocolVersion") => "signalr.http_dispatcher_negotiate_version_invalid",
            ("http_dispatcher", 17, "UserNameChanged") => "signalr.http_dispatcher_user_changed",
            ("http_dispatcher", 18, "NotifyOnReconnectError") => "signalr.http_dispatcher_reconnect_failed",
            ("http_manager", 1, "CreatedNewConnection") => "signalr.http_manager_created",
            ("http_manager", 2, "RemovedConnection") => "signalr.http_manager_removed",
            ("http_manager", 3, "FailedDispose") => "signalr.http_manager_dispose_failed",
            ("http_manager", 5, "ConnectionTimedOut") => "signalr.http_manager_timed_out",
            ("http_manager", 4, "ConnectionReset") => "signalr.http_manager_reset",
            ("http_manager", 7, "ScanningConnectionsFailed") => "signalr.http_manager_scan_failed",
            ("http_manager", 9, "HeartBeatStarted") => "signalr.http_manager_heartbeat_started",
            ("http_manager", 10, "HeartBeatEnded") => "signalr.http_manager_heartbeat_ended",
            ("http_manager", 11, "AuthenticationExpired") => "signalr.http_manager_authentication_expired",
            ("long_polling", 1, "LongPolling204") => "signalr.long_polling_completed",
            ("long_polling", 2, "PollTimedOut") => "signalr.long_polling_timed_out",
            ("long_polling", 3, "LongPollingWritingMessage") => "signalr.long_polling_writing",
            ("long_polling", 4, "LongPollingDisconnected") => "signalr.long_polling_disconnected",
            ("long_polling", 5, "LongPollingTerminated") => "signalr.long_polling_terminated",
            ("sse", 1, "SSEWritingMessage") => "signalr.sse_writing",
            ("websocket", 1, "SocketOpened") => "signalr.websocket_opened",
            ("websocket", 2, "SocketClosed") => "signalr.websocket_closed",
            ("websocket", 3, "ClientClosed") => "signalr.websocket_client_closed",
            ("websocket", 4, "WaitingForSend") => "signalr.websocket_waiting_send",
            ("websocket", 5, "FailedSending") => "signalr.websocket_send_failed",
            ("websocket", 6, "FinishedSending") => "signalr.websocket_send_finished",
            ("websocket", 7, "WaitingForClose") => "signalr.websocket_waiting_close",
            ("websocket", 8, "CloseTimedOut") => "signalr.websocket_close_timeout",
            ("websocket", 9, "MessageReceived") => "signalr.websocket_received",
            ("websocket", 10, "MessageToApplication") => "signalr.websocket_forwarded",
            ("websocket", 11, "SendPayload") => "signalr.websocket_sending",
            ("websocket", 12, "ErrorWritingFrame") => "signalr.websocket_write_failed",
            ("websocket", 14, "ClosedPrematurely") => "signalr.websocket_closed_prematurely",
            ("websocket", 15, "ClosingWebSocketFailed") => "signalr.websocket_close_failed",
            ("websocket", 16, "SendErrored") => "signalr.websocket_send_errored",
            ("protocol", 1, "RegisteredSignalRProtocol") => "signalr.protocol_registered",
            ("protocol", 2, "FoundImplementationForProtocol") => "signalr.protocol_resolved",
            ("http_context", _, _) => "signalr.http_context_unclassified",
            ("http_dispatcher", _, _) => "signalr.http_dispatcher_unclassified",
            ("http_manager", _, _) => "signalr.http_manager_unclassified",
            ("long_polling", _, _) => "signalr.long_polling_unclassified",
            ("sse", _, _) => "signalr.sse_unclassified",
            ("websocket", _, _) => "signalr.websocket_unclassified",
            ("protocol", _, _) => "signalr.protocol_unclassified",
            _ => "signalr.unclassified"
        };
        var properties = new List<LogEventProperty>
        {
            new("EventCode", new ScalarValue(code)),
            new("SourceCategory", new ScalarValue("infrastructure")),
            new("Diagnostic", new ScalarValue(logEvent.Level < LogEventLevel.Warning))
        };
        if (logEvent.Exception != null)
            properties.Add(new LogEventProperty("failureKind", new ScalarValue(RuntimeFailureSummary.Classify(logEvent.Exception))));
        // SourceContext 已匹配固定框架类别；保留原值以兼容下游来源过滤。
        properties.Add(new LogEventProperty("SourceContext", new ScalarValue(source)));
        next.Emit(new LogEvent(logEvent.Timestamp, logEvent.Level, null, logEvent.Exception == null ? Template : FailureTemplate, properties,
            logEvent.TraceId ?? System.Diagnostics.Activity.Current?.TraceId ?? default,
            logEvent.SpanId ?? System.Diagnostics.Activity.Current?.SpanId ?? default));
    }

    private static object? Scalar(LogEvent logEvent, string key) =>
        logEvent.Properties.TryGetValue(key, out var value) && value is ScalarValue scalar ? scalar.Value : null;

    private static (int?, string?) Identity(LogEvent logEvent)
    {
        if (!logEvent.Properties.TryGetValue("EventId", out var value) || value is not StructureValue details)
            return (null, null);
        int? id = null;
        string? name = null;
        foreach (var property in details.Properties)
        {
            if (property.Name == "Id" && property.Value is ScalarValue { Value: int number }) id = number;
            if (property.Name == "Name" && property.Value is ScalarValue { Value: string text }) name = text;
        }
        return (id, name);
    }
}
