using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Radish.Common.OptionTool;
using Radish.Common.TimeTool;
using Radish.DbMigrate;
using Radish.Model.LogModels;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class SchemaMigrationLedgerTest
{
    [Fact]
    public async Task EnsureBusinessSchemaAsync_ShouldApplyPendingLedgerBeforeSeedReadinessInspection()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-schema-ledger-pending-{Guid.NewGuid():N}.db");

        try
        {
            using var db = CreateSqliteScope($"Data Source={path}", "main");
            var mainDb = db.GetConnectionScope("main");
            foreach (var entityType in DbMigrateEntityRegistry.GetEntityTypesForConfig("Main"))
            {
                mainDb.CodeFirst.InitTables(entityType);
            }

            var timeProvider = new FixedTimeProvider(
                new DateTimeOffset(2026, 7, 14, 8, 0, 0, TimeSpan.Zero));
            using var services = new ServiceCollection()
                .AddSingleton<ISqlSugarClient>(db)
                .AddSingleton<TimeProvider>(timeProvider)
                .AddSingleton(new BusinessCalendar(
                    timeProvider,
                    Options.Create(new TimeOptions { DefaultTimeZoneId = "Asia/Shanghai" })))
                .BuildServiceProvider();

            SchemaMigrationLedger.EnsureBaseline(db, timeProvider, ["main"]);
            SchemaMigrationLedger.ApplyPending(db, services, ["main"]);

            var f1MigrationIds = new[]
            {
                ShopOrderFulfillmentSafetyMigration.Instance.MigrationId,
                ShopEntitlementOperationSchemaMigration.Instance.MigrationId,
                UserActiveBenefitSchemaMigration.Instance.MigrationId,
                ShopEntitlementOperationSubjectNullabilityMigration.Instance.MigrationId
            };
            mainDb.Deleteable<SchemaMigrationRecord>()
                .Where(record => f1MigrationIds.Contains(record.MigrationId))
                .ExecuteCommand();
            mainDb.Ado.ExecuteCommand("DROP TABLE \"ShopUserActiveBenefit\"");
            mainDb.Ado.ExecuteCommand("DROP TABLE \"ShopEntitlementOperation\"");
            foreach (var columnName in new[] { "FixedExpiresAt", "FailureStage", "GrantedBenefitId", "GrantedInventoryId" })
            {
                mainDb.Ado.ExecuteCommand($"ALTER TABLE \"ShopOrder\" DROP COLUMN \"{columnName}\"");
            }

            foreach (var columnName in new[] { "RevokedAt", "RevokedById", "RevokedByName", "RevocationReason" })
            {
                mainDb.Ado.ExecuteCommand($"ALTER TABLE \"ShopUserBenefit\" DROP COLUMN \"{columnName}\"");
            }

            var pendingInspection = DbMigrateInspection.InspectSeedReadiness(services, "main");
            Assert.False(pendingInspection.IsReadyForSeed);

            await DbMigrateRunner.EnsureBusinessSchemaAsync(
                services,
                new ConfigurationBuilder().Build(),
                "Test",
                db,
                "main",
                ["main"]);

            Assert.True(DbMigrateInspection.InspectSeedReadiness(services, "main").IsReadyForSeed);
            Assert.All(SchemaMigrationLedger.Inspect(db, ["main"]), status => Assert.True(status.Applied));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task EnsureBaseline_ShouldSerializeConcurrentSqliteAdoption()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-schema-ledger-concurrent-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path};Default Timeout=10";

        try
        {
            using (var setupDb = CreateSqliteScope(connectionString))
            {
                var mainDb = setupDb.GetConnectionScope("Main");
                foreach (var entityType in DbMigrateEntityRegistry.GetEntityTypesForConfig("Main"))
                {
                    mainDb.CodeFirst.InitTables(entityType);
                }
            }

            var timeProvider = new FixedTimeProvider(
                new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero));
            using var barrier = new Barrier(2);

            Task AdoptBaselineAsync()
            {
                return Task.Run(() =>
                {
                    using var db = CreateSqliteScope(connectionString);
                    barrier.SignalAndWait(TestContext.Current.CancellationToken);
                    SchemaMigrationLedger.EnsureBaseline(db, timeProvider, ["Main"]);
                }, TestContext.Current.CancellationToken);
            }

            await Task.WhenAll(AdoptBaselineAsync(), AdoptBaselineAsync());

            using var verifyDb = CreateSqliteScope(connectionString);
            var records = verifyDb.GetConnectionScope("Main")
                .Queryable<SchemaMigrationRecord>()
                .Where(record => record.MigrationId == SchemaMigrationLedger.BaselineMigrationId)
                .ToList();
            Assert.Single(records);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void EnsureBaseline_ShouldAdoptCompleteSchemaAndDetectChecksumDrift()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-schema-ledger-{Guid.NewGuid():N}.db");
        using var db = new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "Main",
            ConnectionString = $"Data Source={path}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });

        try
        {
            var mainDb = db.GetConnectionScope("Main");
            foreach (var entityType in DbMigrateEntityRegistry.GetEntityTypesForConfig("Main"))
            {
                mainDb.CodeFirst.InitTables(entityType);
            }

            var timeProvider = new FixedTimeProvider(
                new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero));
            using var services = new ServiceCollection()
                .AddSingleton<TimeProvider>(timeProvider)
                .AddSingleton(new BusinessCalendar(
                    timeProvider,
                    Options.Create(new TimeOptions { DefaultTimeZoneId = "Asia/Shanghai" })))
                .BuildServiceProvider();

            SchemaMigrationLedger.EnsureBaseline(db, timeProvider, ["Main"]);
            SchemaMigrationLedger.EnsureBaseline(db, timeProvider, ["Main"]);
            SchemaMigrationLedger.ApplyPending(db, services, ["Main"]);
            SchemaMigrationLedger.ApplyPending(db, services, ["Main"]);

            var statuses = SchemaMigrationLedger.Inspect(db, ["Main"]);
            var status = Assert.Single(
                statuses,
                item => item.MigrationId == SchemaMigrationLedger.BaselineMigrationId);
            Assert.True(status.LedgerExists);
            Assert.True(status.Applied);
            Assert.True(status.ChecksumMatches);
            Assert.All(statuses, item => Assert.True(item.Applied));
            Assert.Equal(6, mainDb.Queryable<SchemaMigrationRecord>().Count());
            Assert.True(SchemaMigrationLedger.HasAppliedBaseline(db, "Main"));

            mainDb.Updateable<SchemaMigrationRecord>()
                .SetColumns(record => record.Checksum == "tampered")
                .Where(record => record.MigrationId == SchemaMigrationLedger.BaselineMigrationId)
                .ExecuteCommand();

            status = Assert.Single(
                SchemaMigrationLedger.Inspect(db, ["Main"]),
                item => item.MigrationId == SchemaMigrationLedger.BaselineMigrationId);
            Assert.False(status.ChecksumMatches);
            Assert.Contains("checksum drift", status.Message, StringComparison.Ordinal);
            Assert.Throws<InvalidOperationException>(() =>
                SchemaMigrationLedger.HasAppliedBaseline(db, "Main"));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void EnsureBaseline_ShouldAdoptExistingSplitTablesWithoutRequiringTemplateTable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-schema-ledger-split-{Guid.NewGuid():N}.db");
        using var db = new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "Log",
            ConnectionString = $"Data Source={path}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });

        try
        {
            var logDb = db.GetConnectionScope("Log");
            logDb.CodeFirst.InitTables<AuditLog>();
            var templateTableName = logDb.EntityMaintenance.GetEntityInfo<AuditLog>().DbTableName;
            Assert.False(logDb.DbMaintenance.IsAnyTable(templateTableName, false));
            Assert.Contains(
                logDb.DbMaintenance.GetTableInfoList(false),
                table => table.Name.StartsWith("AuditLog_", StringComparison.Ordinal));

            var timeProvider = new FixedTimeProvider(
                new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero));

            SchemaMigrationLedger.EnsureBaseline(db, timeProvider, ["Log"]);

            Assert.True(SchemaMigrationLedger.HasAppliedBaseline(db, "Log"));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static SqlSugarScope CreateSqliteScope(string connectionString, string configId = "Main")
    {
        return new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = configId,
            ConnectionString = connectionString,
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
    }
}
