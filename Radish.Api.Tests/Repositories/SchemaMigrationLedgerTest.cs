using System;
using System.IO;
using System.Linq;
using Radish.DbMigrate;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class SchemaMigrationLedgerTest
{
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

            SchemaMigrationLedger.EnsureBaseline(db, timeProvider, ["Main"]);
            SchemaMigrationLedger.EnsureBaseline(db, timeProvider, ["Main"]);

            var status = Assert.Single(SchemaMigrationLedger.Inspect(db, ["Main"]));
            Assert.True(status.LedgerExists);
            Assert.True(status.Applied);
            Assert.True(status.ChecksumMatches);
            Assert.Equal(1, mainDb.Queryable<SchemaMigrationRecord>().Count());

            mainDb.Updateable<SchemaMigrationRecord>()
                .SetColumns(record => record.Checksum == "tampered")
                .Where(record => record.MigrationId == SchemaMigrationLedger.BaselineMigrationId)
                .ExecuteCommand();

            status = Assert.Single(SchemaMigrationLedger.Inspect(db, ["Main"]));
            Assert.False(status.ChecksumMatches);
            Assert.Contains("checksum drift", status.Message, StringComparison.Ordinal);
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
}
