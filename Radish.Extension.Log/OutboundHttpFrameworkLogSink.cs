using System.Diagnostics;
using Radish.Common.LogTool;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Radish.Extension.Log;

/// <summary>在上下文合并后重建已确认的 YARP 与出站 HTTP 日志，不改变请求、取消或异常传播。</summary>
public sealed class OutboundHttpFrameworkLogSink(ILogEventSink next) : ILogEventSink
{
    private static readonly MessageTemplate Template = new MessageTemplateParser().Parse("Outbound HTTP event {EventCode}.");
    private static readonly MessageTemplate FailureTemplate = new MessageTemplateParser().Parse("Outbound HTTP event {EventCode}; kind={failureKind}.");

    public void Emit(LogEvent logEvent)
    {
        if (!logEvent.Properties.TryGetValue("SourceContext", out var source) || source is not ScalarValue { Value: string category })
        {
            next.Emit(logEvent);
            return;
        }
        // 客户端名称采用封闭清单；相邻类别与未来注册的名称不自动纳入。
        var owner = category switch
        {
            "Yarp.ReverseProxy.Forwarder.HttpForwarder" => "proxy.forwarder",
            "Yarp.ReverseProxy.Forwarder.ForwarderHttpClientFactory" => "proxy.client",
            "Yarp.ReverseProxy.Forwarder.ForwarderMiddleware" => "proxy.middleware",
            "Yarp.ReverseProxy.LoadBalancing.LoadBalancingMiddleware" => "proxy.balancing",
            "Yarp.ReverseProxy.Model.ProxyPipelineInitializerMiddleware" => "proxy.pipeline",
            "Yarp.ReverseProxy.Limits.LimitsMiddleware" => "proxy.limits",
            "Microsoft.Extensions.Http.DefaultHttpClientFactory" => "http.client_factory",
            "System.Net.Http.HttpClient.PublicHeadSnapshotClient.LogicalHandler" => "http.public_head_pipeline",
            "System.Net.Http.HttpClient.PublicHeadSnapshotClient.ClientHandler" => "http.public_head_handler",
            "System.Net.Http.HttpClient.api-service.LogicalHandler" => "http.api_health_pipeline",
            "System.Net.Http.HttpClient.api-service.ClientHandler" => "http.api_health_handler",
            "System.Net.Http.HttpClient.auth-service.LogicalHandler" => "http.auth_health_pipeline",
            "System.Net.Http.HttpClient.auth-service.ClientHandler" => "http.auth_health_handler",
            "System.Net.Http.HttpClient.console-service.LogicalHandler" => "http.console_health_pipeline",
            "System.Net.Http.HttpClient.console-service.ClientHandler" => "http.console_health_handler",
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
            ("proxy.forwarder", 9, "Forwarding") => "proxy.forwarder_forwarding",
            ("proxy.forwarder", 56, "ResponseReceived") => "proxy.forwarder_response_received",
            ("proxy.forwarder", 48, "ForwardingError") => "proxy.forwarder_failed",
            ("proxy.forwarder", 66, "ForwardingRequestCancelled") => "proxy.forwarder_cancelled",
            ("proxy.forwarder", 59, "NotForwarding") => "proxy.forwarder_skipped",
            ("proxy.forwarder", 61, "RetryingWebSocketDowngradeNoConnect") => "proxy.forwarder_websocket_retry_no_connect",
            ("proxy.forwarder", 62, "RetryingWebSocketDowngradeNoHttp2") => "proxy.forwarder_websocket_retry_no_http2",
            ("proxy.forwarder", 63, "InvalidSecWebSocketKeyHeader") => "proxy.forwarder_websocket_key_invalid",
            ("proxy.client", 45, "ClientCreated") => "proxy.client_created",
            ("proxy.client", 46, "ClientReused") => "proxy.client_reused",
            ("proxy.middleware", 7, "NoAvailableDestinations") => "proxy.middleware_no_destinations",
            ("proxy.middleware", 8, "MultipleDestinationsAvailable") => "proxy.middleware_multiple_destinations",
            ("proxy.balancing", 7, "NoAvailableDestinations") => "proxy.balancing_no_destinations",
            ("proxy.pipeline", 4, "NoClusterFound") => "proxy.pipeline_cluster_missing",
            ("proxy.pipeline", 64, "TimeoutNotApplied") => "proxy.pipeline_timeout_not_applied",
            ("proxy.limits", 60, "MaxRequestBodySizeSet") => "proxy.limits_body_size_set",
            ("http.client_factory", 100, "CleanupCycleStart") => "http.client_factory_cleanup_started",
            ("http.client_factory", 101, "CleanupCycleEnd") => "http.client_factory_cleanup_completed",
            ("http.client_factory", 102, "CleanupItemFailed") => "http.client_factory_cleanup_failed",
            ("http.client_factory", 103, "HandlerExpired") => "http.client_factory_handler_expired",
            ("http.public_head_pipeline", 100, "RequestPipelineStart") => "http.public_head_pipeline_started",
            ("http.public_head_pipeline", 101, "RequestPipelineEnd") => "http.public_head_pipeline_completed",
            ("http.public_head_pipeline", 102, "RequestPipelineRequestHeader") => "http.public_head_pipeline_request_headers",
            ("http.public_head_pipeline", 103, "RequestPipelineResponseHeader") => "http.public_head_pipeline_response_headers",
            ("http.public_head_pipeline", 104, "RequestPipelineFailed") => "http.public_head_pipeline_failed",
            ("http.public_head_handler", 100, "RequestStart") => "http.public_head_handler_started",
            ("http.public_head_handler", 101, "RequestEnd") => "http.public_head_handler_completed",
            ("http.public_head_handler", 102, "RequestHeader") => "http.public_head_handler_request_headers",
            ("http.public_head_handler", 103, "ResponseHeader") => "http.public_head_handler_response_headers",
            ("http.public_head_handler", 104, "RequestFailed") => "http.public_head_handler_failed",
            ("http.api_health_pipeline", 100, "RequestPipelineStart") => "http.api_health_pipeline_started",
            ("http.api_health_pipeline", 101, "RequestPipelineEnd") => "http.api_health_pipeline_completed",
            ("http.api_health_pipeline", 102, "RequestPipelineRequestHeader") => "http.api_health_pipeline_request_headers",
            ("http.api_health_pipeline", 103, "RequestPipelineResponseHeader") => "http.api_health_pipeline_response_headers",
            ("http.api_health_pipeline", 104, "RequestPipelineFailed") => "http.api_health_pipeline_failed",
            ("http.api_health_handler", 100, "RequestStart") => "http.api_health_handler_started",
            ("http.api_health_handler", 101, "RequestEnd") => "http.api_health_handler_completed",
            ("http.api_health_handler", 102, "RequestHeader") => "http.api_health_handler_request_headers",
            ("http.api_health_handler", 103, "ResponseHeader") => "http.api_health_handler_response_headers",
            ("http.api_health_handler", 104, "RequestFailed") => "http.api_health_handler_failed",
            ("http.auth_health_pipeline", 100, "RequestPipelineStart") => "http.auth_health_pipeline_started",
            ("http.auth_health_pipeline", 101, "RequestPipelineEnd") => "http.auth_health_pipeline_completed",
            ("http.auth_health_pipeline", 102, "RequestPipelineRequestHeader") => "http.auth_health_pipeline_request_headers",
            ("http.auth_health_pipeline", 103, "RequestPipelineResponseHeader") => "http.auth_health_pipeline_response_headers",
            ("http.auth_health_pipeline", 104, "RequestPipelineFailed") => "http.auth_health_pipeline_failed",
            ("http.auth_health_handler", 100, "RequestStart") => "http.auth_health_handler_started",
            ("http.auth_health_handler", 101, "RequestEnd") => "http.auth_health_handler_completed",
            ("http.auth_health_handler", 102, "RequestHeader") => "http.auth_health_handler_request_headers",
            ("http.auth_health_handler", 103, "ResponseHeader") => "http.auth_health_handler_response_headers",
            ("http.auth_health_handler", 104, "RequestFailed") => "http.auth_health_handler_failed",
            ("http.console_health_pipeline", 100, "RequestPipelineStart") => "http.console_health_pipeline_started",
            ("http.console_health_pipeline", 101, "RequestPipelineEnd") => "http.console_health_pipeline_completed",
            ("http.console_health_pipeline", 102, "RequestPipelineRequestHeader") => "http.console_health_pipeline_request_headers",
            ("http.console_health_pipeline", 103, "RequestPipelineResponseHeader") => "http.console_health_pipeline_response_headers",
            ("http.console_health_pipeline", 104, "RequestPipelineFailed") => "http.console_health_pipeline_failed",
            ("http.console_health_handler", 100, "RequestStart") => "http.console_health_handler_started",
            ("http.console_health_handler", 101, "RequestEnd") => "http.console_health_handler_completed",
            ("http.console_health_handler", 102, "RequestHeader") => "http.console_health_handler_request_headers",
            ("http.console_health_handler", 103, "ResponseHeader") => "http.console_health_handler_response_headers",
            ("http.console_health_handler", 104, "RequestFailed") => "http.console_health_handler_failed",
            _ => owner + "_unclassified"
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
        var seenId = false;
        var seenName = false;
        foreach (var property in details.Properties)
        {
            if (property.Name == "Id")
            {
                if (seenId || property.Value is not ScalarValue { Value: int number }) return (null, null);
                seenId = true;
                id = number;
            }
            if (property.Name == "Name")
            {
                if (seenName || property.Value is not ScalarValue { Value: string text }) return (null, null);
                seenName = true;
                name = text;
            }
        }
        return (id, name);
    }
}
