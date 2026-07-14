using System.Buffers.Binary;
using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
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

    internal static readonly string BaselineChecksum = ComputeChecksum(BaselineChecksumSource);

    public static IReadOnlyList<SchemaMigrationStatus> Inspect(
        SqlSugarScope dbScope,
        IReadOnlyCollection<string>? scopes = null)
    {
        var statuses = new List<SchemaMigrationStatus>();
        foreach (var scope in ResolveScopes(scopes))
        {
            var db = dbScope.GetConnectionScope(scope);
            var knownMigrations = GetKnownMigrations(scope);
            if (!db.DbMaintenance.IsAnyTable("RadishSchemaVersion", false))
            {
                statuses.AddRange(knownMigrations.Select(migration => new SchemaMigrationStatus(
                    scope,
                    migration.MigrationId,
                    false,
                    false,
                    true,
                    migration.MigrationId == BaselineMigrationId
                        ? "缺少 schema ledger，需由 init/apply 受控接管"
                        : "pending")));
                continue;
            }

            var records = db.Queryable<SchemaMigrationRecord>()
                .ToList()
                .ToDictionary(record => record.MigrationId, StringComparer.Ordinal);
            foreach (var migration in knownMigrations)
            {
                records.TryGetValue(migration.MigrationId, out var record);
                if (record == null)
                {
                    statuses.Add(new SchemaMigrationStatus(
                        scope,
                        migration.MigrationId,
                        true,
                        false,
                        true,
                        migration.MigrationId == BaselineMigrationId ? "baseline pending" : "pending"));
                    continue;
                }

                var checksumMatches = string.Equals(record.Checksum, migration.Checksum, StringComparison.Ordinal);
                statuses.Add(new SchemaMigrationStatus(
                    scope,
                    migration.MigrationId,
                    true,
                    true,
                    checksumMatches,
                    checksumMatches ? "已应用" : "checksum drift"));
            }
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
            BeginMigrationTransaction(db);
            try
            {
                AcquireMigrationLock(db, scope, BaselineMigrationId);
                if (db.DbMaintenance.IsAnyTable("RadishSchemaVersion", false))
                {
                    var appliedBaseline = db.Queryable<SchemaMigrationRecord>()
                        .Where(item => item.MigrationId == BaselineMigrationId)
                        .First();
                    if (appliedBaseline != null)
                    {
                        EnsureBaselineChecksumMatches(scope, appliedBaseline);
                        db.Ado.CommitTran();
                        continue;
                    }
                }

                EnsureCurrentSchemaCanBeAdopted(db, scope);
                db.CodeFirst.InitTables<SchemaMigrationRecord>();

                var existing = db.Queryable<SchemaMigrationRecord>()
                    .Where(item => item.MigrationId == BaselineMigrationId)
                    .First();
                if (existing != null)
                {
                    EnsureBaselineChecksumMatches(scope, existing);
                    db.Ado.CommitTran();
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
                db.Ado.CommitTran();
                Console.WriteLine($"[Radish.DbMigrate] Schema: {scope}.{BaselineMigrationId} 已登记。");
            }
            catch
            {
                db.Ado.RollbackTran();
                throw;
            }
        }
    }

    private static void EnsureBaselineChecksumMatches(string scope, SchemaMigrationRecord record)
    {
        if (!string.Equals(record.Checksum, BaselineChecksum, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{scope}.{BaselineMigrationId} checksum drift，禁止继续迁移。");
        }
    }

    public static bool HasAppliedBaseline(SqlSugarScope dbScope, string scope)
    {
        var db = dbScope.GetConnectionScope(scope);
        if (!db.DbMaintenance.IsAnyTable("RadishSchemaVersion", false))
        {
            return false;
        }

        var record = db.Queryable<SchemaMigrationRecord>()
            .Where(item => item.MigrationId == BaselineMigrationId)
            .First();
        if (record == null)
        {
            return false;
        }

        if (!string.Equals(record.Checksum, BaselineChecksum, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{scope}.{BaselineMigrationId} checksum drift，禁止继续。");
        }

        return true;
    }

    public static void ApplyPending(
        SqlSugarScope dbScope,
        IServiceProvider services,
        IReadOnlyCollection<string>? scopes = null)
    {
        var timeProvider = services.GetRequiredService<TimeProvider>();
        foreach (var scope in ResolveScopes(scopes))
        {
            var db = dbScope.GetConnectionScope(scope);
            if (!db.DbMaintenance.IsAnyTable("RadishSchemaVersion", false))
            {
                throw new InvalidOperationException($"{scope} 缺少 schema ledger，禁止应用后续 migration。");
            }

            EnsureRecordedChecksum(db, scope, BaselineMigrationId, BaselineChecksum);
            foreach (var migration in SchemaMigrationRegistry.All
                         .Where(item => string.Equals(item.Scope, scope, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(item => item.MigrationId, StringComparer.Ordinal))
            {
                var checksum = ComputeChecksum(migration.ChecksumSource);
                var existing = db.Queryable<SchemaMigrationRecord>()
                    .Where(record => record.MigrationId == migration.MigrationId)
                    .First();
                if (existing != null)
                {
                    if (!string.Equals(existing.Checksum, checksum, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"{scope}.{migration.MigrationId} checksum drift，禁止继续迁移。");
                    }

                    ThrowIfVerificationFailed(migration, db, services);
                    continue;
                }

                var stopwatch = Stopwatch.StartNew();
                BeginMigrationTransaction(db);
                try
                {
                    AcquireMigrationLock(db, scope, migration.MigrationId);
                    existing = db.Queryable<SchemaMigrationRecord>()
                        .Where(record => record.MigrationId == migration.MigrationId)
                        .First();
                    if (existing != null)
                    {
                        if (!string.Equals(existing.Checksum, checksum, StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                $"{scope}.{migration.MigrationId} checksum drift，禁止继续迁移。");
                        }

                        ThrowIfVerificationFailed(migration, db, services);
                        db.Ado.CommitTran();
                        continue;
                    }

                    migration.Apply(db, services);
                    ThrowIfVerificationFailed(migration, db, services);
                    db.Insertable(new SchemaMigrationRecord
                    {
                        MigrationId = migration.MigrationId,
                        Scope = scope,
                        Description = migration.Description,
                        Checksum = checksum,
                        AppliedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
                        DurationMs = stopwatch.ElapsedMilliseconds
                    }).ExecuteCommand();
                    db.Ado.CommitTran();
                    Console.WriteLine($"[Radish.DbMigrate] Schema: {scope}.{migration.MigrationId} 已应用。");
                }
                catch
                {
                    db.Ado.RollbackTran();
                    throw;
                }
            }
        }
    }

    private static void BeginMigrationTransaction(ISqlSugarClient db)
    {
        if (db.CurrentConnectionConfig.DbType != SqlSugar.DbType.Sqlite)
        {
            db.Ado.BeginTran();
            return;
        }

        if (db.Ado.Connection is not SqliteConnection connection)
        {
            throw new InvalidOperationException("SQLite migration lock 未取得 Microsoft.Data.Sqlite connection。");
        }

        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        db.Ado.Transaction = connection.BeginTransaction(deferred: false);
    }

    private static void AcquireMigrationLock(
        ISqlSugarClient db,
        string scope,
        string migrationId)
    {
        if (db.CurrentConnectionConfig.DbType != SqlSugar.DbType.PostgreSQL)
        {
            return;
        }

        var lockSource = $"radish-schema-migration:{scope}:{migrationId}";
        var lockHash = SHA256.HashData(Encoding.UTF8.GetBytes(lockSource));
        var lockKey = BinaryPrimitives.ReadInt64BigEndian(lockHash);
        db.Ado.ExecuteCommand(
            "SELECT pg_advisory_xact_lock(@lockKey)",
            new SugarParameter("@lockKey", lockKey));
    }

    public static IReadOnlyList<string> VerifyApplied(
        SqlSugarScope dbScope,
        IServiceProvider services,
        IReadOnlyCollection<string>? scopes = null)
    {
        var issues = new List<string>();
        foreach (var scope in ResolveScopes(scopes))
        {
            var db = dbScope.GetConnectionScope(scope);
            if (!db.DbMaintenance.IsAnyTable("RadishSchemaVersion", false))
            {
                continue;
            }

            foreach (var migration in SchemaMigrationRegistry.All
                         .Where(item => string.Equals(item.Scope, scope, StringComparison.OrdinalIgnoreCase)))
            {
                var record = db.Queryable<SchemaMigrationRecord>()
                    .Where(item => item.MigrationId == migration.MigrationId)
                    .First();
                if (record == null)
                {
                    continue;
                }

                issues.AddRange(migration.Verify(db, services)
                    .Select(issue => $"{scope}.{migration.MigrationId}: {issue}"));
            }
        }

        return issues;
    }

    private static void EnsureCurrentSchemaCanBeAdopted(ISqlSugarClient db, string scope)
    {
        var missingObjects = new List<string>();
        foreach (var entityType in DbMigrateEntityRegistry.GetEntityTypesForConfig(scope))
        {
            var entityInfo = db.EntityMaintenance.GetEntityInfo(entityType);
            var physicalTableNames = ResolvePhysicalTableNames(db, entityType, entityInfo.DbTableName);
            if (physicalTableNames.Count == 0)
            {
                if (!IsSplitTable(entityType))
                {
                    missingObjects.Add(entityInfo.DbTableName);
                }

                continue;
            }

            foreach (var physicalTableName in physicalTableNames)
            {
                var existingColumns = db.DbMaintenance
                    .GetColumnInfosByTableName(physicalTableName, false)
                    .Select(column => column.DbColumnName)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var column in entityInfo.Columns.Where(column => !column.IsIgnore))
                {
                    if (!existingColumns.Contains(column.DbColumnName))
                    {
                        missingObjects.Add($"{physicalTableName}.{column.DbColumnName}");
                    }
                }
            }
        }

        if (missingObjects.Count > 0)
        {
            throw new InvalidOperationException(
                $"{scope} 不满足 baseline adoption：缺少 {string.Join(", ", missingObjects.Take(20))}。");
        }
    }

    private static IReadOnlyList<string> ResolvePhysicalTableNames(
        ISqlSugarClient db,
        Type entityType,
        string configuredTableName)
    {
        if (!IsSplitTable(entityType))
        {
            return db.DbMaintenance.IsAnyTable(configuredTableName, false)
                ? [configuredTableName]
                : [];
        }

        var tokenIndex = configuredTableName.IndexOf('{');
        var tableNamePrefix = tokenIndex >= 0
            ? configuredTableName[..tokenIndex]
            : configuredTableName;

        return db.DbMaintenance.GetTableInfoList(false)
            .Select(table => table.Name)
            .Where(tableName => tableName.StartsWith(tableNamePrefix, StringComparison.OrdinalIgnoreCase))
            .Where(tableName => tableName.Length > tableNamePrefix.Length)
            .Where(tableName => tableName[tableNamePrefix.Length..].All(char.IsDigit))
            .ToList();
    }

    private static bool IsSplitTable(Type entityType)
    {
        return entityType.GetCustomAttributes(typeof(SplitTableAttribute), inherit: true).Any();
    }

    private static void EnsureRecordedChecksum(
        ISqlSugarClient db,
        string scope,
        string migrationId,
        string expectedChecksum)
    {
        var record = db.Queryable<SchemaMigrationRecord>()
            .Where(item => item.MigrationId == migrationId)
            .First();
        if (record == null)
        {
            throw new InvalidOperationException($"{scope}.{migrationId} 尚未应用，禁止继续迁移。");
        }

        if (!string.Equals(record.Checksum, expectedChecksum, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{scope}.{migrationId} checksum drift，禁止继续迁移。");
        }
    }

    private static void ThrowIfVerificationFailed(
        ISchemaMigration migration,
        ISqlSugarClient db,
        IServiceProvider services)
    {
        var issues = migration.Verify(db, services);
        if (issues.Count > 0)
        {
            throw new InvalidOperationException(
                $"{migration.MigrationId} 后置验证失败：{string.Join("；", issues)}");
        }
    }

    private static IReadOnlyList<KnownMigration> GetKnownMigrations(string scope)
    {
        var migrations = new List<KnownMigration>
        {
            new(BaselineMigrationId, BaselineChecksum)
        };
        migrations.AddRange(SchemaMigrationRegistry.All
            .Where(item => string.Equals(item.Scope, scope, StringComparison.OrdinalIgnoreCase))
            .Select(item => new KnownMigration(item.MigrationId, ComputeChecksum(item.ChecksumSource))));
        return migrations;
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

    private sealed record KnownMigration(string MigrationId, string Checksum);
}
