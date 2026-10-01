using System.Text.Json;
using System.Text.Json.Serialization;

namespace Radish.Common.LogTool;

/// <summary>传输契约；不是业务实体或对外 API Vo。仅由规范化入口生成。</summary>
public sealed record RuntimeLogEvent
{
    public int SchemaVersion { get; init; } = 1;
    public required string EventId { get; init; }
    public required string EventCode { get; init; }
    public required string OccurredAtUtc { get; init; }
    public required string ObservedAtUtc { get; init; }
    public required string DeploymentId { get; init; }
    public required string Service { get; init; }
    public required string InstanceId { get; init; }
    public required string Release { get; init; }
    public required string Mode { get; init; }
    public required string SourceCategory { get; init; }
    public required string Level { get; init; }
    public bool Diagnostic { get; init; }
    public bool IsFatal { get; init; }
    public required string MessageTemplate { get; init; }
    public required string Message { get; init; }
    public required IReadOnlyDictionary<string, object> Properties { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public string? OperationId { get; init; }
    public required string NormalizationStatus { get; init; }
    public bool Redacted { get; init; }
    public bool Truncated { get; init; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ToJsonLine()
    {
        var json = JsonSerializer.Serialize(this, JsonOptions);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > RuntimeLogPolicy.MaxEventBytes)
            throw new InvalidOperationException("Runtime log event exceeds the transport byte budget.");
        return json;
    }
}

/// <summary>只从宿主受信配置创建，不从请求属性或日志载荷绑定。</summary>
public sealed record RuntimeLogSource(string DeploymentId, string Service, string InstanceId, string Release);
