using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Radish.Common.LogTool;
using Radish.Common.OptionTool;
using Radish.DbMigrate;
using Radish.IRepository;
using Radish.Model.LogModels;
using Radish.Repository;
using Radish.Service;
using Radish.Service.Logging;
using SqlSugar;

namespace Radish.Api.Tests;

internal sealed class RuntimeLogIngestTestSupport : IDisposable
{
    public readonly string DirectoryPath = Path.Combine(Path.GetTempPath(), "radish-l3-" + Guid.NewGuid().ToString("N"));
    public readonly SqlSugarScope Db;
    public readonly RuntimeLogIngestOptions Options = new()
    {
        Enabled = true, Port = 15199, Credential = new string('k', 40), DeploymentId = "local", Services = ["api", "log-collector"],
        DatabaseTimeoutSeconds = 1
    };
    public readonly RuntimeLogIngestState State = new(2);
    public readonly StringWriter Emergency = new();
    public readonly TimeProvider Clock = new FixedClock();
    public readonly ServiceProvider Services = new ServiceCollection().AddSingleton(TimeProvider.System).BuildServiceProvider();
    public RuntimeLogIngestTestSupport(bool migrate = true)
    {
        Directory.CreateDirectory(DirectoryPath);
        Db = new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "log", ConnectionString = "Data Source=" + Path.Combine(DirectoryPath, "log.db") + ";Pooling=False;Default Timeout=1",
            DbType = DbType.Sqlite, IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute
        });
        if (migrate) RuntimeLogIngestSchemaMigration.Instance.Apply(Db.GetConnectionScope("log"), Services);
    }
    public IRuntimeLogIngestRepository Repository => new RuntimeLogIngestRepository(Db, Options, Clock);
    public RuntimeLogIngestService Service(IRuntimeLogIngestRepository? repository = null) =>
        new(repository ?? Repository, Options, State, new RuntimeLogIngestDiagnostics(Clock, Emergency), Clock);
    public static MemoryStream Body(string text) => new(Encoding.UTF8.GetBytes(text));
    public string Event(string? id = null, string level = "Warning")
    {
        var policy = new RuntimeLogPolicy(new RuntimeLogSource("local", "api", "test", "test"), clock: Clock);
        var value = policy.Create(JsonSerializer.SerializeToElement(new { eventCode = "runtime.started", level }))!;
        return (value with { EventId = id ?? Guid.NewGuid().ToString("D") }).ToJsonLine();
    }
    public void Dispose()
    {
        Db.Dispose(); Services.Dispose(); Emergency.Dispose();
        Directory.Delete(DirectoryPath, recursive: true);
    }
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 5, 1, 2, 3, 456, TimeSpan.Zero);
    }
}
