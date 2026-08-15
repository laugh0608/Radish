using System;
using System.IO;
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
public sealed class ForumContentRevisionSchemaMigrationTest
{
    private const string PostgreSqlConnectionStringEnvironmentVariable =
        "RADISH_TEST_POSTGRES_CONNECTION_STRING";

    [Fact]
    public void Migration_ShouldBackfillCompleteCurrentSnapshotsAndRemainRepeatableOnSqlite()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"radish-forum-content-revision-{Guid.NewGuid():N}.db");
        using var db = CreateSqlite(path);
        using var services = new ServiceCollection().AddSingleton<ISqlSugarClient>(db).BuildServiceProvider();
        try
        {
            Seed(db);
            var migration = ForumContentRevisionSchemaMigration.Instance;

            Assert.Empty(migration.Diagnose(db, services));
            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));

            var post = db.Queryable<Post>().Single();
            var comment = db.Queryable<Comment>().Single();
            Assert.Equal(1, post.ContentRevision);
            Assert.Equal(1, comment.ContentRevision);

            var postRevision = db.Queryable<PostContentRevision>().Single();
            Assert.Equal(post.Id, postRevision.PostId);
            Assert.Equal(ForumContentRevisionIntegrityStatuses.Complete, postRevision.IntegrityStatus);
            Assert.Equal(ForumContentRevisionSourceTypes.Baseline, postRevision.SourceType);

            var commentRevision = db.Queryable<CommentContentRevision>().Single();
            Assert.Equal(comment.Id, commentRevision.CommentId);
            Assert.Equal(ForumContentRevisionIntegrityStatuses.Complete, commentRevision.IntegrityStatus);
            Assert.Single(db.Queryable<PostContentRevisionTag>().ToList());
            Assert.Equal(3, db.Queryable<ForumContentRevisionAttachment>().Count());
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
    public void Verify_ShouldSupportLegacyPostAnswerSchemaBeforeAnswerLifecycleMigration()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"radish-forum-content-revision-legacy-answer-{Guid.NewGuid():N}.db");
        using var db = CreateSqlite(path);
        using var services = new ServiceCollection().AddSingleton<ISqlSugarClient>(db).BuildServiceProvider();
        try
        {
            Seed(db);
            db.Ado.ExecuteCommand(
                """
                CREATE TABLE "PostAnswer" (
                    "Id" INTEGER NOT NULL PRIMARY KEY,
                    "PostId" INTEGER NOT NULL,
                    "AuthorId" INTEGER NOT NULL,
                    "AuthorName" TEXT NOT NULL,
                    "Content" TEXT NOT NULL,
                    "IsAccepted" INTEGER NOT NULL,
                    "TenantId" INTEGER NOT NULL,
                    "IsDeleted" INTEGER NOT NULL
                )
                """);
            db.Ado.ExecuteCommand(
                """
                INSERT INTO "PostAnswer"
                    ("Id", "PostId", "AuthorId", "AuthorName", "Content", "IsAccepted", "TenantId", "IsDeleted")
                VALUES
                    (50, 30, 1003, 'LegacyAnswerer', '历史回答', 0, 9, 0)
                """);

            ForumContentRevisionSchemaMigration.Instance.Apply(db, services);

            Assert.Null(DatabaseIdentifierResolver.ResolveColumn(
                db,
                nameof(PostAnswer),
                nameof(PostAnswer.PublicId)));
            Assert.Empty(ForumContentRevisionSchemaMigration.Instance.Verify(db, services));
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
    public void Migration_ShouldMarkLegacySnapshotIncompleteWithoutInventingAttachmentRelations()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"radish-forum-content-revision-incomplete-{Guid.NewGuid():N}.db");
        using var db = CreateSqlite(path);
        using var services = new ServiceCollection().AddSingleton<ISqlSugarClient>(db).BuildServiceProvider();
        try
        {
            Seed(db);
            db.Updateable<Post>()
                .SetColumns(post => post.Content == "![missing](attachment://999)")
                .Where(post => post.Id == 30)
                .ExecuteCommand();

            ForumContentRevisionSchemaMigration.Instance.Apply(db, services);

            var revision = db.Queryable<PostContentRevision>().Single();
            Assert.Equal(ForumContentRevisionIntegrityStatuses.LegacyIncomplete, revision.IntegrityStatus);
            Assert.DoesNotContain(
                db.Queryable<ForumContentRevisionAttachment>().ToList(),
                reference =>
                    reference.TargetType == ForumContentRevisionTargetTypes.Post &&
                    reference.AttachmentId == 999);
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
    public async Task Migration_ShouldBackfillAndVerifyOnPostgreSql()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(
            PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过论坛内容版本 PostgreSQL 迁移测试");

        var schema = $"forum_content_revision_{Guid.NewGuid():N}";
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
            var connectionString =
                $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
            using var db = PostgreSqlIntegrationSqlSugarFactory.CreateScope(new ConnectionConfig
            {
                ConfigId = "main",
                ConnectionString = connectionString,
                DbType = DbType.PostgreSQL,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });
            using var services = new ServiceCollection()
                .AddSingleton<ISqlSugarClient>(db)
                .BuildServiceProvider();
            Seed(db);

            ForumContentRevisionSchemaMigration.Instance.Apply(db, services);
            ForumContentRevisionSchemaMigration.Instance.Apply(db, services);

            Assert.Empty(ForumContentRevisionSchemaMigration.Instance.Verify(db, services));
            Assert.Single(db.Queryable<PostContentRevision>().ToList());
            Assert.Single(db.Queryable<CommentContentRevision>().ToList());
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE");
        }
    }

    private static void Seed(ISqlSugarClient db)
    {
        db.CodeFirst.InitTables<Post>();
        db.CodeFirst.InitTables<Comment>();
        db.CodeFirst.InitTables<Category>();
        db.CodeFirst.InitTables<Tag>();
        db.CodeFirst.InitTables<PostTag>();
        db.CodeFirst.InitTables<Attachment>();

        db.Insertable(new Category("综合")
        {
            Id = 10,
            IsEnabled = true,
            IsDeleted = false
        }).ExecuteCommand();
        db.Insertable(new Tag("版本")
        {
            Id = 20,
            IsEnabled = true,
            IsDeleted = false
        }).ExecuteCommand();

        var post = new Post(new PostInitializationOptions("版本帖子", "正文 ![post](attachment://101)")
        {
            AuthorId = 1001,
            AuthorName = "Author",
            TenantId = 9,
            CategoryId = 10,
            CoverAttachmentId = 103,
            IsPublished = true
        })
        {
            Id = 30,
            ContentRevision = 0
        };
        db.Insertable(post).ExecuteCommand();
        db.Insertable(new PostTag(30, 20) { Id = 31 }).ExecuteCommand();

        var comment = new Comment(new CommentInitializationOptions("评论 ![comment](attachment://102)")
        {
            PostId = 30,
            AuthorId = 1002,
            AuthorName = "Commenter",
            TenantId = 9
        })
        {
            Id = 40,
            ContentRevision = 0
        };
        db.Insertable(comment).ExecuteCommand();

        db.Insertable(new[]
        {
            Attachment(101, "Post", 30),
            Attachment(102, "Comment", 40),
            Attachment(103, "Post", 30)
        }).ExecuteCommand();
    }

    private static Attachment Attachment(long id, string businessType, long businessId)
    {
        return new Attachment
        {
            Id = id,
            TenantId = 9,
            OriginalName = $"{id}.png",
            StoredName = $"{id}.png",
            Extension = ".png",
            MimeType = "image/png",
            StoragePath = $"forum/{id}.png",
            UploaderId = 1001,
            UploaderName = "Author",
            BusinessType = businessType,
            BusinessId = businessId,
            IsEnabled = true,
            IsDeleted = false,
            CreateBy = "Author",
            CreateId = 1001
        };
    }

    private static SqlSugarScope CreateSqlite(string path) =>
        new(new ConnectionConfig
        {
            ConfigId = "main",
            ConnectionString = $"Data Source={path}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
}
