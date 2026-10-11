using System.Diagnostics;
using Radish.Common.LogTool;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Radish.Extension.Log;

/// <summary>在上下文合并后重建四个精确路由框架来源。</summary>
public sealed class RoutingLogSink(ILogEventSink next) : ILogEventSink
{
    private static readonly MessageTemplate Template = new MessageTemplateParser().Parse("Routing event {EventCode}.");
    private static readonly MessageTemplate FailureTemplate = new MessageTemplateParser().Parse("Routing event {EventCode}; kind={failureKind}.");

    public void Emit(LogEvent logEvent)
    {
        if (!logEvent.Properties.TryGetValue("SourceContext", out var source) || source is not ScalarValue { Value: string category })
        {
            next.Emit(logEvent);
            return;
        }

        var owner = category switch
        {
            "Microsoft.AspNetCore.Routing.EndpointRoutingMiddleware" => "routing.pipeline",
            "Microsoft.AspNetCore.Routing.EndpointMiddleware" => "routing.endpoint",
            "Microsoft.AspNetCore.Routing.Matching.DfaMatcher" => "routing.matcher",
            "Microsoft.AspNetCore.Routing.DefaultLinkGenerator" => "routing.link",
            _ => null
        };
        if (owner == null)
        {
            next.Emit(logEvent);
            return;
        }

        var (id, name) = Identity(logEvent, category);
        var code = (owner, id, name) switch
        {
            ("routing.pipeline", 1, "MatchSuccess") => "routing.pipeline_matched",
            ("routing.pipeline", 2, "MatchFailure") => "routing.pipeline_unmatched",
            ("routing.pipeline", 3, "MatchingSkipped") => "routing.pipeline_matching_skipped",
            ("routing.pipeline", 4, "ExecutingEndpoint") => "routing.pipeline_short_circuit_entered",
            ("routing.pipeline", 5, "ExecutedEndpoint") => "routing.pipeline_short_circuit_exited",
            ("routing.pipeline", 6, "ShortCircuitedEndpoint") => "routing.pipeline_short_circuit_without_delegate",
            ("routing.pipeline", 7, "FallbackMatch") => "routing.pipeline_fallback_matched",
            ("routing.pipeline", 8, "RequestSizeLimitMetadataNotFound") => "routing.pipeline_size_metadata_missing",
            ("routing.pipeline", 9, "RequestSizeFeatureNotFound") => "routing.pipeline_size_feature_missing",
            ("routing.pipeline", 10, "RequestSizeFeatureIsReadOnly") => "routing.pipeline_size_feature_read_only",
            ("routing.pipeline", 11, "MaxRequestBodySizeSet") => "routing.pipeline_body_size_set",
            ("routing.pipeline", 12, "MaxRequestBodySizeDisabled") => "routing.pipeline_body_size_disabled",
            ("routing.endpoint", 0, "ExecutingEndpoint") => "routing.endpoint_entered",
            ("routing.endpoint", 1, "ExecutedEndpoint") => "routing.endpoint_exited",
            ("routing.matcher", 1000, "CandidatesNotFound") => "routing.matcher_candidates_missing",
            ("routing.matcher", 1001, "CandidatesFound") => "routing.matcher_candidates_found",
            ("routing.matcher", 1002, "CandidateRejectedByComplexSegment") => "routing.matcher_candidate_complex_rejected",
            ("routing.matcher", 1003, "CandidateRejectedByConstraint") => "routing.matcher_candidate_constraint_rejected",
            ("routing.matcher", 1004, "CandidateNotValid") => "routing.matcher_candidate_invalid",
            ("routing.matcher", 1005, "CandidateValid") => "routing.matcher_candidate_valid",
            ("routing.link", 100, "EndpointsFound") => "routing.link_endpoints_found",
            ("routing.link", 101, "EndpointsNotFound") => "routing.link_endpoints_missing",
            ("routing.link", 102, "TemplateSucceeded") => "routing.link_template_succeeded",
            ("routing.link", 103, "TemplateFailedRequiredValues") => "routing.link_required_values_rejected",
            ("routing.link", 107, "TemplateFailedConstraint") => "routing.link_constraint_rejected",
            ("routing.link", 104, "TemplateFailedExpansion") => "routing.link_expansion_failed",
            ("routing.link", 105, "LinkGenerationSucceeded") => "routing.link_generated",
            ("routing.link", 106, "LinkGenerationFailed") => "routing.link_generation_failed",
            _ => owner + "_unknown"
        };
        var failureKind = logEvent.Exception == null ? null : RuntimeFailureSummary.Classify(logEvent.Exception);

        var properties = new List<LogEventProperty>
        {
            new("EventCode", new ScalarValue(code)),
            new("SourceCategory", new ScalarValue("http")),
            new("SourceContext", new ScalarValue(category)),
            new("Diagnostic", new ScalarValue(logEvent.Level < LogEventLevel.Warning))
        };
        if (failureKind != null) properties.Add(new LogEventProperty("failureKind", new ScalarValue(failureKind)));
        next.Emit(new LogEvent(logEvent.Timestamp, logEvent.Level, null,
            failureKind == null ? Template : FailureTemplate, properties,
            logEvent.TraceId ?? Activity.Current?.TraceId ?? default,
            logEvent.SpanId ?? Activity.Current?.SpanId ?? default));
    }

    private static (int?, string?) Identity(LogEvent logEvent, string category)
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
        // Serilog.Extensions.Logging 省略值为 0 的 Id；仅这一个已核对的身份允许缺省。
        // 显式 null、错误类型、重复字段仍由上面的严格解析拒绝。
        if (!seenId && category == "Microsoft.AspNetCore.Routing.EndpointMiddleware" && name == "ExecutingEndpoint")
            id = 0;
        return (id, name);
    }
}
