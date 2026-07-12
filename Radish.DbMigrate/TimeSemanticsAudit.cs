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
        if (!db.DbMaintenance.IsAnyTable(tableName, false))
        {
            return;
        }

        var dataType = GetColumnDataType<TEntity>(db, columnName);
        var usesDatabaseDate = IsDatabaseDateType(dataType);
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
        if (!db.DbMaintenance.IsAnyTable(tableName, false))
        {
            return;
        }

        var dataType = GetColumnDataType<TEntity>(db, columnName);
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

    private static string GetColumnDataType<TEntity>(ISqlSugarClient db, string columnName)
        where TEntity : class, new()
    {
        var tableName = db.EntityMaintenance.GetEntityInfo<TEntity>().DbTableName;
        var column = db.DbMaintenance
            .GetColumnInfosByTableName(tableName, false)
            .FirstOrDefault(item => string.Equals(item.DbColumnName, columnName, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(column?.DataType) ? "unknown" : column.DataType.Trim().ToLowerInvariant();
    }

    private static bool IsDatabaseDateType(string dataType)
    {
        return string.Equals(dataType, "date", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record TimeSemanticsAuditResult(
    IReadOnlyList<string> Summaries,
    IReadOnlyList<string> Warnings);
