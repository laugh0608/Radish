using SqlSugar;

namespace Radish.Model.LogModels;

[Tenant("Log"), SugarTable("runtime_log_alert_action")]
public sealed class RuntimeLogAlertAction
{
    [SugarColumn(ColumnName = "id", IsPrimaryKey = true, Length = 80)]
    public string Id { get; set; } = "";
    [SugarColumn(ColumnName = "alert_id", Length = 64)]
    public string AlertId { get; set; } = "";
    [SugarColumn(ColumnName = "actor_id", Length = 24)]
    public string ActorId { get; set; } = "";
    [SugarColumn(ColumnName = "action", Length = 24)]
    public string Action { get; set; } = "";
    [SugarColumn(ColumnName = "at_utc")]
    public DateTime AtUtc { get; set; }
}
