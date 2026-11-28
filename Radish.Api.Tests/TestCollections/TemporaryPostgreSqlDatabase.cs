using System;
using System.Threading.Tasks;
using Npgsql;
using Xunit;

namespace Radish.Api.Tests.TestCollections;

internal static class TemporaryPostgreSqlDatabase
{
    public static async Task RunAsync(Func<string, Task> verify)
    {
        var connection = Environment.GetEnvironmentVariable("RADISH_TEST_POSTGRES_CONNECTION_STRING");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connection), "未配置隔离 PostgreSQL 连接");
        var builder = new NpgsqlConnectionStringBuilder(connection) { Database = "postgres", Pooling = false };
        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync(TestContext.Current.CancellationToken);
        var database = $"radish_account_test_{Guid.NewGuid():N}";
        await using var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin);
        await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        try
        {
            builder.Database = database;
            await verify(builder.ConnectionString);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync(System.Threading.CancellationToken.None);
        }
    }
}
