using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Radish.Extension.Log;

/// <summary>分别重建 Outbox 分派和实际状态写入摘要。</summary>
internal sealed class OutboxLogSink(ILogEventSink next) : ILogEventSink
{
    private const string Dispatcher = "Radish.Api.Services.ReliableOutboxDispatcherJob";
    private const string Repository = "Radish.Repository.ReliableOutboxRepository";
    private const string Dispatched = "outbox.dispatched";
    private const string Retrying = "outbox.retrying";
    private const string DeadLetter = "outbox.dead_letter";
    private const string Unknown = "runtime.unclassified";

    private static readonly Dictionary<string, MessageTemplate> Templates = new(StringComparer.Ordinal)
    {
        [Dispatched] = new MessageTemplateParser().Parse("Outbox dispatch completed."),
        [Retrying] = new MessageTemplateParser().Parse("Outbox retry scheduled."),
        [DeadLetter] = new MessageTemplateParser().Parse("Outbox task moved to dead letter."),
        [Unknown] = new MessageTemplateParser().Parse("Unrecognized outbox event.")
    };

    public void Emit(LogEvent value)
    {
        if (Scalar(value, "SourceContext") is not { Value: string source } || source is not (Dispatcher or Repository))
        {
            next.Emit(value);
            return;
        }

        var code = (source, Scalar(value, "EventCode")?.Value, value.Level) switch
        {
            (Dispatcher, Dispatched, LogEventLevel.Information) => Dispatched,
            (Repository, Retrying, LogEventLevel.Warning) => Retrying,
            (Repository, DeadLetter, LogEventLevel.Error) => DeadLetter,
            _ => Unknown
        };
        var properties = new List<LogEventProperty>
        {
            new("SourceContext", new ScalarValue(source)),
            new("EventCode", new ScalarValue(code)),
            new("SourceCategory", new ScalarValue("job")),
            new("Diagnostic", new ScalarValue(code == Unknown && value.Level < LogEventLevel.Warning))
        };

        if (code == Dispatched)
        {
            if (Scalar(value, "count") is { Value: int and >= 0 } count)
                properties.Add(new("count", count));
            if (Scalar(value, "durationMs") is { Value: double and >= 0 and <= 31_536_000_000d } duration)
                properties.Add(new("durationMs", duration));
        }
        else if (code is Retrying or DeadLetter)
        {
            if (Scalar(value, "databaseScope") is { Value: "main" or "chat" } database)
                properties.Add(new("databaseScope", database));
            if (Scalar(value, "attempt") is { Value: int and >= 0 and <= 1_000_000 } attempt)
                properties.Add(new("attempt", attempt));
            if (Scalar(value, "outcome") is { Value: string outcome } result && outcome == (code == Retrying ? "retrying" : "failed"))
                properties.Add(new("outcome", result));
            if (Scalar(value, "failureKind") is
                { Value: "aggregate" or "database" or "cancelled" or "timeout" or "io" or "access-denied" or "argument" or "invalid-operation" or "other" } failure)
                properties.Add(new("failureKind", failure));
            if (Scalar(value, "operationId") is { Value: string id } operation &&
                Guid.TryParseExact(id, "D", out var parsed) && id == parsed.ToString("D"))
                properties.Add(new("operationId", operation));
        }

        var activity = Activity.Current is { IdFormat: ActivityIdFormat.W3C } current ? current : null;
        next.Emit(new LogEvent(value.Timestamp, value.Level, null, Templates[code], properties,
            value.TraceId ?? activity?.TraceId ?? default, value.SpanId ?? activity?.SpanId ?? default));
    }

    private static ScalarValue? Scalar(LogEvent value, string key) => value.Properties.TryGetValue(key, out var property)
        ? property as ScalarValue : null;
}
