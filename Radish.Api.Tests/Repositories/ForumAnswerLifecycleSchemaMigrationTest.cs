using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Radish.DbMigrate;
using Radish.Model;
using Radish.Shared.Constants;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class ForumAnswerLifecycleSchemaMigrationTest
{
    private const string PostgreSqlConnectionStringEnvironmentVariable =
        "RADISH_TEST_POSTGRES_CONNECTION_STRING";

    [Fact]
    public void Migration_ShouldBackfillPublicIdRevisionAcceptanceAndAttachmentType()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"radish-forum-answer-lifecycle-{Guid.NewGuid():N}.db");
        using var db = new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "main",
            ConnectionString = $"Data Source={path}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
        using var services = new ServiceCollection()
            .AddSingleton<ISqlSugarClient>(db)
            .BuildServiceProvider();
        try
        {
            db.CodeFirst.InitTables<PostAnswer>();
            db.CodeFirst.InitTables<PostQuestion>();
            db.CodeFirst.InitTables<Attachment>();
            db.Insertable(new PostAnswer
            {
                Id = 3001,
                PublicId = string.Empty,
                PostId = 2001,
                AuthorId = 1001,
                AuthorName = "Answerer",
                Content = "历史回答 ![证据](attachment://5001)",
                IsAccepted = true,
                ContentRevision = 0,
                TenantId = 9,
                CreateTime = new DateTime(2026, 7, 27, 8, 0, 0, DateTimeKind.Utc),
                CreateBy = "Answerer",
                CreateId = 1001
            }).ExecuteCommand();
            db.Insertable(new PostQuestion
            {
                Id = 4001,
                PostId = 2001,
                IsSolved = true,
                AcceptedAnswerId = 3001,
                AnswerCount = 1,
                TenantId = 9,
                CreateBy = "Owner",
                CreateId = 2001
            }).ExecuteCommand();
            db.Insertable(new Attachment
            {
                Id = 5001,
                TenantId = 9,
                UploaderId = 1001,
                BusinessType = AttachmentBusinessTypes.Comment,
                BusinessId = 3001,
                CreateBy = "Answerer",
                CreateId = 1001
            }).ExecuteCommand();

            var migration = ForumAnswerLifecycleSchemaMigration.Instance;
            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            var answer = db.Queryable<PostAnswer>().Single();
            Assert.True(PostAnswer.HasPublicIdFormat(answer.PublicId));
            Assert.Equal(1, answer.ContentRevision);
            var revision = Assert.Single(db.Queryable<PostAnswerContentRevision>().ToList());
            Assert.Equal(answer.Id, revision.AnswerId);
            Assert.Equal(ForumContentRevisionSourceTypes.Baseline, revision.SourceType);

            var question = db.Queryable<PostQuestion>().Single();
            Assert.Equal(1, question.AcceptanceRevision);
            Assert.Equal(1, question.AcceptedAnswerContentRevision);
            Assert.Single(db.Queryable<PostAnswerAcceptanceEvent>().ToList());
            Assert.Single(db.Queryable<ForumContentRevisionAttachment>().ToList());
            Assert.Equal(
                AttachmentBusinessTypes.PostAnswer,
                db.Queryable<Attachment>().Single().BusinessType);
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
    public async Task Migration_ShouldApplyAndRemainRepeatableOnPostgreSql()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(
            PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过回答生命周期 PostgreSQL 迁移测试");

        var schema = $"forum_answer_lifecycle_{Guid.NewGuid():N}";
        using var adminDb = PostgreSqlIntegrationSqlSugarFactory.CreateClient(new ConnectionConfig
        {
            ConfigId = "admin",
            ConnectionString = adminConnectionString!,
            DbType = DbType.PostgreSQL,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA \"{schema}\"");
        try
        {
            using var db = new SqlSugarScope(new ConnectionConfig
            {
                ConfigId = "main",
                ConnectionString =
                    $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false",
                DbType = DbType.PostgreSQL,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });
            using var services = new ServiceCollection()
                .AddSingleton<ISqlSugarClient>(db)
                .BuildServiceProvider();
            db.CodeFirst.InitTables<PostAnswer>();
            db.CodeFirst.InitTables<PostQuestion>();
            db.CodeFirst.InitTables<Attachment>();

            ForumAnswerLifecycleSchemaMigration.Instance.Apply(db, services);
            ForumAnswerLifecycleSchemaMigration.Instance.Apply(db, services);

            Assert.Empty(ForumAnswerLifecycleSchemaMigration.Instance.Verify(db, services));
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE");
        }
    }
}
