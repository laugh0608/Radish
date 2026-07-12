using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Radish.Auth.OpenIddict;
using Radish.Common.DbTool;
using Xunit;

namespace Radish.Api.Tests.Auth;

public sealed class AuthOpenIddictMigrationTest
{
    private const string PostgreSqlConnectionStringEnvironmentVariable = "RADISH_TEST_POSTGRES_CONNECTION_STRING";

    [Fact]
    public async Task ApplyMigrationsAsync_ShouldInitializeEmptySqliteAndBeIdempotent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateTemporaryDatabasePath();
        var database = new RuntimeDatabaseConfig(DataBaseType.Sqlite, $"Data Source={path}");

        try
        {
            await using var db = CreateDbContext(database.ConnectionString);

            var firstApplied = await AuthOpenIddictPersistence.ApplyMigrationsAsync(db, database, cancellationToken);
            var secondApplied = await AuthOpenIddictPersistence.ApplyMigrationsAsync(db, database, cancellationToken);

            Assert.Single(firstApplied);
            Assert.Empty(secondApplied);
            Assert.Single(await db.Database.GetAppliedMigrationsAsync(cancellationToken));
            AuthOpenIddictPersistence.EnsureReady(db, database);
        }
        finally
        {
            DeleteTemporaryDatabase(path);
        }
    }

    [Fact]
    public async Task ApplyMigrationsAsync_ShouldAdoptCompleteEnsureCreatedSchema()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateTemporaryDatabasePath();
        var database = new RuntimeDatabaseConfig(DataBaseType.Sqlite, $"Data Source={path}");

        try
        {
            await using (var legacyDb = CreateDbContext(database.ConnectionString))
            {
                Assert.True(await legacyDb.Database.EnsureCreatedAsync(cancellationToken));
                Assert.Empty(await legacyDb.Database.GetAppliedMigrationsAsync(cancellationToken));
            }

            await using var db = CreateDbContext(database.ConnectionString);
            var applied = await AuthOpenIddictPersistence.ApplyMigrationsAsync(db, database, cancellationToken);

            Assert.Empty(applied);
            Assert.Single(await db.Database.GetAppliedMigrationsAsync(cancellationToken));
            AuthOpenIddictPersistence.EnsureReady(db, database);
        }
        finally
        {
            DeleteTemporaryDatabase(path);
        }
    }

    [Fact]
    public async Task ApplyMigrationsAsync_ShouldRejectIncompleteEnsureCreatedSchema()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateTemporaryDatabasePath();
        var database = new RuntimeDatabaseConfig(DataBaseType.Sqlite, $"Data Source={path}");

        try
        {
            await using var db = CreateDbContext(database.ConnectionString);
            Assert.True(await db.Database.EnsureCreatedAsync(cancellationToken));
            await db.Database.ExecuteSqlRawAsync(
                "DROP INDEX \"IX_OpenIddictTokens_ReferenceId\"",
                cancellationToken);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                AuthOpenIddictPersistence.ApplyMigrationsAsync(db, database, cancellationToken));

            Assert.Contains("baseline adoption", exception.Message, StringComparison.Ordinal);
            Assert.Contains("IX_OpenIddictTokens_ReferenceId", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteTemporaryDatabase(path);
        }
    }

    [Fact]
    [Trait("Database", "PostgreSQL")]
    public async Task ApplyMigrationsAsync_ShouldAdoptPostgreSqlEnsureCreatedSchema()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminConnectionString = Environment.GetEnvironmentVariable(PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过 OpenIddict PostgreSQL 迁移测试");

        var databaseName = $"q2b_openiddict_{Guid.NewGuid():N}";
        var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Database = "postgres",
            Pooling = false
        };
        await using var adminConnection = new NpgsqlConnection(adminBuilder.ConnectionString);
        await adminConnection.OpenAsync(cancellationToken);
        await ExecutePostgreSqlAsync(
            adminConnection,
            $"CREATE DATABASE {QuoteIdentifier(databaseName)}",
            cancellationToken);

        try
        {
            var targetBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString)
            {
                Database = databaseName,
                Pooling = false
            };
            var connectionString = targetBuilder.ConnectionString;
            var database = new RuntimeDatabaseConfig(DataBaseType.PostgreSql, connectionString);
            await using (var legacyDb = CreatePostgreSqlDbContext(connectionString))
            {
                Assert.True(await legacyDb.Database.EnsureCreatedAsync(cancellationToken));
                Assert.Empty(await legacyDb.Database.GetAppliedMigrationsAsync(cancellationToken));
            }

            await using var db = CreatePostgreSqlDbContext(connectionString);
            var applied = await AuthOpenIddictPersistence.ApplyMigrationsAsync(db, database, cancellationToken);

            Assert.Empty(applied);
            Assert.Single(await db.Database.GetAppliedMigrationsAsync(cancellationToken));
            AuthOpenIddictPersistence.EnsureReady(db, database);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await ExecutePostgreSqlAsync(
                adminConnection,
                $"DROP DATABASE IF EXISTS {QuoteIdentifier(databaseName)} WITH (FORCE)",
                cancellationToken);
        }
    }

    private static AuthOpenIddictDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AuthOpenIddictDbContext>()
            .UseSqlite(
                connectionString,
                provider => provider.MigrationsAssembly(AuthOpenIddictPersistence.SqliteMigrationsAssembly))
            .Options;
        return new AuthOpenIddictDbContext(options);
    }

    private static AuthOpenIddictDbContext CreatePostgreSqlDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AuthOpenIddictDbContext>()
            .UseNpgsql(
                connectionString,
                provider => provider.MigrationsAssembly(AuthOpenIddictPersistence.PostgreSqlMigrationsAssembly))
            .Options;
        return new AuthOpenIddictDbContext(options);
    }

    private static async Task ExecutePostgreSqlAsync(
        NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string CreateTemporaryDatabasePath()
    {
        return Path.Combine(Path.GetTempPath(), $"radish-openiddict-migration-{Guid.NewGuid():N}.db");
    }

    private static void DeleteTemporaryDatabase(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
