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

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RequiredColumnsByTable =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["WikiDocument"] = ["Visibility", "AllowedRoles", "AllowedPermissions"]
        };

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

        var requiredTables = DbMigrateEntityRegistry.GetTableNamesForConfig(mainDbConnId);
        var missingTables = requiredTables
            .Where(tableName => !probeDb.DbMaintenance.IsAnyTable(tableName, false))
            .ToList();

        var missingColumns = InspectMissingColumns(probeDb, missingTables);
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

    private static IReadOnlyList<string> InspectMissingColumns(ISqlSugarClient db, IReadOnlyCollection<string> missingTables)
    {
        if (missingTables.Count == 0 && RequiredColumnsByTable.Count == 0)
        {
            return Array.Empty<string>();
        }

        var missingTableSet = missingTables.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingColumns = new List<string>();

        foreach (var (tableName, requiredColumns) in RequiredColumnsByTable)
        {
            if (missingTableSet.Contains(tableName) || !db.DbMaintenance.IsAnyTable(tableName, false))
            {
                continue;
            }

            var existingColumns = GetSqliteTableColumns(db, tableName);
            foreach (var columnName in requiredColumns)
            {
                if (!existingColumns.Contains(columnName))
                {
                    missingColumns.Add($"{tableName}.{columnName}");
                }
            }
        }

        return missingColumns;
    }

    private static HashSet<string> GetSqliteTableColumns(ISqlSugarClient db, string tableName)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var schemaTable = db.Ado.GetDataTable($"PRAGMA table_info({QuoteIdentifier(tableName)})");

        foreach (System.Data.DataRow row in schemaTable.Rows)
        {
            var columnName = row["name"]?.ToString();
            if (!string.IsNullOrWhiteSpace(columnName))
            {
                result.Add(columnName);
            }
        }

        return result;
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
