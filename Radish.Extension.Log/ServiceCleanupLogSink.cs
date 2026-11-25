using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Radish.Extension.Log;

/// <summary>分别重建服务内清理批次和前台上传清理失败。</summary>
internal sealed class ServiceCleanupLogSink(ILogEventSink next) : ILogEventSink
{
    private const string Source = "Radish.Service.Internal.ServiceCleanupSummary";
    private const string Completed = "job.cleanup.completed";
    private const string Interrupted = "job.cleanup.interrupted";
    private const string Failed = "job.cleanup.failed";
    private const string ForegroundFailed = "upload.cleanup.failed";
    private const string Unknown = "runtime.unclassified";

    private static readonly string[] BatchCounts = ["processedCount", "updatedCount", "removedDirectoryCount", "settlementCount",
        "skippedCount", "failedCount"];
    private static readonly Dictionary<string, MessageTemplate> Templates = new(StringComparer.Ordinal)
    {
        [Completed] = new MessageTemplateParser().Parse("Service cleanup completed."),
        [Interrupted] = new MessageTemplateParser().Parse("Service cleanup was interrupted; counts describe earlier progress."),
        [Failed] = new MessageTemplateParser().Parse("Service cleanup consumed failures."),
        [ForegroundFailed] = new MessageTemplateParser().Parse("Upload cleanup operation consumed a failure."),
        [Unknown] = new MessageTemplateParser().Parse("Unrecognized service cleanup event.")
    };

    public void Emit(LogEvent value)
    {
        if (Scalar(value, "SourceContext") is not { Value: Source })
        {
            next.Emit(value);
            return;
        }

        var jobKind = Scalar(value, "jobKind");
        var code = (Scalar(value, "EventCode")?.Value, jobKind?.Value, value.Level) switch
        {
            (Completed, "upload-sessions" or "file-tokens", LogEventLevel.Information) => Completed,
            (Interrupted, "upload-sessions" or "file-tokens", LogEventLevel.Information) => Interrupted,
            (Failed, "upload-sessions", LogEventLevel.Error) => Failed,
            (ForegroundFailed, _, LogEventLevel.Error) => ForegroundFailed,
            _ => Unknown
        };
        var properties = new List<LogEventProperty>
        {
            new("SourceContext", new ScalarValue(Source)),
            new("EventCode", new ScalarValue(code)),
            new("SourceCategory", new ScalarValue(code == ForegroundFailed ? "application" : "job")),
            new("Diagnostic", new ScalarValue(code == Unknown && value.Level < LogEventLevel.Warning))
        };

        if (code is Completed or Interrupted or Failed)
        {
            properties.Add(new("jobKind", jobKind!));
            var outcome = Scalar(value, "outcome");
            if (outcome != null && (code, outcome.Value) is (Completed, "succeeded") or (Interrupted, "partial") or (Failed, "partial" or "failed"))
                properties.Add(new("outcome", outcome));

            foreach (var key in BatchCounts)
            {
                if (Scalar(value, key) is { Value: int and >= 0 } count)
                    properties.Add(new(key, count));
            }

            if (Scalar(value, "durationMs") is { Value: double and >= 0 and <= 31_536_000_000d } duration)
                properties.Add(new("durationMs", duration));
        }

        if (code == ForegroundFailed && Scalar(value, "cleanupOperation") is { Value: "directory" or "quota-release" or "quota-complete" } operation)
            properties.Add(new("cleanupOperation", operation));
        if (code is Failed or ForegroundFailed && Scalar(value, "failureKind") is
            { Value: "aggregate" or "database" or "cancelled" or "timeout" or "io" or "access-denied" or "argument" or "invalid-operation" or "other" } failure)
            properties.Add(new("failureKind", failure));

        var activity = Activity.Current is { IdFormat: ActivityIdFormat.W3C } current ? current : null;
        next.Emit(new LogEvent(value.Timestamp, value.Level, null, Templates[code], properties,
            value.TraceId ?? activity?.TraceId ?? default, value.SpanId ?? activity?.SpanId ?? default));
    }

    private static ScalarValue? Scalar(LogEvent value, string key) => value.Properties.TryGetValue(key, out var property)
        ? property as ScalarValue : null;
}
