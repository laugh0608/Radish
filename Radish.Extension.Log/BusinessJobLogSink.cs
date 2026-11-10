using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Radish.Extension.Log;

/// <summary>按固定来源重建共用的后台业务批次摘要。</summary>
internal sealed class BusinessJobLogSink(ILogEventSink next) : ILogEventSink
{
    private const string Source = "Radish.Service.Jobs.BusinessJobSummary";
    private const string UnknownCode = "runtime.unclassified";

    private static readonly string[] CountFields =
        ["processedCount", "updatedCount", "rewardCount", "skippedCount", "rejectedCount", "failedCount"];
    private static readonly Dictionary<string, MessageTemplate> Templates = new(StringComparer.Ordinal)
    {
        ["job.batch.completed"] = new MessageTemplateParser().Parse("Business job batch completed"),
        ["job.batch.warning"] = new MessageTemplateParser().Parse("Business job batch contains rejected results"),
        ["job.batch.failed"] = new MessageTemplateParser().Parse("Business job batch consumed failures"),
        [UnknownCode] = new MessageTemplateParser().Parse("Unrecognized business job batch event.")
    };

    public void Emit(LogEvent value)
    {
        if (!value.Properties.TryGetValue("SourceContext", out var source) || source is not ScalarValue { Value: Source })
        {
            next.Emit(value);
            return;
        }

        var code = value.Properties.TryGetValue("EventCode", out var rawCode) &&
                   rawCode is ScalarValue { Value: string suppliedCode } && Templates.ContainsKey(suppliedCode)
            ? suppliedCode : UnknownCode;
        var properties = new List<LogEventProperty>
        {
            new("SourceContext", new ScalarValue(Source)),
            new("EventCode", new ScalarValue(code)),
            new("SourceCategory", new ScalarValue("job")),
            new("Diagnostic", new ScalarValue(code == UnknownCode && value.Level < LogEventLevel.Warning))
        };

        if (code != UnknownCode)
        {
            if (value.Properties.TryGetValue("jobKind", out var jobKind) && jobKind is ScalarValue
                { Value: "shop-timeouts" or "shop-benefits" or "shop-stats" or "comment-highlight" or "retention-rewards" })
                properties.Add(new("jobKind", jobKind));

            if (value.Properties.TryGetValue("outcome", out var outcome) && outcome is ScalarValue { Value: string result } &&
                (code == "job.batch.completed" ? result == "succeeded" : result is "partial" or "failed"))
                properties.Add(new("outcome", outcome));

            foreach (var key in CountFields)
            {
                if (value.Properties.TryGetValue(key, out var count) && count is ScalarValue { Value: int number } && number >= 0)
                    properties.Add(new(key, count));
            }

            if (value.Properties.TryGetValue("durationMs", out var duration) &&
                duration is ScalarValue { Value: double milliseconds } && milliseconds is >= 0 and <= 31_536_000_000d)
                properties.Add(new("durationMs", duration));

            if (code == "job.batch.failed" && value.Properties.TryGetValue("failureKind", out var failure) && failure is ScalarValue
                { Value: "aggregate" or "database" or "cancelled" or "timeout" or "io" or "access-denied" or "argument" or "invalid-operation" or "other" })
                properties.Add(new("failureKind", failure));
        }

        next.Emit(new LogEvent(value.Timestamp, value.Level, null, Templates[code], properties,
            value.TraceId ?? Activity.Current?.TraceId ?? default, value.SpanId ?? Activity.Current?.SpanId ?? default));
    }
}
