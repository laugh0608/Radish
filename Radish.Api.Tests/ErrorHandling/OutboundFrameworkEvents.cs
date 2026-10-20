using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Radish.Api.Tests.ErrorHandling;

internal sealed record OutboundFrameworkEvent(string Source, string Code, int Id, string Name, LogLevel Level, string Field);

internal static class OutboundFrameworkEvents
{
    internal static readonly OutboundFrameworkEvent[] All =
    [
        new("Yarp.ReverseProxy.Forwarder.HttpForwarder", "proxy.forwarder_forwarding", 9, "Forwarding", LogLevel.Information, "_proxying"),
        new("Yarp.ReverseProxy.Forwarder.HttpForwarder", "proxy.forwarder_response_received", 56, "ResponseReceived", LogLevel.Information, "_responseReceived"),
        new("Yarp.ReverseProxy.Forwarder.HttpForwarder", "proxy.forwarder_failed", 48, "ForwardingError", LogLevel.Warning, "_proxyError"),
        new("Yarp.ReverseProxy.Forwarder.HttpForwarder", "proxy.forwarder_cancelled", 66, "ForwardingRequestCancelled", LogLevel.Debug, "_proxyRequestCancelled"),
        new("Yarp.ReverseProxy.Forwarder.HttpForwarder", "proxy.forwarder_skipped", 59, "NotForwarding", LogLevel.Information, "_notProxying"),
        new("Yarp.ReverseProxy.Forwarder.HttpForwarder", "proxy.forwarder_websocket_retry_no_connect", 61, "RetryingWebSocketDowngradeNoConnect", LogLevel.Information, "_retryingWebSocketDowngradeNoConnect"),
        new("Yarp.ReverseProxy.Forwarder.HttpForwarder", "proxy.forwarder_websocket_retry_no_http2", 62, "RetryingWebSocketDowngradeNoHttp2", LogLevel.Information, "_retryingWebSocketDowngradeNoHttp2"),
        new("Yarp.ReverseProxy.Forwarder.HttpForwarder", "proxy.forwarder_websocket_key_invalid", 63, "InvalidSecWebSocketKeyHeader", LogLevel.Warning, "_invalidKeyHeader"),
        new("Yarp.ReverseProxy.Forwarder.ForwarderHttpClientFactory", "proxy.client_created", 45, "ClientCreated", LogLevel.Debug, "_clientCreated"),
        new("Yarp.ReverseProxy.Forwarder.ForwarderHttpClientFactory", "proxy.client_reused", 46, "ClientReused", LogLevel.Debug, "_clientReused"),
        new("Yarp.ReverseProxy.Forwarder.ForwarderMiddleware", "proxy.middleware_no_destinations", 7, "NoAvailableDestinations", LogLevel.Warning, "_noAvailableDestinations"),
        new("Yarp.ReverseProxy.Forwarder.ForwarderMiddleware", "proxy.middleware_multiple_destinations", 8, "MultipleDestinationsAvailable", LogLevel.Warning, "_multipleDestinationsAvailable"),
        new("Yarp.ReverseProxy.LoadBalancing.LoadBalancingMiddleware", "proxy.balancing_no_destinations", 7, "NoAvailableDestinations", LogLevel.Warning, "_noAvailableDestinations"),
        new("Yarp.ReverseProxy.Model.ProxyPipelineInitializerMiddleware", "proxy.pipeline_cluster_missing", 4, "NoClusterFound", LogLevel.Information, "_noClusterFound"),
        new("Yarp.ReverseProxy.Model.ProxyPipelineInitializerMiddleware", "proxy.pipeline_timeout_not_applied", 64, "TimeoutNotApplied", LogLevel.Error, "_timeoutNotApplied"),
        new("Yarp.ReverseProxy.Limits.LimitsMiddleware", "proxy.limits_body_size_set", 60, "MaxRequestBodySizeSet", LogLevel.Debug, "_maxRequestBodySizeSet"),
        new("Microsoft.Extensions.Http.DefaultHttpClientFactory", "http.client_factory_cleanup_started", 100, "CleanupCycleStart", LogLevel.Debug, "_cleanupCycleStart"),
        new("Microsoft.Extensions.Http.DefaultHttpClientFactory", "http.client_factory_cleanup_completed", 101, "CleanupCycleEnd", LogLevel.Debug, "_cleanupCycleEnd"),
        new("Microsoft.Extensions.Http.DefaultHttpClientFactory", "http.client_factory_cleanup_failed", 102, "CleanupItemFailed", LogLevel.Error, "_cleanupItemFailed"),
        new("Microsoft.Extensions.Http.DefaultHttpClientFactory", "http.client_factory_handler_expired", 103, "HandlerExpired", LogLevel.Debug, "_handlerExpired"),
        new("System.Net.Http.HttpClient.PublicHeadSnapshotClient.LogicalHandler", "http.public_head_pipeline_started", 100, "RequestPipelineStart", LogLevel.Information, ""),
        new("System.Net.Http.HttpClient.PublicHeadSnapshotClient.LogicalHandler", "http.public_head_pipeline_completed", 101, "RequestPipelineEnd", LogLevel.Information, ""),
        new("System.Net.Http.HttpClient.PublicHeadSnapshotClient.LogicalHandler", "http.public_head_pipeline_request_headers", 102, "RequestPipelineRequestHeader", LogLevel.Trace, ""),
        new("System.Net.Http.HttpClient.PublicHeadSnapshotClient.LogicalHandler", "http.public_head_pipeline_response_headers", 103, "RequestPipelineResponseHeader", LogLevel.Trace, ""),
        new("System.Net.Http.HttpClient.PublicHeadSnapshotClient.LogicalHandler", "http.public_head_pipeline_failed", 104, "RequestPipelineFailed", LogLevel.Information, ""),
        new("System.Net.Http.HttpClient.PublicHeadSnapshotClient.ClientHandler", "http.public_head_handler_started", 100, "RequestStart", LogLevel.Information, ""),
        new("System.Net.Http.HttpClient.PublicHeadSnapshotClient.ClientHandler", "http.public_head_handler_completed", 101, "RequestEnd", LogLevel.Information, ""),
        new("System.Net.Http.HttpClient.PublicHeadSnapshotClient.ClientHandler", "http.public_head_handler_request_headers", 102, "RequestHeader", LogLevel.Trace, ""),
        new("System.Net.Http.HttpClient.PublicHeadSnapshotClient.ClientHandler", "http.public_head_handler_response_headers", 103, "ResponseHeader", LogLevel.Trace, ""),
        new("System.Net.Http.HttpClient.PublicHeadSnapshotClient.ClientHandler", "http.public_head_handler_failed", 104, "RequestFailed", LogLevel.Information, ""),
        new("System.Net.Http.HttpClient.api-service.LogicalHandler", "http.api_health_pipeline_started", 100, "RequestPipelineStart", LogLevel.Information, ""),
        new("System.Net.Http.HttpClient.api-service.LogicalHandler", "http.api_health_pipeline_completed", 101, "RequestPipelineEnd", LogLevel.Information, ""),
        new("System.Net.Http.HttpClient.api-service.LogicalHandler", "http.api_health_pipeline_request_headers", 102, "RequestPipelineRequestHeader", LogLevel.Trace, ""),
        new("System.Net.Http.HttpClient.api-service.LogicalHandler", "http.api_health_pipeline_response_headers", 103, "RequestPipelineResponseHeader", LogLevel.Trace, ""),
        new("System.Net.Http.HttpClient.api-service.LogicalHandler", "http.api_health_pipeline_failed", 104, "RequestPipelineFailed", LogLevel.Information, ""),
        new("System.Net.Http.HttpClient.api-service.ClientHandler", "http.api_health_handler_started", 100, "RequestStart", LogLevel.Information, ""),
        new("System.Net.Http.HttpClient.api-service.ClientHandler", "http.api_health_handler_completed", 101, "RequestEnd", LogLevel.Information, ""),
        new("System.Net.Http.HttpClient.api-service.ClientHandler", "http.api_health_handler_request_headers", 102, "RequestHeader", LogLevel.Trace, ""),
        new("System.Net.Http.HttpClient.api-service.ClientHandler", "http.api_health_handler_response_headers", 103, "ResponseHeader", LogLevel.Trace, ""),
        new("System.Net.Http.HttpClient.api-service.ClientHandler", "http.api_health_handler_failed", 104, "RequestFailed", LogLevel.Information, ""),
        new("System.Net.Http.HttpClient.auth-service.LogicalHandler", "http.auth_health_pipeline_started", 100, "RequestPipelineStart", LogLevel.Information, ""),
        new("System.Net.Http.HttpClient.auth-service.LogicalHandler", "http.auth_health_pipeline_completed", 101, "RequestPipelineEnd", LogLevel.Information, ""),
        new("System.Net.Http.HttpClient.auth-service.LogicalHandler", "http.auth_health_pipeline_request_headers", 102, "RequestPipelineRequestHeader", LogLevel.Trace, ""),
        new("System.Net.Http.HttpClient.auth-service.LogicalHandler", "http.auth_health_pipeline_response_headers", 103, "RequestPipelineResponseHeader", LogLevel.Trace, ""),
        new("System.Net.Http.HttpClient.auth-service.LogicalHandler", "http.auth_health_pipeline_failed", 104, "RequestPipelineFailed", LogLevel.Information, ""),
        new("System.Net.Http.HttpClient.auth-service.ClientHandler", "http.auth_health_handler_started", 100, "RequestStart", LogLevel.Information, ""),
        new("System.Net.Http.HttpClient.auth-service.ClientHandler", "http.auth_health_handler_completed", 101, "RequestEnd", LogLevel.Information, ""),
        new("System.Net.Http.HttpClient.auth-service.ClientHandler", "http.auth_health_handler_request_headers", 102, "RequestHeader", LogLevel.Trace, ""),
        new("System.Net.Http.HttpClient.auth-service.ClientHandler", "http.auth_health_handler_response_headers", 103, "ResponseHeader", LogLevel.Trace, ""),
        new("System.Net.Http.HttpClient.auth-service.ClientHandler", "http.auth_health_handler_failed", 104, "RequestFailed", LogLevel.Information, ""),
        new("System.Net.Http.HttpClient.console-service.LogicalHandler", "http.console_health_pipeline_started", 100, "RequestPipelineStart", LogLevel.Information, ""),
        new("System.Net.Http.HttpClient.console-service.LogicalHandler", "http.console_health_pipeline_completed", 101, "RequestPipelineEnd", LogLevel.Information, ""),
        new("System.Net.Http.HttpClient.console-service.LogicalHandler", "http.console_health_pipeline_request_headers", 102, "RequestPipelineRequestHeader", LogLevel.Trace, ""),
        new("System.Net.Http.HttpClient.console-service.LogicalHandler", "http.console_health_pipeline_response_headers", 103, "RequestPipelineResponseHeader", LogLevel.Trace, ""),
        new("System.Net.Http.HttpClient.console-service.LogicalHandler", "http.console_health_pipeline_failed", 104, "RequestPipelineFailed", LogLevel.Information, ""),
        new("System.Net.Http.HttpClient.console-service.ClientHandler", "http.console_health_handler_started", 100, "RequestStart", LogLevel.Information, ""),
        new("System.Net.Http.HttpClient.console-service.ClientHandler", "http.console_health_handler_completed", 101, "RequestEnd", LogLevel.Information, ""),
        new("System.Net.Http.HttpClient.console-service.ClientHandler", "http.console_health_handler_request_headers", 102, "RequestHeader", LogLevel.Trace, ""),
        new("System.Net.Http.HttpClient.console-service.ClientHandler", "http.console_health_handler_response_headers", 103, "ResponseHeader", LogLevel.Trace, ""),
        new("System.Net.Http.HttpClient.console-service.ClientHandler", "http.console_health_handler_failed", 104, "RequestFailed", LogLevel.Information, ""),
    ];
    internal static readonly (string Source, string UnknownCode)[] Sources =
    [
        ("Yarp.ReverseProxy.Forwarder.HttpForwarder", "proxy.forwarder_unclassified"),
        ("Yarp.ReverseProxy.Forwarder.ForwarderHttpClientFactory", "proxy.client_unclassified"),
        ("Yarp.ReverseProxy.Forwarder.ForwarderMiddleware", "proxy.middleware_unclassified"),
        ("Yarp.ReverseProxy.LoadBalancing.LoadBalancingMiddleware", "proxy.balancing_unclassified"),
        ("Yarp.ReverseProxy.Model.ProxyPipelineInitializerMiddleware", "proxy.pipeline_unclassified"),
        ("Yarp.ReverseProxy.Limits.LimitsMiddleware", "proxy.limits_unclassified"),
        ("Microsoft.Extensions.Http.DefaultHttpClientFactory", "http.client_factory_unclassified"),
        ("System.Net.Http.HttpClient.PublicHeadSnapshotClient.LogicalHandler", "http.public_head_pipeline_unclassified"),
        ("System.Net.Http.HttpClient.PublicHeadSnapshotClient.ClientHandler", "http.public_head_handler_unclassified"),
        ("System.Net.Http.HttpClient.api-service.LogicalHandler", "http.api_health_pipeline_unclassified"),
        ("System.Net.Http.HttpClient.api-service.ClientHandler", "http.api_health_handler_unclassified"),
        ("System.Net.Http.HttpClient.auth-service.LogicalHandler", "http.auth_health_pipeline_unclassified"),
        ("System.Net.Http.HttpClient.auth-service.ClientHandler", "http.auth_health_handler_unclassified"),
        ("System.Net.Http.HttpClient.console-service.LogicalHandler", "http.console_health_pipeline_unclassified"),
        ("System.Net.Http.HttpClient.console-service.ClientHandler", "http.console_health_handler_unclassified"),
    ];

