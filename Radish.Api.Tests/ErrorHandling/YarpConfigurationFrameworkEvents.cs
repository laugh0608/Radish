using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Radish.Api.Tests.ErrorHandling;

internal static class YarpConfigurationFrameworkEvents
{
    internal static readonly OutboundFrameworkEvent[] All =
    [
        new("Yarp.ReverseProxy.Configuration.ConfigProvider.ConfigurationConfigProvider", "proxy.config_source_loading", 1, "ApplyProxyConfig", LogLevel.Information, "_loadData"),
        new("Yarp.ReverseProxy.Configuration.ConfigProvider.ConfigurationConfigProvider", "proxy.config_source_notification_failed", 2, "ApplyProxyConfigFailed", LogLevel.Error, "_errorSignalingChange"),
        new("Yarp.ReverseProxy.Configuration.ConfigProvider.ConfigurationConfigProvider", "proxy.config_source_conversion_failed", 47, "ConfigurationDataConversionFailed", LogLevel.Error, "_configurationDataConversionFailed"),
        new("Yarp.ReverseProxy.Management.ProxyConfigManager", "proxy.config_manager_cluster_added", 21, "ClusterAdded", LogLevel.Debug, "_clusterAdded"),
        new("Yarp.ReverseProxy.Management.ProxyConfigManager", "proxy.config_manager_cluster_changed", 22, "ClusterChanged", LogLevel.Debug, "_clusterChanged"),
        new("Yarp.ReverseProxy.Management.ProxyConfigManager", "proxy.config_manager_cluster_removed", 23, "ClusterRemoved", LogLevel.Debug, "_clusterRemoved"),
        new("Yarp.ReverseProxy.Management.ProxyConfigManager", "proxy.config_manager_destination_added", 24, "EndpointAdded", LogLevel.Debug, "_destinationAdded"),
        new("Yarp.ReverseProxy.Management.ProxyConfigManager", "proxy.config_manager_destination_changed", 25, "EndpointChanged", LogLevel.Debug, "_destinationChanged"),
        new("Yarp.ReverseProxy.Management.ProxyConfigManager", "proxy.config_manager_destination_removed", 26, "EndpointRemoved", LogLevel.Debug, "_destinationRemoved"),
        new("Yarp.ReverseProxy.Management.ProxyConfigManager", "proxy.config_manager_route_added", 27, "RouteAdded", LogLevel.Debug, "_routeAdded"),
        new("Yarp.ReverseProxy.Management.ProxyConfigManager", "proxy.config_manager_route_changed", 28, "RouteChanged", LogLevel.Debug, "_routeChanged"),
        new("Yarp.ReverseProxy.Management.ProxyConfigManager", "proxy.config_manager_route_removed", 29, "RouteRemoved", LogLevel.Debug, "_routeRemoved"),
        new("Yarp.ReverseProxy.Management.ProxyConfigManager", "proxy.config_manager_reload_failed", 43, "ErrorReloadingConfig", LogLevel.Error, "_errorReloadingConfig"),
        new("Yarp.ReverseProxy.Management.ProxyConfigManager", "proxy.config_manager_apply_failed", 44, "ErrorApplyingConfig", LogLevel.Error, "_errorApplyingConfig"),
        new("Yarp.ReverseProxy.Configuration.ConfigValidator", "proxy.config_validator_http10_detected", 58, "Http10RequestVersionDetected", LogLevel.Warning, "_http10RequestVersionDetected"),
    ];
    internal static readonly (string Source, string UnknownCode)[] Sources =
    [
        ("Yarp.ReverseProxy.Configuration.ConfigProvider.ConfigurationConfigProvider", "proxy.config_source_unclassified"),
        ("Yarp.ReverseProxy.Management.ProxyConfigManager", "proxy.config_manager_unclassified"),
        ("Yarp.ReverseProxy.Configuration.ConfigValidator", "proxy.config_validator_unclassified"),
    ];
    internal static Type OwnerType(OutboundFrameworkEvent row) => Assembly.Load("Yarp.ReverseProxy").GetType(
        row.Source == "Yarp.ReverseProxy.Configuration.ConfigValidator"
            ? "Yarp.ReverseProxy.Configuration.ClusterValidators.ProxyHttpRequestValidator" : row.Source, true)!;
    internal static Type LogType(OutboundFrameworkEvent row) => OwnerType(row).GetNestedType("Log", BindingFlags.NonPublic)!;
    internal static void Generate(OutboundFrameworkEvent row, ILogger logger)
    {
        var callback = (Delegate)LogType(row).GetField(row.Field, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        callback.DynamicInvoke(callback.Method.GetParameters().Select(p => p.ParameterType == typeof(ILogger) ? (object)logger :
            p.ParameterType == typeof(string) ? HttpBoundaryCapture.Secret :
            p.ParameterType == typeof(Exception) ? new System.IO.IOException(HttpBoundaryCapture.Secret) :
            throw new InvalidOperationException("Unexpected configuration generator argument: " + p.ParameterType.FullName)).ToArray());
    }
}
