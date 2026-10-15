using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Radish.Extension.Log;

/// <summary>重建精确自有来源；按事件分别校验字段，防止 scope 或原始载荷混入。</summary>
public sealed class HostProgramLogSink(ILogEventSink next) : ILogEventSink
{
    private static readonly string[] Jobs = ["outbox-dispatch", "chat-reactions", "wiki-drafts", "notification-inbox",
        "file-deleted", "file-temp", "file-recycle", "file-orphan", "upload-sessions", "file-tokens",
        "comment-highlight", "retention-rewards", "post-lottery", "shop-timeouts", "shop-benefits", "shop-stats"];
    private static readonly string[] Reasons = ["disabled", "both-tables-missing", "document-table-missing",
        "revision-table-missing", "directory-missing", "no-markdown", "unknown"];
    private static readonly string[] Failures = ["aggregate", "database", "cancelled", "timeout", "io",
        "access-denied", "argument", "invalid-operation", "other"];
    private static readonly string[] Counts = ["markdownFileCount", "descriptorCount", "generatedNodeCount", "syncedCount",
        "createdCount", "updatedCount", "restoredCount", "parentAdjustedCount", "softDeletedCount", "skippedCount"];
    private static readonly Dictionary<string, MessageTemplate> Templates = new(StringComparer.Ordinal)
    {
        ["job.recurring_registration_returned"] = Parse("Recurring job registration returned; job={jobKind}."),
        ["document.builtin_sync_skipped"] = Parse("Built-in document startup sync skipped; reason={builtInSyncSkipReason}."),
        ["document.builtin_sync_completed"] = Parse("Built-in document startup sync completed; markdown={markdownFileCount}; descriptors={descriptorCount}; generated={generatedNodeCount}; synced={syncedCount}; created={createdCount}; updated={updatedCount}; restored={restoredCount}; parentAdjusted={parentAdjustedCount}; softDeleted={softDeletedCount}; skipped={skippedCount}."),
        ["document.builtin_sync_failed"] = Parse("Built-in document startup sync failed; failure={failureKind}."),
        ["runtime.stopped"] = Parse("Gateway stopped notification received."),
        ["runtime.program_unknown"] = Parse("Unrecognized host program event.")
    };

    public void Emit(LogEvent value)
    {
        if (!value.Properties.TryGetValue("SourceContext", out var source) ||
            source is not ScalarValue { Value: HostProgramLog.Source })
        {
            next.Emit(value);
            return;
        }
        var code = value.Properties.TryGetValue("EventCode", out var raw) && raw is ScalarValue { Value: string text } &&
            Templates.ContainsKey(text) ? text : "runtime.program_unknown";
        var properties = new List<LogEventProperty>
        {
            new("SourceContext", new ScalarValue(HostProgramLog.Source)), new("EventCode", new ScalarValue(code)),
            new("SourceCategory", new ScalarValue(code == "job.recurring_registration_returned" ? "job"
                : code == "runtime.stopped" ? "lifecycle" : "application")),
            new("Diagnostic", new ScalarValue(code == "runtime.program_unknown" && value.Level < LogEventLevel.Warning))
        };
        switch (code)
        {
            case "job.recurring_registration_returned": CopyEnum(value, properties, "jobKind", Jobs); break;
            case "document.builtin_sync_skipped": CopyEnum(value, properties, "builtInSyncSkipReason", Reasons); break;
            case "document.builtin_sync_failed": CopyEnum(value, properties, "failureKind", Failures); break;
            case "document.builtin_sync_completed":
                foreach (var key in Counts)
                    if (value.Properties.TryGetValue(key, out var count) && count is ScalarValue { Value: int number } && number >= 0)
                        properties.Add(new(key, count));
                break;
        }
        next.Emit(new LogEvent(value.Timestamp, value.Level, null, Templates[code], properties,
            value.TraceId ?? Activity.Current?.TraceId ?? default, value.SpanId ?? Activity.Current?.SpanId ?? default));
    }

    private static MessageTemplate Parse(string template) => new MessageTemplateParser().Parse(template);

    private static void CopyEnum(LogEvent source, List<LogEventProperty> target, string key, string[] allowed)
    {
        if (source.Properties.TryGetValue(key, out var value) && value is ScalarValue { Value: string text } && allowed.Contains(text))
            target.Add(new(key, value));
    }
}
