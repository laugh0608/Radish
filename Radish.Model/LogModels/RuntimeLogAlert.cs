using SqlSugar;

namespace Radish.Model.LogModels;

[Tenant("Log"), SugarTable("runtime_log_alert")]
[SugarIndex("ix_runtime_alert_deployment", nameof(DeploymentId), OrderByType.Asc, nameof(OpenedUtc), OrderByType.Desc)]
public sealed class RuntimeLogAlert
{
    [SugarColumn(ColumnName = "id", IsPrimaryKey = true, Length = 64)]
    public string Id { get; set; } = "";
    [SugarColumn(ColumnName = "deployment_id", Length = 64)]
    public string DeploymentId { get; set; } = "";
    [SugarColumn(ColumnName = "service", Length = 64)]
    public string Service { get; set; } = "";
    [SugarColumn(ColumnName = "code", Length = 128)]
    public string Code { get; set; } = "";
    [SugarColumn(ColumnName = "event_id", Length = 36, IsNullable = true)]
    public string? EventId { get; set; }
    [SugarColumn(ColumnName = "opened_utc")]
    public DateTime OpenedUtc { get; set; }
    [SugarColumn(ColumnName = "updated_utc")]
    public DateTime UpdatedUtc { get; set; }
    [SugarColumn(ColumnName = "state", Length = 20)]
    public string State { get; set; } = "Open";
    [SugarColumn(ColumnName = "version")]
    public int Version { get; set; }
    [SugarColumn(ColumnName = "known_failures")]
    public long KnownFailures { get; set; }
}
