using System.Diagnostics;
using Radish.Common.LogTool;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Radish.Extension.Log;

/// <summary>在上下文合并后重建健康服务事件，避免 scope 或直接 Serilog 调用带回原始数据。</summary>
public sealed class HealthCheckLogSink(ILogEventSink next) : ILogEventSink
{
    private static readonly MessageTemplate Template = new MessageTemplateParser().Parse("Health check event {EventCode}.");
    private static readonly MessageTemplate FailureTemplate = new MessageTemplateParser().Parse("Health check event {EventCode}; kind={failureKind}.");

    public void Emit(LogEvent logEvent)
    {
        if (!logEvent.Properties.TryGetValue("SourceContext", out var source) ||
            source is not ScalarValue { Value: HealthCheckLogIdentity.Source })
        {
            next.Emit(logEvent);
            return;
        }

        var (id, name) = Identity(logEvent);
        var code = HealthCheckLogIdentity.Code(id, name);
        string? failureKind = null;
        if (logEvent.Exception != null) failureKind = RuntimeFailureSummary.Classify(logEvent.Exception);
        else if (logEvent.Properties.TryGetValue("failureKind", out var kind) &&
                 kind is ScalarValue { Value: "aggregate" or "database" or "cancelled" or "timeout" or "io" or
                     "access-denied" or "argument" or "invalid-operation" or "other" })
            failureKind = (string)((ScalarValue)kind).Value!;

        var properties = new List<LogEventProperty>
        {
            new("EventCode", new ScalarValue(code)),
            new("SourceCategory", new ScalarValue("infrastructure")),
            new("SourceContext", new ScalarValue(HealthCheckLogIdentity.Source)),
            new("Diagnostic", new ScalarValue(logEvent.Level < LogEventLevel.Warning))
        };
        if (failureKind != null) properties.Add(new("failureKind", new ScalarValue(failureKind)));
        next.Emit(new LogEvent(logEvent.Timestamp, logEvent.Level, null,
            failureKind == null ? Template : FailureTemplate, properties,
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

internal static class HealthCheckLogIdentity
{
    internal const string Source = "Microsoft.Extensions.Diagnostics.HealthChecks.DefaultHealthCheckService";

    internal static string Code(int? id, string? name) => (id, name) switch
    {
        (100, "HealthCheckProcessingBegin") => "health.run_started",
        (101, "HealthCheckProcessingEnd") => "health.run_completed",
        (102, "HealthCheckBegin") => "health.check_started",
        (103, "HealthCheckEnd") => "health.check_completed",
        (104, "HealthCheckError") => "health.check_failed",
        (105, "HealthCheckData") => "health.check_data",
        _ => "health.unknown"
    };
}
