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

public sealed class ChatMessagePinRepositoryTest
{
    private const string PostgreSqlConnectionStringEnvironmentVariable = "RADISH_TEST_POSTGRES_CONNECTION_STRING";

    [Fact]
    public async Task SetAsync_ShouldApplyDesiredStateRestoreAndRevisionAtomically()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-chat-pin-{Guid.NewGuid():N}.db");
        using var db = CreateSqliteScope(path);

        try
        {
            var chatDb = db.GetConnectionScope("chat");
            chatDb.CodeFirst.InitTables<Channel, ChannelMessage, ChatMessagePin>();
            var now = new DateTime(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc);
            chatDb.Insertable(CreateChannel()).ExecuteCommand();
            chatDb.Insertable(CreateMessage(73001, now)).ExecuteCommand();
            var repository = CreateRepository(db);

            var first = await repository.SetAsync(CreateCommand(73001, true, now));
            var noChange = await repository.SetAsync(CreateCommand(73001, true, now.AddSeconds(1)));
            var removed = await repository.SetAsync(CreateCommand(73001, false, now.AddSeconds(2)));
            var removeAgain = await repository.SetAsync(CreateCommand(73001, false, now.AddSeconds(3)));
            var restored = await repository.SetAsync(CreateCommand(73001, true, now.AddSeconds(4)));

            Assert.Equal(new ChatMessagePinWriteResult(1, true), first);
            Assert.Equal(new ChatMessagePinWriteResult(1, false), noChange);
            Assert.Equal(new ChatMessagePinWriteResult(2, true), removed);
            Assert.Equal(new ChatMessagePinWriteResult(2, false), removeAgain);
            Assert.Equal(new ChatMessagePinWriteResult(3, true), restored);
            Assert.Equal(3, chatDb.Queryable<Channel>().InSingle(70001).PinRevision);
            var pin = chatDb.Queryable<ChatMessagePin>().Single();
            Assert.False(pin.IsDeleted);
            Assert.Equal(now.AddSeconds(4), pin.PinnedAt);
            Assert.Equal(20002, pin.PinnedByUserId);

            var snapshot = await repository.GetSnapshotAsync(30000, 70001);
            Assert.NotNull(snapshot);
            Assert.Equal(3, snapshot.Revision);
            Assert.Single(snapshot.Items);
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
    public async Task SetAsync_ShouldRejectTwentyFirstActivePinWithoutChangingRevision()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-chat-pin-limit-{Guid.NewGuid():N}.db");
        using var db = CreateSqliteScope(path);

        try
        {
            var chatDb = db.GetConnectionScope("chat");
            chatDb.CodeFirst.InitTables<Channel, ChannelMessage, ChatMessagePin>();
            var now = new DateTime(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc);
            chatDb.Insertable(CreateChannel()).ExecuteCommand();
            chatDb.Insertable(Enumerable.Range(0, 21)
                .Select(index => CreateMessage(73001 + index, now.AddSeconds(index)))
                .ToList()).ExecuteCommand();
            var repository = CreateRepository(db);

            for (var index = 0; index < 20; index++)
            {
                var result = await repository.SetAsync(CreateCommand(
                    73001 + index,
                    true,
                    now.AddSeconds(index)));
                Assert.True(result.Changed);
            }

            await Assert.ThrowsAsync<ChatMessagePinLimitExceededException>(() =>
                repository.SetAsync(CreateCommand(73021, true, now.AddSeconds(21))));
            Assert.Equal(20, chatDb.Queryable<ChatMessagePin>().Count(pin => !pin.IsDeleted));
            Assert.Equal(20, chatDb.Queryable<Channel>().InSingle(70001).PinRevision);
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
    public async Task SetAsync_ShouldConvergeConcurrentDesiredStateOnPostgreSql()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过 Chat Pin PostgreSQL 并发测试");

        var schema = $"chat_pin_write_{Guid.NewGuid():N}";
        using var adminDb = CreatePostgreSqlClient(adminConnectionString!);
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA {QuoteIdentifier(schema)}");
        try
        {
            var connectionString = $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
            using var setupDb = CreatePostgreSqlScope(connectionString);
            using var firstDb = CreatePostgreSqlScope(connectionString);
            using var secondDb = CreatePostgreSqlScope(connectionString);
            var chatDb = setupDb.GetConnectionScope("chat");
            chatDb.CodeFirst.InitTables<Channel, ChannelMessage, ChatMessagePin>();
            var now = new DateTime(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc);
            chatDb.Insertable(CreateChannel()).ExecuteCommand();
            chatDb.Insertable(CreateMessage(73001, now)).ExecuteCommand();
            var firstRepository = CreateRepository(firstDb);
            var secondRepository = CreateRepository(secondDb);

            var results = await Task.WhenAll(
                firstRepository.SetAsync(CreateCommand(73001, true, now)),
                secondRepository.SetAsync(CreateCommand(73001, true, now.AddMilliseconds(1))));

            Assert.Single(results, result => result.Changed);
            Assert.Single(results, result => !result.Changed);
            Assert.Equal(1, chatDb.Queryable<Channel>().InSingle(70001).PinRevision);
            Assert.Equal(1, chatDb.Queryable<ChatMessagePin>().Count(pin => !pin.IsDeleted));
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS {QuoteIdentifier(schema)} CASCADE");
        }
    }

    private static ChatMessagePinRepository CreateRepository(SqlSugarScope db) => new(
        new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance));

    private static SqlSugarScope CreateSqliteScope(string path) => new(new ConnectionConfig
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

    private static SqlSugarScope CreatePostgreSqlScope(string connectionString) =>
        PostgreSqlIntegrationSqlSugarFactory.CreateScope(new ConnectionConfig
        {
            ConfigId = "chat",
            ConnectionString = connectionString,
            DbType = DbType.PostgreSQL,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });

    private static Channel CreateChannel() => new()
    {
        Id = 70001,
        TenantId = 30000,
        Name = "General",
        Slug = "general",
        Type = ChannelType.Public,
        IsEnabled = true,
        CreateTime = DateTime.UtcNow,
        CreateBy = "System"
    };

    private static ChannelMessage CreateMessage(long id, DateTime now) => new()
    {
        Id = id,
        TenantId = 30000,
        ChannelId = 70001,
        UserId = 20001,
        UserName = "Sender",
        Type = MessageType.Text,
        Content = $"message-{id}",
        SearchText = $"message-{id}",
        CreateTime = now
    };

    private static ChatMessagePinSetCommand CreateCommand(long messageId, bool isPinned, DateTime now) => new(
        30000,
        70001,
        messageId,
        20002,
        "Moderator",
        isPinned,
        now);

    private static string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
