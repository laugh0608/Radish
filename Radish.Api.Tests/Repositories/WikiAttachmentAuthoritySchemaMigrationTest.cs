using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Radish.DbMigrate;
using Radish.Model;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class WikiAttachmentAuthoritySchemaMigrationTest
{
    private const string PostgreSqlConnectionStringEnvironmentVariable =
        "RADISH_TEST_POSTGRES_CONNECTION_STRING";

    [Fact]
    public void Migration_ShouldBackfillOnlyWikiAndProvenLegacyDocumentOnSqlite()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"radish-wiki-attachment-migration-{Guid.NewGuid():N}.db");
        using var db = CreateSqlite(path);
        using var services = CreateServices(db);
        try
        {
            Seed(db);
            var migration = WikiAttachmentAuthoritySchemaMigration.Instance;

            Assert.Empty(migration.Diagnose(db, services));
            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            AssertBackfill(db);
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
    public void Migration_ShouldReapplyAfterSqliteBackupRestore()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"radish-wiki-attachment-restore-{Guid.NewGuid():N}.db");
        var backupPath = $"{path}.backup";
        try
        {
            using (var seedDb = CreateSqlite(path))
            {
                Seed(seedDb);
            }
            File.Copy(path, backupPath);

            using (var firstDb = CreateSqlite(path))
            using (var firstServices = CreateServices(firstDb))
            {
                WikiAttachmentAuthoritySchemaMigration.Instance.Apply(firstDb, firstServices);
                AssertBackfill(firstDb);
            }

            File.Copy(backupPath, path, overwrite: true);
            using var restoredDb = CreateSqlite(path);
            using var restoredServices = CreateServices(restoredDb);
            WikiAttachmentAuthoritySchemaMigration.Instance.Apply(restoredDb, restoredServices);

            Assert.Empty(WikiAttachmentAuthoritySchemaMigration.Instance.Verify(
                restoredDb,
                restoredServices));
            AssertBackfill(restoredDb);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
    }

    [Fact]
    public void Migration_ShouldRejectCrossTenantReferenceWithoutGuessing()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"radish-wiki-attachment-invalid-{Guid.NewGuid():N}.db");
        using var db = CreateSqlite(path);
        using var services = CreateServices(db);
        try
        {
            Seed(db);
            db.Updateable<Attachment>()
                .SetColumns(attachment => attachment.TenantId == 10)
                .Where(attachment => attachment.Id == 101)
                .ExecuteCommand();

            var issues = WikiAttachmentAuthoritySchemaMigration.Instance.Diagnose(db, services);

            Assert.Contains(issues, issue => issue.Contains("跨租户", StringComparison.Ordinal));
            Assert.Throws<InvalidOperationException>(() =>
                WikiAttachmentAuthoritySchemaMigration.Instance.Apply(db, services));
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
    public void Migration_ReapplyShouldConvergeChangedSourceTargetSet()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"radish-wiki-attachment-converge-{Guid.NewGuid():N}.db");
        using var db = CreateSqlite(path);
        using var services = CreateServices(db);
        try
        {
            Seed(db);
            var migration = WikiAttachmentAuthoritySchemaMigration.Instance;
            migration.Apply(db, services);

            db.Updateable<WikiDocument>()
                .SetColumns(document => new WikiDocument
                {
                    MarkdownContent = "![wiki](attachment://101)",
                    CoverAttachmentId = null
                })
                .Where(document => document.Id == 201)
                .ExecuteCommand();
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            var removedCurrentReferences = db.Queryable<WikiAttachmentReference>()
                .Where(reference =>
                    reference.DocumentId == 201 &&
                    reference.IsDeleted &&
                    (reference.AttachmentId == 102 || reference.AttachmentId == 105))
                .ToList();
            Assert.Equal(2, removedCurrentReferences.Count);
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
    public void Migration_ShouldRejectDraftOwnedByAnotherTenant()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"radish-wiki-attachment-source-tenant-{Guid.NewGuid():N}.db");
        using var db = CreateSqlite(path);
        using var services = CreateServices(db);
        try
        {
            Seed(db);
            db.Updateable<WikiDocumentDraft>()
                .SetColumns(draft => draft.TenantId == 10)
                .Where(draft => draft.Id == 301)
                .ExecuteCommand();

            var issues = WikiAttachmentAuthoritySchemaMigration.Instance.Diagnose(db, services);

            Assert.Contains(issues, issue =>
                issue.Contains("草稿", StringComparison.Ordinal) &&
                issue.Contains("跨租户", StringComparison.Ordinal));
            Assert.Throws<InvalidOperationException>(() =>
                WikiAttachmentAuthoritySchemaMigration.Instance.Apply(db, services));
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
    public void Migration_ShouldReportLegacyUserUploadUrlWithoutTreatingCodeExampleAsReference()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"radish-wiki-attachment-legacy-url-{Guid.NewGuid():N}.db");
        using var db = CreateSqlite(path);
        using var services = CreateServices(db);
        try
        {
            Seed(db);
            db.Updateable<WikiDocument>()
                .SetColumns(document => document.MarkdownContent ==
                    "![legacy](/uploads/Wiki/old.png)\n`![example](/uploads/Wiki/example.png)`")
                .Where(document => document.Id == 201)
                .ExecuteCommand();

            var issues = WikiAttachmentAuthoritySchemaMigration.Instance.Diagnose(db, services);

            var legacyIssue = Assert.Single(issues, issue =>
                issue.Contains("未迁移的站内旧附件 URL", StringComparison.Ordinal));
            Assert.Contains("/uploads/Wiki/old.png", legacyIssue, StringComparison.Ordinal);
            Assert.DoesNotContain("example.png", legacyIssue, StringComparison.Ordinal);
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
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过 Wiki 附件 PostgreSQL 迁移测试");

        var schema = $"wiki_attachment_migration_{Guid.NewGuid():N}";
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
            using var db = new SqlSugarScope(new ConnectionConfig
            {
                ConfigId = "main",
                ConnectionString = connectionString,
                DbType = DbType.PostgreSQL,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
                MoreSettings = new ConnMoreSettings { PgSqlIsAutoToLower = false }
            });
            using var services = CreateServices(db);
            Seed(db);

            db.Ado.BeginTran();
            try
            {
                WikiAttachmentAuthoritySchemaMigration.Instance.Apply(db, services);
                Assert.Empty(WikiAttachmentAuthoritySchemaMigration.Instance.Verify(db, services));
            }
            finally
            {
                db.Ado.RollbackTran();
            }

            Assert.All(
                db.Queryable<Attachment>().ToList(),
                attachment => Assert.True(attachment.IsPublic));
            WikiAttachmentAuthoritySchemaMigration.Instance.Apply(db, services);
            WikiAttachmentAuthoritySchemaMigration.Instance.Apply(db, services);

            Assert.Empty(WikiAttachmentAuthoritySchemaMigration.Instance.Verify(db, services));
            AssertBackfill(db);
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE");
        }
    }

    private static void Seed(ISqlSugarClient db)
    {
        db.CodeFirst.InitTables<Attachment>();
        db.CodeFirst.InitTables<WikiDocument>();
        db.CodeFirst.InitTables<WikiDocumentDraft>();
        db.CodeFirst.InitTables<WikiDocumentRevision>();
        db.Insertable(new[]
        {
            Attachment(101, AttachmentBusinessTypes.Wiki),
            Attachment(102, AttachmentBusinessTypes.Wiki),
            Attachment(103, AttachmentBusinessTypes.Wiki),
            Attachment(104, AttachmentBusinessTypes.Document),
            Attachment(105, AttachmentBusinessTypes.Document),
            Attachment(106, AttachmentBusinessTypes.Wiki)
        }).ExecuteCommand();
        db.Insertable(new WikiDocument
        {
            Id = 201,
            TenantId = 9,
            Title = "Guide",
            Slug = "guide",
            MarkdownContent = "![wiki](attachment://101) [legacy](attachment://105)",
            CoverAttachmentId = 102,
            Status = (int)WikiDocumentStatusEnum.Published,
            Visibility = (int)WikiDocumentVisibilityEnum.Public,
            SourceType = "Custom",
            Version = 1,
            CreateId = 1001,
            CreateBy = "Author"
        }).ExecuteCommand();
        db.Insertable(new WikiDocumentDraft
        {
            Id = 301,
            TenantId = 9,
            DocumentId = 201,
            Title = "Guide draft",
            Slug = "guide",
            MarkdownContent = "![draft](attachment://103)",
            ReviewState = (int)WikiDocumentDraftState.Editing,
            DraftVersion = 1,
            CreateId = 1001,
            CreateBy = "Author"
        }).ExecuteCommand();
        db.Insertable(new WikiDocumentRevision
        {
            Id = 401,
            TenantId = 9,
            DocumentId = 201,
            Version = 1,
            Title = "Guide",
            MarkdownContent = "![old](attachment://101)",
            SourceType = "Custom",
            CreateId = 1001,
            CreateBy = "Author"
        }).ExecuteCommand();
    }

    private static Attachment Attachment(long id, string businessType) =>
        new()
        {
            Id = id,
            TenantId = 9,
            OriginalName = $"{id}.png",
            StoredName = $"{id}.png",
            Extension = ".png",
            FileSize = 1,
            MimeType = "image/png",
            StoragePath = $"{businessType}/{id}.png",
            UploaderId = 1001,
            UploaderName = "Author",
            BusinessType = businessType,
            IsPublic = true,
            IsEnabled = true,
            CreateId = 1001,
            CreateBy = "Author"
        };

    private static void AssertBackfill(ISqlSugarClient db)
    {
        var references = db.Queryable<WikiAttachmentReference>()
            .Where(reference => !reference.IsDeleted)
            .ToList();
        Assert.Equal(5, references.Count);
        Assert.Contains(references, reference =>
            reference.AttachmentId == 102 &&
            reference.ReferenceKind == (int)WikiAttachmentReferenceKind.DocumentCover);
        Assert.Contains(references, reference =>
            reference.AttachmentId == 103 &&
            reference.ReferenceKind == (int)WikiAttachmentReferenceKind.DraftContent);
        Assert.Contains(references, reference =>
            reference.AttachmentId == 101 &&
            reference.ReferenceKind == (int)WikiAttachmentReferenceKind.RevisionContent);

        var attachments = db.Queryable<Attachment>()
            .ToList()
            .ToDictionary(attachment => attachment.Id);
        Assert.False(attachments[101].IsPublic);
        Assert.False(attachments[102].IsPublic);
        Assert.False(attachments[103].IsPublic);
        Assert.True(attachments[104].IsPublic);
        Assert.False(attachments[105].IsPublic);
        Assert.False(attachments[106].IsPublic);
    }

    private static ServiceProvider CreateServices(ISqlSugarClient db) =>
        new ServiceCollection()
            .AddSingleton(db)
            .BuildServiceProvider();

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
