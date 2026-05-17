using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using Radish.Common.DbTool;

namespace Radish.DbMigrate;

internal static class DbMigrateInspection
{
    internal sealed record SeedInspectionResult(
        IReadOnlyList<string> MissingTables,
        IReadOnlyList<string> MissingColumns,
        bool DatabaseFileMissing,
        string? DatabaseFilePath)
    {
        public bool IsReadyForSeed => !DatabaseFileMissing && MissingTables.Count == 0 && MissingColumns.Count == 0;
    }

    public static SeedInspectionResult InspectSeedReadiness(IServiceProvider services, string? mainDbConnId)
    {
        var db = services.GetRequiredService<ISqlSugarClient>();
        var sqlSugarScope = db as SqlSugarScope;
        var probeDb = db;

        if (sqlSugarScope != null && !string.IsNullOrWhiteSpace(mainDbConnId))
        {
            probeDb = sqlSugarScope.GetConnectionScope(mainDbConnId.ToLowerInvariant());
        }

        var mainConfig = BaseDbConfig.AllConfigs.FirstOrDefault(config =>
            string.Equals(config.ConfigId?.ToString(), mainDbConnId, StringComparison.OrdinalIgnoreCase));

        if (mainConfig?.DbType == SqlSugar.DbType.Sqlite)
        {
            var databaseFilePath = ExtractSqliteFilePath(mainConfig.ConnectionString);
            if (!string.IsNullOrWhiteSpace(databaseFilePath) && !File.Exists(databaseFilePath))
            {
                return new SeedInspectionResult(Array.Empty<string>(), Array.Empty<string>(), true, databaseFilePath);
            }
        }

        var entityInfos = DbMigrateEntityRegistry.GetEntityTypesForConfig(mainDbConnId)
            .Select(probeDb.EntityMaintenance.GetEntityInfo)
            .ToList();

        var requiredTables = entityInfos
            .Select(entityInfo => entityInfo.DbTableName)
            .Where(tableName => !string.IsNullOrWhiteSpace(tableName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var missingTables = requiredTables
            .Where(tableName => !probeDb.DbMaintenance.IsAnyTable(tableName, false))
            .ToList();

        var missingColumns = InspectMissingColumns(probeDb, entityInfos, missingTables);
        return new SeedInspectionResult(missingTables, missingColumns, false, null);
    }

    public static string? ExtractSqliteFilePath(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        const string prefix = "DataSource=";
        return connectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? connectionString[prefix.Length..]
            : connectionString;
    }

    private static IReadOnlyList<string> InspectMissingColumns(
        ISqlSugarClient db,
        IReadOnlyCollection<EntityInfo> entityInfos,
        IReadOnlyCollection<string> missingTables)
    {
        if (entityInfos.Count == 0)
        {
            return Array.Empty<string>();
        }

        var missingTableSet = missingTables.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingColumnsByTable = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var missingColumns = new List<string>();

        foreach (var entityInfo in entityInfos)
        {
            var tableName = entityInfo.DbTableName;
            if (string.IsNullOrWhiteSpace(tableName) ||
                missingTableSet.Contains(tableName) ||
                !db.DbMaintenance.IsAnyTable(tableName, false))
            {
                continue;
            }

            if (!existingColumnsByTable.TryGetValue(tableName, out var existingColumns))
            {
                existingColumns = GetTableColumns(db, tableName);
                existingColumnsByTable[tableName] = existingColumns;
            }

            var expectedColumns = entityInfo.Columns
                .Where(column => !column.IsIgnore && !string.IsNullOrWhiteSpace(column.DbColumnName))
                .Select(column => column.DbColumnName)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var columnName in expectedColumns)
            {
                if (!existingColumns.Contains(columnName))
                {
                    missingColumns.Add($"{tableName}.{columnName}");
                }
            }
        }

        return missingColumns
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HashSet<string> GetTableColumns(ISqlSugarClient db, string tableName)
    {
        return db.DbMaintenance
            .GetColumnInfosByTableName(tableName, false)
            .Select(column => column.DbColumnName)
            .Where(columnName => !string.IsNullOrWhiteSpace(columnName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
