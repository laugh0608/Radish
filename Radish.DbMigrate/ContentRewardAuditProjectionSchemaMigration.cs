using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>为内容赞赏的 Log 审计投影建立幂等来源键。</summary>
internal sealed class ContentRewardAuditProjectionSchemaMigration : ISchemaMigration
{
    private const string TablePrefix = "BalanceChangeLog_";
    private const string SourceEventIndex = "idx_balance_change_source_event";

    public static ContentRewardAuditProjectionSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260727_014_content_reward_audit_projection";

    public string Scope => "Log";

    public string Description => "为余额变动日志增加可靠投影来源幂等键";

    public string ChecksumSource =>
        "20260727_014_content_reward_audit_projection|Log|" +
        "BalanceChangeLog.SourceEventKey-nullable-v1|tenant-source-event-unique-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        db.CodeFirst.InitTables<BalanceChangeLog>();
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var splitTables = GetBalanceChangeLogTables(db);
        if (splitTables.Count == 0)
        {
            return ["缺少当前 BalanceChangeLog 分表。"];
        }

        var managedTables = splitTables
            .Where(tableName => DatabaseIdentifierResolver.ResolveColumn(
                db,
                tableName,
                nameof(BalanceChangeLog.SourceEventKey)) != null)
            .ToList();
        if (managedTables.Count == 0)
        {
            return [$"BalanceChangeLog 分表缺少 {nameof(BalanceChangeLog.SourceEventKey)} 列。"];
        }

        var issues = new List<string>();
        foreach (var tableName in managedTables)
        {
            var indexName = GetPhysicalIndexName(tableName);
            if (!IndexExists(db, tableName, indexName))
            {
                issues.Add($"{tableName} 缺少索引 {indexName}。");
            }
        }

        return issues;
    }

    private static List<string> GetBalanceChangeLogTables(ISqlSugarClient db)
    {
        return db.DbMaintenance.GetTableInfoList(false)
            .Select(table => table.Name)
            .Where(tableName => tableName.StartsWith(TablePrefix, StringComparison.OrdinalIgnoreCase))
            .Where(tableName => tableName.Length > TablePrefix.Length)
            .Where(tableName => tableName[TablePrefix.Length..].All(char.IsDigit))
            .OrderBy(tableName => tableName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IndexExists(ISqlSugarClient db, string tableName, string indexName)
    {
        if (db.CurrentConnectionConfig.DbType != DbType.PostgreSQL)
        {
            return db.DbMaintenance.IsAnyIndex(indexName);
        }

        return db.DbMaintenance.GetIndexList(tableName)
            .Any(index => string.Equals(index, indexName, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetPhysicalIndexName(string tableName) =>
        $"{SourceEventIndex}{tableName}";
}
