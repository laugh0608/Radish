using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Radish.DbMigrate;
using Radish.Model;
using Radish.Api.Tests.TestCollections;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

[Collection(PostgreSqlIntegrationCollection.CollectionName)]
public sealed class ExperienceAuthoritativeGovernanceSchemaMigrationTest
{
    private const string PostgreSqlConnectionStringEnvironmentVariable = "RADISH_TEST_POSTGRES_CONNECTION_STRING";

    [Fact]
    public void Apply_ShouldBeRepeatableAndAcceptLegacyActionsWithoutVersionSnapshots()
    {
        var path = CreatePath("apply");
        using var db = CreateClient(path);
        using var services = new ServiceCollection().BuildServiceProvider();
        try
        {
            var migration = ExperienceAuthoritativeGovernanceSchemaMigration.Instance;
            migration.Apply(db, services);
            migration.Apply(db, services);
            db.Insertable(CreateAction(expectedVersion: null, resultVersion: null)).ExecuteCommand();

            Assert.Empty(migration.Verify(db, services));
            Assert.True(db.DbMaintenance.IsAnyIndex("idx_exp_governance_target_createtime"));
            Assert.True(db.DbMaintenance.IsAnyIndex("idx_experience_level_recalc_time"));
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public void Verify_ShouldRejectPartialOrNonMonotonicVersionSnapshots()
    {
        var path = CreatePath("verify");
        using var db = CreateClient(path);
        using var services = new ServiceCollection().BuildServiceProvider();
        try
        {
            var migration = ExperienceAuthoritativeGovernanceSchemaMigration.Instance;
            migration.Apply(db, services);
            db.Insertable(CreateAction(expectedVersion: 4, resultVersion: null)).ExecuteCommand();

            var issues = migration.Verify(db, services);

            Assert.Contains(issues, issue => issue.Contains("版本快照无效", StringComparison.Ordinal));
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public void Registry_ShouldAppendExperienceGovernanceMigrationToMainScope()
    {
        var migration = SchemaMigrationRegistry.All.Single(item =>
            item.MigrationId == "20260813_022_experience_authoritative_governance");

        Assert.Same(ExperienceAuthoritativeGovernanceSchemaMigration.Instance, migration);
        Assert.Equal("Main", migration.Scope);
    }

    [Fact]
    [Trait("Database", "PostgreSQL")]
    public async Task Apply_ShouldBeRepeatableOnPostgreSql()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(
            PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过经验治理 PostgreSQL 迁移测试");

        var schema = $"experience_governance_{Guid.NewGuid():N}";
        using var adminDb = PostgreSqlIntegrationSqlSugarFactory.CreateClient(new ConnectionConfig
        {
            ConfigId = "Main",
            ConnectionString = adminConnectionString!,
            DbType = DbType.PostgreSQL,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA {QuoteIdentifier(schema)}");
        try
        {
            var connectionString =
                $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
            using var db = PostgreSqlIntegrationSqlSugarFactory.CreateScope(new ConnectionConfig
            {
                ConfigId = "Main",
                ConnectionString = connectionString,
                DbType = DbType.PostgreSQL,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });
            using var services = new ServiceCollection().BuildServiceProvider();
            var migration = ExperienceAuthoritativeGovernanceSchemaMigration.Instance;

            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync(
                $"DROP SCHEMA IF EXISTS {QuoteIdentifier(schema)} CASCADE");
        }
    }

    private static UserExperienceGovernanceAction CreateAction(
        int? expectedVersion,
        int? resultVersion) => new()
    {
        Id = 3001,
        TenantId = 9,
        TargetUserId = 1001,
        ActionType = 1,
        Remark = "历史经验治理动作",
        ExpectedVersion = expectedVersion,
        ResultVersion = resultVersion,
        CreateTime = new DateTime(2026, 8, 13, 4, 0, 0, DateTimeKind.Utc),
        CreateBy = "admin",
        CreateId = 9001
    };

    private static SqlSugarScope CreateClient(string path) => new(new ConnectionConfig
    {
        ConfigId = "Main",
        ConnectionString = $"Data Source={path}",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = true,
        InitKeyType = InitKeyType.Attribute
    });

    private static string CreatePath(string scope) => Path.Combine(
        Path.GetTempPath(),
        $"radish-experience-governance-migration-{scope}-{Guid.NewGuid():N}.db");

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
