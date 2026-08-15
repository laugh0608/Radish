using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Radish.DbMigrate;
using Radish.Model;
using Radish.Api.Tests.TestCollections;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

[Collection(PostgreSqlIntegrationCollection.CollectionName)]
public sealed class ContentRewardSchemaMigrationTest
{
    private const string PostgreSqlConnectionStringEnvironmentVariable =
        "RADISH_TEST_POSTGRES_CONNECTION_STRING";

    [Fact]
    public void MainMigration_ShouldBeRepeatableAndEnforceSenderTargetUniqueness()
    {
        var path = CreatePath("main");
        using var db = CreateClient("main", path);
        using var services = new ServiceCollection()
            .AddSingleton<ISqlSugarClient>(db)
            .BuildServiceProvider();
        try
        {
            ContentRewardSchemaMigration.Instance.Apply(db, services);
            ContentRewardSchemaMigration.Instance.Apply(db, services);

            Assert.Empty(ContentRewardSchemaMigration.Instance.Verify(db, services));
            db.Insertable(CreateReward(1, 11)).ExecuteCommand();
            Assert.ThrowsAny<Exception>(() =>
                db.Insertable(CreateReward(2, 12)).ExecuteCommand());
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public void LogMigration_ShouldBeRepeatableAndEnforceProjectionKeyUniqueness()
    {
        var path = CreatePath("log");
        using var db = CreateClient("log", path);
        using var services = new ServiceCollection()
            .AddSingleton<ISqlSugarClient>(db)
            .BuildServiceProvider();
        try
        {
            ContentRewardAuditProjectionSchemaMigration.Instance.Apply(db, services);
            ContentRewardAuditProjectionSchemaMigration.Instance.Apply(db, services);

            Assert.Empty(ContentRewardAuditProjectionSchemaMigration.Instance.Verify(db, services));
            db.Insertable(CreateBalanceLog(1)).SplitTable().ExecuteCommand();
            Assert.ThrowsAny<Exception>(() =>
                db.Insertable(CreateBalanceLog(2)).SplitTable().ExecuteCommand());
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    [Trait("Database", "PostgreSQL")]
    public async Task Migrations_ShouldApplyAndVerifyOnPostgreSql()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(
            PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过内容赞赏 PostgreSQL 迁移测试");

        var schema = $"content_reward_{Guid.NewGuid():N}";
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

            ContentRewardSchemaMigration.Instance.Apply(db, services);
            ContentRewardAuditProjectionSchemaMigration.Instance.Apply(db, services);

            Assert.Empty(ContentRewardSchemaMigration.Instance.Verify(db, services));
            Assert.Empty(ContentRewardAuditProjectionSchemaMigration.Instance.Verify(db, services));
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE");
        }
    }

    private static ContentReward CreateReward(long id, long transactionId)
    {
        return new ContentReward
        {
            Id = id,
            TenantId = 9,
            TargetType = "Post",
            TargetId = 7001,
            PostId = 7001,
            SenderUserId = 1001,
            RecipientUserId = 2002,
            Amount = 1,
            ReasonCode = "Helpful",
            CoinTransactionId = transactionId,
            CreateTime = DateTime.UtcNow,
            CreateBy = "test",
            CreateId = 1001
        };
    }

    private static BalanceChangeLog CreateBalanceLog(long id)
    {
        return new BalanceChangeLog
        {
            Id = id,
            TenantId = 9,
            UserId = 1001,
            TransactionId = 8101,
            ChangeAmount = -1,
            BalanceBefore = 3,
            BalanceAfter = 2,
            ChangeType = "TRANSFER_OUT",
            SourceEventKey = "content-reward:8101:1001:out",
            CreateTime = DateTime.UtcNow,
            CreateBy = "test",
            CreateId = 1001
        };
    }

    private static SqlSugarScope CreateClient(string configId, string path)
    {
        return new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = configId,
            DbType = DbType.Sqlite,
            ConnectionString = $"Data Source={path}",
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
    }

    private static string CreatePath(string scope)
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"radish-content-reward-migration-{scope}-{Guid.NewGuid():N}.db");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
