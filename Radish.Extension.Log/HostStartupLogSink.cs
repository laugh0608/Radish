using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Radish.Extension.Log;

/// <summary>只重建精确启动来源；合并后的任意 scope、异常及原始配置值不进入输出。</summary>
public sealed class HostStartupLogSink(ILogEventSink next) : ILogEventSink
{
    private const string CommonTemplate = "Host startup summary; host={startupHost}; environment={startupEnvironment}; listeners={listenEndpointCount}; corsOrigins={corsOriginCount}";
    private static readonly MessageTemplate AuthTemplate = new MessageTemplateParser().Parse(CommonTemplate + "; authentication={startupAuthentication}.");
    private static readonly MessageTemplate GatewayTemplate = new MessageTemplateParser().Parse(CommonTemplate + "; healthTargets={healthTargetCount}; httpsRedirection={startupHttpsRedirection}; delegation={startupDelegation}.");
    private static readonly MessageTemplate UnknownTemplate = new MessageTemplateParser().Parse("Unrecognized host startup event.");

    public void Emit(LogEvent logEvent)
    {
        if (!logEvent.Properties.TryGetValue("SourceContext", out var source) ||
            source is not ScalarValue { Value: HostStartupLog.Source })
        {
            next.Emit(logEvent);
            return;
        }

        var known = logEvent.Properties.TryGetValue("EventCode", out var code) &&
            code is ScalarValue { Value: "runtime.startup_summary" };
        var properties = new List<LogEventProperty>
        {
            new("SourceContext", new ScalarValue(HostStartupLog.Source)),
            new("EventCode", new ScalarValue(known ? "runtime.startup_summary" : "runtime.startup_unknown")),
            new("SourceCategory", new ScalarValue("lifecycle")),
            new("Diagnostic", new ScalarValue(!known && logEvent.Level < LogEventLevel.Warning))
        };
        var template = UnknownTemplate;
        if (known)
        {
            var host = CopyEnum(logEvent, properties, "startupHost", ["auth", "api", "gateway"]);
            CopyEnum(logEvent, properties, "startupEnvironment", ["development", "production", "staging", "other"]);
            CopyCount(logEvent, properties, "listenEndpointCount");
            CopyCount(logEvent, properties, "corsOriginCount");
            template = AuthTemplate;
            if (host == "auth")
                CopyEnum(logEvent, properties, "startupAuthentication", ["not-initialized", "development-keys", "file-certificates"]);
            if (host == "api")
                CopyEnum(logEvent, properties, "startupAuthentication", ["authority", "local-certificate"]);
            if (host == "gateway")
            {
                template = GatewayTemplate;
                CopyCount(logEvent, properties, "healthTargetCount");
                CopyEnum(logEvent, properties, "startupHttpsRedirection", ["enabled", "disabled"]);
                CopyEnum(logEvent, properties, "startupDelegation", ["removed", "retained"]);
            }
        }
        next.Emit(new LogEvent(logEvent.Timestamp, logEvent.Level, null, template, properties,
            logEvent.TraceId ?? Activity.Current?.TraceId ?? default,
            logEvent.SpanId ?? Activity.Current?.SpanId ?? default));
    }

    private static string? CopyEnum(LogEvent source, List<LogEventProperty> target, string key, string[] allowed)
    {
        if (!source.Properties.TryGetValue(key, out var value) || value is not ScalarValue { Value: string text } ||
            !allowed.Contains(text)) return null;
        target.Add(new(key, value));
        return text;
    }

    private static void CopyCount(LogEvent source, List<LogEventProperty> target, string key)
    {
        if (source.Properties.TryGetValue(key, out var value) && value is ScalarValue { Value: int count } && count >= 0)
            target.Add(new(key, value));
    }
}
