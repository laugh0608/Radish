using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Radish.Common.TimeTool;
using SqlSugar;

namespace Radish.DbMigrate;

internal sealed class ExperienceNaturalDateSchemaMigration : ISchemaMigration
{
    private static readonly NaturalDateColumn[] Columns =
    [
        new("ExpTransaction", "CreatedDate", false, true),
        new("UserExpDailyStats", "StatDate", false, true),
        new("UserExperienceGovernanceAction", "StatDate", true, false)
    ];

    public static ExperienceNaturalDateSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260712_001_experience_natural_dates";

    public string Scope => "Main";

    public string Description => "经验流水、每日统计与治理动作自然日列迁移为 date";

    public string ChecksumSource =>
        "20260712_001_experience_natural_dates|Main|ExpTransaction.CreatedDate," +
        "UserExpDailyStats.StatDate,UserExperienceGovernanceAction.StatDate|" +
        "validate-business-date-v1|sqlite-rebuild-v1|postgres-timezone-date-v2";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        var businessCalendar = services.GetRequiredService<BusinessCalendar>();
        var semanticIssues = InspectValueSemantics(db, businessCalendar, requireDateStorage: false);
        if (semanticIssues.Count > 0)
        {
            throw new InvalidOperationException(
                $"{MigrationId} 前置时间审计失败：{string.Join("；", semanticIssues)}");
        }

        switch (db.CurrentConnectionConfig.DbType)
        {
            case SqlSugar.DbType.Sqlite:
                NormalizeSqliteValues(db, businessCalendar);
                foreach (var column in Columns)
                {
                    RebuildSqliteTableWithDateColumn(db, column);
                }
                break;

            case SqlSugar.DbType.PostgreSQL:
                ApplyPostgreSqlColumns(db, businessCalendar);
                break;

            default:
                throw new NotSupportedException(
                    $"{MigrationId} 不支持数据库 {db.CurrentConnectionConfig.DbType}。");
        }
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        var issues = new List<string>();
        foreach (var column in Columns)
        {
            var dataType = GetColumnType(db, column.TableName, column.ColumnName);
            if (!string.Equals(dataType, "date", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add($"{column.TableName}.{column.ColumnName} 类型仍为 {dataType}，期望 date。");
            }
        }

        issues.AddRange(InspectValueSemantics(
            db,
            services.GetRequiredService<BusinessCalendar>(),
            requireDateStorage: true));
        return issues;
    }

    private static IReadOnlyList<string> InspectValueSemantics(
        ISqlSugarClient db,
        BusinessCalendar businessCalendar,
        bool requireDateStorage)
    {
        var issues = new List<string>();
        foreach (var column in Columns)
        {
            var dataType = GetColumnType(db, column.TableName, column.ColumnName);
            var usesDateStorage = string.Equals(dataType, "date", StringComparison.OrdinalIgnoreCase);
            if (requireDateStorage && !usesDateStorage)
            {
                continue;
            }

            if (db.CurrentConnectionConfig.DbType == SqlSugar.DbType.PostgreSQL && !usesDateStorage)
            {
                var invalidIds = ReadInvalidPostgreSqlIds(db, column, dataType, businessCalendar);
                if (invalidIds.Count > 0)
                {
                    issues.Add(
                        $"{column.TableName}.{column.ColumnName} 存在不符合" +
                        $"{(column.UsesBusinessDayStart ? "业务时区午夜" : "UTC 自然日午夜")}" +
                        $"的记录，ID 样本：{string.Join(", ", invalidIds)}。");
                }

                continue;
            }

            var rows = db.Ado.GetDataTable(
                $"SELECT {QuoteIdentifier("Id")}, {QuoteIdentifier(column.ColumnName)} FROM {QuoteIdentifier(column.TableName)}");
            var invalidSamples = new List<string>();
            foreach (DataRow row in rows.Rows)
            {
                if (column.IsNullable && row[1] == DBNull.Value)
                {
                    continue;
                }

                var value = ReadDateTime(row[1]);
                var isValid = usesDateStorage || !column.UsesBusinessDayStart
                    ? value.TimeOfDay == TimeSpan.Zero
                    : IsBusinessDateStart(value, businessCalendar);
                if (!isValid && invalidSamples.Count < 10)
                {
                    invalidSamples.Add(
                        $"{Convert.ToInt64(row[0], CultureInfo.InvariantCulture)}={value:O}({value.Kind})");
                }
            }

            if (invalidSamples.Count > 0)
            {
                issues.Add(
                    $"{column.TableName}.{column.ColumnName} 存在不符合" +
                    $"{(usesDateStorage ? "date 午夜" : column.UsesBusinessDayStart ? "业务日 UTC 起点" : "自然日午夜")}" +
                    $"的记录，ID=值(Kind) 样本：{string.Join(", ", invalidSamples)}。");
            }
        }

        return issues;
    }

