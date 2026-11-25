using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Radish.Extension.Log;

/// <summary>按来源和事件重建文件及记录清理摘要。</summary>
internal sealed class CleanupJobLogSink(ILogEventSink next) : ILogEventSink
{
    private const string FileSource = "Radish.Service.Jobs.FileCleanupJob";
    private const string InboxSource = "Radish.Api.Services.NotificationInboxCleanupJob";
    private const string WikiSource = "Radish.Api.Services.WikiDraftPayloadCleanupJob";
    private const string ChatSource = "Radish.Api.Services.ChatMessageReactionOperationCleanupJob";
    private const string Completed = "job.cleanup.completed";
    private const string Warning = "job.cleanup.warning";
    private const string Failed = "job.cleanup.failed";
    private const string CapacityWarning = "job.cleanup.capacity_warning";
    private const string Unknown = "runtime.unclassified";

    private static readonly string[] FileCounts = ["processedCount", "movedCount", "missingCount", "skippedCount",
        "removedDirectoryCount", "failedCount", "directoryFailureCount"];
    private static readonly string[] InboxCounts = ["relationCount", "groupCount", "notificationCount"];
    private static readonly string[] SingleCount = ["count"];
    private static readonly Dictionary<string, MessageTemplate> Templates = new(StringComparer.Ordinal)
    {
        [Completed] = new MessageTemplateParser().Parse("Cleanup batch completed."),
        [Warning] = new MessageTemplateParser().Parse("Cleanup batch completed with warnings."),
        [Failed] = new MessageTemplateParser().Parse("Cleanup batch consumed failures."),
        [CapacityWarning] = new MessageTemplateParser().Parse("Notification inbox capacity requires attention."),
        [Unknown] = new MessageTemplateParser().Parse("Unrecognized cleanup job event.")
    };

    public void Emit(LogEvent value)
    {
        if (Scalar(value, "SourceContext") is not { Value: string source } ||
            source is not (FileSource or InboxSource or WikiSource or ChatSource))
        {
            next.Emit(value);
            return;
        }

        var code = (source, Scalar(value, "EventCode")?.Value) switch
        {
            (_, Completed) => Completed,
            (FileSource, Warning) => Warning,
            (FileSource, Failed) => Failed,
            (InboxSource, CapacityWarning) => CapacityWarning,
            _ => Unknown
        };
        var properties = new List<LogEventProperty>
        {
            new("SourceContext", new ScalarValue(source)),
            new("EventCode", new ScalarValue(code)),
            new("SourceCategory", new ScalarValue("job")),
            new("Diagnostic", new ScalarValue(code == Unknown && value.Level < LogEventLevel.Warning))
        };

        if (code != Unknown)
        {
            if (source == FileSource)
                AddFileFields(value, code, properties);
            else
            {
                var jobKind = source switch
                {
                    InboxSource => "notification-inbox",
                    WikiSource => "wiki-drafts",
                    _ => "chat-reactions"
                };
                properties.Add(new("jobKind", new ScalarValue(jobKind)));
            }

            var countFields = (source, code) switch
            {
                (FileSource, _) => FileCounts,
                (InboxSource, Completed) => InboxCounts,
                _ => SingleCount
            };
            foreach (var key in countFields)
            {
                if (Scalar(value, key) is { Value: int count } property && count >= 0)
                    properties.Add(new(key, property));
            }

            if (code != CapacityWarning && Scalar(value, "durationMs") is
                { Value: double and >= 0 and <= 31_536_000_000d } duration)
                properties.Add(new("durationMs", duration));
        }

        var activity = Activity.Current is { IdFormat: ActivityIdFormat.W3C } current ? current : null;
        next.Emit(new LogEvent(value.Timestamp, value.Level, null, Templates[code], properties,
            value.TraceId ?? activity?.TraceId ?? default, value.SpanId ?? activity?.SpanId ?? default));
    }

    private static void AddFileFields(LogEvent value, string code, List<LogEventProperty> properties)
    {
        if (Scalar(value, "jobKind") is { Value: "file-deleted" or "file-temp" or "file-recycle" or "file-orphan" } jobKind)
            properties.Add(new("jobKind", jobKind));

        var outcome = Scalar(value, "outcome");
        var matchesOutcome = (code, outcome?.Value) is (Completed, "succeeded") or (Warning, "partial") or (Failed, "partial" or "failed");
        if (outcome != null && matchesOutcome)
            properties.Add(new("outcome", outcome));

        var directoryFailed = code == Warning && Scalar(value, "directoryFailureCount") is { Value: int and > 0 };
        if ((code == Failed || directoryFailed) && Scalar(value, "failureKind") is
            { Value: "aggregate" or "database" or "cancelled" or "timeout" or "io" or "access-denied" or "argument" or "invalid-operation" or "other" } failure)
            properties.Add(new("failureKind", failure));
    }

    private static ScalarValue? Scalar(LogEvent value, string key) => value.Properties.TryGetValue(key, out var property)
        ? property as ScalarValue : null;
}
