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
public sealed class UserBlockAuthoritySchemaMigrationTest
{
    private const string PostgreSqlConnectionStringEnvironmentVariable = "RADISH_TEST_POSTGRES_CONNECTION_STRING";
    private static readonly DateTime OldestBlockUtc =
        new(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Migration_ShouldBackfillDirectBlockAndRemainRepeatableOnSqlite()
    {
        var mainPath = Path.Combine(Path.GetTempPath(), $"radish-user-block-main-{Guid.NewGuid():N}.db");
        var chatPath = Path.Combine(Path.GetTempPath(), $"radish-user-block-chat-{Guid.NewGuid():N}.db");
        using var scope = CreateSqliteScope(mainPath, chatPath);
        using var services = CreateServices(scope);
        try
        {
            SeedLegacyState(scope);
            var migration = UserBlockAuthoritySchemaMigration.Instance;

            Assert.Empty(migration.Diagnose(scope.GetConnectionScope("main"), services));
            migration.Apply(scope.GetConnectionScope("main"), services);
            migration.Apply(scope.GetConnectionScope("main"), services);

            Assert.Empty(migration.Verify(scope.GetConnectionScope("main"), services));
            AssertBackfill(scope.GetConnectionScope("main"));
            Assert.ThrowsAny<Exception>(() =>
                scope.GetConnectionScope("main").Insertable(new UserBlock
                {
                    Id = 9901,
                    TenantId = 9,
                    BlockerUserId = 1001,
                    BlockedUserId = 1001,
                    RelationshipVersion = 1,
                    CreateTime = DateTime.UtcNow,
                    CreateBy = "test",
                    CreateId = 1001
                }).ExecuteCommand());
        }
        finally
        {
            DeleteIfExists(mainPath);
            DeleteIfExists(chatPath);
        }
    }

    [Fact]
    public void Migration_ShouldReapplyAfterSqliteBackupRestore()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var mainPath = Path.Combine(Path.GetTempPath(), $"radish-user-block-restore-main-{suffix}.db");
        var chatPath = Path.Combine(Path.GetTempPath(), $"radish-user-block-restore-chat-{suffix}.db");
        var mainBackupPath = $"{mainPath}.backup";
        var chatBackupPath = $"{chatPath}.backup";
        try
        {
            using (var seedScope = CreateSqliteScope(mainPath, chatPath))
            {
                SeedLegacyState(seedScope);
            }

            File.Copy(mainPath, mainBackupPath);
            File.Copy(chatPath, chatBackupPath);
            using (var firstScope = CreateSqliteScope(mainPath, chatPath))
            using (var firstServices = CreateServices(firstScope))
            {
                UserBlockAuthoritySchemaMigration.Instance.Apply(
                    firstScope.GetConnectionScope("main"),
                    firstServices);
                AssertBackfill(firstScope.GetConnectionScope("main"));
            }

            File.Copy(mainBackupPath, mainPath, overwrite: true);
            File.Copy(chatBackupPath, chatPath, overwrite: true);
            using var restoredScope = CreateSqliteScope(mainPath, chatPath);
            using var restoredServices = CreateServices(restoredScope);
            UserBlockAuthoritySchemaMigration.Instance.Apply(
                restoredScope.GetConnectionScope("main"),
                restoredServices);

            Assert.Empty(UserBlockAuthoritySchemaMigration.Instance.Verify(
                restoredScope.GetConnectionScope("main"),
                restoredServices));
            AssertBackfill(restoredScope.GetConnectionScope("main"));
        }
        finally
        {
            DeleteIfExists(mainPath);
            DeleteIfExists(chatPath);
            DeleteIfExists(mainBackupPath);
            DeleteIfExists(chatBackupPath);
        }
    }

    [Fact]
    public void Migration_ShouldReportInvalidLegacyActorWithoutGuessing()
    {
        var mainPath = Path.Combine(Path.GetTempPath(), $"radish-user-block-invalid-main-{Guid.NewGuid():N}.db");
        var chatPath = Path.Combine(Path.GetTempPath(), $"radish-user-block-invalid-chat-{Guid.NewGuid():N}.db");
        using var scope = CreateSqliteScope(mainPath, chatPath);
        using var services = CreateServices(scope);
        try
        {
            SeedLegacyState(scope);
            var chatDb = scope.GetConnectionScope("chat");
            chatDb.Updateable<DirectConversation>()
                .SetColumns(item => item.BlockedByUserId == 9999)
                .Where(item => item.Id == 3001)
                .ExecuteCommand();

            var issues = UserBlockAuthoritySchemaMigration.Instance.Diagnose(
                scope.GetConnectionScope("main"),
                services);

            Assert.Contains(issues, issue => issue.Contains("不是会话成员", StringComparison.Ordinal));
            Assert.Throws<InvalidOperationException>(() =>
                UserBlockAuthoritySchemaMigration.Instance.Apply(
                    scope.GetConnectionScope("main"),
                    services));
            Assert.False(scope.GetConnectionScope("main").DbMaintenance.IsAnyTable(nameof(UserBlock), false));
        }
        finally
        {
            DeleteIfExists(mainPath);
            DeleteIfExists(chatPath);
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
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过用户屏蔽 PostgreSQL 迁移测试");

        var schema = $"user_block_migration_{Guid.NewGuid():N}";
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
            using var scope = CreatePostgreSqlScope(connectionString);
            using var services = CreateServices(scope);
            SeedLegacyState(scope);
            var migration = UserBlockAuthoritySchemaMigration.Instance;

            migration.Apply(scope.GetConnectionScope("main"), services);
            migration.Apply(scope.GetConnectionScope("main"), services);

            Assert.Empty(migration.Verify(scope.GetConnectionScope("main"), services));
            AssertBackfill(scope.GetConnectionScope("main"));
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE");
        }
    }

