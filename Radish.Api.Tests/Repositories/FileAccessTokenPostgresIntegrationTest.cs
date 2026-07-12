using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.Common.Security;
using Radish.Model.Models;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class FileAccessTokenPostgresIntegrationTest
{
    private const string ConnectionStringEnvironmentVariable = "RADISH_TEST_POSTGRES_CONNECTION_STRING";

    [Fact]
    [Trait("Database", "PostgreSQL")]
    public async Task Consume_ShouldEnforceLimitAcrossPostgreSqlWorkers()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {ConnectionStringEnvironmentVariable}，跳过 PostgreSQL 文件 token 集成测试");

        var schema = $"q1c_file_token_{Guid.NewGuid():N}";
        using var adminDb = CreateClient(adminConnectionString!);
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA {QuoteIdentifier(schema)}");
        try
        {
            var connectionString = $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
            using var initDb = CreateScope(connectionString);
            initDb.CodeFirst.InitTables<FileAccessToken>();
            var writer = CreateRepository(initDb);
            var now = new DateTime(2026, 7, 11, 12, 0, 0);
            var tokenHash = FileAccessTokenHashing.HashToken("postgres-concurrent-token");
            await writer.AddAsync(new FileAccessToken
            {
                Id = 1001,
                TokenHash = tokenHash,
                AttachmentId = 5001,
                MaxAccessCount = 5,
                ExpiresAt = now.AddHours(1),
                CreatedBy = 42,
                CreateTime = now
            });

            using var workerDbA = CreateScope(connectionString);
            using var workerDbB = CreateScope(connectionString);
            var workerA = CreateRepository(workerDbA);
            var workerB = CreateRepository(workerDbB);
            var results = await Task.WhenAll(Enumerable.Range(0, 30).Select(index =>
                (index % 2 == 0 ? workerA : workerB)
                    .TryConsumeAsync(tokenHash, null, "127.0.0.1", now)));

            Assert.Equal(5, results.Count(result => result != null));
            var stored = await writer.GetByHashAsync(tokenHash);
            Assert.NotNull(stored);
            Assert.Equal(5, stored.AccessCount);
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS {QuoteIdentifier(schema)} CASCADE");
        }
    }

    private static FileAccessTokenRepository CreateRepository(SqlSugarScope db)
    {
        return new FileAccessTokenRepository(new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance));
    }

    private static SqlSugarScope CreateScope(string connectionString)
    {
        return PostgreSqlIntegrationSqlSugarFactory.CreateScope(CreateConfig(connectionString));
    }

    private static SqlSugarClient CreateClient(string connectionString)
    {
        return PostgreSqlIntegrationSqlSugarFactory.CreateClient(CreateConfig(connectionString));
    }

    private static ConnectionConfig CreateConfig(string connectionString)
    {
        return new ConnectionConfig
        {
            ConfigId = "main",
            ConnectionString = connectionString,
            DbType = DbType.PostgreSQL,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        };
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"")}\"";
    }
}
