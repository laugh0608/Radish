using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Radish.Common.DbTool;

namespace Radish.Auth.OpenIddict;

public sealed record AuthOpenIddictMigrationStatus(
    RuntimeDatabaseConfig Database,
    IReadOnlyList<string> AppliedMigrations,
    IReadOnlyList<string> PendingMigrations,
    IReadOnlyList<string> ModelDifferences);

public static class AuthOpenIddictPersistence
{
    public const string PostgreSqlMigrationsAssembly = "Radish.Auth.Migrations.PostgreSql";
    public const string SqliteMigrationsAssembly = "Radish.Auth.Migrations.Sqlite";

    public static RuntimeDatabaseConfig AddAuthOpenIddictDbContext(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var database = ResolveDatabase(configuration);
        services.AddDbContext<AuthOpenIddictDbContext>(options => Configure(options, database));
        return database;
    }

    public static RuntimeDatabaseConfig ResolveDatabase(IConfiguration configuration)
    {
        return RuntimeDatabaseConfigResolver.Resolve(
            configuration,
            "OpenIddict:Database",
            configuration.GetConnectionString("OpenIddict"),
            "Radish.OpenIddict.db");
    }

    public static AuthOpenIddictMigrationStatus Inspect(
        AuthOpenIddictDbContext db,
        RuntimeDatabaseConfig database)
    {
        EnsureDatabaseExistsForRead(database);

        var applied = db.Database.GetAppliedMigrations().ToList();
        var appliedSet = applied.ToHashSet(StringComparer.Ordinal);
        var pending = db.Database.GetMigrations()
            .Where(migration => !appliedSet.Contains(migration))
            .ToList();
        var modelDifferences = AuthOpenIddictModelConsistency.Inspect(db);

        return new AuthOpenIddictMigrationStatus(database, applied, pending, modelDifferences);
    }

    public static void EnsureReady(
        AuthOpenIddictDbContext db,
        RuntimeDatabaseConfig database)
    {
        var status = Inspect(db, database);
        if (status.ModelDifferences.Count > 0)
        {
            throw new InvalidOperationException(
                $"OpenIddict 运行态模型与 migration snapshot 不一致：" +
                $"{string.Join("；", status.ModelDifferences)}。");
        }

        if (status.PendingMigrations.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"OpenIddict schema 未就绪，存在 {status.PendingMigrations.Count} 个 pending migration：" +
            $"{string.Join(", ", status.PendingMigrations)}。请先执行 Radish.DbMigrate apply。");
    }

    public static async Task<IReadOnlyList<string>> ApplyMigrationsAsync(
        AuthOpenIddictDbContext db,
        RuntimeDatabaseConfig database,
        CancellationToken cancellationToken = default)
    {
        await AuthOpenIddictSchemaAdoption.AdoptEnsureCreatedSchemaIfNeededAsync(
            db,
            database,
            cancellationToken);

        var modelDifferences = AuthOpenIddictModelConsistency.Inspect(db);
        if (modelDifferences.Count > 0)
        {
            throw new InvalidOperationException(
                $"OpenIddict 运行态模型与 migration snapshot 不一致：" +
                $"{string.Join("；", modelDifferences)}。");
        }

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            await db.Database.MigrateAsync(cancellationToken);
        }

        return pending;
    }

    private static void Configure(
        DbContextOptionsBuilder options,
        RuntimeDatabaseConfig database)
    {
        if (database.DbType == DataBaseType.PostgreSql)
        {
            options.UseNpgsql(
                database.ConnectionString,
                provider => provider.MigrationsAssembly(PostgreSqlMigrationsAssembly));
            return;
        }

        options.UseSqlite(
            database.ConnectionString,
            provider => provider.MigrationsAssembly(SqliteMigrationsAssembly));
    }

    private static void EnsureDatabaseExistsForRead(RuntimeDatabaseConfig database)
    {
        if (database.DbType != DataBaseType.Sqlite)
        {
            return;
        }

        var dataSource = new SqliteConnectionStringBuilder(database.ConnectionString).DataSource;
        if (string.IsNullOrWhiteSpace(dataSource) || !File.Exists(dataSource))
        {
            throw new InvalidOperationException(
                $"OpenIddict SQLite 数据库不存在：{dataSource}。请先执行 Radish.DbMigrate apply。");
        }
    }
}
