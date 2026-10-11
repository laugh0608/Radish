using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Radish.Api.Tests.ErrorHandling;

internal sealed record RoutingFrameworkEvent(string Source, int Id, string Name, LogLevel Level, string Code)
{
    public MethodInfo Generator => Assembly.Load("Microsoft.AspNetCore.Routing").GetType(Source, true)!
        .GetNestedType("Log", BindingFlags.NonPublic)!.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        .Single(m => m.GetCustomAttribute<LoggerMessageAttribute>() is { } a && a.EventId == Id);
}

internal static class RoutingFrameworkEvents
{
    internal static readonly RoutingFrameworkEvent[] All =
    [
        new("Microsoft.AspNetCore.Routing.EndpointRoutingMiddleware", 1, "MatchSuccess", LogLevel.Debug, "routing.pipeline_matched"),
        new("Microsoft.AspNetCore.Routing.EndpointRoutingMiddleware", 2, "MatchFailure", LogLevel.Debug, "routing.pipeline_unmatched"),
        new("Microsoft.AspNetCore.Routing.EndpointRoutingMiddleware", 3, "MatchingSkipped", LogLevel.Debug, "routing.pipeline_matching_skipped"),
        new("Microsoft.AspNetCore.Routing.EndpointRoutingMiddleware", 4, "ExecutingEndpoint", LogLevel.Information, "routing.pipeline_short_circuit_entered"),
        new("Microsoft.AspNetCore.Routing.EndpointRoutingMiddleware", 5, "ExecutedEndpoint", LogLevel.Information, "routing.pipeline_short_circuit_exited"),
        new("Microsoft.AspNetCore.Routing.EndpointRoutingMiddleware", 6, "ShortCircuitedEndpoint", LogLevel.Information, "routing.pipeline_short_circuit_without_delegate"),
        new("Microsoft.AspNetCore.Routing.EndpointRoutingMiddleware", 7, "FallbackMatch", LogLevel.Debug, "routing.pipeline_fallback_matched"),
        new("Microsoft.AspNetCore.Routing.EndpointRoutingMiddleware", 8, "RequestSizeLimitMetadataNotFound", LogLevel.Trace, "routing.pipeline_size_metadata_missing"),
        new("Microsoft.AspNetCore.Routing.EndpointRoutingMiddleware", 9, "RequestSizeFeatureNotFound", LogLevel.Warning, "routing.pipeline_size_feature_missing"),
        new("Microsoft.AspNetCore.Routing.EndpointRoutingMiddleware", 10, "RequestSizeFeatureIsReadOnly", LogLevel.Warning, "routing.pipeline_size_feature_read_only"),
        new("Microsoft.AspNetCore.Routing.EndpointRoutingMiddleware", 11, "MaxRequestBodySizeSet", LogLevel.Debug, "routing.pipeline_body_size_set"),
        new("Microsoft.AspNetCore.Routing.EndpointRoutingMiddleware", 12, "MaxRequestBodySizeDisabled", LogLevel.Debug, "routing.pipeline_body_size_disabled"),
        new("Microsoft.AspNetCore.Routing.EndpointMiddleware", 0, "ExecutingEndpoint", LogLevel.Information, "routing.endpoint_entered"),
        new("Microsoft.AspNetCore.Routing.EndpointMiddleware", 1, "ExecutedEndpoint", LogLevel.Information, "routing.endpoint_exited"),
        new("Microsoft.AspNetCore.Routing.Matching.DfaMatcher", 1000, "CandidatesNotFound", LogLevel.Debug, "routing.matcher_candidates_missing"),
        new("Microsoft.AspNetCore.Routing.Matching.DfaMatcher", 1001, "CandidatesFound", LogLevel.Debug, "routing.matcher_candidates_found"),
        new("Microsoft.AspNetCore.Routing.Matching.DfaMatcher", 1002, "CandidateRejectedByComplexSegment", LogLevel.Debug, "routing.matcher_candidate_complex_rejected"),
        new("Microsoft.AspNetCore.Routing.Matching.DfaMatcher", 1003, "CandidateRejectedByConstraint", LogLevel.Debug, "routing.matcher_candidate_constraint_rejected"),
        new("Microsoft.AspNetCore.Routing.Matching.DfaMatcher", 1004, "CandidateNotValid", LogLevel.Debug, "routing.matcher_candidate_invalid"),
        new("Microsoft.AspNetCore.Routing.Matching.DfaMatcher", 1005, "CandidateValid", LogLevel.Debug, "routing.matcher_candidate_valid"),
        new("Microsoft.AspNetCore.Routing.DefaultLinkGenerator", 100, "EndpointsFound", LogLevel.Debug, "routing.link_endpoints_found"),
        new("Microsoft.AspNetCore.Routing.DefaultLinkGenerator", 101, "EndpointsNotFound", LogLevel.Debug, "routing.link_endpoints_missing"),
        new("Microsoft.AspNetCore.Routing.DefaultLinkGenerator", 102, "TemplateSucceeded", LogLevel.Debug, "routing.link_template_succeeded"),
        new("Microsoft.AspNetCore.Routing.DefaultLinkGenerator", 103, "TemplateFailedRequiredValues", LogLevel.Debug, "routing.link_required_values_rejected"),
        new("Microsoft.AspNetCore.Routing.DefaultLinkGenerator", 107, "TemplateFailedConstraint", LogLevel.Debug, "routing.link_constraint_rejected"),
        new("Microsoft.AspNetCore.Routing.DefaultLinkGenerator", 104, "TemplateFailedExpansion", LogLevel.Debug, "routing.link_expansion_failed"),
        new("Microsoft.AspNetCore.Routing.DefaultLinkGenerator", 105, "LinkGenerationSucceeded", LogLevel.Debug, "routing.link_generated"),
        new("Microsoft.AspNetCore.Routing.DefaultLinkGenerator", 106, "LinkGenerationFailed", LogLevel.Debug, "routing.link_generation_failed"),
    ];
    internal static readonly (string Source, string UnknownCode)[] Sources =
    [
        ("Microsoft.AspNetCore.Routing.EndpointRoutingMiddleware", "routing.pipeline_unknown"),
        ("Microsoft.AspNetCore.Routing.EndpointMiddleware", "routing.endpoint_unknown"),
        ("Microsoft.AspNetCore.Routing.Matching.DfaMatcher", "routing.matcher_unknown"),
        ("Microsoft.AspNetCore.Routing.DefaultLinkGenerator", "routing.link_unknown"),
    ];

    internal static void Generate(RoutingFrameworkEvent row, ILogger logger) =>
        row.Generator.Invoke(null, row.Generator.GetParameters().Select(p => p.ParameterType == typeof(ILogger) ? (object)logger :
            p.ParameterType == typeof(string) || p.ParameterType == typeof(object) ? HttpBoundaryCapture.Secret :
            p.ParameterType == typeof(Endpoint) ? new Endpoint(_ => Task.CompletedTask, EndpointMetadataCollection.Empty, HttpBoundaryCapture.Secret) :
            p.ParameterType == typeof(IEnumerable<string>) ? new[] { HttpBoundaryCapture.Secret } :
            p.ParameterType == typeof(IRouteConstraint) ? new NamedConstraint() :
            p.ParameterType == typeof(int) ? (object)2 :
            p.ParameterType == typeof(long) ? 100L :
            throw new InvalidOperationException("Unexpected routing generator argument: " + p.ParameterType.FullName)).ToArray());

    private sealed class NamedConstraint : IRouteConstraint
    {
        public bool Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection) => false;
        public override string ToString() => HttpBoundaryCapture.Secret;
    }
}
