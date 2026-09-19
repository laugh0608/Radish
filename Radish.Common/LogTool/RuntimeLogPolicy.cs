using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Radish.Common.LogTool;

/// <summary>
/// .NET / Node 共用版本化策略。第一阶段只开放有类型约束的元数据；
/// 任意消息、异常文本、对象、数组及未登记属性在序列化前丢弃。
/// </summary>
public sealed class RuntimeLogPolicy
{
    private static readonly JsonElement Contract = LoadContract();
    public static int MaxEventBytes => Contract.GetProperty("maxEventBytes").GetInt32();
    public static bool IsRegisteredEventCode(string code) => Contract.GetProperty("events").TryGetProperty(code, out _);
    private static readonly Regex SourceToken = new("^[a-zA-Z0-9][a-zA-Z0-9._-]{0,63}$", RegexOptions.CultureInvariant);
    private readonly RuntimeLogSource _source;
    private readonly string _mode;
    private readonly string _minimumLevel;
    private readonly bool _diagnostics;
    private readonly TimeProvider _clock;

    public RuntimeLogPolicy(RuntimeLogSource source, string mode = "Production",
        string minimumLevel = "Info", bool diagnostics = false, TimeProvider? clock = null)
    {
        if (mode is not ("Development" or "Production") ||
            minimumLevel is not ("Info" or "Warning" or "Error") ||
            (mode == "Production" && diagnostics))
            throw new ArgumentException("Invalid runtime logging mode, level or diagnostics configuration.");
        if (!Contains("services", source.Service) ||
            new[] { source.DeploymentId, source.InstanceId, source.Release }.Any(v => (v == null || !SourceToken.IsMatch(v) || v.Contains('\n') || v.Contains('\r'))))
            throw new ArgumentException("Invalid runtime logging source configuration.");
        _source = source;
        _mode = mode;
        _minimumLevel = minimumLevel;
        _diagnostics = diagnostics;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>返回 null 表示策略丢弃；调用方不得向其他介质另行输出原文。</summary>
    public RuntimeLogEvent? Create(JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.Object) throw new ArgumentException("Expected event object.");
        if ((input.TryGetProperty("level", out var suppliedLevel) && suppliedLevel.ValueKind != JsonValueKind.String) ||
            (input.TryGetProperty("diagnostic", out var suppliedDiagnostic) && suppliedDiagnostic.ValueKind is not (JsonValueKind.True or JsonValueKind.False)))
            throw new ArgumentException("Invalid runtime log level or diagnostic flag type.");
        var rawLevel = Text(input, "level") ?? "Info";
        if (!Contract.GetProperty("levels").TryGetProperty(rawLevel, out var mapping))
            throw new ArgumentException("Unknown runtime log level.");
        var level = mapping.GetProperty("level").GetString()!;
        var diagnostic = mapping.GetProperty("diagnostic").GetBoolean() ||
                         (input.TryGetProperty("diagnostic", out var detail) && detail.ValueKind == JsonValueKind.True);
        if ((diagnostic && (_mode == "Production" || !_diagnostics)) || Rank(level) < Rank(_minimumLevel)) return null;

        var eventCode = Text(input, "eventCode") ?? "runtime.unclassified";
        var known = Contract.GetProperty("events").TryGetProperty(eventCode, out var message);
        if (!known)
        {
            eventCode = "runtime.unclassified";
            message = Contract.GetProperty("events").GetProperty(eventCode);
        }
        var redacted = !known || input.EnumerateObject().Any(p => p.Name is not
            ("level" or "diagnostic" or "eventCode" or "sourceCategory" or "properties" or "traceId" or "spanId" or "operationId"));
        foreach (var key in new[] { "eventCode", "sourceCategory" })
            if (input.TryGetProperty(key, out var suppliedText) && suppliedText.ValueKind != JsonValueKind.String) redacted = true;
        var category = Text(input, "sourceCategory") ?? "application";
        if (!Contains("categories", category)) { category = "application"; redacted = true; }
        var properties = new SortedDictionary<string, object>(StringComparer.Ordinal);
        if (input.TryGetProperty("properties", out var supplied))
        {
            if (supplied.ValueKind != JsonValueKind.Object) redacted = true;
            else foreach (var property in supplied.EnumerateObject())
            {
                if (!Contract.GetProperty("properties").TryGetProperty(property.Name, out var rule))
                { redacted = true; continue; }
                if (rule.GetProperty("type").GetString() == "number" && property.Value.ValueKind == JsonValueKind.Number &&
                    property.Value.TryGetDouble(out var number) && double.IsFinite(number) &&
                    number >= rule.GetProperty("min").GetDouble() && number <= rule.GetProperty("max").GetDouble() &&
                    (!rule.TryGetProperty("integer", out var integer) || !integer.GetBoolean() || Math.Truncate(number) == number))
                    properties[property.Name] = number;
                else if (rule.GetProperty("type").GetString() == "enum" && property.Value.ValueKind == JsonValueKind.String &&
                         rule.GetProperty("values").EnumerateArray().Any(v => v.GetString() == property.Value.GetString()))
                    properties[property.Name] = property.Value.GetString()!;
                else redacted = true;
            }
        }
        var traceId = Correlation(input, "traceId", "^[0-9a-f]{32}$", ref redacted);
        var spanId = Correlation(input, "spanId", "^[0-9a-f]{16}$", ref redacted);
        var operationId = Correlation(input, "operationId", "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", ref redacted);
        var now = _clock.GetUtcNow().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        return new RuntimeLogEvent
        {
            EventId = Guid.NewGuid().ToString("D"), EventCode = eventCode,
            OccurredAtUtc = now, ObservedAtUtc = now,
            DeploymentId = _source.DeploymentId, Service = _source.Service, InstanceId = _source.InstanceId,
            Release = _source.Release, Mode = _mode, SourceCategory = category,
            Level = level, Diagnostic = diagnostic, IsFatal = mapping.GetProperty("isFatal").GetBoolean(),
            MessageTemplate = message.GetString()!, Message = message.GetString()!, Properties = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object>(properties),
            TraceId = traceId, SpanId = spanId, OperationId = operationId,
            NormalizationStatus = known && eventCode != "runtime.unclassified" ? "normalized" : "unclassified", Redacted = redacted, Truncated = false
        };
    }

    private static string? Correlation(JsonElement input, string key, string pattern, ref bool redacted)
    {
        if (!input.TryGetProperty(key, out var value)) return null;
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (text != null && text.Length <= 36 && !text.Contains('\n') && !text.Contains('\r') && Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant) &&
            text.Any(c => c != '0' && c != '-')) return text;
        redacted = true;
        return null;
    }

    private static string? Text(JsonElement value, string key) =>
        value.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.String ? field.GetString() : null;
    private static int Rank(string level) => level switch { "Info" => 0, "Warning" => 1, _ => 2 };
    private static bool Contains(string key, string value) => Contract.GetProperty(key).EnumerateArray().Any(v => v.GetString() == value);
    private static JsonElement LoadContract()
    {
        using var stream = typeof(RuntimeLogPolicy).Assembly.GetManifestResourceStream(
            "Radish.Common.LogTool.Contracts.runtime-log-policy.v1.json")!;
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }
}