    internal static Type LogType(OutboundFrameworkEvent row) => Assembly.Load(row.Source.StartsWith("Yarp.", StringComparison.Ordinal)
        ? "Yarp.ReverseProxy" : "Microsoft.Extensions.Http").GetType(row.Source + "+Log", true)!;

    internal static void Generate(OutboundFrameworkEvent row, ILogger logger)
    {
        if (row.Field.Length > 0)
        {
            var callback = (Delegate)LogType(row).GetField(row.Field, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            callback.DynamicInvoke(callback.Method.GetParameters().Select(p => Argument(p.ParameterType, logger)).ToArray());
            return;
        }
        // 执行安装版本的真实 LogHelper，包括 headers 的 ILogger.Log 与 URI scope。
        var helper = Assembly.Load("Microsoft.Extensions.Http").GetType("Microsoft.Extensions.Http.Logging.LogHelper", true)!;
        var pipeline = row.Source.EndsWith(".LogicalHandler", StringComparison.Ordinal);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.invalid/" + HttpBoundaryCapture.Secret);
        request.Headers.Add("X-Private", HttpBoundaryCapture.Secret);
        using var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        response.Headers.Add("X-Private", HttpBoundaryCapture.Secret);
        using var scope = (IDisposable?)helper.GetMethod("BeginRequestPipelineScope")!.Invoke(null, new object?[] { logger, request, null });
        var operation = row.Id switch { 100 or 102 => "Start", 101 or 103 => "End", _ => "Failed" };
        var method = helper.GetMethod("LogRequest" + (pipeline ? "Pipeline" : "") + operation)!;
        Func<string, bool> redact = _ => false; // 主动提供敏感 header，证明输出安全不依赖框架默认脱敏。
        object?[] args = operation switch
        {
            "Start" when pipeline => [logger, request, HttpBoundaryCapture.Secret, redact],
            "Start" => [logger, request, redact],
            "End" => [logger, response, TimeSpan.FromSeconds(1), redact],
            _ => [logger, TimeSpan.FromSeconds(1), new HttpRequestException(HttpBoundaryCapture.Secret)]
        };
        method.Invoke(null, args);
    }

    private static object? Argument(Type type, ILogger logger)
    {
        if (type == typeof(ILogger)) return logger;
        if (type == typeof(string)) return HttpBoundaryCapture.Secret;
        if (type == typeof(Exception)) return new System.IO.IOException(HttpBoundaryCapture.Secret);
        if (type == typeof(Version)) return new Version(2, 0);
        if (type == typeof(HttpMethod)) return HttpMethod.Get;
        if (type.IsValueType) return Activator.CreateInstance(type);
        throw new InvalidOperationException("Unexpected generator argument: " + type.FullName);
    }
}
