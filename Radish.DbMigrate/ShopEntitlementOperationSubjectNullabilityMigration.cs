using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>放宽通用权益流水中仅适用于消耗品的主体字段。</summary>
internal sealed class ShopEntitlementOperationSubjectNullabilityMigration : ISchemaMigration
{
    private const string TableName = "ShopEntitlementOperation";
    private static readonly string[] NullableSubjectColumns =
    [
        "InventoryId",
        "ConsumableType",
        "Quantity",
        "ItemValue"
    ];
    private static readonly string[] RequiredIndexes =
    [
        "idx_shop_entitlement_operation_idempotency",
        "idx_shop_entitlement_operation_user_time",
        "idx_shop_entitlement_operation_inventory_time",
        "idx_shop_entitlement_operation_benefit_time"
    ];

    public static ShopEntitlementOperationSubjectNullabilityMigration Instance { get; } = new();

    public string MigrationId => "20260714_001_shop_entitlement_operation_subject_nullability";

    public string Scope => "Main";

    public string Description => "放宽通用权益流水的消耗品专属字段可空约束";

    public string ChecksumSource =>
        "20260714_001_shop_entitlement_operation_subject_nullability|Main|" +
        "InventoryId,ConsumableType,Quantity,ItemValue-nullable|" +
        "sqlite-rebuild-preserve-schema-v1|postgres-drop-not-null-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (!db.DbMaintenance.IsAnyTable(TableName, false))
        {
            throw new InvalidOperationException($"{TableName} 表不存在，无法迁移主体字段可空约束。");
        }

        switch (db.CurrentConnectionConfig.DbType)
        {
            case SqlSugar.DbType.Sqlite:
                RebuildSqliteTable(db);
                break;
            case SqlSugar.DbType.PostgreSQL:
                DropPostgreSqlNotNullConstraints(db);
                break;
            default:
                throw new InvalidOperationException(
                    $"{MigrationId} 不支持数据库类型 {db.CurrentConnectionConfig.DbType}。");
        }
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (!db.DbMaintenance.IsAnyTable(TableName, false))
        {
            return [$"{TableName} 表不存在。"];
        }

        var issues = new List<string>();
        foreach (var configuredColumnName in NullableSubjectColumns)
        {
            var column = DatabaseIdentifierResolver.ResolveColumn(db, TableName, configuredColumnName);
            if (column == null)
            {
                issues.Add($"{TableName}.{configuredColumnName} 列不存在。");
                continue;
            }

            if (!IsColumnNullable(db, column))
            {
                issues.Add($"{TableName}.{configuredColumnName} 仍为 NOT NULL，权益流水无法留空。");
            }
        }

        foreach (var indexName in RequiredIndexes)
        {
            if (!db.DbMaintenance.IsAnyIndex(indexName))
            {
                issues.Add($"{TableName} 缺少索引 {indexName}。");
            }
        }

