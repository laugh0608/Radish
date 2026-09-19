using System.Diagnostics;
using System.Text.Json;
using Radish.Common.LogTool;
using Serilog.Core;
using Serilog.Events;

namespace Radish.Extension.Log;

/// <summary>兼容 ILogger / Serilog 调用，不渲染模板、异常或解构对象。</summary>
public sealed class RuntimeSerilogSink(RuntimeLogOutput output) : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        try
        {
            var code = Scalar(logEvent, "EventCode") as string;
            var input = new Dictionary<string, object?>
            {
                ["eventCode"] = code ?? "runtime.unclassified",
                ["level"] = logEvent.Level.ToString(),
                ["sourceCategory"] = Scalar(logEvent, "SourceCategory") as string ?? "application",
                // 未迁移的普通过程信息只进入显式开发诊断；未知 Warning / Error 仍保留安全摘要。
                ["diagnostic"] = Scalar(logEvent, "Diagnostic") is true ||
                    ((code == null || !RuntimeLogPolicy.IsRegisteredEventCode(code)) && logEvent.Level < LogEventLevel.Warning),
                ["contentOmitted"] = true
            };
            var properties = new Dictionary<string, object?>();
            foreach (var (key, value) in logEvent.Properties)
            {
                if (key is "EventCode" or "SourceCategory" or "Diagnostic") continue;
                if (key is "traceId" or "spanId" or "operationId")
                {
                    input[key] = value is ScalarValue { Value: string text } && text.Length <= 36 ? text : null;
                    continue;
                }
                // 只传内建标量，由公共策略再次执行键名、枚举和数值范围校验。
                properties[key] = value is ScalarValue scalar ? Primitive(scalar.Value) : null;
            }
            input["properties"] = properties;
            if (logEvent.TraceId is { } trace) input["traceId"] = trace.ToHexString();
            else if (Activity.Current is { IdFormat: ActivityIdFormat.W3C } activity) input["traceId"] = activity.TraceId.ToHexString();
            if (logEvent.SpanId is { } span) input["spanId"] = span.ToHexString();
            else if (Activity.Current is { IdFormat: ActivityIdFormat.W3C } current) input["spanId"] = current.SpanId.ToHexString();
            output.Write(JsonSerializer.SerializeToElement(input));
        }
        catch (Exception)
        {
            output.ReportFailure();
        }
    }

    private static object? Scalar(LogEvent value, string key) =>
        value.Properties.TryGetValue(key, out var property) && property is ScalarValue scalar ? scalar.Value : null;

    private static object? Primitive(object? value) => value switch
    {
        string text when text.Length <= 128 => text,
        bool or byte or sbyte or short or ushort or int or uint or long or ulong or decimal => value,
        double number when double.IsFinite(number) => number,
        float number when float.IsFinite(number) => number,
        _ => null
    };
}
