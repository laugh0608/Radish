using Radish.Model.LogModels;
using SqlSugar;

namespace Radish.DbMigrate;

internal sealed class RuntimeLogConsoleSchemaMigration : ISchemaMigration
{
    public static RuntimeLogConsoleSchemaMigration Instance { get; } = new();
    public string MigrationId => "20261121_024_runtime_log_console";
    public string Scope => "Log";
    public string Description => "建立 Console 运维告警、处理轨迹和采集状态";
    public string ChecksumSource => "20261121_024_runtime_log_console|Log|alert-v1|alert-action-v1|pipeline-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        Create<RuntimeLogAlert>(db, "runtime_log_alert");
        Create<RuntimeLogAlertAction>(db, "runtime_log_alert_action");
        Create<RuntimeLogPipeline>(db, "runtime_log_pipeline");
    }

    private static void Create<T>(ISqlSugarClient db, string name) where T : class, new()
    {
        if (!db.DbMaintenance.IsAnyTable(name, false)) db.CodeFirst.InitTables<T>();
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        var issues = new List<string>();
        Verify<RuntimeLogAlert>(db, "runtime_log_alert", issues);
        Verify<RuntimeLogAlertAction>(db, "runtime_log_alert_action", issues);
        Verify<RuntimeLogPipeline>(db, "runtime_log_pipeline", issues);
        return issues;
    }

    private static void Verify<T>(ISqlSugarClient db, string table, List<string> issues)
    {
        if (!db.DbMaintenance.IsAnyTable(table, false))
        {
            issues.Add("Missing " + table);
            return;
        }
        var columns = db.DbMaintenance.GetColumnInfosByTableName(table, false);
        foreach (var expected in db.EntityMaintenance.GetEntityInfo<T>().Columns.Where(c => !c.IsIgnore))
        {
            if (!columns.Any(c => c.DbColumnName == expected.DbColumnName && c.IsPrimarykey == expected.IsPrimarykey &&
                c.IsNullable == expected.IsNullable))
                issues.Add("Invalid column " + table + "." + expected.DbColumnName);
        }
    }
}
