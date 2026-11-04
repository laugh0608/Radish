using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Radish.Extension.Log;

/// <summary>重建两个自有 Hangfire 来源，拒绝 scope 与其他事件的字段混入最终输出。</summary>
public sealed class HangfireLogSink(ILogEventSink next) : ILogEventSink
{
    public const string ProviderSource = "Radish.Api.Services.HangfireRuntimeLogProvider";
    public const string StateSource = "Radish.Api.Services.HangfireRuntimeStateFilter";

    private static readonly string[] FailureKinds = ["aggregate", "database", "cancelled", "timeout", "io",
        "access-denied", "argument", "invalid-operation", "other"];
    private static readonly Dictionary<string, MessageTemplate> Templates = new(StringComparer.Ordinal)
    {
        ["hangfire.runtime_warning"] = Parse("Hangfire runtime requires attention."),
        ["hangfire.runtime_failed"] = Parse("Hangfire infrastructure operation failed."),
        ["hangfire.diagnostic"] = Parse("Hangfire development diagnostic."),
        ["hangfire.runtime_unknown"] = Parse("Unrecognized Hangfire provider event."),
        ["job.retrying"] = Parse("Job state election selected an automatic retry."),
        ["job.failed"] = Parse("Job state election selected failure without automatic retry."),
        ["job.state_unknown"] = Parse("Unrecognized Hangfire state observation event.")
    };

    public void Emit(LogEvent value)
    {
        if (!value.Properties.TryGetValue("SourceContext", out var rawSource) ||
            rawSource is not ScalarValue { Value: string source } || source is not (ProviderSource or StateSource))
        {
            next.Emit(value);
            return;
        }

        var rawCode = value.Properties.TryGetValue("EventCode", out var codeValue) &&
            codeValue is ScalarValue { Value: string text } ? text : null;
        var code = source == ProviderSource
            ? rawCode is "hangfire.runtime_warning" or "hangfire.runtime_failed" or "hangfire.diagnostic"
                ? rawCode : "hangfire.runtime_unknown"
            : rawCode is "job.retrying" or "job.failed" ? rawCode : "job.state_unknown";
        var unknown = code is "hangfire.runtime_unknown" or "job.state_unknown";
        var properties = new List<LogEventProperty>
        {
            new("SourceContext", new ScalarValue(source)), new("EventCode", new ScalarValue(code)),
            new("SourceCategory", new ScalarValue("job")),
            new("Diagnostic", new ScalarValue(code == "hangfire.diagnostic" || unknown && value.Level < LogEventLevel.Warning))
        };
        if (!unknown)
        {
            if (value.Properties.TryGetValue("failureKind", out var failure) &&
                failure is ScalarValue { Value: string kind } && FailureKinds.Contains(kind))
                properties.Add(new("failureKind", failure));
            if (source == StateSource)
            {
                var expectedOutcome = code == "job.retrying" ? "retrying" : "failed";
                if (value.Properties.TryGetValue("outcome", out var outcome) &&
                    outcome is ScalarValue { Value: string selected } && selected == expectedOutcome)
                    properties.Add(new("outcome", outcome));
                if (value.Properties.TryGetValue("operationId", out var operation) &&
                    operation is ScalarValue { Value: string id } && Guid.TryParseExact(id, "D", out var parsed) &&
                    id == parsed.ToString("D"))
                    properties.Add(new("operationId", operation));
            }
        }
        next.Emit(new LogEvent(value.Timestamp, value.Level, null, Templates[code], properties,
            value.TraceId ?? Activity.Current?.TraceId ?? default, value.SpanId ?? Activity.Current?.SpanId ?? default));
    }

    private static MessageTemplate Parse(string template) => new MessageTemplateParser().Parse(template);
}
