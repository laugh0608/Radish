using System.Diagnostics;
using Radish.Common.LogTool;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Radish.Extension.Log;

/// <summary>在上下文合并后重建 OpenIddict Server 事件，不输出协议消息与认证材料。</summary>
public sealed class OpenIddictLogSink(ILogEventSink next) : ILogEventSink
{
    private static readonly MessageTemplate Template = new MessageTemplateParser().Parse("OpenIddict event {EventCode}.");
    private static readonly MessageTemplate FailureTemplate = new MessageTemplateParser().Parse("OpenIddict event {EventCode}; kind={failureKind}.");

    public void Emit(LogEvent logEvent)
    {
        if (!logEvent.Properties.TryGetValue("SourceContext", out var value) ||
            value is not ScalarValue { Value: string source } || !OpenIddictLogIdentity.IsSource(source))
        {
            next.Emit(logEvent);
            return;
        }

        var (id, name) = Identity(logEvent);
        var code = OpenIddictLogIdentity.Code(source, id, name);
        string? failureKind = null;
        if (logEvent.Exception != null) failureKind = RuntimeFailureSummary.Classify(logEvent.Exception);
        else if (logEvent.Properties.TryGetValue("failureKind", out var kind) &&
                 kind is ScalarValue { Value: "aggregate" or "database" or "cancelled" or "timeout" or "io" or
                     "access-denied" or "argument" or "invalid-operation" or "other" })
            failureKind = (string)((ScalarValue)kind).Value!;

        var properties = new List<LogEventProperty>
        {
            new("EventCode", new ScalarValue(code)),
            new("SourceContext", new ScalarValue(source)),
            new("SourceCategory", new ScalarValue("http")),
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
                // 数字事件由桥接省略 Name；显式 null / 错误类型不等同于字段缺省。
                if (seenName || property.Value is not ScalarValue { Value: string text }) return (null, null);
                seenName = true;
                name = text;
            }
        }
        return (id, name);
    }
}
