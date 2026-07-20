using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.DbMigrate;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class ChatReadReceiptRepositoryTest
{
    private const string PostgreSqlConnectionStringEnvironmentVariable = "RADISH_TEST_POSTGRES_CONNECTION_STRING";

    [Fact]
    public async Task AdvanceAsync_ShouldCreateAndAdvanceMonotonicallyWithoutChangingArchive()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-chat-read-state-{Guid.NewGuid():N}.db");
        using var db = CreateSqliteScope(path);

        try
        {
            var chatDb = db.GetConnectionScope("chat");
            chatDb.CodeFirst.InitTables<ChannelMember>();
            var archivedAt = new DateTime(2026, 7, 19, 8, 0, 0, DateTimeKind.Utc);
            chatDb.Insertable(CreateMember(71001, 20001, 120, archivedAt: archivedAt)).ExecuteCommand();
            chatDb.Insertable(CreateMember(71002, 20004, 120, isDeleted: true)).ExecuteCommand();
            var repository = CreateRepository(db);

            var older = await repository.AdvanceAsync(CreateCommand(20001, 100, false));
            var newer = await repository.AdvanceAsync(CreateCommand(20001, 180, false));
            var created = await repository.AdvanceAsync(CreateCommand(20002, 160, true));

            Assert.Equal(new AdvanceChatReadStateResult(120, false), older);
            Assert.Equal(new AdvanceChatReadStateResult(180, true), newer);
            Assert.Equal(new AdvanceChatReadStateResult(160, true), created);
            var existing = chatDb.Queryable<ChannelMember>().InSingle(71001);
            Assert.Equal(180, existing.LastReadMessageId);
            Assert.Equal(archivedAt, existing.ArchivedAt);
            var createdMember = chatDb.Queryable<ChannelMember>()
                .Single(member => member.UserId == 20002);
            Assert.False(createdMember.IsDeleted);
            Assert.Null(createdMember.ArchivedAt);
            Assert.Equal(160, createdMember.LastReadMessageId);
            await Assert.ThrowsAsync<ChatReadMemberUnavailableException>(() =>
                repository.AdvanceAsync(CreateCommand(20003, 200, false)));
            await Assert.ThrowsAsync<ChatReadMemberUnavailableException>(() =>
                repository.AdvanceAsync(CreateCommand(20004, 200, true)));
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
    public async Task ReadAggregation_ShouldRespectCursorJoinTimeDeletionAndKeyset()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-chat-read-aggregate-{Guid.NewGuid():N}.db");
        using var db = CreateSqliteScope(path);

        try
        {
            var chatDb = db.GetConnectionScope("chat");
            chatDb.CodeFirst.InitTables<ChannelMessage, ChannelMember>();
            var firstTime = new DateTime(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc);
            var secondTime = firstTime.AddHours(1);
            chatDb.Insertable(new[]
            {
                CreateMessage(100, firstTime),
                CreateMessage(200, secondTime)
            }).ExecuteCommand();
            chatDb.Insertable(new[]
            {
                CreateMember(72001, 20001, 200, joinedAt: firstTime.AddDays(-1)),
                CreateMember(72002, 20002, 200, joinedAt: firstTime.AddDays(-1)),
                CreateMember(72003, 20003, 200, joinedAt: firstTime.AddMinutes(30)),
                CreateMember(72004, 20004, 100, joinedAt: firstTime.AddDays(-1)),
                CreateMember(72005, 20005, 200, joinedAt: firstTime.AddDays(-1), isDeleted: true)
            }).ExecuteCommand();
            var repository = CreateRepository(db);

            var counts = await repository.GetReadCountsAsync(70001, 20001, [100, 200]);
            var firstReaders = await repository.GetReaderUserIdsAsync(
                70001,
                20001,
                100,
                firstTime,
                null,
                10);
            var afterSecondReader = await repository.GetReaderUserIdsAsync(
                70001,
                20001,
                100,
                firstTime,
                20002,
                10);

            Assert.Equal(2, counts.Single(item => item.MessageId == 100).ReadCount);
            Assert.Equal(2, counts.Single(item => item.MessageId == 200).ReadCount);
            Assert.Equal([20002L, 20004L], firstReaders);
            Assert.Equal([20004L], afterSecondReader);
            Assert.Equal(200, await repository.GetMemberReadCursorAsync(70001, 20003));
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
    public async Task ReadAggregation_ShouldBoundLargePrivateReaderPage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-chat-read-large-{Guid.NewGuid():N}.db");
        using var db = CreateSqliteScope(path);

        try
        {
            const int readerCount = 1_000;
            var chatDb = db.GetConnectionScope("chat");
            chatDb.CodeFirst.InitTables<ChannelMessage, ChannelMember>();
            var messageTime = new DateTime(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc);
            chatDb.Insertable(CreateMessage(100, messageTime)).ExecuteCommand();
            chatDb.Insertable(CreateMember(73001, 20001, 100, joinedAt: messageTime.AddDays(-1)))
                .ExecuteCommand();
            var readers = Enumerable.Range(0, readerCount)
                .Select(index => CreateMember(
                    74000 + index,
                    30000 + index,
                    100,
                    joinedAt: messageTime.AddDays(-1)))
                .ToList();
            chatDb.Insertable(readers).ExecuteCommand();
            var repository = CreateRepository(db);

            var counts = await repository.GetReadCountsAsync(70001, 20001, [100]);
            var firstBoundedPage = await repository.GetReaderUserIdsAsync(
                70001,
                20001,
                100,
                messageTime,
                null,
                51);

            Assert.Equal(readerCount, counts.Single().ReadCount);
            Assert.Equal(51, firstBoundedPage.Count);
            Assert.Equal(30000, firstBoundedPage[0]);
            Assert.Equal(30050, firstBoundedPage[^1]);
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
    public async Task AdvanceAsync_ShouldConvergeConcurrentTargetsOnPostgreSql()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过 Chat 阅读游标 PostgreSQL 并发测试");

        var schema = $"chat_read_{Guid.NewGuid():N}";
        using var adminDb = CreatePostgreSqlClient(adminConnectionString!);
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA {QuoteIdentifier(schema)}");
        try
        {
            var connectionString = $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
            using var setupDb = CreatePostgreSqlScope(connectionString);
            using var firstDb = CreatePostgreSqlScope(connectionString);
            using var secondDb = CreatePostgreSqlScope(connectionString);
            var chatDb = setupDb.GetConnectionScope("chat");
            chatDb.CodeFirst.InitTables<ChannelMember>();
            using var services = new ServiceCollection().BuildServiceProvider();
            ChatReadReceiptSchemaMigration.Instance.Apply(chatDb, services);
            var indexNames = chatDb.Ado.SqlQuery<string>(
                "SELECT indexname FROM pg_indexes WHERE schemaname = current_schema()");
            Assert.Contains("idx_channel_member_channel_user", indexNames);

            var results = await Task.WhenAll(
                CreateRepository(firstDb).AdvanceAsync(CreateCommand(20001, 100, true)),
                CreateRepository(secondDb).AdvanceAsync(CreateCommand(20001, 200, true)));

            Assert.All(results, result => Assert.True(result.LastReadMessageId is 100 or 200));
            var members = chatDb.Queryable<ChannelMember>().ToList();
            Assert.Single(members);
            Assert.Equal(200, members[0].LastReadMessageId);
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS {QuoteIdentifier(schema)} CASCADE");
        }
    }

    private static ChatReadReceiptRepository CreateRepository(SqlSugarScope db) => new(
        new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance));

    private static AdvanceChatReadStateCommand CreateCommand(long userId, long messageId, bool allowCreate) => new(
        70001,
        userId,
        30000,
        messageId,
        allowCreate,
        "Reader",
        new DateTime(2026, 7, 19, 12, 0, 0, DateTimeKind.Utc));

    private static ChannelMember CreateMember(
        long id,
        long userId,
        long? lastReadMessageId,
        DateTime? joinedAt = null,
        DateTime? archivedAt = null,
        bool isDeleted = false) => new()
    {
        Id = id,
        TenantId = 30000,
        ChannelId = 70001,
        UserId = userId,
        LastReadMessageId = lastReadMessageId,
        JoinedAt = joinedAt ?? new DateTime(2026, 7, 19, 9, 0, 0, DateTimeKind.Utc),
        ArchivedAt = archivedAt,
        IsDeleted = isDeleted,
        CreateTime = DateTime.UtcNow,
        CreateBy = "Test",
        CreateId = userId
    };

    private static ChannelMessage CreateMessage(long id, DateTime createTime) => new()
    {
        Id = id,
        TenantId = 30000,
        ChannelId = 70001,
        UserId = 20001,
        UserName = "Sender",
        Type = MessageType.Text,
        Content = $"message-{id}",
        CreateTime = createTime
    };

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

    private static string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
