using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Radish.DbMigrate;
using Radish.Model;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class WikiAuthorCollaborationSchemaMigrationTest
{
    private const string PostgreSqlConnectionStringEnvironmentVariable = "RADISH_TEST_POSTGRES_CONNECTION_STRING";
    [Fact(Timeout = 10_000)]
    public void Migration_ShouldCreateAuthoringTablesAndBackfillSafeOwnerOnSqlite()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-wiki-author-migration-{Guid.NewGuid():N}.db");
        using var db = CreateClient(path);
        using var services = new ServiceCollection().BuildServiceProvider();
        try
        {
            db.CodeFirst.InitTables<User>();
            db.CodeFirst.InitTables<LegacyWikiDocument>();
            db.Insertable(new User
            {
                Id = 10001,
                TenantId = 0,
                PublicId = "usr_00000000000000000000000000000001",
                UserName = "Author",
                UserEmail = "author@example.test",
                LoginPassword = "not-used",
                IsEnable = true,
                IsDeleted = false
            }).ExecuteCommand();
            db.Insertable(new LegacyWikiDocument
            {
                Id = 20001,
                TenantId = 0,
                SourceType = "Custom",
                CreateId = 10001,
                ModifyTime = DateTime.UtcNow
            }).ExecuteCommand();

            var migration = WikiAuthorCollaborationSchemaMigration.Instance;
            var connection = Assert.IsType<SqliteConnection>(db.Ado.Connection);
            connection.Open();
            db.Ado.Transaction = connection.BeginTransaction(deferred: false);
            try
            {
                migration.Apply(db, services);
                Assert.Empty(migration.Verify(db, services));
                db.Ado.CommitTran();
            }
            catch
            {
                db.Ado.RollbackTran();
                throw;
            }

            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            Assert.True(db.DbMaintenance.IsAnyTable("WikiDocumentDraft", false));
            Assert.True(db.DbMaintenance.IsAnyTable("WikiDocumentCollaborator", false));
            Assert.True(db.DbMaintenance.IsAnyTable("WikiDocumentReviewEvent", false));
            Assert.True(db.DbMaintenance.IsAnyIndex("idx_wikidoc_active_draft"));
            Assert.True(db.DbMaintenance.IsAnyIndex("idx_wikicollab_document_user"));
            Assert.Equal(10001, db.Queryable<WikiDocument>().Select(document => document.OwnerUserId).Single());
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
    public async Task Migration_ShouldCreateAuthoringSchemaAndRemainRepeatableOnPostgreSql()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过 Wiki authoring PostgreSQL 迁移测试");
        var schema = $"wiki_author_migration_{Guid.NewGuid():N}";
        using var admin = CreatePostgreSqlClient(adminConnectionString!);
        await admin.Ado.ExecuteCommandAsync($"CREATE SCHEMA {QuoteIdentifier(schema)}");
        try
        {
            using var db = CreatePostgreSqlClient(
                $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false");
            using var services = new ServiceCollection().BuildServiceProvider();
            db.CodeFirst.InitTables<User>();
            db.CodeFirst.InitTables<WikiDocument>();

            var migration = WikiAuthorCollaborationSchemaMigration.Instance;
            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
        }
        finally
        {
            await admin.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS {QuoteIdentifier(schema)} CASCADE");
        }
    }

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

    [SugarTable("WikiDocument")]
    private sealed class LegacyWikiDocument
    {
        [SugarColumn(IsPrimaryKey = true)]
        public long Id { get; set; }

        public long TenantId { get; set; }

        [SugarColumn(Length = 30, IsNullable = false)]
        public string SourceType { get; set; } = "Manual";

        public long CreateId { get; set; }

        [SugarColumn(IsNullable = true)]
        public DateTime? ModifyTime { get; set; }
    }
}