    private static IReadOnlyList<long> ReadInvalidPostgreSqlIds(
        ISqlSugarClient db,
        NaturalDateColumn column,
        string dataType,
        BusinessCalendar businessCalendar)
    {
        var timeZoneId = column.UsesBusinessDayStart
            ? ResolvePostgreSqlTimeZoneId(businessCalendar.TimeZone)
            : "UTC";
        var localTimestampExpression = BuildPostgreSqlLocalTimestampExpression(
            column,
            dataType,
            timeZoneId);
        var rows = db.Ado.GetDataTable(
            $"""
            SELECT {QuoteIdentifier("Id")}
            FROM {QuoteIdentifier(column.TableName)}
            WHERE {QuoteIdentifier(column.ColumnName)} IS NOT NULL
              AND ({localTimestampExpression})::time <> TIME '00:00:00'
            ORDER BY {QuoteIdentifier("Id")}
            LIMIT 10
            """);
        return rows.Rows.Cast<DataRow>()
            .Select(row => Convert.ToInt64(row[0], CultureInfo.InvariantCulture))
            .ToList();
    }

    private static bool IsBusinessDateStart(DateTime value, BusinessCalendar businessCalendar)
    {
        var utcValue = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
        var businessDate = businessCalendar.GetDate(new DateTimeOffset(utcValue));
        return businessCalendar.GetUtcRange(businessDate).StartUtc == utcValue;
    }

    private static DateTime ReadDateTime(object value)
    {
        return value switch
        {
            DateTime dateTime => dateTime,
            DateTimeOffset dateTimeOffset => dateTimeOffset.UtcDateTime,
            DateOnly dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
            _ => Convert.ToDateTime(value, CultureInfo.InvariantCulture)
        };
    }

