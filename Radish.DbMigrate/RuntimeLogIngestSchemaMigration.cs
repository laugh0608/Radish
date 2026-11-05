using Radish.Model.LogModels;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>运行事件及有限拒收日桶；仅 Log scope，已有结构漂移不得被 Code First 修补。</summary>
internal sealed class RuntimeLogIngestSchemaMigration : ISchemaMigration
{
    public static RuntimeLogIngestSchemaMigration Instance { get; } = new();
    public string MigrationId => "20261105_023_runtime_log_ingest";
    public string Scope => "Log";
    public string Description => "建立幂等运行事件与有界拒收摘要";
    public string ChecksumSource => "20261105_023_runtime_log_ingest|Log|runtime-log-event-v1|rejection-day-bucket-v1|indexes-v1";

    private static readonly (string Name, string Table, bool Unique, string Columns)[] Indexes =
    [
        ("ux_runtime_log_identity", "runtime_log_event", true, "deployment_id,event_id"),
        ("ix_runtime_log_time", "runtime_log_event", false, "occurred_at_utc,event_id"),
        ("ix_runtime_log_service_time", "runtime_log_event", false, "service,occurred_at_utc"),
        ("ix_runtime_log_level_time", "runtime_log_event", false, "level,occurred_at_utc"),
        ("ix_runtime_log_trace", "runtime_log_event", false, "trace_id"),
        ("ix_runtime_log_operation", "runtime_log_event", false, "operation_id"),
        ("ix_runtime_log_rejection_day", "runtime_log_rejection_bucket", false, "day_utc")
    ];

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        EnsureProvider(db);
        var issues = Inspect(db, requireTables: false);
        if (issues.Count != 0) throw new InvalidOperationException("Runtime log schema drift: " + string.Join("; ", issues));
        if (!Exists(db, "runtime_log_event")) db.CodeFirst.InitTables<RuntimeLogEventRecord>();
        if (!Exists(db, "runtime_log_rejection_bucket")) db.CodeFirst.InitTables<RuntimeLogRejectionBucket>();
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services) => Inspect(db, requireTables: true);

    private static List<string> Inspect(ISqlSugarClient db, bool requireTables)
    {
        EnsureProvider(db);
        var issues = new List<string>();
        InspectEntity<RuntimeLogEventRecord>(db, "runtime_log_event", requireTables, issues);
        InspectEntity<RuntimeLogRejectionBucket>(db, "runtime_log_rejection_bucket", requireTables, issues);
        foreach (var (name, table, unique, expectedColumns) in Indexes)
        {
            if (!Exists(db, table)) continue;
            if (db.CurrentConnectionConfig.DbType == DbType.Sqlite)
            {
                var index = db.Ado.SqlQuery<SqliteIndex>("SELECT name, [unique] AS IsUnique FROM pragma_index_list(@table)",
                    new SugarParameter("@table", table)).SingleOrDefault(i => i.Name == name);
                var columns = db.Ado.SqlQuery<SqliteIndexColumn>("SELECT name, seqno FROM pragma_index_info(@name)",
                    new SugarParameter("@name", name)).OrderBy(c => c.Seqno).Select(c => c.Name);
                if (index == null || (index.IsUnique != 0) != unique || string.Join(',', columns) != expectedColumns)
                    issues.Add("Invalid index " + name);
            }
            else
            {
                var rows = db.Ado.SqlQuery<PostgreSqlIndex>("""
                    SELECT ix.indisunique AS "IsUnique", string_agg(a.attname, ',' ORDER BY ord.n) AS "Columns"
                    FROM pg_index ix JOIN pg_class t ON t.oid=ix.indrelid
                    JOIN pg_namespace ns ON ns.oid=t.relnamespace JOIN pg_class i ON i.oid=ix.indexrelid
                    JOIN LATERAL unnest(ix.indkey) WITH ORDINALITY AS ord(attnum,n) ON true
                    JOIN pg_attribute a ON a.attrelid=t.oid AND a.attnum=ord.attnum
                    WHERE ns.nspname=current_schema() AND t.relname=@table AND i.relname=@name
                      AND ix.indisvalid AND ix.indpred IS NULL
                    GROUP BY ix.indisunique
                    """, new SugarParameter("@table", table), new SugarParameter("@name", name));
                if (rows.Count != 1 || rows[0].IsUnique != unique || rows[0].Columns != expectedColumns)
                    issues.Add("Invalid index " + name);
            }
        }
        return issues;
    }

    private static void InspectEntity<T>(ISqlSugarClient db, string table, bool required, List<string> issues)
    {
        if (!Exists(db, table)) { if (required) issues.Add("Missing table " + table); return; }
        var actual = db.DbMaintenance.GetColumnInfosByTableName(table, false);
        foreach (var expected in db.EntityMaintenance.GetEntityInfo<T>().Columns.Where(c => !c.IsIgnore))
        {
            var column = actual.SingleOrDefault(c => c.DbColumnName == expected.DbColumnName);
            if (column == null) { issues.Add("Missing column " + table + "." + expected.DbColumnName); continue; }
            var type = Nullable.GetUnderlyingType(expected.PropertyInfo.PropertyType) ?? expected.PropertyInfo.PropertyType;
            var storageType = column.DataType.ToLowerInvariant();
            var validType = type == typeof(DateTime) ? storageType is "datetime" or "timestamp" or "timestamp without time zone"
                : type == typeof(long) ? storageType is "bigint" or "integer"
                : storageType is "text" or "varchar" or "character varying" or "nvarchar";
            if (!validType || column.IsNullable != expected.IsNullable || column.IsPrimarykey != expected.IsPrimarykey ||
                (db.CurrentConnectionConfig.DbType == DbType.PostgreSQL && expected.Length > 0 && column.Length != expected.Length))
                issues.Add("Invalid column " + table + "." + expected.DbColumnName);
        }
    }

    private static bool Exists(ISqlSugarClient db, string table) => db.DbMaintenance.GetTableInfoList(false).Any(t => t.Name == table);
    private static void EnsureProvider(ISqlSugarClient db)
    {
        if (db.CurrentConnectionConfig.DbType is not (DbType.Sqlite or DbType.PostgreSQL))
            throw new NotSupportedException("Runtime log schema supports SQLite and PostgreSQL only.");
    }
    private sealed class SqliteIndex { public string Name { get; set; } = ""; public int IsUnique { get; set; } }
    private sealed class SqliteIndexColumn { public string Name { get; set; } = ""; public int Seqno { get; set; } }
    private sealed class PostgreSqlIndex { public bool IsUnique { get; set; } public string Columns { get; set; } = ""; }
}
