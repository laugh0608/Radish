using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            AssertModelIsCurrent(db, database);
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
            AssertModelIsCurrent(db, database);
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
    public async Task DbMigrateApply_ShouldInitializeEmptyPostgreSqlOpenIddictAndBeIdempotent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminConnectionString = Environment.GetEnvironmentVariable(PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过 OpenIddict PostgreSQL 迁移测试");

        var suffix = Guid.NewGuid().ToString("N");
        var databaseNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Main"] = $"q2b_openiddict_main_{suffix}",
            ["Log"] = $"q2b_openiddict_log_{suffix}",
            ["Message"] = $"q2b_openiddict_message_{suffix}",
            ["Chat"] = $"q2b_openiddict_chat_{suffix}",
            ["OpenIddict"] = $"q2b_openiddict_auth_{suffix}"
        };
        var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Database = "postgres",
            Pooling = false
        };
        await using var adminConnection = new NpgsqlConnection(adminBuilder.ConnectionString);
        await adminConnection.OpenAsync(cancellationToken);
        foreach (var databaseName in databaseNames.Values)
        {
            await ExecutePostgreSqlAsync(
                adminConnection,
                $"CREATE DATABASE {QuoteIdentifier(databaseName)}",
                cancellationToken);
        }

        try
        {
            var environment = BuildDbMigrateEnvironment(adminConnectionString!, databaseNames);

            var firstOutput = await RunDbMigrateAsync(environment, cancellationToken);
            var secondOutput = await RunDbMigrateAsync(environment, cancellationToken);

            Assert.Contains("OpenIddict provider=PostgreSql, applied=1", firstOutput, StringComparison.Ordinal);
            Assert.Contains("OpenIddict provider=PostgreSql, applied=0", secondOutput, StringComparison.Ordinal);

            var openIddictConnectionString = environment["OpenIddict__Database__ConnectionString"];
            var database = new RuntimeDatabaseConfig(DataBaseType.PostgreSql, openIddictConnectionString);
            await using var db = CreatePostgreSqlDbContext(openIddictConnectionString);
            AssertModelIsCurrent(db, database);
            Assert.Single(await db.Database.GetAppliedMigrationsAsync(cancellationToken));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            foreach (var databaseName in databaseNames.Values.Reverse())
            {
                await ExecutePostgreSqlAsync(
                    adminConnection,
                    $"DROP DATABASE IF EXISTS {QuoteIdentifier(databaseName)} WITH (FORCE)",
                    cancellationToken);
            }
        }
    }

    [Fact]
    [Trait("Database", "PostgreSQL")]
    public async Task ApplyMigrationsAsync_ShouldInitializeEmptyPostgreSqlAndBeIdempotent()
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
            await using var db = CreatePostgreSqlDbContext(connectionString);

            var firstApplied = await AuthOpenIddictPersistence.ApplyMigrationsAsync(db, database, cancellationToken);
            var secondApplied = await AuthOpenIddictPersistence.ApplyMigrationsAsync(db, database, cancellationToken);

            Assert.Single(firstApplied);
            Assert.Empty(secondApplied);
            Assert.Single(await db.Database.GetAppliedMigrationsAsync(cancellationToken));
            AuthOpenIddictPersistence.EnsureReady(db, database);
            AssertModelIsCurrent(db, database);
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
            AssertModelIsCurrent(db, database);
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

    private static void AssertModelIsCurrent(
        AuthOpenIddictDbContext db,
        RuntimeDatabaseConfig database)
    {
        var status = AuthOpenIddictPersistence.Inspect(db, database);
        Assert.Empty(status.PendingMigrations);
        Assert.Empty(status.ModelDifferences);
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

    private static IReadOnlyDictionary<string, string> BuildDbMigrateEnvironment(
        string adminConnectionString,
        IReadOnlyDictionary<string, string> databaseNames)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DOTNET_ENVIRONMENT"] = "Production",
            ["RadishDeployment__Stage"] = "production",
            ["Seed__DeveloperDefaultsEnabled"] = "false",
            ["Redis__Enable"] = "false",
            ["MainDb"] = "Main"
        };
        var connectionIds = new[] { "Main", "Log", "Message", "Chat" };
        for (var index = 0; index < connectionIds.Length; index++)
        {
            var connectionId = connectionIds[index];
            environment[$"Databases__{index}__ConnId"] = connectionId;
            environment[$"Databases__{index}__DbType"] = ((int)DataBaseType.PostgreSql).ToString();
            environment[$"Databases__{index}__Enabled"] = "true";
            environment[$"Databases__{index}__HitRate"] = "50";
            environment[$"Databases__{index}__ConnectionString"] = BuildDatabaseConnectionString(
                adminConnectionString,
                databaseNames[connectionId]);
        }

        environment["OpenIddict__Database__DbType"] = ((int)DataBaseType.PostgreSql).ToString();
        environment["OpenIddict__Database__ConnectionString"] = BuildDatabaseConnectionString(
            adminConnectionString,
            databaseNames["OpenIddict"]);
        return environment;
    }

    private static string BuildDatabaseConnectionString(string connectionString, string databaseName)
    {
        return new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = databaseName,
            Pooling = false
        }.ConnectionString;
    }

    private static async Task<string> RunDbMigrateAsync(
        IReadOnlyDictionary<string, string> environment,
        CancellationToken cancellationToken)
    {
        var assemblyPath = Path.Combine(AppContext.BaseDirectory, "Radish.DbMigrate.dll");
        Assert.True(File.Exists(assemblyPath), $"未找到 DbMigrate 构建产物：{assemblyPath}");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = FindSolutionRoot()
        };
        startInfo.ArgumentList.Add(assemblyPath);
        startInfo.ArgumentList.Add("apply");
        foreach (var (key, value) in environment)
        {
            startInfo.Environment[key] = value;
        }

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = await outputTask;
        var error = await errorTask;

        Assert.True(
            process.ExitCode == 0,
            $"DbMigrate apply 失败，ExitCode={process.ExitCode}。\nSTDOUT:\n{output}\nSTDERR:\n{error}");
        return $"{output}\n{error}";
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Radish.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
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
