using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Radish.Extension.Log;

/// <summary>按精确生成身份重建公开 head、sitemap 和 Gateway 注入的运行事件。</summary>
internal sealed class PublicMetadataLogSink(ILogEventSink next) : ILogEventSink
{
    private const string Head = "Radish.Service.PublicHeadSnapshotService";
    private const string Sitemap = "Radish.Service.PublicSitemapService";
    private const string Client = "Radish.Gateway.PublicHead.PublicHeadSnapshotClient";
    private const string Middleware = "Radish.Gateway.PublicHead.PublicHeadSnapshotMiddleware";
    private const string Unknown = "runtime.unclassified";

    private static readonly Dictionary<string, MessageTemplate> Templates = new(StringComparer.Ordinal)
    {
        ["public_head.cache_read_failed"] = new MessageTemplateParser().Parse("Public head cache read failed."),
        ["public_head.cache_write_failed"] = new MessageTemplateParser().Parse("Public head cache write failed."),
        ["sitemap.cache_read_failed"] = new MessageTemplateParser().Parse("Sitemap cache read failed."),
        ["sitemap.cache_write_failed"] = new MessageTemplateParser().Parse("Sitemap cache write failed."),
        ["sitemap.section_count_failed"] = new MessageTemplateParser().Parse("Sitemap section count failed."),
        ["sitemap.generation_failed"] = new MessageTemplateParser().Parse("Sitemap generation failed."),
        ["public_head.snapshot_unavailable"] = new MessageTemplateParser().Parse("Public head snapshot response is unavailable."),
        ["public_head.html_unavailable"] = new MessageTemplateParser().Parse("Frontend HTML response is unavailable."),
        ["public_head.snapshot_request_failed"] = new MessageTemplateParser().Parse("Public head snapshot request failed."),
        ["public_head.html_request_failed"] = new MessageTemplateParser().Parse("Frontend HTML request failed."),
        ["public_head.injection_failed"] = new MessageTemplateParser().Parse("Public head HTML injection or response failed."),
        [Unknown] = new MessageTemplateParser().Parse("Unrecognized public metadata event.")
    };

    public void Emit(LogEvent value)
    {
        if (Scalar(value, "SourceContext") is not { Value: string source } ||
            source is not (Head or Sitemap or Client or Middleware))
        {
            next.Emit(value);
            return;
        }

        var gateway = source is Client or Middleware;
        var code = gateway ? GatewayCode(source, value) : ApiCode(source, value);
        var properties = new List<LogEventProperty>
        {
            new("SourceContext", new ScalarValue(source)),
            new("EventCode", new ScalarValue(code)),
            new("SourceCategory", new ScalarValue(gateway ? "http" : "application")),
            new("Diagnostic", new ScalarValue(code == Unknown && value.Level < LogEventLevel.Warning))
        };

        if (code is "public_head.snapshot_unavailable" or "public_head.html_unavailable")
        {
            if (Scalar(value, "statusCode") is { Value: int status } field &&
                status is >= 100 and <= 599 and not (>= 200 and <= 299) &&
                (code == "public_head.html_unavailable" || status != 404))
                properties.Add(new("statusCode", field));
        }
        else if (code != Unknown && Scalar(value, "failureKind") is
                 { Value: "aggregate" or "database" or "cancelled" or "timeout" or "io" or "access-denied" or "argument" or "invalid-operation" or "other" } failure)
        {
            properties.Add(new("failureKind", failure));
        }

        var activity = Activity.Current is { IdFormat: ActivityIdFormat.W3C } current ? current : null;
        next.Emit(new LogEvent(value.Timestamp, value.Level, null, Templates[code], properties,
            value.TraceId ?? activity?.TraceId ?? default, value.SpanId ?? activity?.SpanId ?? default));
    }

    private static string ApiCode(string source, LogEvent value)
    {
        var code = Scalar(value, "EventCode")?.Value as string;
        return (source, code, value.Level) switch
        {
            (Head, "public_head.cache_read_failed" or "public_head.cache_write_failed", LogEventLevel.Warning) => code,
            (Sitemap, "sitemap.cache_read_failed" or "sitemap.cache_write_failed" or "sitemap.section_count_failed", LogEventLevel.Warning) => code,
            (Sitemap, "sitemap.generation_failed", LogEventLevel.Error) => code,
            _ => Unknown
        };
    }

    private static string GatewayCode(string source, LogEvent value)
    {
        if (value.Level != LogEventLevel.Warning || !value.Properties.TryGetValue("EventId", out var identity) ||
            identity is not StructureValue details)
            return Unknown;

        var ids = details.Properties.Where(p => p.Name == "Id").ToArray();
        var names = details.Properties.Where(p => p.Name == "Name").ToArray();
        if (ids.Length != 1 || ids[0].Value is not ScalarValue { Value: int id } ||
            names.Length != 1 || names[0].Value is not ScalarValue { Value: string name })
            return Unknown;

        return (source, id, name) switch
        {
            (Client, 1, "PublicHeadSnapshotUnavailable") => "public_head.snapshot_unavailable",
            (Client, 2, "PublicHeadSnapshotRequestFailed") => "public_head.snapshot_request_failed",
            (Client, 3, "PublicHeadHtmlUnavailable") => "public_head.html_unavailable",
            (Client, 4, "PublicHeadHtmlRequestFailed") => "public_head.html_request_failed",
            (Middleware, 1, "PublicHeadInjectionFailed") => "public_head.injection_failed",
            _ => Unknown
        };
    }

    private static ScalarValue? Scalar(LogEvent value, string key) =>
        value.Properties.TryGetValue(key, out var property) ? property as ScalarValue : null;
}
