using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class ChatMessageReactionRepositoryTest
{
    private const string PostgreSqlConnectionStringEnvironmentVariable = "RADISH_TEST_POSTGRES_CONNECTION_STRING";

    [Fact]
    public async Task SetAsync_ShouldApplyDesiredStateReplayAndRevisionAtomically()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-chat-reaction-{Guid.NewGuid():N}.db");
        using var db = CreateClient(path);

        try
        {
            var chatDb = db.GetConnectionScope("chat");
            chatDb.CodeFirst.InitTables<ChannelMessage>();
            chatDb.CodeFirst.InitTables<ChatMessageReaction>();
            chatDb.CodeFirst.InitTables<ChatMessageReactionOperation>();
            var now = new DateTime(2026, 7, 19, 8, 0, 0, DateTimeKind.Utc);
            chatDb.Insertable(CreateMessage(now)).ExecuteCommand();

            var repository = new ChatMessageReactionRepository(
                new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance));
            var activate = CreateCommand("operation-activate", true, now);

            var first = await repository.SetAsync(activate);
            var replay = await repository.SetAsync(activate);
            var noChange = await repository.SetAsync(CreateCommand("operation-no-change", true, now.AddSeconds(1)));
            var deactivate = await repository.SetAsync(CreateCommand("operation-deactivate", false, now.AddSeconds(2)));
            var restore = await repository.SetAsync(CreateCommand("operation-restore", true, now.AddSeconds(3)));

            Assert.Equal(new ChatMessageReactionWriteResult(1, true, false), first);
            Assert.Equal(new ChatMessageReactionWriteResult(1, false, true), replay);
            Assert.Equal(new ChatMessageReactionWriteResult(1, false, false), noChange);
            Assert.Equal(new ChatMessageReactionWriteResult(2, true, false), deactivate);
            Assert.Equal(new ChatMessageReactionWriteResult(3, true, false), restore);
            Assert.Equal(3, chatDb.Queryable<ChannelMessage>().InSingle(73001).ReactionRevision);
            Assert.False(chatDb.Queryable<ChatMessageReaction>().Single().IsDeleted);
            Assert.Equal(4, chatDb.Queryable<ChatMessageReactionOperation>().Count());

            var conflictingCommand = activate with { MessageId = 73002 };
            await Assert.ThrowsAsync<ChatMessageReactionIdempotencyConflictException>(() =>
                repository.SetAsync(conflictingCommand));

            var deletedCount = await repository.DeleteExpiredOperationsAsync(now.AddDays(31), 100);
            Assert.Equal(4, deletedCount);
            Assert.Equal(0, chatDb.Queryable<ChatMessageReactionOperation>().Count());
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
    public async Task SetAsync_ShouldRejectEleventhActiveReactionType()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-chat-reaction-limit-{Guid.NewGuid():N}.db");
        using var db = CreateClient(path);

        try
        {
            var chatDb = db.GetConnectionScope("chat");
            chatDb.CodeFirst.InitTables<ChannelMessage>();
            chatDb.CodeFirst.InitTables<ChatMessageReaction>();
            chatDb.CodeFirst.InitTables<ChatMessageReactionOperation>();
            var now = new DateTime(2026, 7, 19, 8, 0, 0, DateTimeKind.Utc);
            chatDb.Insertable(CreateMessage(now)).ExecuteCommand();
            var repository = new ChatMessageReactionRepository(
                new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance));

            for (var index = 0; index < 10; index++)
            {
                await repository.SetAsync(CreateCommand(
                    $"operation-limit-{index}",
                    true,
                    now.AddSeconds(index)) with
                {
                    EmojiValue = $"emoji-{index}"
                });
            }

            await Assert.ThrowsAsync<ChatMessageReactionLimitExceededException>(() =>
                repository.SetAsync(CreateCommand("operation-limit-10", true, now.AddSeconds(10)) with
                {
                    EmojiValue = "emoji-10"
                }));
            Assert.Equal(10, chatDb.Queryable<ChatMessageReaction>().Count(reaction => !reaction.IsDeleted));
            Assert.Equal(10, chatDb.Queryable<ChannelMessage>().InSingle(73001).ReactionRevision);
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
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过 Chat Reaction PostgreSQL 并发测试");

        var schema = $"chat_reaction_write_{Guid.NewGuid():N}";
        using var adminDb = CreatePostgreSqlClient(adminConnectionString!);
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA {QuoteIdentifier(schema)}");
        try
        {
            var connectionString = $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
            using var setupDb = CreatePostgreSqlScope(connectionString);
            using var firstDb = CreatePostgreSqlScope(connectionString);
            using var secondDb = CreatePostgreSqlScope(connectionString);
            var chatDb = setupDb.GetConnectionScope("chat");
            chatDb.CodeFirst.InitTables<ChannelMessage>();
            chatDb.CodeFirst.InitTables<ChatMessageReaction>();
            chatDb.CodeFirst.InitTables<ChatMessageReactionOperation>();
            var now = new DateTime(2026, 7, 19, 8, 0, 0, DateTimeKind.Utc);
            chatDb.Insertable(CreateMessage(now)).ExecuteCommand();
            var firstRepository = new ChatMessageReactionRepository(
                new UnitOfWorkManage(firstDb, NullLogger<UnitOfWorkManage>.Instance));
            var secondRepository = new ChatMessageReactionRepository(
                new UnitOfWorkManage(secondDb, NullLogger<UnitOfWorkManage>.Instance));
            var firstCommand = CreateCommand("operation-postgres-first", true, now);
            var secondCommand = CreateCommand("operation-postgres-second", true, now);

            var attempts = await Task.WhenAll(
                CaptureSetAsync(firstRepository, firstCommand),
                CaptureSetAsync(secondRepository, secondCommand));

            for (var index = 0; index < attempts.Length; index++)
            {
                if (attempts[index].Exception is ChatMessageReactionConcurrentConflictException)
                {
                    var repository = index == 0 ? firstRepository : secondRepository;
                    var command = index == 0 ? firstCommand : secondCommand;
                    var retry = await repository.SetAsync(command);
                    Assert.False(retry.Changed);
                    Assert.False(retry.Replayed);
                }
                else
                {
                    Assert.Null(attempts[index].Exception);
                }
            }

            Assert.Contains(attempts, attempt => attempt.Result is { Changed: true });
            Assert.Equal(1, chatDb.Queryable<ChannelMessage>().InSingle(73001).ReactionRevision);
            Assert.Equal(1, chatDb.Queryable<ChatMessageReaction>().Count(reaction => !reaction.IsDeleted));
            Assert.Equal(2, chatDb.Queryable<ChatMessageReactionOperation>().Count());
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS {QuoteIdentifier(schema)} CASCADE");
        }
    }

    private static SqlSugarScope CreateClient(string path)
    {
        return new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "chat",
            ConnectionString = $"Data Source={path}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
    }

    private static SqlSugarClient CreatePostgreSqlClient(string connectionString)
    {
        return PostgreSqlIntegrationSqlSugarFactory.CreateClient(new ConnectionConfig
        {
            ConfigId = "chat",
            ConnectionString = connectionString,
            DbType = DbType.PostgreSQL,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
    }

    private static SqlSugarScope CreatePostgreSqlScope(string connectionString)
    {
        return PostgreSqlIntegrationSqlSugarFactory.CreateScope(new ConnectionConfig
        {
            ConfigId = "chat",
            ConnectionString = connectionString,
            DbType = DbType.PostgreSQL,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
    }

    private static ChannelMessage CreateMessage(DateTime now)
    {
        return new ChannelMessage
        {
            Id = 73001,
            TenantId = 30000,
            ChannelId = 70001,
            UserId = 20001,
            UserName = "Requester",
            Type = MessageType.Text,
            Content = "hello",
            SearchText = "hello",
            CreateTime = now
        };
    }

    private static ChatMessageReactionSetCommand CreateCommand(
        string operationId,
        bool isActive,
        DateTime now)
    {
        return new ChatMessageReactionSetCommand(
            30000,
            70001,
            73001,
            20002,
            "Receiver",
            "unicode",
            "👍",
            null,
            isActive,
            operationId,
            now,
            now.AddDays(30));
    }

    private static async Task<ReactionWriteAttempt> CaptureSetAsync(
        ChatMessageReactionRepository repository,
        ChatMessageReactionSetCommand command)
    {
        try
        {
            return new ReactionWriteAttempt(await repository.SetAsync(command), null);
        }
        catch (Exception exception)
        {
            return new ReactionWriteAttempt(null, exception);
        }
    }

    private static string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private sealed record ReactionWriteAttempt(
        ChatMessageReactionWriteResult? Result,
        Exception? Exception);
}
