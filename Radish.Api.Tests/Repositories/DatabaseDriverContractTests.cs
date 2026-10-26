using System;
using System.IO;
using System.Security.Cryptography;
using Radish.Api.Tests.TestCollections;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

[Collection(PostgreSqlIntegrationCollection.CollectionName)]
public sealed class DatabaseDriverContractTests
{
    [Fact]
    public void SqlSugar_UsesApprovedNoDriveAssembly()
    {
        // SystemCommon 也带同名 DLL；必须实际加载 NoDrive 的固定实现，不能只检查包版本。
        var assemblyPath = typeof(SqlSugarClient).Assembly.Location;
        using var stream = File.OpenRead(assemblyPath);
        var hash = Convert.ToHexStringLower(SHA256.HashData(stream));

        Assert.Equal("38a361e486a809ee2242861ce5ecb94016dca99bcaa9541d8f54cda8b06e9d0b", hash);
    }

    [Fact]
    public void SqliteDriver_ExecutesParameterizedInt64Query()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            DbType = DbType.Sqlite,
            ConnectionString = "Data Source=:memory:",
            IsAutoCloseConnection = true
        });

        var value = db.Ado.SqlQuerySingle<long>("SELECT @value", new SugarParameter("@value", long.MaxValue));

        Assert.Equal(long.MaxValue, value);
        Assert.IsType<Microsoft.Data.Sqlite.SqliteConnection>(db.Ado.Connection);
        Assert.Equal(new Version(10, 0, 0, 0), db.Ado.Connection.GetType().Assembly.GetName().Version);
    }

    [Fact]
    [Trait("Database", "PostgreSQL")]
    public void PostgreSqlDriver_ExecutesParameterizedInt64Query()
    {
        var connection = Environment.GetEnvironmentVariable("RADISH_TEST_POSTGRES_CONNECTION_STRING");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connection),
            "未配置 RADISH_TEST_POSTGRES_CONNECTION_STRING，跳过 PostgreSQL 驱动契约验证。");
        using var db = PostgreSqlIntegrationSqlSugarFactory.CreateClient(new ConnectionConfig
        {
            DbType = DbType.PostgreSQL,
            ConnectionString = connection,
            IsAutoCloseConnection = true
        });

        var value = db.Ado.SqlQuerySingle<long>("SELECT CAST(@value AS bigint)", new SugarParameter("@value", long.MaxValue));

        Assert.Equal(long.MaxValue, value);
        Assert.IsType<Npgsql.NpgsqlConnection>(db.Ado.Connection);
        Assert.Equal(new Version(10, 0, 0, 0), db.Ado.Connection.GetType().Assembly.GetName().Version);
    }
}
