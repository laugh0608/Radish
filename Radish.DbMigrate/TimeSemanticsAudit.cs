using System.Linq.Expressions;
using Radish.Common.TimeTool;
using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

public static class TimeSemanticsAudit
{
    private const int AuditPageSize = 1000;
    private const int WarningSampleLimit = 10;

    public static TimeSemanticsAuditResult Inspect(ISqlSugarClient db, BusinessCalendar businessCalendar)
    {
        var summaries = new List<string>();
        var warnings = new List<string>();

        InspectUtcBusinessDateColumn<ExpTransaction>(
            db,
            businessCalendar,
            nameof(ExpTransaction.CreatedDate),
            entity => entity.Id,
            entity => entity.Id,
            entity => entity.CreatedDate,
            summaries,
            warnings);
        InspectUtcBusinessDateColumn<UserExpDailyStats>(
            db,
            businessCalendar,
            nameof(UserExpDailyStats.StatDate),
            entity => entity.Id,
            entity => entity.Id,
            entity => entity.StatDate,
            summaries,
            warnings);
        InspectDatabaseDateColumn<CommentHighlight>(
            db,
            nameof(CommentHighlight.StatDate),
            entity => entity.Id,
            entity => entity.Id,
            entity => entity.StatDate,
            summaries,
            warnings);
        InspectDatabaseDateColumn<UserExperienceGovernanceAction>(
            db,
            nameof(UserExperienceGovernanceAction.StatDate),
            entity => entity.Id,
            entity => entity.Id,
            entity => entity.StatDate,
            summaries,
            warnings);

        return new TimeSemanticsAuditResult(summaries, warnings);
    }

    private static void InspectUtcBusinessDateColumn<TEntity>(
        ISqlSugarClient db,
        BusinessCalendar businessCalendar,
        string columnName,
        Expression<Func<TEntity, object>> orderById,
        Func<TEntity, long> idSelector,
        Func<TEntity, DateTime> valueSelector,
        List<string> summaries,
        List<string> warnings)
        where TEntity : class, new()
    {
        var tableName = db.EntityMaintenance.GetEntityInfo<TEntity>().DbTableName;
        var physicalColumn = DatabaseIdentifierResolver.ResolveColumn(db, tableName, columnName);
        if (physicalColumn == null)
        {
            return;
        }

        var dataType = physicalColumn.DataType;
        var usesDatabaseDate = IsDatabaseDateType(dataType);
        if (db.CurrentConnectionConfig.DbType == DbType.PostgreSQL &&
            IsPostgreSqlTimestampWithTimeZone(dataType))
        {
            InspectPostgreSqlTimestampColumn(
                db,
                businessCalendar,
                physicalColumn.TableName,
                physicalColumn.ColumnName,
                usesBusinessTimeZone: true,
                summaries,
                warnings);
            return;
        }

        var totalCount = db.Queryable<TEntity>().Count();
        var invalidCount = 0;
        var invalidIds = new List<long>(WarningSampleLimit);
        for (var pageIndex = 1; pageIndex * AuditPageSize <= totalCount + AuditPageSize; pageIndex++)
        {
            var entities = db.Queryable<TEntity>()
                .OrderBy(orderById)
                .ToPageList(pageIndex, AuditPageSize);
            foreach (var entity in entities)
            {
                var value = valueSelector(entity);
                var isValid = usesDatabaseDate
                    ? value.TimeOfDay == TimeSpan.Zero
                    : IsBusinessDateStart(value, businessCalendar);
                if (isValid)
                {
                    continue;
                }

                invalidCount++;
                if (invalidIds.Count < WarningSampleLimit)
                {
                    invalidIds.Add(idSelector(entity));
                }
            }

            if (entities.Count < AuditPageSize)
            {
                break;
            }
        }

        var storageContract = usesDatabaseDate ? "date 自然日" : "datetime 业务日 UTC 起点（待迁移 date）";
        summaries.Add(
            $"{tableName}.{columnName}: 类型 {dataType}，语义 {storageContract}，{totalCount} 行，异常 {invalidCount} 行");

        if (invalidCount > 0)
        {
            warnings.Add(
                usesDatabaseDate
                    ? $"{tableName}.{columnName} 作为数据库 date 字段仍含时间分量，记录 ID 样本: {string.Join(", ", invalidIds)}。"
                    : $"{tableName}.{columnName} 存在非业务日 UTC 起点值，记录 ID 样本: {string.Join(", ", invalidIds)}；禁止自动平移，请先确认 legacy 时区。");
        }
    }

