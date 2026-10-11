using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Radish.Api.Tests.ErrorHandling;

internal sealed record YarpConditionalEvent(string Source, string Code, int Id, string Name, LogLevel Level, string Field, string GeneratorType);

internal static class YarpConditionalFrameworkEvents
{
    internal static readonly YarpConditionalEvent[] All =
    [
        new("Yarp.ReverseProxy.SessionAffinity.SessionAffinityMiddleware", "proxy.affinity_rejected", 34, "AffinityResolutionFailedForCluster", LogLevel.Warning, "_affinityResolutionFailedForCluster", "Yarp.ReverseProxy.SessionAffinity.SessionAffinityMiddleware+Log"),
        new("Yarp.ReverseProxy.SessionAffinity.SessionAffinityMiddleware", "proxy.affinity_failure_handled", 41, "AffinityResolutionFailureWasHandledProcessingWillBeContinued", LogLevel.Debug, "_affinityResolutionFailureWasHandledProcessingWillBeContinued", "Yarp.ReverseProxy.SessionAffinity.SessionAffinityMiddleware+Log"),
        new("Yarp.ReverseProxy.SessionAffinity.CookieSessionAffinityPolicy", "proxy.affinity_cookie_no_destinations", 36, "AffinityCannotBeEstablishedBecauseNoDestinationsFoundOnCluster", LogLevel.Warning, "_affinityCannotBeEstablishedBecauseNoDestinationsFound", "Yarp.ReverseProxy.SessionAffinity.Log"),
        new("Yarp.ReverseProxy.SessionAffinity.CookieSessionAffinityPolicy", "proxy.affinity_cookie_decrypt_failed", 38, "RequestAffinityKeyDecryptionFailed", LogLevel.Error, "_requestAffinityKeyDecryptionFailed", "Yarp.ReverseProxy.SessionAffinity.Log"),
        new("Yarp.ReverseProxy.SessionAffinity.CookieSessionAffinityPolicy", "proxy.affinity_cookie_destination_missing", 39, "DestinationMatchingToAffinityKeyNotFound", LogLevel.Warning, "_destinationMatchingToAffinityKeyNotFound", "Yarp.ReverseProxy.SessionAffinity.Log"),
        new("Yarp.ReverseProxy.SessionAffinity.CustomHeaderSessionAffinityPolicy", "proxy.affinity_header_no_destinations", 36, "AffinityCannotBeEstablishedBecauseNoDestinationsFoundOnCluster", LogLevel.Warning, "_affinityCannotBeEstablishedBecauseNoDestinationsFound", "Yarp.ReverseProxy.SessionAffinity.Log"),
        new("Yarp.ReverseProxy.SessionAffinity.CustomHeaderSessionAffinityPolicy", "proxy.affinity_header_decrypt_failed", 38, "RequestAffinityKeyDecryptionFailed", LogLevel.Error, "_requestAffinityKeyDecryptionFailed", "Yarp.ReverseProxy.SessionAffinity.Log"),
        new("Yarp.ReverseProxy.SessionAffinity.CustomHeaderSessionAffinityPolicy", "proxy.affinity_header_destination_missing", 39, "DestinationMatchingToAffinityKeyNotFound", LogLevel.Warning, "_destinationMatchingToAffinityKeyNotFound", "Yarp.ReverseProxy.SessionAffinity.Log"),
        new("Yarp.ReverseProxy.SessionAffinity.CustomHeaderSessionAffinityPolicy", "proxy.affinity_header_multiple_values", 40, "RequestAffinityHeaderHasMultipleValues", LogLevel.Error, "_requestAffinityHeaderHasMultipleValues", "Yarp.ReverseProxy.SessionAffinity.CustomHeaderSessionAffinityPolicy+Log"),
        new("Yarp.ReverseProxy.SessionAffinity.HashCookieSessionAffinityPolicy", "proxy.affinity_hash_no_destinations", 36, "AffinityCannotBeEstablishedBecauseNoDestinationsFoundOnCluster", LogLevel.Warning, "_affinityCannotBeEstablishedBecauseNoDestinationsFound", "Yarp.ReverseProxy.SessionAffinity.Log"),
        new("Yarp.ReverseProxy.SessionAffinity.HashCookieSessionAffinityPolicy", "proxy.affinity_hash_destination_missing", 39, "DestinationMatchingToAffinityKeyNotFound", LogLevel.Warning, "_destinationMatchingToAffinityKeyNotFound", "Yarp.ReverseProxy.SessionAffinity.Log"),
        new("Yarp.ReverseProxy.SessionAffinity.ArrCookieSessionAffinityPolicy", "proxy.affinity_arr_no_destinations", 36, "AffinityCannotBeEstablishedBecauseNoDestinationsFoundOnCluster", LogLevel.Warning, "_affinityCannotBeEstablishedBecauseNoDestinationsFound", "Yarp.ReverseProxy.SessionAffinity.Log"),
        new("Yarp.ReverseProxy.SessionAffinity.ArrCookieSessionAffinityPolicy", "proxy.affinity_arr_destination_missing", 39, "DestinationMatchingToAffinityKeyNotFound", LogLevel.Warning, "_destinationMatchingToAffinityKeyNotFound", "Yarp.ReverseProxy.SessionAffinity.Log"),
        new("Yarp.ReverseProxy.Health.ActiveHealthCheckMonitor", "proxy.health_probe_all_failed", 10, "ExplicitActiveCheckOfAllClustersHealthFailed", LogLevel.Error, "_explicitActiveCheckOfAllClustersHealthFailed", "Yarp.ReverseProxy.Health.ActiveHealthCheckMonitor+Log"),
        new("Yarp.ReverseProxy.Health.ActiveHealthCheckMonitor", "proxy.health_probe_cluster_failed", 11, "ActiveHealthProbingFailedOnCluster", LogLevel.Error, "_activeHealthProbingFailedOnCluster", "Yarp.ReverseProxy.Health.ActiveHealthCheckMonitor+Log"),
        new("Yarp.ReverseProxy.Health.ActiveHealthCheckMonitor", "proxy.health_probe_cleanup_failed", 12, "ErrorOccuredDuringActiveHealthProbingShutdownOnCluster", LogLevel.Error, "_errorOccuredDuringActiveHealthProbingShutdownOnCluster", "Yarp.ReverseProxy.Health.ActiveHealthCheckMonitor+Log"),
        new("Yarp.ReverseProxy.Health.ActiveHealthCheckMonitor", "proxy.health_probe_construction_failed", 13, "ActiveHealthProbeConstructionFailedOnCluster", LogLevel.Error, "_activeHealthProbeConstructionFailedOnCluster", "Yarp.ReverseProxy.Health.ActiveHealthCheckMonitor+Log"),
        new("Yarp.ReverseProxy.Health.ActiveHealthCheckMonitor", "proxy.health_probe_started", 14, "StartingActiveHealthProbingOnCluster", LogLevel.Debug, "_startingActiveHealthProbingOnCluster", "Yarp.ReverseProxy.Health.ActiveHealthCheckMonitor+Log"),
        new("Yarp.ReverseProxy.Health.ActiveHealthCheckMonitor", "proxy.health_probe_stopped", 15, "StoppedActiveHealthProbingOnCluster", LogLevel.Debug, "_stoppedActiveHealthProbingOnCluster", "Yarp.ReverseProxy.Health.ActiveHealthCheckMonitor+Log"),
        new("Yarp.ReverseProxy.Health.ActiveHealthCheckMonitor", "proxy.health_probe_completed", 16, "DestinationActiveProbingCompleted", LogLevel.Information, "_destinationProbingCompleted", "Yarp.ReverseProxy.Health.ActiveHealthCheckMonitor+Log"),
        new("Yarp.ReverseProxy.Health.ActiveHealthCheckMonitor", "proxy.health_probe_failed", 17, "DestinationActiveProbingFailed", LogLevel.Warning, "_destinationProbingFailed", "Yarp.ReverseProxy.Health.ActiveHealthCheckMonitor+Log"),
        new("Yarp.ReverseProxy.Health.ActiveHealthCheckMonitor", "proxy.health_probe_sending", 18, "SendingHealthProbeToEndpointOfDestination", LogLevel.Debug, "_sendingHealthProbeToEndpointOfDestination", "Yarp.ReverseProxy.Health.ActiveHealthCheckMonitor+Log"),
        new("Yarp.ReverseProxy.Health.DestinationHealthUpdater", "proxy.health_state_reactivation_scheduled", 19, "UnhealthyDestinationIsScheduledForReactivation", LogLevel.Warning, "_unhealthyDestinationIsScheduledForReactivation", "Yarp.ReverseProxy.Health.DestinationHealthUpdater+Log"),
        new("Yarp.ReverseProxy.Health.DestinationHealthUpdater", "proxy.health_state_passive_reset", 20, "PassiveDestinationHealthResetToUnkownState", LogLevel.Information, "_passiveDestinationHealthResetToUnkownState", "Yarp.ReverseProxy.Health.DestinationHealthUpdater+Log"),
        new("Yarp.ReverseProxy.Health.DestinationHealthUpdater", "proxy.health_state_active_unhealthy", 49, "ActiveDestinationHealthStateIsSetToUnhealthy", LogLevel.Warning, "_activeDestinationHealthStateIsSetToUnhealthy", "Yarp.ReverseProxy.Health.DestinationHealthUpdater+Log"),
        new("Yarp.ReverseProxy.Health.DestinationHealthUpdater", "proxy.health_state_active_changed", 50, "ActiveDestinationHealthStateIsSet", LogLevel.Information, "_activeDestinationHealthStateIsSet", "Yarp.ReverseProxy.Health.DestinationHealthUpdater+Log"),
    ];
    internal static readonly (string Source, string UnknownCode)[] Sources =
    [
        ("Yarp.ReverseProxy.SessionAffinity.SessionAffinityMiddleware", "proxy.affinity_unclassified"),
        ("Yarp.ReverseProxy.SessionAffinity.CookieSessionAffinityPolicy", "proxy.affinity_cookie_unclassified"),
        ("Yarp.ReverseProxy.SessionAffinity.CustomHeaderSessionAffinityPolicy", "proxy.affinity_header_unclassified"),
        ("Yarp.ReverseProxy.SessionAffinity.HashCookieSessionAffinityPolicy", "proxy.affinity_hash_unclassified"),
        ("Yarp.ReverseProxy.SessionAffinity.ArrCookieSessionAffinityPolicy", "proxy.affinity_arr_unclassified"),
        ("Yarp.ReverseProxy.Health.ActiveHealthCheckMonitor", "proxy.health_probe_unclassified"),
        ("Yarp.ReverseProxy.Health.DestinationHealthUpdater", "proxy.health_state_unclassified"),
    ];
    internal static Type OwnerType(YarpConditionalEvent row) => Assembly.Load("Yarp.ReverseProxy").GetType(row.Source, true)!;
    internal static Type LogType(YarpConditionalEvent row) => Assembly.Load("Yarp.ReverseProxy").GetType(row.GeneratorType, true)!;
    internal static void Generate(YarpConditionalEvent row, ILogger logger)
    {
        var callback = (Delegate)LogType(row).GetField(row.Field, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        callback.DynamicInvoke(callback.Method.GetParameters().Select(p => p.ParameterType == typeof(ILogger) ? (object)logger :
            p.ParameterType == typeof(string) ? HttpBoundaryCapture.Secret :
            p.ParameterType == typeof(Exception) ? new System.IO.IOException(HttpBoundaryCapture.Secret) :
            p.ParameterType == typeof(Uri) ? new Uri("https://example.invalid/" + HttpBoundaryCapture.Secret) :
            p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) :
            throw new InvalidOperationException("Unexpected conditional generator argument: " + p.ParameterType.FullName)).ToArray());
    }
}
