using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Radish.Common.TimeTool;
using SqlSugar;

namespace Radish.DbMigrate;

[SugarTable("RadishSchemaVersion")]
internal sealed class SchemaMigrationRecord
{
    [SugarColumn(IsPrimaryKey = true, Length = 100, IsNullable = false)]
    public string MigrationId { get; set; } = string.Empty;

    [SugarColumn(Length = 32, IsNullable = false)]
    public string Scope { get; set; } = string.Empty;

    [SugarColumn(Length = 300, IsNullable = false)]
    public string Description { get; set; } = string.Empty;

    [SugarColumn(Length = 64, IsNullable = false)]
    public string Checksum { get; set; } = string.Empty;

    [SugarColumn(IsNullable = false)]
    public DateTime AppliedAtUtc { get; set; }

    [SugarColumn(IsNullable = false)]
    public long DurationMs { get; set; }

    [SugarColumn(Length = 50, IsNullable = true)]
    public string? AppVersion { get; set; }
}

internal sealed record SchemaMigrationStatus(
    string Scope,
    string MigrationId,
    bool LedgerExists,
    bool Applied,
    bool ChecksumMatches,
    string Message);

internal static class SchemaMigrationLedger
{
    internal const string BaselineMigrationId = "20260712_000_baseline";
    private const string BaselineDescription = "Q2-B 接管 Q2-A 完成后的数据库结构基线";
    private const string BaselineChecksumSource =
        "20260712_000_baseline|Main,Log,Message,Chat|Q2-A-time-semantics-complete|schema-ledger-v1";

    private static readonly string BaselineChecksum = ComputeChecksum(BaselineChecksumSource);

    public static IReadOnlyList<SchemaMigrationStatus> Inspect(
        SqlSugarScope dbScope,
        IReadOnlyCollection<string>? scopes = null)
    {
        var statuses = new List<SchemaMigrationStatus>();
        foreach (var scope in ResolveScopes(scopes))
        {
            var db = dbScope.GetConnectionScope(scope);
            if (!db.DbMaintenance.IsAnyTable("RadishSchemaVersion", false))
            {
                statuses.Add(new SchemaMigrationStatus(
                    scope,
                    BaselineMigrationId,
                    false,
                    false,
                    true,
                    "缺少 schema ledger，需由 init/apply 受控接管"));
                continue;
            }

            var record = db.Queryable<SchemaMigrationRecord>()
                .Where(item => item.MigrationId == BaselineMigrationId)
                .First();
            if (record == null)
            {
                statuses.Add(new SchemaMigrationStatus(
                    scope,
                    BaselineMigrationId,
                    true,
                    false,
                    true,
                    "baseline pending"));
                continue;
            }

            var checksumMatches = string.Equals(record.Checksum, BaselineChecksum, StringComparison.Ordinal);
            statuses.Add(new SchemaMigrationStatus(
                scope,
                BaselineMigrationId,
                true,
                true,
                checksumMatches,
                checksumMatches ? "已应用" : "checksum drift"));
        }

        return statuses;
    }

    public static void EnsureBaseline(
        SqlSugarScope dbScope,
        TimeProvider timeProvider,
        IReadOnlyCollection<string>? scopes = null)
    {
        foreach (var scope in ResolveScopes(scopes))
        {
            var db = dbScope.GetConnectionScope(scope);
            EnsureCurrentSchemaCanBeAdopted(db, scope);
            db.CodeFirst.InitTables<SchemaMigrationRecord>();

            var existing = db.Queryable<SchemaMigrationRecord>()
                .Where(item => item.MigrationId == BaselineMigrationId)
                .First();
            if (existing != null)
            {
                if (!string.Equals(existing.Checksum, BaselineChecksum, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"{scope}.{BaselineMigrationId} checksum drift，禁止继续迁移。");
                }

                continue;
            }

            var stopwatch = Stopwatch.StartNew();
            db.Insertable(new SchemaMigrationRecord
            {
                MigrationId = BaselineMigrationId,
                Scope = scope,
                Description = BaselineDescription,
                Checksum = BaselineChecksum,
                AppliedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
                DurationMs = stopwatch.ElapsedMilliseconds
            }).ExecuteCommand();
            Console.WriteLine($"[Radish.DbMigrate] Schema: {scope}.{BaselineMigrationId} 已登记。");
        }
    }

    private static void EnsureCurrentSchemaCanBeAdopted(ISqlSugarClient db, string scope)
    {
        var missingObjects = new List<string>();
        foreach (var entityType in DbMigrateEntityRegistry.GetEntityTypesForConfig(scope))
        {
            var entityInfo = db.EntityMaintenance.GetEntityInfo(entityType);
            if (!db.DbMaintenance.IsAnyTable(entityInfo.DbTableName, false))
            {
                missingObjects.Add(entityInfo.DbTableName);
                continue;
            }

            var existingColumns = db.DbMaintenance
                .GetColumnInfosByTableName(entityInfo.DbTableName, false)
                .Select(column => column.DbColumnName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var column in entityInfo.Columns.Where(column => !column.IsIgnore))
            {
                if (!existingColumns.Contains(column.DbColumnName))
                {
                    missingObjects.Add($"{entityInfo.DbTableName}.{column.DbColumnName}");
                }
            }
        }

        if (missingObjects.Count > 0)
        {
            throw new InvalidOperationException(
                $"{scope} 不满足 baseline adoption：缺少 {string.Join(", ", missingObjects.Take(20))}。");
        }
    }

    private static string ComputeChecksum(string source)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
    }

    private static IReadOnlyList<string> ResolveScopes(IReadOnlyCollection<string>? scopes)
    {
        if (scopes != null)
        {
            return scopes.Where(scope => !string.IsNullOrWhiteSpace(scope)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        return Radish.Common.DbTool.BaseDbConfig.AllConfigs
            .Select(config => config.ConfigId?.ToString())
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
