using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.Model;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class DirectConversationRepositoryTest
{
    private const string PostgreSqlConnectionStringEnvironmentVariable = "RADISH_TEST_POSTGRES_CONNECTION_STRING";

    [Fact]
    public async Task CreateOrGetAsync_ShouldConvergeConcurrentPairCreationWithoutOrphans()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-direct-conversation-create-{Guid.NewGuid():N}.db");
        using var setupDb = CreateClient(path);
        using var firstDb = CreateClient(path);
        using var secondDb = CreateClient(path);

        try
        {
            var chatDb = setupDb.GetConnectionScope("chat");
            chatDb.CodeFirst.InitTables<Channel>();
            chatDb.CodeFirst.InitTables<ChannelMember>();
            chatDb.CodeFirst.InitTables<DirectConversation>();

            var firstRepository = new DirectConversationRepository(
                new UnitOfWorkManage(firstDb, NullLogger<UnitOfWorkManage>.Instance));
            var secondRepository = new DirectConversationRepository(
                new UnitOfWorkManage(secondDb, NullLogger<UnitOfWorkManage>.Instance));
            var firstDraft = CreateDraft(70001, 71001, 72001, 72002);
            var secondDraft = CreateDraft(70002, 71002, 72003, 72004);

            var results = await Task.WhenAll(
                firstRepository.CreateOrGetAsync(
                    firstDraft.Conversation,
                    firstDraft.Channel,
                    firstDraft.LowMember,
                    firstDraft.HighMember),
                secondRepository.CreateOrGetAsync(
                    secondDraft.Conversation,
                    secondDraft.Channel,
                    secondDraft.LowMember,
                    secondDraft.HighMember));

            Assert.Equal(results[0].Conversation.ChannelId, results[1].Conversation.ChannelId);
            Assert.Single(results, result => result.WasCreated);
            Assert.Equal(1, chatDb.Queryable<DirectConversation>().Count());
            Assert.Equal(1, chatDb.Queryable<Channel>().Count());
            Assert.Equal(2, chatDb.Queryable<ChannelMember>().Count());
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
    public async Task CreateOrGetAsync_ShouldConvergeConcurrentPairCreationOnPostgreSql()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过私聊并发创建 PostgreSQL 测试");

        var schema = $"direct_create_{Guid.NewGuid():N}";
        using var adminDb = PostgreSqlIntegrationSqlSugarFactory.CreateClient(new ConnectionConfig
        {
            ConfigId = "chat",
            ConnectionString = adminConnectionString!,
            DbType = DbType.PostgreSQL,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA {QuoteIdentifier(schema)}");
        try
        {
            var connectionString = $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
            using var setupDb = CreatePostgreSqlClient(connectionString);
            using var firstDb = CreatePostgreSqlClient(connectionString);
            using var secondDb = CreatePostgreSqlClient(connectionString);
            var chatDb = setupDb.GetConnectionScope("chat");
            chatDb.CodeFirst.InitTables<Channel>();
            chatDb.CodeFirst.InitTables<ChannelMember>();
            chatDb.CodeFirst.InitTables<DirectConversation>();
            var firstRepository = new DirectConversationRepository(
                new UnitOfWorkManage(firstDb, NullLogger<UnitOfWorkManage>.Instance));
            var secondRepository = new DirectConversationRepository(
                new UnitOfWorkManage(secondDb, NullLogger<UnitOfWorkManage>.Instance));
            var firstDraft = CreateDraft(80001, 81001, 82001, 82002);
            var secondDraft = CreateDraft(80002, 81002, 82003, 82004);

            var results = await Task.WhenAll(
                firstRepository.CreateOrGetAsync(
                    firstDraft.Conversation,
                    firstDraft.Channel,
                    firstDraft.LowMember,
                    firstDraft.HighMember),
                secondRepository.CreateOrGetAsync(
                    secondDraft.Conversation,
                    secondDraft.Channel,
                    secondDraft.LowMember,
                    secondDraft.HighMember));

            Assert.Equal(results[0].Conversation.ChannelId, results[1].Conversation.ChannelId);
            Assert.Single(results, result => result.WasCreated);
            Assert.Equal(1, chatDb.Queryable<DirectConversation>().Count());
            Assert.Equal(1, chatDb.Queryable<Channel>().Count());
            Assert.Equal(2, chatDb.Queryable<ChannelMember>().Count());
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

    private static SqlSugarScope CreatePostgreSqlClient(string connectionString)
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

    private static string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static ConversationDraft CreateDraft(
        long channelId,
        long conversationId,
        long lowMemberId,
        long highMemberId)
    {
        var now = DateTime.UtcNow;
        return new ConversationDraft(
            new DirectConversation
            {
                Id = conversationId,
                ChannelId = channelId,
                ParticipantLowUserId = 20001,
                ParticipantHighUserId = 20002,
                RequestedByUserId = 20001,
                RequestStatus = DirectConversationRequestStatus.Pending,
                TenantId = 30000,
                CreateTime = now,
                CreateBy = "Requester",
                CreateId = 20001
            },
            new Channel
            {
                Id = channelId,
                Name = "Direct conversation",
                Slug = $"direct-{channelId}",
                Type = ChannelType.Private,
                IsEnabled = true,
                TenantId = 30000,
                CreateTime = now,
                CreateBy = "Requester",
                CreateId = 20001
            },
            CreateMember(lowMemberId, channelId, 20001, now),
            CreateMember(highMemberId, channelId, 20002, now));
    }

    private static ChannelMember CreateMember(long id, long channelId, long userId, DateTime now)
    {
        return new ChannelMember
        {
            Id = id,
            ChannelId = channelId,
            UserId = userId,
            Role = MemberRole.Member,
            JoinedAt = now,
            TenantId = 30000,
            CreateTime = now,
            CreateBy = "Requester",
            CreateId = 20001
        };
    }

    private sealed record ConversationDraft(
        DirectConversation Conversation,
        Channel Channel,
        ChannelMember LowMember,
        ChannelMember HighMember);
}
