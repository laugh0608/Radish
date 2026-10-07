using Radish.Common.LogTool;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Radish.Extension.Log;

/// <summary>在上下文合并后重建 SignalR Core 事件，禁止原始异常、模板与 scope 进入输出。</summary>
public sealed class SignalRFrameworkLogSink(ILogEventSink next) : ILogEventSink
{
    private static readonly MessageTemplate Template = new MessageTemplateParser()
        .Parse("SignalR event {EventCode}.");
    private static readonly MessageTemplate FailureTemplate = new MessageTemplateParser()
        .Parse("SignalR event {EventCode}; kind={failureKind}.");

    public void Emit(LogEvent logEvent)
    {
        var source = Scalar(logEvent, "SourceContext") as string;
        var owner = source switch
        {
            "Microsoft.AspNetCore.SignalR.HubConnectionContext" => "connection",
            "Microsoft.AspNetCore.SignalR.HubConnectionHandler" => "handler",
            "Microsoft.AspNetCore.SignalR.Internal.DefaultHubDispatcher" => "dispatcher",
            _ => null
        };
        if (owner == null)
        {
            next.Emit(logEvent);
            return;
        }

        var (id, name) = Identity(logEvent);
        var code = (owner, id, name) switch
        {
            ("handler", 1, "ErrorDispatchingHubEvent") => Scalar(logEvent, "HubMethod") switch
            {
                "OnConnectedAsync" => "signalr.connect_failed",
                "OnDisconnectedAsync" => "signalr.disconnect_failed",
                _ => "signalr.unclassified"
            },
            ("handler", 2, "ErrorProcessingRequest") => "signalr.message_failed",
            ("handler", 4, "ErrorSendingClose") => "signalr.close_failed",
            ("connection", 5, "HandshakeFailed") => "signalr.handshake_failed",
            ("connection", 6, "FailedWritingMessage") => "signalr.write_failed",
            ("dispatcher", 8, "FailedInvokingHubMethod") => "signalr.invocation_failed",
            ("dispatcher", 25, "FailedStreaming") => "signalr.stream_failed",
            ("dispatcher", 3, "UnknownHubMethod") => "signalr.invocation_rejected",
            ("dispatcher", 22, "InvalidHubParameters") => "signalr.invocation_rejected",
            _ => "signalr.unclassified"
        };
        var properties = new List<LogEventProperty>
        {
            new("EventCode", new ScalarValue(code)),
            new("SourceCategory", new ScalarValue("infrastructure")),
            new("Diagnostic", new ScalarValue(logEvent.Level < LogEventLevel.Warning))
        };
        if (logEvent.Exception != null)
            properties.Add(new LogEventProperty("failureKind", new ScalarValue(RuntimeFailureSummary.Classify(logEvent.Exception))));
        // SourceContext 已匹配三个固定框架类别；保留原值以兼容下游来源过滤。
        properties.Add(new LogEventProperty("SourceContext", new ScalarValue(source)));
        next.Emit(new LogEvent(logEvent.Timestamp, logEvent.Level, null, logEvent.Exception == null ? Template : FailureTemplate, properties,
            logEvent.TraceId ?? System.Diagnostics.Activity.Current?.TraceId ?? default,
            logEvent.SpanId ?? System.Diagnostics.Activity.Current?.SpanId ?? default));
    }

    private static object? Scalar(LogEvent logEvent, string key) =>
        logEvent.Properties.TryGetValue(key, out var value) && value is ScalarValue scalar ? scalar.Value : null;

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
