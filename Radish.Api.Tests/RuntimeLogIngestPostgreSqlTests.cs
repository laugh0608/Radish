using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Radish.Api.Tests.Repositories;
using Radish.Api.Tests.TestCollections;
using Radish.Common.LogTool;
using Radish.Common.OptionTool;
using Radish.DbMigrate;
using Radish.Model.LogModels;
using Radish.Repository;
using Radish.Service;
using Radish.Service.Logging;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests;

[Collection(PostgreSqlIntegrationCollection.CollectionName)]
public sealed class RuntimeLogIngestPostgreSqlTests
{
    [Fact]
    [Trait("Database", "PostgreSQL")]
    public async Task MigrationAndConcurrentReplay_ShouldPreserveUtcAndConflictSemantics()
    {
        var connection = Environment.GetEnvironmentVariable("RADISH_TEST_POSTGRES_CONNECTION_STRING");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connection), "PostgreSQL integration requires separately authorized connection.");
        var schema = "runtime_log_" + Guid.NewGuid().ToString("N");
        using var admin = PostgreSqlIntegrationSqlSugarFactory.CreateClient(new ConnectionConfig
        {
            ConfigId = "log", ConnectionString = connection!, DbType = DbType.PostgreSQL,
            IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute
        });
        await admin.Ado.ExecuteCommandAsync("CREATE SCHEMA " + schema);
        try
        {
            using var scope = PostgreSqlIntegrationSqlSugarFactory.CreateScope(new ConnectionConfig
            {
                ConfigId = "log", ConnectionString = connection!.TrimEnd(';') + ";Search Path=" + schema + ";Pooling=false",
                DbType = DbType.PostgreSQL, IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute
            });
            using var services = new ServiceCollection().AddSingleton(TimeProvider.System).BuildServiceProvider();
            var database = scope.GetConnectionScope("log");
            var migration = RuntimeLogIngestSchemaMigration.Instance;
            migration.Apply(database, services); migration.Apply(database, services);
            Assert.Empty(migration.Verify(database, services));
            var options = new RuntimeLogIngestOptions { Enabled = true, DeploymentId = "local", Services = ["api"] };
            var state = new RuntimeLogIngestState(2);
            var diagnostics = new RuntimeLogIngestDiagnostics(writer: TextWriter.Null);
            var repository = new RuntimeLogIngestRepository(scope, options, TimeProvider.System);
            var service = new RuntimeLogIngestService(repository, options, state, diagnostics, TimeProvider.System);
            var policy = new RuntimeLogPolicy(new RuntimeLogSource("local", "api", "test", "test"));
            var value = policy.Create(JsonSerializer.SerializeToElement(new { level = "Warning", eventCode = "runtime.started" }))!;
            var line = value.ToJsonLine();
            var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => service.IngestAsync(RuntimeLogIngestTestSupport.Body(line), CancellationToken.None)));
            Assert.All(results, r => Assert.Equal(200, r.StatusCode));
            Assert.Equal(1, results.Sum(r => r.Summary.VoAccepted)); Assert.Equal(1, results.Sum(r => r.Summary.VoDuplicate));
            var conflict = await service.IngestAsync(RuntimeLogIngestTestSupport.Body((value with { Level = "Error" }).ToJsonLine()), CancellationToken.None);
            Assert.Equal(1, conflict.Summary.VoRejected);
            var row = Assert.Single(database.Queryable<RuntimeLogEventRecord>().ToList());
            RuntimeLogPolicy.TryReadUtc(value.OccurredAtUtc, out var expected);
            Assert.Equal(expected.Ticks, row.OccurredAtUtc.Ticks);
            Assert.Equal("Warning", row.Level);
        }
        finally { await admin.Ado.ExecuteCommandAsync("DROP SCHEMA " + schema + " CASCADE"); }
    }
}
