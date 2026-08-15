using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Radish.DbMigrate;
using Radish.Model;
using Radish.Shared.CustomEnum;
using Radish.Api.Tests.TestCollections;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

[Collection(PostgreSqlIntegrationCollection.CollectionName)]
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

    [Fact(Timeout = 10_000)]
    public void AppealMigrations_ShouldRemainRepeatableOnSqlite()
    {
        var mainPath = Path.Combine(Path.GetTempPath(), $"radish-appeal-main-{Guid.NewGuid():N}.db");
        var chatPath = Path.Combine(Path.GetTempPath(), $"radish-appeal-chat-{Guid.NewGuid():N}.db");
        using var services = CreateServices();
        try
        {
            using (var main = CreateClient(mainPath))
            {
                ContentModerationCaseSchemaMigration.Instance.Apply(main, services);
                main.CodeFirst.InitTables<Post>();
                main.Insertable(new Post
                {
                    Id = 7001,
                    TenantId = 9,
                    Title = "legacy restricted",
                    Content = "content",
                    AuthorId = 5001,
                    AuthorName = "target",
                    IsPublished = true,
                    PublishTime = DateTime.UtcNow,
                    IsDeleted = true,
                    CreateTime = DateTime.UtcNow,
                    CreateBy = "target",
                    CreateId = 5001
                }).ExecuteCommand();
                main.Insertable(new ContentModerationCase
                {
                    Id = 7101,
                    TenantId = 9,
                    PublicId = "case_legacy_restricted",
                    OpenTargetKey = "1:7001",
                    TargetType = (int)ContentReportTargetTypeEnum.Post,
                    TargetContentId = 7001,
                    TargetUserId = 5001,
                    Status = (int)ContentModerationCaseStatus.Resolved,
                    Decision = (int)ContentModerationDecision.Violation,
                    TargetDisposition = (int)ContentModerationTargetDisposition.Restricted,
                    ResolvedAt = DateTime.UtcNow,
                    ResolvedById = 9001,
                    ResolvedByName = "reviewer",
                    CreateTime = DateTime.UtcNow,
                    CreateBy = "reviewer",
                    CreateId = 9001
                }).ExecuteCommand();
                main.Insertable(new ContentModerationCaseEvent
                {
                    Id = 7201,
                    TenantId = 9,
                    CaseId = 7101,
                    EventSequence = 1,
                    EventType = "DecisionRecorded",
                    ResultCode = "Restricted",
                    ActorUserId = 9001,
                    ActorName = "reviewer",
                    CreateTime = DateTime.UtcNow
                }).ExecuteCommand();
                ContentModerationAppealSchemaMigration.Instance.Apply(main, services);
                ContentModerationAppealSchemaMigration.Instance.Apply(main, services);
                Assert.Empty(ContentModerationAppealSchemaMigration.Instance.Verify(main, services));
                var source = Assert.Single(main.Queryable<ContentModerationTargetAction>().ToList());
                Assert.True(source.ChangedTargetState);
                Assert.Equal(source.Id, main.Queryable<Post>().InSingle(7001).ModerationTargetActionId);
            }

            using (var chat = CreateClient(chatPath))
            {
                ChatModerationReliefSchemaMigration.Instance.Apply(chat, services);
                ChatModerationReliefSchemaMigration.Instance.Apply(chat, services);
                Assert.Empty(ChatModerationReliefSchemaMigration.Instance.Verify(chat, services));
            }
        }
        finally
        {
            if (File.Exists(mainPath))
            {
                File.Delete(mainPath);
            }

            if (File.Exists(chatPath))
            {
                File.Delete(chatPath);
            }
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
