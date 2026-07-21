using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Radish.DbMigrate;
using Radish.Model;
using Radish.Shared.CustomEnum;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class ContentModerationCaseSchemaMigrationTest
{
    private const string PostgreSqlConnectionStringEnvironmentVariable = "RADISH_TEST_POSTGRES_CONNECTION_STRING";

    [Fact(Timeout = 10_000)]
    public void Migration_ShouldAggregatePendingReportsAndRemainRepeatableOnSqlite()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-moderation-migration-{Guid.NewGuid():N}.db");
        using var db = CreateClient(path);
        using var services = CreateServices();
        try
        {
            db.CodeFirst.InitTables<ContentReport>();
            db.CodeFirst.InitTables<UserModerationAction>();
            db.Insertable(new[]
            {
                CreatePendingReport(1001, 5001),
                CreatePendingReport(1002, 5002)
            }).ExecuteCommand();
            db.Insertable(new UserModerationAction
            {
                Id = 6001,
                TenantId = 9,
                TargetUserId = 5001,
                TargetUserName = "target",
                ActionType = (int)ModerationActionTypeEnum.Mute,
                Reason = "legacy",
                StartTime = DateTime.UtcNow.AddHours(-1),
                EndTime = DateTime.UtcNow.AddHours(23),
                IsActive = true,
                CreateTime = DateTime.UtcNow.AddHours(-1),
                CreateBy = "legacy",
                CreateId = 9001
            }).ExecuteCommand();

            var migration = ContentModerationCaseSchemaMigration.Instance;
            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            var moderationCase = Assert.Single(db.Queryable<ContentModerationCase>().ToList());
            Assert.Equal((int)ContentModerationCaseStatus.Open, moderationCase.Status);
            Assert.Equal(2, db.Queryable<ContentReport>().Count(report => report.CaseId == moderationCase.Id));
            Assert.Equal(2, db.Queryable<ContentModerationEvidence>().Count());
            Assert.All(db.Queryable<ContentReport>().ToList(), report => Assert.StartsWith("rpt_", report.PublicId));
            var state = Assert.Single(db.Queryable<UserModerationState>().ToList());
            Assert.Equal((int)UserModerationPolicyType.Mute, state.PolicyType);
            Assert.Equal((int)UserModerationStateValue.Active, state.State);
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
    [Trait("Database", "PostgreSQL")]
    public async Task Migration_ShouldRemainRepeatableOnPostgreSql()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过内容治理 PostgreSQL 迁移测试");
        var schema = $"moderation_case_migration_{Guid.NewGuid():N}";
        using var admin = CreatePostgreSqlClient(adminConnectionString!);
        await admin.Ado.ExecuteCommandAsync($"CREATE SCHEMA {QuoteIdentifier(schema)}");
        try
        {
            using var db = CreatePostgreSqlClient(
                $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false");
            using var services = CreateServices();
            db.CodeFirst.InitTables<ContentReport>();
            db.CodeFirst.InitTables<UserModerationAction>();
            db.Insertable(CreatePendingReport(1001, 5001)).ExecuteCommand();

            var migration = ContentModerationCaseSchemaMigration.Instance;
            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            Assert.Single(db.Queryable<ContentModerationCase>().ToList());
        }
        finally
        {
            await admin.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS {QuoteIdentifier(schema)} CASCADE");
        }
    }

    private static ContentReport CreatePendingReport(long id, long reporterUserId) => new()
    {
        Id = id,
        TenantId = 9,
        ReportTargetType = (int)ContentReportTargetTypeEnum.Post,
        TargetContentId = 7001,
        TargetSnapshotTitle = "reported",
        TargetSnapshotSummary = "snapshot",
        TargetUserId = 5001,
        TargetUserName = "target",
        ReporterUserId = reporterUserId,
        ReporterUserName = $"reporter-{reporterUserId}",
        ReasonType = "Spam",
        Status = (int)ContentReportStatusEnum.Pending,
        CreateTime = DateTime.UtcNow,
        CreateBy = $"reporter-{reporterUserId}",
        CreateId = reporterUserId
    };

    private static ServiceProvider CreateServices() => new ServiceCollection()
        .AddSingleton(TimeProvider.System)
        .BuildServiceProvider();

    private static SqlSugarClient CreateClient(string path) => new(new ConnectionConfig
    {
        ConfigId = "main",
        ConnectionString = $"Data Source={path}",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = true,
        InitKeyType = InitKeyType.Attribute
    });

    private static SqlSugarClient CreatePostgreSqlClient(string connectionString) =>
        PostgreSqlIntegrationSqlSugarFactory.CreateClient(new ConnectionConfig
        {
            ConfigId = "main",
            ConnectionString = connectionString,
            DbType = DbType.PostgreSQL,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });

    private static string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
