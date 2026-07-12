using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Radish.Common.DbTool;

namespace Radish.Auth.OpenIddict;

internal static class AuthOpenIddictSchemaAdoption
{
    private const string EfProductVersion = "10.0.0";

    public static async Task AdoptEnsureCreatedSchemaIfNeededAsync(
        AuthOpenIddictDbContext db,
        RuntimeDatabaseConfig database,
        CancellationToken cancellationToken)
    {
        var appliedMigrations = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        if (appliedMigrations.Count > 0)
        {
            return;
        }

        var knownMigrations = db.Database.GetMigrations().ToList();
        if (knownMigrations.Count == 0)
        {
            throw new InvalidOperationException("OpenIddict 未发现可用 EF migrations，禁止继续。");
        }

        var expectedSchema = BuildExpectedSchema(db);
        var actualSchema = await ReadActualSchemaAsync(db, database, cancellationToken);
        var existingDomainTables = expectedSchema.Keys
            .Where(actualSchema.Tables.Contains)
            .ToList();
        if (existingDomainTables.Count == 0)
        {
            return;
        }

        var missingTables = expectedSchema.Keys
            .Where(table => !actualSchema.Tables.Contains(table))
            .ToList();
        var missingColumns = expectedSchema
            .SelectMany(table => table.Value.Columns
                .Where(column => !actualSchema.Columns.GetValueOrDefault(table.Key, EmptySet).Contains(column))
                .Select(column => $"{table.Key}.{column}"))
            .ToList();
        var missingIndexes = expectedSchema
            .SelectMany(table => table.Value.Indexes
                .Where(index => !actualSchema.Indexes.Contains(index))
                .Select(index => $"{table.Key}.{index}"))
            .ToList();

        if (missingTables.Count > 0 || missingColumns.Count > 0 || missingIndexes.Count > 0)
        {
            var problems = missingTables.Select(table => $"table:{table}")
                .Concat(missingColumns.Select(column => $"column:{column}"))
                .Concat(missingIndexes.Select(index => $"index:{index}"));
            throw new InvalidOperationException(
                $"OpenIddict 既有 EnsureCreated schema 不满足 baseline adoption：{string.Join(", ", problems.Take(30))}。");
        }

        if (knownMigrations.Count != 1)
        {
            throw new InvalidOperationException(
                $"OpenIddict 既有 schema 无迁移历史，但当前包含 {knownMigrations.Count} 个 migrations；禁止自动推断 baseline。");
        }

        var historyRepository = db.GetService<IHistoryRepository>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            historyRepository.GetCreateIfNotExistsScript(),
            cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            historyRepository.GetInsertScript(new HistoryRow(knownMigrations[0], EfProductVersion)),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        Console.WriteLine(
            $"[Radish.DbMigrate] OpenIddict 既有 EnsureCreated schema 已采用 baseline：{knownMigrations[0]}。");
    }

    private static IReadOnlyDictionary<string, ExpectedTable> BuildExpectedSchema(AuthOpenIddictDbContext db)
    {
        return db.Model.GetEntityTypes()
            .Where(entityType => !string.IsNullOrWhiteSpace(entityType.GetTableName()))
            .GroupBy(entityType => entityType.GetTableName()!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var storeObject = StoreObjectIdentifier.Table(group.Key, group.First().GetSchema());
                    var columns = group.SelectMany(entityType => entityType.GetProperties())
                        .Select(property => property.GetColumnName(storeObject))
                        .Where(column => !string.IsNullOrWhiteSpace(column))
                        .Select(column => column!)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var indexes = group.SelectMany(entityType => entityType.GetIndexes())
                        .Select(index => index.GetDatabaseName())
                        .Where(index => !string.IsNullOrWhiteSpace(index))
                        .Select(index => index!)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    return new ExpectedTable(columns, indexes);
                },
                StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<ActualSchema> ReadActualSchemaAsync(
        AuthOpenIddictDbContext db,
        RuntimeDatabaseConfig database,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            return database.DbType == DataBaseType.PostgreSql
                ? await ReadPostgreSqlSchemaAsync(connection, cancellationToken)
                : await ReadSqliteSchemaAsync(connection, cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<ActualSchema> ReadSqliteSchemaAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var tables = await ReadSingleColumnAsync(
            connection,
            "SELECT name FROM sqlite_master WHERE type = 'table'",
            cancellationToken);
        var indexes = await ReadSingleColumnAsync(
            connection,
            "SELECT name FROM sqlite_master WHERE type = 'index' AND name NOT LIKE 'sqlite_autoindex_%'",
            cancellationToken);
        var columns = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in tables)
        {
            columns[table] = await ReadSingleColumnAsync(
                connection,
                $"SELECT name FROM pragma_table_info('{EscapeSqlLiteral(table)}')",
                cancellationToken);
        }

        return new ActualSchema(tables, columns, indexes);
    }

    private static async Task<ActualSchema> ReadPostgreSqlSchemaAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var tables = await ReadSingleColumnAsync(
            connection,
            "SELECT table_name FROM information_schema.tables WHERE table_schema = current_schema()",
            cancellationToken);
        var indexes = await ReadSingleColumnAsync(
            connection,
            "SELECT indexname FROM pg_indexes WHERE schemaname = current_schema()",
            cancellationToken);
        var columns = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in tables)
        {
            columns[table] = await ReadSingleColumnAsync(
                connection,
                $"SELECT column_name FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = '{EscapeSqlLiteral(table)}'",
                cancellationToken);
        }

        return new ActualSchema(tables, columns, indexes);
    }

    private static async Task<HashSet<string>> ReadSingleColumnAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                values.Add(reader.GetString(0));
            }
        }

        return values;
    }

    private static string EscapeSqlLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static readonly HashSet<string> EmptySet = new(StringComparer.OrdinalIgnoreCase);

    private sealed record ExpectedTable(HashSet<string> Columns, HashSet<string> Indexes);

    private sealed record ActualSchema(
        HashSet<string> Tables,
        Dictionary<string, HashSet<string>> Columns,
        HashSet<string> Indexes);
}
