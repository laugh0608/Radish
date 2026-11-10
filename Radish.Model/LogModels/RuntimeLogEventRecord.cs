using SqlSugar;

namespace Radish.Model.LogModels;

/// <summary>Log 库运行事件；不继承业务软删除或历史分表模型。</summary>
[Tenant("Log")]
[SugarTable("runtime_log_event")]
[SugarIndex("ux_runtime_log_identity", nameof(DeploymentId), OrderByType.Asc, nameof(EventId), OrderByType.Asc, IsUnique = true)]
[SugarIndex("ix_runtime_log_time", nameof(OccurredAtUtc), OrderByType.Asc, nameof(EventId), OrderByType.Asc)]
[SugarIndex("ix_runtime_log_service_time", nameof(Service), OrderByType.Asc, nameof(OccurredAtUtc), OrderByType.Asc)]
[SugarIndex("ix_runtime_log_level_time", nameof(Level), OrderByType.Asc, nameof(OccurredAtUtc), OrderByType.Asc)]
[SugarIndex("ix_runtime_log_trace", nameof(TraceId), OrderByType.Asc)]
[SugarIndex("ix_runtime_log_operation", nameof(OperationId), OrderByType.Asc)]
public sealed class RuntimeLogEventRecord
{
    [SugarColumn(ColumnName = "id", IsPrimaryKey = true, Length = 36)]
    public string Id { get; set; } = string.Empty;
    [SugarColumn(ColumnName = "deployment_id", Length = 64)]
    public string DeploymentId { get; set; } = string.Empty;
    [SugarColumn(ColumnName = "event_id", Length = 36)]
    public string EventId { get; set; } = string.Empty;
    [SugarColumn(ColumnName = "event_code", Length = 128)]
    public string EventCode { get; set; } = string.Empty;
    [SugarColumn(ColumnName = "service", Length = 64)]
    public string Service { get; set; } = string.Empty;
    [SugarColumn(ColumnName = "level", Length = 8)]
    public string Level { get; set; } = string.Empty;
    [SugarColumn(ColumnName = "occurred_at_utc")]
    public DateTime OccurredAtUtc { get; set; }
    [SugarColumn(ColumnName = "observed_at_utc")]
    public DateTime ObservedAtUtc { get; set; }
    [SugarColumn(ColumnName = "ingested_at_utc")]
    public DateTime IngestedAtUtc { get; set; }
    [SugarColumn(ColumnName = "trace_id", Length = 32, IsNullable = true)]
    public string? TraceId { get; set; }
    [SugarColumn(ColumnName = "operation_id", Length = 36, IsNullable = true)]
    public string? OperationId { get; set; }
    [SugarColumn(ColumnName = "content_hash", Length = 64)]
    public string ContentHash { get; set; } = string.Empty;
    [SugarColumn(ColumnName = "canonical_json", ColumnDataType = "text")]
    public string CanonicalJson { get; set; } = string.Empty;
}
