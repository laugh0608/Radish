using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Radish.DbMigrate;
using Radish.Model;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class WikiDocumentGovernanceSchemaMigrationTest
{
    private const string PostgreSqlConnectionStringEnvironmentVariable =
        "RADISH_TEST_POSTGRES_CONNECTION_STRING";

    [Fact]
    public void Migration_ShouldUpgradeLegacyDocumentsAndRemainRepeatableOnSqlite()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"radish-wiki-governance-migration-{Guid.NewGuid():N}.db");
        using var db = CreateSqliteClient(path);
        using var services = new ServiceCollection().BuildServiceProvider();

        try
        {
            SeedLegacyDocument(db);
            var migration = WikiDocumentGovernanceSchemaMigration.Instance;

            Assert.Contains(
                migration.Diagnose(db, services),
                warning => warning.Contains("1 篇历史 Wiki 文档", StringComparison.Ordinal));

            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            Assert.Equal(0, db.Queryable<WikiDocument>().InSingle(20001).GovernanceVersion);
            Assert.Contains(
                SchemaMigrationRegistry.All,
                item => item.MigrationId == "20260813_021_wiki_document_governance" &&
                        item.Scope == "Main");

            db.Insertable(CreateEvent(30001)).ExecuteCommand();
            Assert.ThrowsAny<Exception>(() => db.Insertable(CreateEvent(30002)).ExecuteCommand());
            Assert.Empty(migration.Verify(db, services));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    [Trait("Database", "PostgreSQL")]
    public async Task Migration_ShouldSupportPostgreSql()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(
            PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过 Wiki 治理 PostgreSQL 迁移测试");

        var schema = $"wiki_governance_{Guid.NewGuid():N}";
        using var adminDb = CreatePostgreSqlClient(adminConnectionString!);
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA {QuoteIdentifier(schema)}");
        try
        {
            var connectionString =
                $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
            using var db = CreatePostgreSqlClient(connectionString);
            using var services = new ServiceCollection().BuildServiceProvider();
            SeedLegacyDocument(db);

            var migration = WikiDocumentGovernanceSchemaMigration.Instance;
            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            Assert.Equal(0, db.Queryable<WikiDocument>().InSingle(20001).GovernanceVersion);
            db.Insertable(CreateEvent(30001)).ExecuteCommand();
            Assert.ThrowsAny<Exception>(() => db.Insertable(CreateEvent(30002)).ExecuteCommand());
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync(
                $"DROP SCHEMA IF EXISTS {QuoteIdentifier(schema)} CASCADE");
        }
    }

    private static void SeedLegacyDocument(ISqlSugarClient db)
    {
        db.CodeFirst.InitTables<WikiDocument>();
        db.Insertable(new WikiDocument
        {
            Id = 20001,
            TenantId = 0,
            Title = "Legacy",
            Slug = "legacy",
            MarkdownContent = "legacy body",
            Status = (int)WikiDocumentStatusEnum.Draft,
            Visibility = (int)WikiDocumentVisibilityEnum.Authenticated,
            SourceType = "Custom",
            Version = 1,
            CreateId = 10001,
            CreateBy = "Tester",
            CreateTime = DateTime.UtcNow
        }).ExecuteCommand();
        DropColumn(db, "WikiDocument", nameof(WikiDocument.GovernanceVersion));
    }

    private static WikiDocumentGovernanceEvent CreateEvent(long id) => new()
    {
        Id = id,
        TenantId = 0,
        DocumentId = 20001,
        Action = WikiDocumentGovernanceActions.Publish,
        FromStatus = (int)WikiDocumentStatusEnum.Draft,
        ToStatus = (int)WikiDocumentStatusEnum.Published,
        FromVisibility = (int)WikiDocumentVisibilityEnum.Authenticated,
        ToVisibility = (int)WikiDocumentVisibilityEnum.Authenticated,
        FromIsDeleted = false,
        ToIsDeleted = false,
        FromDocumentVersion = 1,
        ToDocumentVersion = 1,
        ExpectedGovernanceVersion = 0,
        ResultGovernanceVersion = 1,
        Reason = "首次发布",
        ActorUserId = 10001,
        ActorName = "Tester",
        CreateTime = DateTime.UtcNow
    };

    private static SqlSugarScope CreateSqliteClient(string path) => new(new ConnectionConfig
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

    private static void DropColumn(ISqlSugarClient db, string tableName, string columnName)
    {
        var column = DatabaseIdentifierResolver.ResolveColumn(db, tableName, columnName)
                     ?? throw new InvalidOperationException($"{tableName}.{columnName} 不存在。");
        db.Ado.ExecuteCommand(
            $"ALTER TABLE {QuoteIdentifier(column.TableName)} DROP COLUMN {QuoteIdentifier(column.ColumnName)}");
    }

    private static string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