        return issues;
    }

    private static void RebuildSqliteTable(ISqlSugarClient db)
    {
        var columns = NullableSubjectColumns
            .Select(columnName => DatabaseIdentifierResolver.ResolveColumn(db, TableName, columnName)
                                  ?? throw new InvalidOperationException($"{TableName}.{columnName} 列不存在。"))
            .ToList();
        var columnsToChange = columns
            .Where(column => IsSqliteColumnNotNull(db, column))
            .ToList();
        if (columnsToChange.Count == 0)
        {
            return;
        }

        var physicalTableName = columns[0].TableName;
        var createSql = db.Ado.GetString(
            "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = @tableName",
            new SugarParameter("@tableName", physicalTableName));
        if (string.IsNullOrWhiteSpace(createSql))
        {
            throw new InvalidOperationException($"未读取到 SQLite 表 {physicalTableName} 的 CREATE SQL。");
        }

        var nullableCreateSql = columnsToChange.Aggregate(
            createSql,
            (current, column) => RemoveSqliteNotNullConstraint(current, column.ColumnName));
        var temporaryTableName = $"__radish_{physicalTableName}_nullable_subject";
        var temporaryCreateSql = ReplaceSqliteCreateTableName(
            nullableCreateSql,
            physicalTableName,
            temporaryTableName);
        var indexScripts = ReadSqliteSchemaScripts(db, physicalTableName, "index");
        var triggerScripts = ReadSqliteSchemaScripts(db, physicalTableName, "trigger");
        var tableInfo = db.Ado.GetDataTable($"PRAGMA table_info({QuoteIdentifier(physicalTableName)})");
        var columnNames = tableInfo.Rows.Cast<DataRow>()
            .Select(row => Convert.ToString(row["name"], CultureInfo.InvariantCulture))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToList();
        var quotedColumns = string.Join(", ", columnNames.Select(QuoteIdentifier));

        db.Ado.ExecuteCommand($"DROP TABLE IF EXISTS {QuoteIdentifier(temporaryTableName)}");
        db.Ado.ExecuteCommand(temporaryCreateSql);
        db.Ado.ExecuteCommand(
            $"INSERT INTO {QuoteIdentifier(temporaryTableName)} ({quotedColumns}) " +
            $"SELECT {quotedColumns} FROM {QuoteIdentifier(physicalTableName)}");
        db.Ado.ExecuteCommand($"DROP TABLE {QuoteIdentifier(physicalTableName)}");
        db.Ado.ExecuteCommand(
            $"ALTER TABLE {QuoteIdentifier(temporaryTableName)} " +
            $"RENAME TO {QuoteIdentifier(physicalTableName)}");

        foreach (var script in indexScripts.Concat(triggerScripts))
        {
            db.Ado.ExecuteCommand(script);
        }
    }

    private static void DropPostgreSqlNotNullConstraints(ISqlSugarClient db)
    {
        foreach (var configuredColumnName in NullableSubjectColumns)
        {
            var column = DatabaseIdentifierResolver.ResolveColumn(db, TableName, configuredColumnName)
                         ?? throw new InvalidOperationException($"{TableName}.{configuredColumnName} 列不存在。");
            if (IsPostgreSqlColumnNullable(db, column))
            {
                continue;
            }

            db.Ado.ExecuteCommand(
                $"ALTER TABLE {QuoteIdentifier(column.TableName)} " +
                $"ALTER COLUMN {QuoteIdentifier(column.ColumnName)} DROP NOT NULL");
        }
    }

    private static bool IsColumnNullable(ISqlSugarClient db, DatabaseColumnReference column)
    {
        return db.CurrentConnectionConfig.DbType switch
        {
            SqlSugar.DbType.Sqlite => !IsSqliteColumnNotNull(db, column),
            SqlSugar.DbType.PostgreSQL => IsPostgreSqlColumnNullable(db, column),
            _ => false
        };
    }

    private static bool IsSqliteColumnNotNull(ISqlSugarClient db, DatabaseColumnReference column)
    {
        var tableInfo = db.Ado.GetDataTable($"PRAGMA table_info({QuoteIdentifier(column.TableName)})");
        var row = tableInfo.Rows.Cast<DataRow>().FirstOrDefault(item =>
            string.Equals(
                Convert.ToString(item["name"], CultureInfo.InvariantCulture),
                column.ColumnName,
                StringComparison.OrdinalIgnoreCase));
        if (row == null)
        {
            throw new InvalidOperationException($"{column.TableName}.{column.ColumnName} 列不存在。");
        }

        return Convert.ToInt32(row["notnull"], CultureInfo.InvariantCulture) == 1;
    }

    private static bool IsPostgreSqlColumnNullable(ISqlSugarClient db, DatabaseColumnReference column)
    {
        var isNullable = db.Ado.GetString(
            "SELECT is_nullable FROM information_schema.columns " +
            "WHERE table_schema = current_schema() " +
            "AND lower(table_name) = lower(@tableName) " +
            "AND lower(column_name) = lower(@columnName) LIMIT 1",
            new SugarParameter("@tableName", column.TableName),
            new SugarParameter("@columnName", column.ColumnName));
        if (string.IsNullOrWhiteSpace(isNullable))
        {
            throw new InvalidOperationException($"{column.TableName}.{column.ColumnName} 列不存在。");
        }

        return string.Equals(isNullable, "YES", StringComparison.OrdinalIgnoreCase);
    }

    private static string RemoveSqliteNotNullConstraint(string createSql, string columnName)
    {
        var escapedColumnName = Regex.Escape(columnName);
        var columnToken = $"(?:\"{escapedColumnName}\"|`{escapedColumnName}`|\\[{escapedColumnName}\\]|{escapedColumnName})";
        var pattern =
            $"(?<prefix>{columnToken}\\s+[A-Za-z][A-Za-z0-9_]*(?:\\s*\\([^)]*\\))?)\\s+NOT\\s+NULL";
        var updated = Regex.Replace(
            createSql,
            pattern,
            "${prefix}",
            RegexOptions.IgnoreCase,
            TimeSpan.FromSeconds(1));
        if (string.Equals(updated, createSql, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"无法在 SQLite CREATE SQL 中移除 {TableName}.{columnName} 的 NOT NULL 约束。");
        }

        return updated;
    }

    private static string ReplaceSqliteCreateTableName(
        string createSql,
        string expectedTableName,
        string temporaryTableName)
    {
        const string pattern =
            """^\s*CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?(?<table>"(?:[^"]|"")*"|`[^`]*`|\[[^\]]*\]|[A-Za-z_][A-Za-z0-9_]*)""";
        var match = Regex.Match(
            createSql,
            pattern,
            RegexOptions.IgnoreCase,
            TimeSpan.FromSeconds(1));
        if (!match.Success)
        {
            throw new InvalidOperationException($"无法在 SQLite CREATE SQL 中定位表 {expectedTableName}。");
        }

        var rawTableToken = match.Groups["table"].Value;
        var resolvedTableName = rawTableToken.Trim('"', '`', '[', ']');
        if (!string.Equals(resolvedTableName, expectedTableName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"SQLite CREATE SQL 表名异常：期望 {expectedTableName}，实际 {resolvedTableName}。");
        }

        return string.Concat(
            createSql.AsSpan(0, match.Groups["table"].Index),
            QuoteIdentifier(temporaryTableName),
            createSql.AsSpan(match.Groups["table"].Index + match.Groups["table"].Length));
    }

    private static IReadOnlyList<string> ReadSqliteSchemaScripts(
        ISqlSugarClient db,
        string tableName,
        string objectType)
    {
        var scripts = db.Ado.GetDataTable(
            "SELECT sql FROM sqlite_master " +
            "WHERE type = @objectType AND tbl_name = @tableName AND sql IS NOT NULL",
            new SugarParameter("@objectType", objectType),
            new SugarParameter("@tableName", tableName));
        return scripts.Rows.Cast<DataRow>()
            .Select(row => Convert.ToString(row["sql"], CultureInfo.InvariantCulture))
            .Where(script => !string.IsNullOrWhiteSpace(script))
            .Select(script => script!)
            .ToList();
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
