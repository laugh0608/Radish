using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Radish.DbMigrate;
using Radish.Model;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class ChatReadReceiptSchemaMigrationTest
{
    private const string PostgreSqlConnectionStringEnvironmentVariable = "RADISH_TEST_POSTGRES_CONNECTION_STRING";
    private const string ReadCursorIndex = "idx_channel_member_read_cursor";

    [Fact]
    public void Migration_ShouldCreateReadCursorIndexAndRemainRepeatableOnSqlite()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-chat-read-migration-{Guid.NewGuid():N}.db");
        using var db = CreateSqliteClient(path);
        using var services = new ServiceCollection().BuildServiceProvider();

        try
        {
            db.CodeFirst.InitTables<ChannelMember>();
            db.Ado.ExecuteCommand($"DROP INDEX IF EXISTS \"{ReadCursorIndex}\"");
            db.Insertable(CreateMember()).ExecuteCommand();

            var migration = ChatReadReceiptSchemaMigration.Instance;
            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Diagnose(db, services));
            Assert.Empty(migration.Verify(db, services));
            Assert.True(db.DbMaintenance.IsAnyIndex(ReadCursorIndex));
            Assert.Equal(100, db.Queryable<ChannelMember>().Single().LastReadMessageId);
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
    public async Task Migration_ShouldCreateReadCursorIndexAndRemainRepeatableOnPostgreSql()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过 Chat 阅读回执 PostgreSQL 迁移测试");

        var schema = $"chat_read_migration_{Guid.NewGuid():N}";
        using var adminDb = CreatePostgreSqlClient(adminConnectionString!);
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA {QuoteIdentifier(schema)}");
        try
        {
            var connectionString = $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
            using var db = CreatePostgreSqlClient(connectionString);
            using var services = new ServiceCollection().BuildServiceProvider();
            db.CodeFirst.InitTables<ChannelMember>();
            db.Ado.ExecuteCommand($"DROP INDEX IF EXISTS {QuoteIdentifier(ReadCursorIndex)}");
            db.Insertable(CreateMember()).ExecuteCommand();

            var migration = ChatReadReceiptSchemaMigration.Instance;
            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            Assert.True(db.DbMaintenance.IsAnyIndex(ReadCursorIndex));
            Assert.Equal(100, db.Queryable<ChannelMember>().Single().LastReadMessageId);
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS {QuoteIdentifier(schema)} CASCADE");
        }
    }

    private static ChannelMember CreateMember() => new()
    {
        Id = 71001,
        TenantId = 30000,
        ChannelId = 70001,
        UserId = 20001,
        LastReadMessageId = 100,
        JoinedAt = DateTime.UtcNow,
        CreateTime = DateTime.UtcNow,
        CreateBy = "Test",
        CreateId = 20001
    };

    private static SqlSugarClient CreateSqliteClient(string path) => new(new ConnectionConfig
    {
        ConfigId = "chat",
        ConnectionString = $"Data Source={path}",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = true,
        InitKeyType = InitKeyType.Attribute
    });

    private static SqlSugarClient CreatePostgreSqlClient(string connectionString) =>
        PostgreSqlIntegrationSqlSugarFactory.CreateClient(new ConnectionConfig
        {
            ConfigId = "chat",
            ConnectionString = connectionString,
            DbType = DbType.PostgreSQL,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });

    private static string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
