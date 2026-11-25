using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Radish.Common.LogTool;

public sealed partial class RuntimeLogPolicy
{
    private static readonly HashSet<string> TransportFields = new(StringComparer.Ordinal)
    {
        "schemaVersion", "eventId", "eventCode", "occurredAtUtc", "observedAtUtc", "deploymentId", "service",
        "instanceId", "containerId", "release", "mode", "sourceCategory", "level", "diagnostic", "isFatal",
        "messageTemplate", "message", "properties", "traceId", "spanId", "requestId", "operationId", "normalizationStatus",
        "redacted", "truncated"
    };
    public const string UtcFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    /// <summary>接收已规范化事件；只验证 / 规范编码，不重新产生身份、来源或发生时间。</summary>
    public static RuntimeLogEvent? ReadTransport(JsonElement input, string deployment,
        IReadOnlyCollection<string> services, out string rejectionReason, string expectedMode = "Production")
    {
        rejectionReason = "invalid-event";
        if (input.ValueKind != JsonValueKind.Object || input.EnumerateObject().Any(p => !TransportFields.Contains(p.Name))) return null;
        if (!input.TryGetProperty("schemaVersion", out var schema) || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version) || version != 1)
        { rejectionReason = "invalid-schema"; return null; }
        var service = Text(input, "service");
        if (Text(input, "deploymentId") != deployment || service == null || !services.Contains(service))
        { rejectionReason = "invalid-source"; return null; }
        if (!TryReadUtc(Text(input, "occurredAtUtc"), out _) || !TryReadUtc(Text(input, "observedAtUtc"), out _))
        { rejectionReason = "invalid-time"; return null; }
        var id = Text(input, "eventId");
        var level = Text(input, "level");
        var mode = Text(input, "mode");
        var code = Text(input, "eventCode");
        var instance = Text(input, "instanceId");
        var release = Text(input, "release");
        if (id == null || !Guid.TryParseExact(id, "D", out var uuid) || uuid == Guid.Empty || uuid.ToString("D") != id ||
            level is not ("Info" or "Warning" or "Error") || mode is not ("Development" or "Production") || mode != expectedMode ||
            code == null || !IsRegisteredEventCode(code) || instance == null || release == null ||
            !SourceToken.IsMatch(instance) || !SourceToken.IsMatch(release) ||
            instance.Contains('\n') || instance.Contains('\r') || release.Contains('\n') || release.Contains('\r') ||
            !ReadBoolean(input, "diagnostic", out var diagnostic) || !ReadBoolean(input, "isFatal", out var fatal) ||
            !ReadBoolean(input, "redacted", out var redacted) || !ReadBoolean(input, "truncated", out var truncated) ||
            (fatal && level != "Error") || (diagnostic && mode == "Production")) return null;
        var container = Text(input, "containerId");
        if (input.TryGetProperty("containerId", out _) && (container == null ||
            !Regex.IsMatch(container, "\\A[0-9a-f]{64}\\z", RegexOptions.CultureInvariant) ||
            (instance != container && instance != container[..12]))) return null;
        var status = Text(input, "normalizationStatus");
        if (status is not ("normalized" or "unclassified" or "rejected") ||
            (status == "rejected" && code != "pipeline.rejected") ||
            (status == "unclassified" && code != "runtime.unclassified") ||
            (code == "runtime.unclassified" && status != "unclassified")) return null;
        if (!input.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object) return null;
        // 只给共享生成策略它所拥有的字段；它负责属性、关联键、固定说明和分类规则。
        var payload = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var key in new[] { "level", "diagnostic", "eventCode", "sourceCategory", "properties", "traceId", "spanId", "requestId", "operationId" })
            if (input.TryGetProperty(key, out var value)) payload.Add(key, value);
        if (fatal && payload.ContainsKey("requestId")) return null;
        var policy = new RuntimeLogPolicy(new RuntimeLogSource(deployment, service, instance, release), mode,
            diagnostics: mode == "Development");
        var normalized = policy.Create(JsonSerializer.SerializeToElement(payload));
        if (normalized == null || normalized.Redacted || Text(input, "sourceCategory") != normalized.SourceCategory ||
            Text(input, "message") != normalized.Message || Text(input, "messageTemplate") != normalized.MessageTemplate) return null;
        rejectionReason = string.Empty;
        return normalized with
        {
            EventId = id, OccurredAtUtc = Text(input, "occurredAtUtc")!, ObservedAtUtc = Text(input, "observedAtUtc")!,
            ContainerId = container, IsFatal = fatal, NormalizationStatus = status, Redacted = redacted, Truncated = truncated
        };
    }

    public static bool TryReadUtc(string? text, out DateTime utc) => DateTime.TryParseExact(text, UtcFormat,
        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out utc) && utc.Year >= 1970;

    private static bool ReadBoolean(JsonElement input, string name, out bool value)
    {
        value = false;
        if (!input.TryGetProperty(name, out var field) || field.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
        value = field.GetBoolean();
        return true;
    }
}
