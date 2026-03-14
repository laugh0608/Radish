using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using Radish.Common.DbTool;

namespace Radish.DbMigrate;

internal static class DbMigrateInspection
{
    internal sealed record SeedInspectionResult(
        IReadOnlyList<string> MissingTables,
        bool DatabaseFileMissing,
        string? DatabaseFilePath)
    {
        public bool IsReadyForSeed => !DatabaseFileMissing && MissingTables.Count == 0;
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

        if (mainConfig?.DbType == DbType.Sqlite)
        {
            var databaseFilePath = ExtractSqliteFilePath(mainConfig.ConnectionString);
            if (!string.IsNullOrWhiteSpace(databaseFilePath) && !File.Exists(databaseFilePath))
            {
                return new SeedInspectionResult(Array.Empty<string>(), true, databaseFilePath);
            }
        }

        var requiredTables = DbMigrateEntityRegistry.GetTableNamesForConfig(mainDbConnId);
        var missingTables = requiredTables
            .Where(tableName => !probeDb.DbMaintenance.IsAnyTable(tableName, false))
            .ToList();

        return new SeedInspectionResult(missingTables, false, null);
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
}
