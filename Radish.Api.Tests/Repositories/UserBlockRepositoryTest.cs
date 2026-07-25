using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class UserBlockRepositoryTest
{
    private const string PostgreSqlConnectionStringEnvironmentVariable = "RADISH_TEST_POSTGRES_CONNECTION_STRING";

    [Fact]
    public async Task MutateAsync_ShouldConvergeReplayRestoreAndFollowConsistencyOnSqlite()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-user-block-{Guid.NewGuid():N}.db");
        using var setupDb = CreateSqliteClient(path);
        using var firstDb = CreateSqliteClient(path);
        using var secondDb = CreateSqliteClient(path);
        try
        {
            InitializeSchema(setupDb);
            SeedFollows(setupDb);
            var firstRepository = CreateRepository(firstDb);
            var secondRepository = CreateRepository(secondDb);
            var nowUtc = new DateTime(2026, 7, 25, 8, 0, 0, DateTimeKind.Utc);

            var results = await Task.WhenAll(
                firstRepository.MutateAsync(Block("block-a", nowUtc)),
                secondRepository.MutateAsync(Block("block-b", nowUtc)));

            Assert.Single(results, result => result.Changed);
            Assert.Single(setupDb.Queryable<UserBlock>().ToList());
            Assert.Equal(2, setupDb.Queryable<UserBlockOperation>().Count());
            Assert.Single(setupDb.Queryable<ReliableOutboxMessage>().ToList());
            Assert.All(setupDb.Queryable<UserFollow>().ToList(), follow => Assert.True(follow.IsDeleted));

            var replay = await firstRepository.MutateAsync(Block("block-a", nowUtc.AddMinutes(1)));
            Assert.True(replay.Replayed);
            Assert.Equal(2, setupDb.Queryable<UserBlockOperation>().Count());
            Assert.Single(setupDb.Queryable<ReliableOutboxMessage>().ToList());

            var unblocked = await firstRepository.MutateAsync(Unblock("unblock-a", nowUtc.AddMinutes(2)));
            Assert.True(unblocked.Changed);
            Assert.Equal(2, unblocked.RelationshipVersion);
            var unblockReplay = await firstRepository.MutateAsync(
                Unblock("unblock-a", nowUtc.AddMinutes(3)));
            Assert.True(unblockReplay.Replayed);

            var reblocked = await firstRepository.MutateAsync(Block("reblock-a", nowUtc.AddMinutes(4)));
            Assert.True(reblocked.Changed);
            Assert.Equal(3, reblocked.RelationshipVersion);
            Assert.False(Assert.Single(setupDb.Queryable<UserBlock>().ToList()).IsDeleted);
            Assert.Equal(3, setupDb.Queryable<ReliableOutboxMessage>().Count());
            Assert.All(setupDb.Queryable<UserFollow>().ToList(), follow => Assert.True(follow.IsDeleted));
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
    public async Task MutateAsync_ShouldRejectOperationKeyReuseForDifferentCommand()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-user-block-key-{Guid.NewGuid():N}.db");
        using var db = CreateSqliteClient(path);
        try
        {
            InitializeSchema(db);
            var repository = CreateRepository(db);
            var nowUtc = DateTime.UtcNow;
            await repository.MutateAsync(Block("same-key", nowUtc));

            await Assert.ThrowsAsync<UserBlockOperationConflictException>(() =>
                repository.MutateAsync(Unblock("same-key", nowUtc.AddSeconds(1))));
            Assert.False(Assert.Single(db.Queryable<UserBlock>().ToList()).IsDeleted);
            Assert.Single(db.Queryable<UserBlockOperation>().ToList());
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
    public async Task MutateAsync_ShouldSerializeConcurrentOperationKeyReuseAcrossDifferentTargetsOnSqlite()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-user-block-key-race-{Guid.NewGuid():N}.db");
        using var setupDb = CreateSqliteClient(path);
        using var firstDb = CreateSqliteClient(path);
        using var secondDb = CreateSqliteClient(path);
        try
        {
            InitializeSchema(setupDb);
            var nowUtc = DateTime.UtcNow;
            var outcomes = await Task.WhenAll(
                CaptureMutationAsync(
                    CreateRepository(firstDb),
                    new UserBlockMutationCommand(
                        9,
                        1001,
                        2002,
                        UserBlockOperationTypes.Block,
                        "shared-key",
                        "alice",
                        nowUtc)),
                CaptureMutationAsync(
                    CreateRepository(secondDb),
                    new UserBlockMutationCommand(
                        9,
                        1001,
                        3003,
                        UserBlockOperationTypes.Block,
                        "shared-key",
                        "alice",
                        nowUtc)));

            Assert.Single(outcomes, outcome => outcome.Result != null);
            Assert.Single(outcomes, outcome => outcome.Error is UserBlockOperationConflictException);
            Assert.Single(setupDb.Queryable<UserBlockOperation>().ToList());
            Assert.Single(setupDb.Queryable<UserBlock>().ToList());
            Assert.Single(setupDb.Queryable<ReliableOutboxMessage>().ToList());
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
    public async Task MutateAsync_ShouldConvergeConcurrentBlockAndReplayOnPostgreSql()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(
            PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过用户屏蔽 PostgreSQL 并发测试");

        var schema = $"user_block_{Guid.NewGuid():N}";
        using var adminDb = PostgreSqlIntegrationSqlSugarFactory.CreateClient(new ConnectionConfig
        {
            ConfigId = "main",
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
            using var setupDb = CreatePostgreSqlClient(connectionString);
            using var firstDb = CreatePostgreSqlClient(connectionString);
            using var secondDb = CreatePostgreSqlClient(connectionString);
            InitializeSchema(setupDb);
            SeedFollows(setupDb);
            var nowUtc = DateTime.UtcNow;

            var results = await Task.WhenAll(
                CreateRepository(firstDb).MutateAsync(Block("postgres-a", nowUtc)),
                CreateRepository(secondDb).MutateAsync(Block("postgres-b", nowUtc)));

            Assert.Single(results, result => result.Changed);
            Assert.Single(setupDb.Queryable<UserBlock>().ToList());
            Assert.Single(setupDb.Queryable<ReliableOutboxMessage>().ToList());
            Assert.All(setupDb.Queryable<UserFollow>().ToList(), follow => Assert.True(follow.IsDeleted));
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE");
        }
    }

    private static UserBlockMutationCommand Block(string operationKey, DateTime nowUtc) =>
        new(9, 1001, 2002, UserBlockOperationTypes.Block, operationKey, "alice", nowUtc);

    private static UserBlockMutationCommand Unblock(string operationKey, DateTime nowUtc) =>
        new(9, 1001, 2002, UserBlockOperationTypes.Unblock, operationKey, "alice", nowUtc);

    private static async Task<(UserBlockWriteResult? Result, Exception? Error)> CaptureMutationAsync(
        UserBlockRepository repository,
        UserBlockMutationCommand command)
    {
        try
        {
            return (await repository.MutateAsync(command), null);
        }
        catch (Exception exception)
        {
            return (null, exception);
        }
    }

    private static UserBlockRepository CreateRepository(SqlSugarScope db) =>
        new(new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance));

    private static void InitializeSchema(SqlSugarScope db)
    {
        db.CodeFirst.InitTables<UserBlock>();
        db.CodeFirst.InitTables<UserBlockOperation>();
        db.CodeFirst.InitTables<UserFollow>();
        db.CodeFirst.InitTables<ReliableOutboxMessage>();
    }

    private static void SeedFollows(SqlSugarScope db)
    {
        var nowUtc = DateTime.UtcNow;
        db.Insertable(new[]
        {
            new UserFollow
            {
                Id = 1,
                TenantId = 9,
                FollowerUserId = 1001,
                FollowingUserId = 2002,
                FollowTime = nowUtc,
                CreateTime = nowUtc,
                CreateBy = "Seed",
                CreateId = 1001
            },
            new UserFollow
            {
                Id = 2,
                TenantId = 9,
                FollowerUserId = 2002,
                FollowingUserId = 1001,
                FollowTime = nowUtc,
                CreateTime = nowUtc,
                CreateBy = "Seed",
                CreateId = 2002
            }
        }).ExecuteCommand();
    }

    private static SqlSugarScope CreateSqliteClient(string path) =>
        new(new ConnectionConfig
        {
            ConfigId = "main",
            ConnectionString = $"Data Source={path};Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });

    private static SqlSugarScope CreatePostgreSqlClient(string connectionString) =>
        PostgreSqlIntegrationSqlSugarFactory.CreateScope(new ConnectionConfig
        {
            ConfigId = "main",
            ConnectionString = connectionString,
            DbType = DbType.PostgreSQL,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
}
