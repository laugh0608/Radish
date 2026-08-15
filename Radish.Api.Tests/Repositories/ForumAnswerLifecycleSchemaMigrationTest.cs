using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Radish.DbMigrate;
using Radish.Model;
using Radish.Shared.Constants;
using Radish.Api.Tests.TestCollections;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

[Collection(PostgreSqlIntegrationCollection.CollectionName)]
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
            var strictMigration = ForumAnswerLifecycleStrictSchemaMigration.Instance;
            strictMigration.Apply(db, services);
            strictMigration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            Assert.Empty(strictMigration.Verify(db, services));
            Assert.True(db.DbMaintenance.IsAnyIndex("idx_postanswer_public_id"));
            Assert.True(db.DbMaintenance.IsAnyIndex("idx_postanswer_tenant_post_visibility_page"));
            Assert.True(db.DbMaintenance.IsAnyIndex("idx_postanswer_tenant_author_history"));
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
    public void StrictVerify_ShouldReportAcceptanceCountAndAttachmentOwnershipDrift()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"radish-forum-answer-lifecycle-strict-{Guid.NewGuid():N}.db");
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
                Id = 3101,
                PublicId = PostAnswer.GeneratePublicId(),
                PostId = 2101,
                AuthorId = 1101,
                AuthorName = "Answerer",
                Content = "回答 ![证据](attachment://5101)",
                IsAccepted = true,
                ContentRevision = 1,
                TenantId = 9,
                CreateBy = "Answerer",
                CreateId = 1101
            }).ExecuteCommand();
            db.Insertable(new PostQuestion
            {
                Id = 4101,
                PostId = 2101,
                IsSolved = true,
                AcceptedAnswerId = 3101,
                AnswerCount = 1,
                TenantId = 9,
                CreateBy = "Owner",
                CreateId = 2101
            }).ExecuteCommand();
            db.Insertable(new Attachment
            {
                Id = 5101,
                TenantId = 9,
                UploaderId = 1101,
                BusinessType = AttachmentBusinessTypes.PostAnswer,
                BusinessId = 3101,
                CreateBy = "Answerer",
                CreateId = 1101
            }).ExecuteCommand();

            ForumAnswerLifecycleSchemaMigration.Instance.Apply(db, services);
            ForumAnswerLifecycleStrictSchemaMigration.Instance.Apply(db, services);
            Assert.Empty(ForumAnswerLifecycleStrictSchemaMigration.Instance.Verify(db, services));

            db.Updateable<PostQuestion>()
                .SetColumns(question => new PostQuestion
                {
                    IsSolved = false,
                    AnswerCount = 4
                })
                .Where(question => question.Id == 4101)
                .ExecuteCommand();
            db.Updateable<PostAnswer>()
                .SetColumns(answer => new PostAnswer { IsAccepted = false })
                .Where(answer => answer.Id == 3101)
                .ExecuteCommand();
            db.Updateable<Attachment>()
                .SetColumns(attachment => new Attachment
                {
                    BusinessType = AttachmentBusinessTypes.Comment
                })
                .Where(attachment => attachment.Id == 5101)
                .ExecuteCommand();

            var issues = ForumAnswerLifecycleStrictSchemaMigration.Instance.Verify(db, services);

            Assert.Contains(issues, issue => issue.Contains("可见回答重建值", StringComparison.Ordinal));
            Assert.Contains(issues, issue => issue.Contains("当前采纳指向或状态投影", StringComparison.Ordinal));
            Assert.Contains(issues, issue => issue.Contains("IsAccepted 与 PostQuestion 指针", StringComparison.Ordinal));
            Assert.Contains(issues, issue => issue.Contains("仍占用 Comment 业务类型", StringComparison.Ordinal));
            Assert.Contains(issues, issue => issue.Contains("缺少有效归属或持久引用", StringComparison.Ordinal));
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
            ForumAnswerLifecycleStrictSchemaMigration.Instance.Apply(db, services);
            ForumAnswerLifecycleStrictSchemaMigration.Instance.Apply(db, services);

            Assert.Empty(ForumAnswerLifecycleSchemaMigration.Instance.Verify(db, services));
            Assert.Empty(ForumAnswerLifecycleStrictSchemaMigration.Instance.Verify(db, services));
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE");
        }
    }
}