    private static void SeedLegacyState(SqlSugarScope scope)
    {
        var mainDb = scope.GetConnectionScope("main");
        var chatDb = scope.GetConnectionScope("chat");
        mainDb.CodeFirst.InitTables<User>();
        mainDb.CodeFirst.InitTables<UserFollow>();
        mainDb.CodeFirst.InitTables<ReliableOutboxMessage>();
        chatDb.CodeFirst.InitTables<DirectConversation>();
        mainDb.Insertable(new[]
        {
            new User
            {
                Id = 1001,
                PublicId = "usr_migration_alice",
                PublicIndex = 1001,
                UserName = "alice",
                UserEmail = "alice@example.test",
                LoginPassword = "hash",
                TenantId = 9,
                IsEnable = true
            },
            new User
            {
                Id = 2002,
                PublicId = "usr_migration_bob",
                PublicIndex = 2002,
                UserName = "bob",
                UserEmail = "bob@example.test",
                LoginPassword = "hash",
                TenantId = 9,
                IsEnable = true
            }
        }).ExecuteCommand();
        mainDb.Insertable(new[]
        {
            Follow(4001, 1001, 2002),
            Follow(4002, 2002, 1001)
        }).ExecuteCommand();
        chatDb.Insertable(new DirectConversation
        {
            Id = 3001,
            ChannelId = 7001,
            ParticipantLowUserId = 1001,
            ParticipantHighUserId = 2002,
            RequestedByUserId = 1001,
            RequestStatus = DirectConversationRequestStatus.Accepted,
            BlockedByUserId = 1001,
            BlockedAt = OldestBlockUtc,
            TenantId = 9,
            CreateTime = OldestBlockUtc.AddDays(-1),
            CreateBy = "alice",
            CreateId = 1001
        }).ExecuteCommand();
    }

    private static UserFollow Follow(long id, long followerUserId, long followingUserId) =>
        new()
        {
            Id = id,
            TenantId = 9,
            FollowerUserId = followerUserId,
            FollowingUserId = followingUserId,
            FollowTime = OldestBlockUtc.AddDays(-2),
            CreateTime = OldestBlockUtc.AddDays(-2),
            CreateBy = "Seed",
            CreateId = followerUserId
        };

    private static void AssertBackfill(ISqlSugarClient mainDb)
    {
        var block = Assert.Single(mainDb.Queryable<UserBlock>().ToList());
        Assert.Equal(1001, block.BlockerUserId);
        Assert.Equal(2002, block.BlockedUserId);
        Assert.Equal(OldestBlockUtc, DateTime.SpecifyKind(block.CreateTime, DateTimeKind.Utc));
        Assert.False(block.IsDeleted);
        Assert.All(mainDb.Queryable<UserFollow>().ToList(), follow => Assert.True(follow.IsDeleted));
        Assert.Single(mainDb.Queryable<ReliableOutboxMessage>().ToList());
    }

    private static ServiceProvider CreateServices(SqlSugarScope scope) =>
        new ServiceCollection()
            .AddSingleton<ISqlSugarClient>(scope)
            .AddSingleton(TimeProvider.System)
            .BuildServiceProvider();

    private static SqlSugarScope CreateSqliteScope(string mainPath, string chatPath) =>
        new(
        [
            new ConnectionConfig
            {
                ConfigId = "main",
                ConnectionString = $"Data Source={mainPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            },
            new ConnectionConfig
            {
                ConfigId = "chat",
                ConnectionString = $"Data Source={chatPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            }
        ]);

    private static SqlSugarScope CreatePostgreSqlScope(string connectionString) =>
        PostgreSqlIntegrationSqlSugarFactory.CreateScope(
        [
            new ConnectionConfig
            {
                ConfigId = "main",
                ConnectionString = connectionString,
                DbType = DbType.PostgreSQL,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            },
            new ConnectionConfig
            {
                ConfigId = "chat",
                ConnectionString = connectionString,
                DbType = DbType.PostgreSQL,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            }
        ]);

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
