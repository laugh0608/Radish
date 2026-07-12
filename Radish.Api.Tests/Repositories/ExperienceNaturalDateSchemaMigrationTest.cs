using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Radish.Common.OptionTool;
using Radish.Common.TimeTool;
using Radish.DbMigrate;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class ExperienceNaturalDateSchemaMigrationTest
{
    private const string PostgreSqlConnectionStringEnvironmentVariable = "RADISH_TEST_POSTGRES_CONNECTION_STRING";

    [Fact]
    public void Apply_ShouldConvertLegacySqliteValuesAndPreserveIndexes()
    {
        var path = CreateTemporaryDatabasePath();
        using var db = CreateClient(path);
        using var services = CreateServices();

        try
        {
            CreateLegacyTables(db);
            db.Ado.ExecuteCommand(
                "INSERT INTO \"ExpTransaction\" (\"Id\", \"CreatedDate\") VALUES (1, '2026-07-11 16:00:00')");
            db.Ado.ExecuteCommand(
                "INSERT INTO \"UserExpDailyStats\" (\"Id\", \"StatDate\") VALUES (2, '2026-07-11 16:00:00')");
            db.Ado.ExecuteCommand(
                "INSERT INTO \"UserExperienceGovernanceAction\" (\"Id\", \"StatDate\") " +
                "VALUES (3, '2026-07-12 00:00:00'), (4, NULL)");
            db.CodeFirst.InitTables<SchemaMigrationRecord>();
            db.Insertable(new SchemaMigrationRecord
            {
                MigrationId = SchemaMigrationLedger.BaselineMigrationId,
                Scope = "Main",
                Description = "test baseline",
                Checksum = SchemaMigrationLedger.BaselineChecksum,
                AppliedAtUtc = new DateTime(2026, 7, 12, 8, 0, 0, DateTimeKind.Utc)
            }).ExecuteCommand();

            var migration = ExperienceNaturalDateSchemaMigration.Instance;
            SchemaMigrationLedger.ApplyPending(db, services, ["Main"]);
            SchemaMigrationLedger.ApplyPending(db, services, ["Main"]);

            Assert.Empty(migration.Verify(db, services));
            Assert.Equal(2, db.Queryable<SchemaMigrationRecord>().Count());
            Assert.Equal("date", GetColumnType(db, "ExpTransaction", "CreatedDate"));
            Assert.Equal("date", GetColumnType(db, "UserExpDailyStats", "StatDate"));
            Assert.Equal("date", GetColumnType(db, "UserExperienceGovernanceAction", "StatDate"));
            Assert.StartsWith(
                "2026-07-12",
                db.Ado.GetString("SELECT \"CreatedDate\" FROM \"ExpTransaction\" WHERE \"Id\" = 1"),
                StringComparison.Ordinal);
            Assert.StartsWith(
                "2026-07-12",
                db.Ado.GetString("SELECT \"StatDate\" FROM \"UserExpDailyStats\" WHERE \"Id\" = 2"),
                StringComparison.Ordinal);
            Assert.True(db.DbMaintenance.IsAnyIndex("idx_legacy_exp_date"));
            Assert.True(db.DbMaintenance.IsAnyIndex("idx_legacy_stats_date"));
        }
        finally
        {
            DeleteTemporaryDatabase(path);
        }
    }

    [Fact]
    public void Apply_ShouldRejectMisalignedLegacySqliteValue()
    {
        var path = CreateTemporaryDatabasePath();
        using var db = CreateClient(path);
        using var services = CreateServices();

        try
        {
            CreateLegacyTables(db);
            db.Ado.ExecuteCommand(
                "INSERT INTO \"ExpTransaction\" (\"Id\", \"CreatedDate\") VALUES (1, '2026-07-11 17:00:00')");

            var exception = Assert.Throws<InvalidOperationException>(() =>
                ExperienceNaturalDateSchemaMigration.Instance.Apply(db, services));

            Assert.Contains("前置时间审计失败", exception.Message, StringComparison.Ordinal);
            Assert.Contains("1=", exception.Message, StringComparison.Ordinal);
            Assert.Equal("text", GetColumnType(db, "ExpTransaction", "CreatedDate"));
        }
        finally
        {
            DeleteTemporaryDatabase(path);
        }
    }

    [Fact]
    public async Task ApplyPending_ShouldSerializeConcurrentSqliteWriters()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateTemporaryDatabasePath();
        using var services = CreateServices();

        try
        {
            using (var initializer = CreateClient(path))
            {
                CreateLegacyTables(initializer);
                SeedValidLegacyValues(initializer);
                InsertBaseline(initializer);
            }

            using var barrier = new Barrier(2);
            Task RunApplyAsync() => Task.Run(() =>
            {
                using var db = CreateClient(path);
                barrier.SignalAndWait(cancellationToken);
                SchemaMigrationLedger.ApplyPending(db, services, ["Main"]);
            }, cancellationToken);

            await Task.WhenAll(RunApplyAsync(), RunApplyAsync());

            using var verifier = CreateClient(path);
            Assert.Equal(2, verifier.Queryable<SchemaMigrationRecord>().Count());
            Assert.Equal("date", GetColumnType(verifier, "ExpTransaction", "CreatedDate"));
        }
        finally
        {
            DeleteTemporaryDatabase(path);
        }
    }

    [Fact]
    public void ApplyPending_ShouldRestoreSqliteBackupAndReapply()
    {
        var path = CreateTemporaryDatabasePath();
        var backupPath = $"{path}.before-q2b";
        using var services = CreateServices();

        try
        {
            using (var initializer = CreateClient(path))
            {
                CreateLegacyTables(initializer);
                SeedValidLegacyValues(initializer);
                InsertBaseline(initializer);
            }

            File.Copy(path, backupPath, overwrite: false);
            using (var firstApply = CreateClient(path))
            {
                SchemaMigrationLedger.ApplyPending(firstApply, services, ["Main"]);
                Assert.Equal(2, firstApply.Queryable<SchemaMigrationRecord>().Count());
            }

            File.Copy(backupPath, path, overwrite: true);
            using var restoredApply = CreateClient(path);
            SchemaMigrationLedger.ApplyPending(restoredApply, services, ["Main"]);

            Assert.Equal(2, restoredApply.Queryable<SchemaMigrationRecord>().Count());
            Assert.Equal("date", GetColumnType(restoredApply, "ExpTransaction", "CreatedDate"));
            Assert.StartsWith(
                "2026-07-12",
                restoredApply.Ado.GetString(
                    "SELECT \"CreatedDate\" FROM \"ExpTransaction\" WHERE \"Id\" = 1"),
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteTemporaryDatabase(path);
            DeleteTemporaryDatabase(backupPath);
        }
    }

    [Fact]
    [Trait("Database", "PostgreSQL")]
    public async Task Apply_ShouldConvertLegacyPostgreSqlValuesAndPreserveIndexes()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过经验自然日 PostgreSQL 迁移测试");

        var schema = $"q2b_natural_date_{Guid.NewGuid():N}";
        using var adminDb = CreatePostgreSqlClient(adminConnectionString!);
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA {QuoteIdentifier(schema)}");
        try
        {
            var connectionString = $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
            using var db = CreatePostgreSqlClient(connectionString);
            using var services = CreateServices();
            CreateLegacyPostgreSqlTables(db);
            db.Ado.ExecuteCommand(
                "INSERT INTO \"ExpTransaction\" (\"Id\", \"CreatedDate\") " +
                "VALUES (1, TIMESTAMP '2026-07-11 16:00:00')");
            db.Ado.ExecuteCommand(
                "INSERT INTO \"UserExpDailyStats\" (\"Id\", \"StatDate\") " +
                "VALUES (2, TIMESTAMPTZ '2026-07-11 16:00:00+00')");
            db.Ado.ExecuteCommand(
                "INSERT INTO \"UserExperienceGovernanceAction\" (\"Id\", \"StatDate\") " +
                "VALUES (3, TIMESTAMP '2026-07-12 00:00:00'), (4, NULL)");

            var migration = ExperienceNaturalDateSchemaMigration.Instance;
            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            Assert.Equal("date", GetColumnType(db, "ExpTransaction", "CreatedDate"));
            Assert.Equal("date", GetColumnType(db, "UserExpDailyStats", "StatDate"));
            var storedDate = db.Ado.GetDataTable(
                "SELECT \"CreatedDate\" FROM \"ExpTransaction\" WHERE \"Id\" = 1").Rows[0][0];
            Assert.Equal(new DateOnly(2026, 7, 12), Assert.IsType<DateOnly>(storedDate));
            Assert.True(db.DbMaintenance.IsAnyIndex("idx_legacy_exp_date"));
            Assert.True(db.DbMaintenance.IsAnyIndex("idx_legacy_stats_date"));
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS {QuoteIdentifier(schema)} CASCADE");
        }
    }

    [Fact]
    [Trait("Database", "PostgreSQL")]
    public async Task ApplyPending_ShouldResolveLowercasePostgreSqlPhysicalIdentifiers()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过 PostgreSQL 物理标识符迁移测试");

        var schema = $"q2b_lowercase_identifier_{Guid.NewGuid():N}";
        using var adminDb = CreatePostgreSqlClient(adminConnectionString!);
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA {QuoteIdentifier(schema)}");
        try
        {
            var connectionString = $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
            using var db = CreatePostgreSqlScope(connectionString);
            using var services = CreateServices();
            CreateLowercasePostgreSqlTables(db);
            SeedValidLowercasePostgreSqlValues(db);
            InsertBaseline(db);

            SchemaMigrationLedger.ApplyPending(db, services, ["Main"]);
            SchemaMigrationLedger.ApplyPending(db, services, ["Main"]);

            Assert.Equal(2, db.Queryable<SchemaMigrationRecord>().Count());
            Assert.Equal("date", GetColumnType(db, "exptransaction", "createddate"));
            Assert.Equal("date", GetColumnType(db, "userexpdailystats", "statdate"));
            Assert.Equal("date", GetColumnType(db, "userexperiencegovernanceaction", "statdate"));
            var storedDate = db.Ado.GetDataTable(
                "SELECT createddate FROM exptransaction WHERE id = 1").Rows[0][0];
            Assert.Equal(new DateOnly(2026, 7, 12), Assert.IsType<DateOnly>(storedDate));
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS {QuoteIdentifier(schema)} CASCADE");
        }
    }

    [Fact]
    [Trait("Database", "PostgreSQL")]
    public async Task ApplyPending_ShouldSerializeConcurrentPostgreSqlWriters()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminConnectionString = Environment.GetEnvironmentVariable(PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过 schema migration PostgreSQL 并发测试");

        var schema = $"q2b_migration_lock_{Guid.NewGuid():N}";
        using var adminDb = CreatePostgreSqlClient(adminConnectionString!);
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA {QuoteIdentifier(schema)}");
        try
        {
            var connectionString = $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
            using var services = CreateServices();
            using (var initializer = CreatePostgreSqlScope(connectionString))
            {
                CreateLegacyPostgreSqlTables(initializer);
                SeedValidLegacyPostgreSqlValues(initializer);
                InsertBaseline(initializer);
            }

            using var barrier = new Barrier(2);
            Task RunApplyAsync() => Task.Run(() =>
            {
                using var db = CreatePostgreSqlScope(connectionString);
                barrier.SignalAndWait(cancellationToken);
                SchemaMigrationLedger.ApplyPending(db, services, ["Main"]);
            }, cancellationToken);

            await Task.WhenAll(RunApplyAsync(), RunApplyAsync());

            using var verifier = CreatePostgreSqlScope(connectionString);
            Assert.Equal(2, verifier.Queryable<SchemaMigrationRecord>().Count());
            Assert.Equal("date", GetColumnType(verifier, "ExpTransaction", "CreatedDate"));
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS {QuoteIdentifier(schema)} CASCADE");
        }
    }

    private static SqlSugarScope CreateClient(string path)
    {
        return new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "Main",
            ConnectionString = $"Data Source={path};Default Timeout=10",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
    }

    private static SqlSugarClient CreatePostgreSqlClient(string connectionString)
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConfigId = "Main",
            ConnectionString = connectionString,
            DbType = DbType.PostgreSQL,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
    }

    private static SqlSugarScope CreatePostgreSqlScope(string connectionString)
    {
        return new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "Main",
            ConnectionString = connectionString,
            DbType = DbType.PostgreSQL,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
    }

    private static ServiceProvider CreateServices()
    {
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero));
        return new ServiceCollection()
            .AddSingleton<TimeProvider>(timeProvider)
            .AddSingleton(new BusinessCalendar(
                timeProvider,
                Options.Create(new TimeOptions { DefaultTimeZoneId = "Asia/Shanghai" })))
            .BuildServiceProvider();
    }

    private static void CreateLegacyTables(ISqlSugarClient db)
    {
        db.Ado.ExecuteCommand(
            "CREATE TABLE \"ExpTransaction\" (\"Id\" INTEGER NOT NULL PRIMARY KEY, \"CreatedDate\" TEXT NOT NULL)");
        db.Ado.ExecuteCommand(
            "CREATE INDEX \"idx_legacy_exp_date\" ON \"ExpTransaction\" (\"CreatedDate\")");
        db.Ado.ExecuteCommand(
            "CREATE TABLE \"UserExpDailyStats\" (\"Id\" INTEGER NOT NULL PRIMARY KEY, \"StatDate\" TEXT NOT NULL)");
        db.Ado.ExecuteCommand(
            "CREATE INDEX \"idx_legacy_stats_date\" ON \"UserExpDailyStats\" (\"StatDate\")");
        db.Ado.ExecuteCommand(
            "CREATE TABLE \"UserExperienceGovernanceAction\" " +
            "(\"Id\" INTEGER NOT NULL PRIMARY KEY, \"StatDate\" TEXT NULL)");
    }

    private static void CreateLegacyPostgreSqlTables(ISqlSugarClient db)
    {
        db.Ado.ExecuteCommand(
            "CREATE TABLE \"ExpTransaction\" " +
            "(\"Id\" bigint NOT NULL PRIMARY KEY, \"CreatedDate\" timestamp without time zone NOT NULL)");
        db.Ado.ExecuteCommand(
            "CREATE INDEX \"idx_legacy_exp_date\" ON \"ExpTransaction\" (\"CreatedDate\")");
        db.Ado.ExecuteCommand(
            "CREATE TABLE \"UserExpDailyStats\" " +
            "(\"Id\" bigint NOT NULL PRIMARY KEY, \"StatDate\" timestamp with time zone NOT NULL)");
        db.Ado.ExecuteCommand(
            "CREATE INDEX \"idx_legacy_stats_date\" ON \"UserExpDailyStats\" (\"StatDate\")");
        db.Ado.ExecuteCommand(
            "CREATE TABLE \"UserExperienceGovernanceAction\" " +
            "(\"Id\" bigint NOT NULL PRIMARY KEY, \"StatDate\" timestamp without time zone NULL)");
    }

    private static void CreateLowercasePostgreSqlTables(ISqlSugarClient db)
    {
        db.Ado.ExecuteCommand(
            "CREATE TABLE exptransaction " +
            "(id bigint NOT NULL PRIMARY KEY, createddate timestamp without time zone NOT NULL)");
        db.Ado.ExecuteCommand(
            "CREATE TABLE userexpdailystats " +
            "(id bigint NOT NULL PRIMARY KEY, statdate timestamp with time zone NOT NULL)");
        db.Ado.ExecuteCommand(
            "CREATE TABLE userexperiencegovernanceaction " +
            "(id bigint NOT NULL PRIMARY KEY, statdate timestamp without time zone NULL)");
    }

    private static void SeedValidLegacyValues(ISqlSugarClient db)
    {
        db.Ado.ExecuteCommand(
            "INSERT INTO \"ExpTransaction\" (\"Id\", \"CreatedDate\") VALUES (1, '2026-07-11 16:00:00')");
        db.Ado.ExecuteCommand(
            "INSERT INTO \"UserExpDailyStats\" (\"Id\", \"StatDate\") VALUES (2, '2026-07-11 16:00:00')");
        db.Ado.ExecuteCommand(
            "INSERT INTO \"UserExperienceGovernanceAction\" (\"Id\", \"StatDate\") " +
            "VALUES (3, '2026-07-12 00:00:00'), (4, NULL)");
    }

    private static void SeedValidLegacyPostgreSqlValues(ISqlSugarClient db)
    {
        db.Ado.ExecuteCommand(
            "INSERT INTO \"ExpTransaction\" (\"Id\", \"CreatedDate\") " +
            "VALUES (1, TIMESTAMP '2026-07-11 16:00:00')");
        db.Ado.ExecuteCommand(
            "INSERT INTO \"UserExpDailyStats\" (\"Id\", \"StatDate\") " +
            "VALUES (2, TIMESTAMPTZ '2026-07-11 16:00:00+00')");
        db.Ado.ExecuteCommand(
            "INSERT INTO \"UserExperienceGovernanceAction\" (\"Id\", \"StatDate\") " +
            "VALUES (3, TIMESTAMP '2026-07-12 00:00:00'), (4, NULL)");
    }

    private static void SeedValidLowercasePostgreSqlValues(ISqlSugarClient db)
    {
        db.Ado.ExecuteCommand(
            "INSERT INTO exptransaction (id, createddate) " +
            "VALUES (1, TIMESTAMP '2026-07-11 16:00:00')");
        db.Ado.ExecuteCommand(
            "INSERT INTO userexpdailystats (id, statdate) " +
            "VALUES (2, TIMESTAMPTZ '2026-07-11 16:00:00+00')");
        db.Ado.ExecuteCommand(
            "INSERT INTO userexperiencegovernanceaction (id, statdate) " +
            "VALUES (3, TIMESTAMP '2026-07-12 00:00:00'), (4, NULL)");
    }

    private static void InsertBaseline(ISqlSugarClient db)
    {
        db.CodeFirst.InitTables<SchemaMigrationRecord>();
        db.Insertable(new SchemaMigrationRecord
        {
            MigrationId = SchemaMigrationLedger.BaselineMigrationId,
            Scope = "Main",
            Description = "test baseline",
            Checksum = SchemaMigrationLedger.BaselineChecksum,
            AppliedAtUtc = new DateTime(2026, 7, 12, 8, 0, 0, DateTimeKind.Utc)
        }).ExecuteCommand();
    }

    private static string GetColumnType(ISqlSugarClient db, string tableName, string columnName)
    {
        return db.DbMaintenance.GetColumnInfosByTableName(tableName, false)
            .Single(column => string.Equals(column.DbColumnName, columnName, StringComparison.OrdinalIgnoreCase))
            .DataType
            .ToLowerInvariant();
    }

    private static string CreateTemporaryDatabasePath()
    {
        return Path.Combine(Path.GetTempPath(), $"radish-natural-date-{Guid.NewGuid():N}.db");
    }

    private static void DeleteTemporaryDatabase(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
