using System.Diagnostics;
using Radish.Common.LogTool;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Radish.Extension.Log;

/// <summary>在上下文合并后重建已确认的 YARP 与出站 HTTP 日志，不改变配置加载 / 重载、请求、取消或异常传播。</summary>
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
            "Yarp.ReverseProxy.SessionAffinity.SessionAffinityMiddleware" => "proxy.affinity",
            "Yarp.ReverseProxy.SessionAffinity.CookieSessionAffinityPolicy" => "proxy.affinity_cookie",
            "Yarp.ReverseProxy.SessionAffinity.CustomHeaderSessionAffinityPolicy" => "proxy.affinity_header",
            "Yarp.ReverseProxy.SessionAffinity.HashCookieSessionAffinityPolicy" => "proxy.affinity_hash",
            "Yarp.ReverseProxy.SessionAffinity.ArrCookieSessionAffinityPolicy" => "proxy.affinity_arr",
            "Yarp.ReverseProxy.Health.ActiveHealthCheckMonitor" => "proxy.health_probe",
            "Yarp.ReverseProxy.Health.DestinationHealthUpdater" => "proxy.health_state",
            "Yarp.ReverseProxy.Configuration.ConfigProvider.ConfigurationConfigProvider" => "proxy.config_source",
            "Yarp.ReverseProxy.Management.ProxyConfigManager" => "proxy.config_manager",
            "Yarp.ReverseProxy.Configuration.ConfigValidator" => "proxy.config_validator",
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
            // 亲和基类借用具体策略的 logger；Hash / ARR 不产生解密事件。框架 EventName 原拼写保持。
            ("proxy.affinity", 34, "AffinityResolutionFailedForCluster") => "proxy.affinity_rejected",
            ("proxy.affinity", 41, "AffinityResolutionFailureWasHandledProcessingWillBeContinued") => "proxy.affinity_failure_handled",
            ("proxy.affinity_cookie", 36, "AffinityCannotBeEstablishedBecauseNoDestinationsFoundOnCluster") => "proxy.affinity_cookie_no_destinations",
            ("proxy.affinity_cookie", 38, "RequestAffinityKeyDecryptionFailed") => "proxy.affinity_cookie_decrypt_failed",
            ("proxy.affinity_cookie", 39, "DestinationMatchingToAffinityKeyNotFound") => "proxy.affinity_cookie_destination_missing",
            ("proxy.affinity_header", 36, "AffinityCannotBeEstablishedBecauseNoDestinationsFoundOnCluster") => "proxy.affinity_header_no_destinations",
            ("proxy.affinity_header", 38, "RequestAffinityKeyDecryptionFailed") => "proxy.affinity_header_decrypt_failed",
            ("proxy.affinity_header", 39, "DestinationMatchingToAffinityKeyNotFound") => "proxy.affinity_header_destination_missing",
            ("proxy.affinity_header", 40, "RequestAffinityHeaderHasMultipleValues") => "proxy.affinity_header_multiple_values",
            ("proxy.affinity_hash", 36, "AffinityCannotBeEstablishedBecauseNoDestinationsFoundOnCluster") => "proxy.affinity_hash_no_destinations",
            ("proxy.affinity_hash", 39, "DestinationMatchingToAffinityKeyNotFound") => "proxy.affinity_hash_destination_missing",
            ("proxy.affinity_arr", 36, "AffinityCannotBeEstablishedBecauseNoDestinationsFoundOnCluster") => "proxy.affinity_arr_no_destinations",
            ("proxy.affinity_arr", 39, "DestinationMatchingToAffinityKeyNotFound") => "proxy.affinity_arr_destination_missing",
            ("proxy.health_probe", 10, "ExplicitActiveCheckOfAllClustersHealthFailed") => "proxy.health_probe_all_failed",
            ("proxy.health_probe", 11, "ActiveHealthProbingFailedOnCluster") => "proxy.health_probe_cluster_failed",
            ("proxy.health_probe", 12, "ErrorOccuredDuringActiveHealthProbingShutdownOnCluster") => "proxy.health_probe_cleanup_failed",
            ("proxy.health_probe", 13, "ActiveHealthProbeConstructionFailedOnCluster") => "proxy.health_probe_construction_failed",
            ("proxy.health_probe", 14, "StartingActiveHealthProbingOnCluster") => "proxy.health_probe_started",
            ("proxy.health_probe", 15, "StoppedActiveHealthProbingOnCluster") => "proxy.health_probe_stopped",
            ("proxy.health_probe", 16, "DestinationActiveProbingCompleted") => "proxy.health_probe_completed",
            ("proxy.health_probe", 17, "DestinationActiveProbingFailed") => "proxy.health_probe_failed",
            ("proxy.health_probe", 18, "SendingHealthProbeToEndpointOfDestination") => "proxy.health_probe_sending",
            ("proxy.health_state", 19, "UnhealthyDestinationIsScheduledForReactivation") => "proxy.health_state_reactivation_scheduled",
            ("proxy.health_state", 20, "PassiveDestinationHealthResetToUnkownState") => "proxy.health_state_passive_reset",
            ("proxy.health_state", 49, "ActiveDestinationHealthStateIsSetToUnhealthy") => "proxy.health_state_active_unhealthy",
            ("proxy.health_state", 50, "ActiveDestinationHealthStateIsSet") => "proxy.health_state_active_changed",
            // ConfigValidator 是实际 ILogger 类别；事件生成类型 ProxyHttpRequestValidator 不作为来源匹配。
            // 配置通知失败不等于应用事务回滚；EndpointAdded/Changed/Removed 保留框架实际 Name。
            ("proxy.config_source", 1, "ApplyProxyConfig") => "proxy.config_source_loading",
            ("proxy.config_source", 2, "ApplyProxyConfigFailed") => "proxy.config_source_notification_failed",
            ("proxy.config_source", 47, "ConfigurationDataConversionFailed") => "proxy.config_source_conversion_failed",
            ("proxy.config_manager", 21, "ClusterAdded") => "proxy.config_manager_cluster_added",
            ("proxy.config_manager", 22, "ClusterChanged") => "proxy.config_manager_cluster_changed",
            ("proxy.config_manager", 23, "ClusterRemoved") => "proxy.config_manager_cluster_removed",
            ("proxy.config_manager", 24, "EndpointAdded") => "proxy.config_manager_destination_added",
            ("proxy.config_manager", 25, "EndpointChanged") => "proxy.config_manager_destination_changed",
            ("proxy.config_manager", 26, "EndpointRemoved") => "proxy.config_manager_destination_removed",
            ("proxy.config_manager", 27, "RouteAdded") => "proxy.config_manager_route_added",
            ("proxy.config_manager", 28, "RouteChanged") => "proxy.config_manager_route_changed",
            ("proxy.config_manager", 29, "RouteRemoved") => "proxy.config_manager_route_removed",
            ("proxy.config_manager", 43, "ErrorReloadingConfig") => "proxy.config_manager_reload_failed",
            ("proxy.config_manager", 44, "ErrorApplyingConfig") => "proxy.config_manager_apply_failed",
            ("proxy.config_validator", 58, "Http10RequestVersionDetected") => "proxy.config_validator_http10_detected",
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
