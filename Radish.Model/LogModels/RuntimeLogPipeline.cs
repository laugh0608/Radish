using SqlSugar;

namespace Radish.Model.LogModels;

[Tenant("Log"), SugarTable("runtime_log_pipeline")]
public sealed class RuntimeLogPipeline
{
    [SugarColumn(ColumnName = "deployment_id", IsPrimaryKey = true, Length = 64)]
    public string DeploymentId { get; set; } = "";
    [SugarColumn(ColumnName = "alert_id", Length = 64, IsNullable = true)]
    public string? AlertId { get; set; }
    [SugarColumn(ColumnName = "sample", ColumnDataType = "text", IsNullable = true)]
    public string? Sample { get; set; }
    [SugarColumn(ColumnName = "fault_sample", ColumnDataType = "text", IsNullable = true)]
    public string? FaultSample { get; set; }
    [SugarColumn(ColumnName = "state", Length = 20)]
    public string State { get; set; } = "Unknown";
}
