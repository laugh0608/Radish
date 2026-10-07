using System.Diagnostics;
using Radish.Common.LogTool;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Radish.Extension.Log;

/// <summary>只适配异常处理中间件的日志输出，不改变 HTTP 处理、取消或异常传播。</summary>
public sealed class HttpExceptionFrameworkLogSink(ILogEventSink next) : ILogEventSink
{
    private const string Source = "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware";
    private static readonly MessageTemplate Template = new MessageTemplateParser()
        .Parse("HTTP framework event {EventCode}.");
    private static readonly MessageTemplate FailureTemplate = new MessageTemplateParser()
        .Parse("HTTP framework event {EventCode}; kind={failureKind}.");

    public void Emit(LogEvent logEvent)
    {
        if (!logEvent.Properties.TryGetValue("SourceContext", out var source) ||
            source is not ScalarValue { Value: Source })
        {
            next.Emit(logEvent);
            return;
        }

        var code = Identity(logEvent) switch
        {
            // UnhandledException 也可在错误页已完成时生成，不将其命名为最终未处理。
            (1, "UnhandledException") => "http.framework_exception",
            (2, "ResponseStarted") => "http.response_started",
            (3, "Exception") => "http.exception_handler_failed",
            (4, "RequestAborted") => "http.request_aborted",
            _ => "http.framework_unclassified"
        };
        var properties = new List<LogEventProperty>
        {
            new("EventCode", new ScalarValue(code)),
            new("SourceCategory", new ScalarValue("http")),
            new("SourceContext", new ScalarValue(Source)),
            new("Diagnostic", new ScalarValue(logEvent.Level < LogEventLevel.Warning))
        };
        if (logEvent.Exception != null)
            properties.Add(new LogEventProperty("failureKind", new ScalarValue(RuntimeFailureSummary.Classify(logEvent.Exception))));

        next.Emit(new LogEvent(logEvent.Timestamp, logEvent.Level, null,
            logEvent.Exception == null ? Template : FailureTemplate, properties,
            logEvent.TraceId ?? Activity.Current?.TraceId ?? default,
            logEvent.SpanId ?? Activity.Current?.SpanId ?? default));
    }

    private static (int?, string?) Identity(LogEvent logEvent)
    {
        if (!logEvent.Properties.TryGetValue("EventId", out var value) || value is not StructureValue details)
            return (null, null);
        int? id = null;
        string? name = null;
        foreach (var property in details.Properties)
        {
            if (property.Name == "Id" && property.Value is ScalarValue { Value: int number }) id = number;
            if (property.Name == "Name" && property.Value is ScalarValue { Value: string text }) name = text;
        }
        return (id, name);
    }
}