    private static void ApplyPostgreSqlColumns(ISqlSugarClient db, BusinessCalendar businessCalendar)
    {
        foreach (var column in Columns)
        {
            var dataType = GetColumnType(db, column.TableName, column.ColumnName);
            if (string.Equals(dataType, "date", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!IsPostgreSqlTimestampType(dataType))
            {
                throw new InvalidOperationException(
                    $"{column.TableName}.{column.ColumnName} PostgreSQL 类型为 {dataType}；" +
                    "仅允许从 timestamp with/without time zone 迁移为 date。");
            }

            var timeZoneId = column.UsesBusinessDayStart
                ? ResolvePostgreSqlTimeZoneId(businessCalendar.TimeZone)
                : "UTC";
            var localTimestampExpression = BuildPostgreSqlLocalTimestampExpression(
                column,
                dataType,
                timeZoneId);

            db.Ado.ExecuteCommand(
                $"""
                ALTER TABLE {QuoteIdentifier(column.TableName)}
                ALTER COLUMN {QuoteIdentifier(column.ColumnName)} TYPE date
                USING ({localTimestampExpression})::date
                """);
        }
    }

    private static string BuildPostgreSqlLocalTimestampExpression(
        NaturalDateColumn column,
        string dataType,
        string timeZoneId)
    {
        var columnIdentifier = QuoteIdentifier(column.ColumnName);
        return IsPostgreSqlTimestampWithoutTimeZone(dataType)
            ? $"timezone({QuoteLiteral(timeZoneId)}, {columnIdentifier} AT TIME ZONE 'UTC')"
            : $"timezone({QuoteLiteral(timeZoneId)}, {columnIdentifier})";
    }

    private static bool IsPostgreSqlTimestampType(string dataType)
    {
        return IsPostgreSqlTimestampWithoutTimeZone(dataType) ||
               string.Equals(dataType, "timestamp with time zone", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(dataType, "timestamptz", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPostgreSqlTimestampWithoutTimeZone(string dataType)
    {
        return string.Equals(dataType, "timestamp without time zone", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(dataType, "timestamp", StringComparison.OrdinalIgnoreCase);
    }

    private static void NormalizeSqliteValues(ISqlSugarClient db, BusinessCalendar businessCalendar)
    {
        foreach (var column in Columns)
        {
            if (string.Equals(
                    GetColumnType(db, column.TableName, column.ColumnName),
                    "date",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rows = db.Ado.GetDataTable(
                $"SELECT {QuoteIdentifier("Id")}, {QuoteIdentifier(column.ColumnName)} FROM {QuoteIdentifier(column.TableName)}");
            foreach (DataRow row in rows.Rows)
            {
                if (column.IsNullable && row[1] == DBNull.Value)
                {
                    continue;
                }

                var value = ReadDateTime(row[1]);
                var utcValue = value.Kind switch
                {
                    DateTimeKind.Utc => value,
                    DateTimeKind.Local => value.ToUniversalTime(),
                    _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
                };
                var businessDate = column.UsesBusinessDayStart
                    ? businessCalendar.GetDate(new DateTimeOffset(utcValue))
                    : DateOnly.FromDateTime(utcValue);
                var normalized = businessDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
                db.Ado.ExecuteCommand(
                    $"UPDATE {QuoteIdentifier(column.TableName)} " +
                    $"SET {QuoteIdentifier(column.ColumnName)} = @value WHERE {QuoteIdentifier("Id")} = @id",
                    new SugarParameter("@value", normalized),
                    new SugarParameter("@id", Convert.ToInt64(row[0], CultureInfo.InvariantCulture)));
            }
        }
    }

    private static void RebuildSqliteTableWithDateColumn(ISqlSugarClient db, NaturalDateColumn column)
    {
        var currentType = GetColumnType(db, column.TableName, column.ColumnName);
        if (string.Equals(currentType, "date", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var createSql = db.Ado.GetString(
            "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = @tableName",
            new SugarParameter("@tableName", column.TableName));
        if (string.IsNullOrWhiteSpace(createSql))
        {
            throw new InvalidOperationException($"未读取到 SQLite 表 {column.TableName} 的 CREATE SQL。");
        }

        var indexScripts = ReadSqliteSchemaScripts(db, column.TableName, "index");
        var triggerScripts = ReadSqliteSchemaScripts(db, column.TableName, "trigger");
        var tableInfo = db.Ado.GetDataTable($"PRAGMA table_info({QuoteIdentifier(column.TableName)})");
        var columnNames = tableInfo.Rows.Cast<DataRow>()
            .Select(row => Convert.ToString(row["name"], CultureInfo.InvariantCulture))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToList();

        var columnPattern =
            $"(?<prefix>{Regex.Escape(QuoteIdentifier(column.ColumnName))}\\s+){Regex.Escape(currentType)}\\b";
        var dateCreateSql = Regex.Replace(
            createSql,
            columnPattern,
            "${prefix}date",
            RegexOptions.IgnoreCase,
            TimeSpan.FromSeconds(1));
        if (string.Equals(dateCreateSql, createSql, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"无法在 SQLite CREATE SQL 中定位 {column.TableName}.{column.ColumnName} 的 {currentType} 类型声明。");
        }

        var temporaryTableName = $"__radish_{column.TableName}_{column.ColumnName}_date";
        var tableToken = QuoteIdentifier(column.TableName);
        var tableTokenIndex = dateCreateSql.IndexOf(tableToken, StringComparison.OrdinalIgnoreCase);
        if (tableTokenIndex < 0)
        {
            throw new InvalidOperationException($"无法在 SQLite CREATE SQL 中定位表 {column.TableName}。");
        }

        var temporaryCreateSql = string.Concat(
            dateCreateSql.AsSpan(0, tableTokenIndex),
            QuoteIdentifier(temporaryTableName),
            dateCreateSql.AsSpan(tableTokenIndex + tableToken.Length));
        var quotedColumns = string.Join(", ", columnNames.Select(QuoteIdentifier));

        db.Ado.ExecuteCommand($"DROP TABLE IF EXISTS {QuoteIdentifier(temporaryTableName)}");
        db.Ado.ExecuteCommand(temporaryCreateSql);
        db.Ado.ExecuteCommand(
            $"INSERT INTO {QuoteIdentifier(temporaryTableName)} ({quotedColumns}) " +
            $"SELECT {quotedColumns} FROM {QuoteIdentifier(column.TableName)}");
        db.Ado.ExecuteCommand($"DROP TABLE {QuoteIdentifier(column.TableName)}");
        db.Ado.ExecuteCommand(
            $"ALTER TABLE {QuoteIdentifier(temporaryTableName)} RENAME TO {QuoteIdentifier(column.TableName)}");

        foreach (var script in indexScripts.Concat(triggerScripts))
        {
            db.Ado.ExecuteCommand(script);
        }
    }

    private static IReadOnlyList<string> ReadSqliteSchemaScripts(
        ISqlSugarClient db,
        string tableName,
        string objectType)
    {
        var scripts = db.Ado.GetDataTable(
            "SELECT sql FROM sqlite_master WHERE type = @objectType AND tbl_name = @tableName AND sql IS NOT NULL",
            new SugarParameter("@objectType", objectType),
            new SugarParameter("@tableName", tableName));
        return scripts.Rows.Cast<DataRow>()
            .Select(row => Convert.ToString(row["sql"], CultureInfo.InvariantCulture))
            .Where(script => !string.IsNullOrWhiteSpace(script))
            .Select(script => script!)
            .ToList();
    }

    private static string GetColumnType(ISqlSugarClient db, string tableName, string columnName)
    {
        if (!db.DbMaintenance.IsAnyTable(tableName, false))
        {
            return "missing-table";
        }

        var column = db.DbMaintenance.GetColumnInfosByTableName(tableName, false)
            .FirstOrDefault(item => string.Equals(item.DbColumnName, columnName, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(column?.DataType)
            ? "missing-column"
            : column.DataType.Trim().ToLowerInvariant();
    }

    private static string ResolvePostgreSqlTimeZoneId(TimeZoneInfo timeZone)
    {
        if (string.Equals(timeZone.Id, "UTC", StringComparison.OrdinalIgnoreCase))
        {
            return "UTC";
        }

        return TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZone.Id, out var ianaId)
            ? ianaId
            : timeZone.Id;
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string QuoteLiteral(string value)
    {
        return $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    }

    private sealed record NaturalDateColumn(
        string TableName,
        string ColumnName,
        bool IsNullable,
        bool UsesBusinessDayStart);
}
