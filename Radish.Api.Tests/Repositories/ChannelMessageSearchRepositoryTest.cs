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

public sealed class ChannelMessageSearchRepositoryTest
{
    [Fact]
    public async Task SearchAsync_ShouldUseLiteralContainsStableOrderAndSnapshot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-chat-search-repository-{Guid.NewGuid():N}.db");
        using var scope = CreateSqliteScope(path);

        try
        {
            var chatDb = scope.GetConnectionScope("chat");
            chatDb.CodeFirst.InitTables<ChannelMessage>();
            var createTime = DateTime.UtcNow.AddMinutes(-2);
            chatDb.Insertable(new[]
            {
                CreateMessage(1000, 7001, "literal %_\\ 'other tenant'", createTime.AddMinutes(4), tenantId: 40000),
                CreateMessage(1001, 7001, "literal %_\\ 'quoted'", createTime),
                CreateMessage(1002, 7001, "literal %_\\ 'second'", createTime),
                CreateMessage(1003, 7002, "ordinary text", createTime.AddMinutes(1)),
                CreateMessage(1004, 7001, "literal anything", createTime.AddMinutes(2), isDeleted: true),
                CreateMessage(1005, 7001, "literal %_\\ future", createTime.AddMinutes(3))
            }).ExecuteCommand();
            var repository = CreateRepository(scope);

            var snapshot = await repository.GetSnapshotMaxMessageIdAsync(30000, new long[] { 7001 });
            var results = await repository.SearchAsync(new ChannelMessageSearchQuery(
                30000,
                new long[] { 7001 },
                "%_\\ '",
                null,
                null,
                1002,
                null,
                null,
                10));

            Assert.Equal(1005, snapshot);
            Assert.Equal(new long[] { 1002, 1001 }, results.Select(message => message.Id));
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
    public async Task SearchAsync_ShouldUsePostgreSqlLiteralContains()
    {
        const string environmentVariable = "RADISH_TEST_POSTGRES_CONNECTION_STRING";
        var adminConnectionString = Environment.GetEnvironmentVariable(environmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {environmentVariable}，跳过 Chat 搜索 PostgreSQL 仓储测试");

        var schema = $"chat_search_repo_{Guid.NewGuid():N}";
        using var adminDb = PostgreSqlIntegrationSqlSugarFactory.CreateClient(new ConnectionConfig
        {
            ConfigId = "admin",
            ConnectionString = adminConnectionString,
            DbType = DbType.PostgreSQL,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA \"{schema}\"");
        try
        {
            var connectionString = $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
            using var scope = PostgreSqlIntegrationSqlSugarFactory.CreateScope(new ConnectionConfig
            {
                ConfigId = "chat",
                ConnectionString = connectionString,
                DbType = DbType.PostgreSQL,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });
            var chatDb = scope.GetConnectionScope("chat");
            chatDb.CodeFirst.InitTables<ChannelMessage>();
            chatDb.Insertable(CreateMessage(2001, 8001, "postgres %_\\ 'literal'", DateTime.UtcNow)).ExecuteCommand();
            var repository = CreateRepository(scope);

            var results = await repository.SearchAsync(new ChannelMessageSearchQuery(
                30000,
                new long[] { 8001 },
                "%_\\ '",
                null,
                null,
                2001,
                null,
                null,
                10));

            Assert.Single(results);
            Assert.Equal(2001, results[0].Id);
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE");
        }
    }

    private static ChannelMessageSearchRepository CreateRepository(SqlSugarScope scope)
    {
        return new ChannelMessageSearchRepository(
            new UnitOfWorkManage(scope, NullLogger<UnitOfWorkManage>.Instance));
    }

    private static SqlSugarScope CreateSqliteScope(string path)
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

    private static ChannelMessage CreateMessage(
        long id,
        long channelId,
        string content,
        DateTime createTime,
        bool isDeleted = false,
        long tenantId = 30000)
    {
        return new ChannelMessage
        {
            Id = id,
            ChannelId = channelId,
            UserId = 20001,
            UserName = "Tester",
            Type = MessageType.Text,
            Content = content,
            SearchText = ChatMessageSearchTextNormalizer.Normalize(content),
            TenantId = tenantId,
            CreateTime = createTime,
            IsDeleted = isDeleted
        };
    }
}