    private static void InspectDatabaseDateColumn<TEntity>(
        ISqlSugarClient db,
        string columnName,
        Expression<Func<TEntity, object>> orderById,
        Func<TEntity, long> idSelector,
        Func<TEntity, DateTime?> valueSelector,
        List<string> summaries,
        List<string> warnings)
        where TEntity : class, new()
    {
        var tableName = db.EntityMaintenance.GetEntityInfo<TEntity>().DbTableName;
        var physicalColumn = DatabaseIdentifierResolver.ResolveColumn(db, tableName, columnName);
        if (physicalColumn == null)
        {
            return;
        }

        var dataType = physicalColumn.DataType;
        if (db.CurrentConnectionConfig.DbType == DbType.PostgreSQL &&
            IsPostgreSqlTimestampWithTimeZone(dataType))
        {
            InspectPostgreSqlTimestampColumn(
                db,
                null,
                physicalColumn.TableName,
                physicalColumn.ColumnName,
                usesBusinessTimeZone: false,
                summaries,
                warnings);
            return;
        }

        var totalCount = db.Queryable<TEntity>().Count();
        var invalidCount = 0;
        var invalidIds = new List<long>(WarningSampleLimit);
        for (var pageIndex = 1; pageIndex * AuditPageSize <= totalCount + AuditPageSize; pageIndex++)
        {
            var entities = db.Queryable<TEntity>()
                .OrderBy(orderById)
                .ToPageList(pageIndex, AuditPageSize);
            foreach (var entity in entities)
            {
                var value = valueSelector(entity);
                if (!value.HasValue || value.Value.TimeOfDay == TimeSpan.Zero)
                {
                    continue;
                }

                invalidCount++;
                if (invalidIds.Count < WarningSampleLimit)
                {
                    invalidIds.Add(idSelector(entity));
                }
            }

            if (entities.Count < AuditPageSize)
            {
                break;
            }
        }

        var storageContract = IsDatabaseDateType(dataType) ? "date 自然日" : "datetime 自然日兼容态（待迁移 date）";
        summaries.Add(
            $"{tableName}.{columnName}: 类型 {dataType}，语义 {storageContract}，{totalCount} 行，非午夜异常 {invalidCount} 行");

        if (invalidCount > 0)
        {
            warnings.Add(
                $"{tableName}.{columnName} 作为数据库 date 字段仍含时间分量，记录 ID 样本: {string.Join(", ", invalidIds)}。");
        }
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
        var (startUtc, _) = businessCalendar.GetUtcRange(businessDate);
        return utcValue == startUtc;
    }

    private static bool IsDatabaseDateType(string dataType)
    {
        return string.Equals(dataType, "date", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPostgreSqlTimestampWithTimeZone(string dataType)
    {
        return string.Equals(dataType, "timestamp with time zone", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(dataType, "timestamptz", StringComparison.OrdinalIgnoreCase);
    }

    private static void InspectPostgreSqlTimestampColumn(
        ISqlSugarClient db,
        BusinessCalendar? businessCalendar,
        string tableName,
        string columnName,
        bool usesBusinessTimeZone,
        List<string> summaries,
        List<string> warnings)
    {
        var timeZoneId = usesBusinessTimeZone
            ? ResolvePostgreSqlTimeZoneId(businessCalendar!.TimeZone)
            : "UTC";
        var totalCount = Convert.ToInt32(db.Ado.GetScalar(
            $"SELECT COUNT(*) FROM {QuoteIdentifier(tableName)}"));
        var invalidPredicate =
            $"{QuoteIdentifier(columnName)} IS NOT NULL AND " +
            $"timezone({QuoteLiteral(timeZoneId)}, {QuoteIdentifier(columnName)})::time <> TIME '00:00:00'";
        var invalidCount = Convert.ToInt32(db.Ado.GetScalar(
            $"SELECT COUNT(*) FROM {QuoteIdentifier(tableName)} WHERE {invalidPredicate}"));
        var invalidRows = db.Ado.GetDataTable(
            $"""
            SELECT {QuoteIdentifier("Id")}
            FROM {QuoteIdentifier(tableName)}
            WHERE {invalidPredicate}
            ORDER BY {QuoteIdentifier("Id")}
            LIMIT {WarningSampleLimit}
            """);
        var invalidIds = invalidRows.Rows.Cast<System.Data.DataRow>()
            .Select(row => Convert.ToInt64(row[0]))
            .ToList();

        var semantic = usesBusinessTimeZone
            ? "timestamp 业务日 UTC 起点（待迁移 date）"
            : "timestamp 自然日 UTC 午夜（待迁移 date）";
        summaries.Add(
            $"{tableName}.{columnName}: 类型 timestamp with time zone，语义 {semantic}，" +
            $"{totalCount} 行，异常 {invalidCount} 行");
        if (invalidCount > 0)
        {
            warnings.Add(
                $"{tableName}.{columnName} 存在不符合" +
                $"{(usesBusinessTimeZone ? "业务时区午夜" : "UTC 自然日午夜")}" +
                $"的记录，ID 样本: {string.Join(", ", invalidIds)}。");
        }
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
}

public sealed record TimeSemanticsAuditResult(
    IReadOnlyList<string> Summaries,
    IReadOnlyList<string> Warnings);
