using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OpenIddict.Abstractions;
using Radish.Auth.OpenIddict;
using Radish.DbMigrate;
using Radish.Extension.Log;
using Radish.Model;
using Radish.Model.LogModels;
using Serilog;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class SeedLoggingTests
{
    private const string Secret = "SEED_PRIVATE_SENTINEL";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SeedFailure_ShouldKeepConsoleAndExceptionOwnership(bool candidate)
    {
        using var output = new StringWriter();
        using var logger = CreateLogger(output, candidate);
        using var error = new StringWriter();
        var previous = Log.Logger;
        var console = Console.Out;
        var failure = new InvalidOperationException(Secret);
        try
        {
            Log.Logger = logger;
            var result = await RuntimeProcess.RunAsync("dbmigrate", async () =>
            {
                var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    InitialDataSeeder.RunSeedStepAsync(SeedStep.DeveloperUsers, () =>
                    {
                        Assert.Same(console, Console.Out);
                        throw failure;
                    }));
                Assert.Same(failure, actual);
                throw actual;
            }, error);
            Assert.Equal(1, result);
        }
        finally { Log.Logger = previous; }

        Assert.Same(console, Console.Out);
        Assert.Single(Lines(output));
        Assert.Contains("failed", output.ToString());
        Assert.Contains("DeveloperUsers", output.ToString());
        Assert.Single(Lines(error));
        Assert.Contains("runtime.failed", error.ToString());
        Assert.DoesNotContain(Secret, output.ToString() + error);
    }

    [Fact]
    public async Task ConcurrentSeedStages_ShouldKeepLogicalScopesIsolated()
    {
        using var output = new StringWriter();
        using var logger = CreateLogger(output, true);
        var previous = Log.Logger;
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            Log.Logger = logger;
            var first = InitialDataSeeder.RunSeedStepAsync(SeedStep.Roles, async () =>
            {
                firstEntered.SetResult();
                await releaseFirst.Task;
            });
            await firstEntered.Task;
            try { await InitialDataSeeder.RunSeedStepAsync(SeedStep.Tenants, () => Task.CompletedTask); }
            finally { releaseFirst.SetResult(); }
            await first;
            logger.ForContext("EventCode", "dbmigrate.seed.completed").Information("Completed");
        }
        finally { Log.Logger = previous; }

        var lines = Lines(output);
        Assert.Equal(3, lines.Length);
        using var tenants = JsonDocument.Parse(lines[0]);
        using var roles = JsonDocument.Parse(lines[1]);
        using var completed = JsonDocument.Parse(lines[2]);
        Assert.Equal("Tenants", tenants.RootElement.GetProperty("properties").GetProperty("seedStep").GetString());
        Assert.Equal("Roles", roles.RootElement.GetProperty("properties").GetProperty("seedStep").GetString());
        Assert.False(completed.RootElement.GetProperty("properties").TryGetProperty("seedStep", out _));
    }

    [Fact]
    public async Task RoleSeed_ShouldRemainIdempotent_WithoutRawOutput()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = "Data Source=:memory:", DbType = DbType.Sqlite,
            IsAutoCloseConnection = false, InitKeyType = InitKeyType.Attribute
        });
        db.CodeFirst.InitTables<Role>();
        var method = typeof(InitialDataSeeder).GetMethod("SeedRolesAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        var previous = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            await (Task)method.Invoke(null, [db])!;
            await (Task)method.Invoke(null, [db])!;
        }
        finally { Console.SetOut(previous); }
        Assert.Equal(3, await db.Queryable<Role>().CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal("", output.ToString());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AuthSeed_ShouldKeepMutationsAndOnlyEmitSafeCompletion(bool candidate, bool existing)
    {
        using var output = new StringWriter();
        using var logger = CreateLogger(output, candidate);
        using var factory = LoggerFactory.Create(builder => builder.AddSerilog(logger));
        var applications = new Mock<IOpenIddictApplicationManager>();
        var scopes = new Mock<IOpenIddictScopeManager>();
        var descriptors = new List<OpenIddictApplicationDescriptor>();
        applications.Setup(m => m.FindByClientIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing ? new object() : null);
        scopes.Setup(m => m.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing ? new object() : null);
        applications.Setup(m => m.CreateAsync(It.IsAny<OpenIddictApplicationDescriptor>(), It.IsAny<CancellationToken>()))
            .Callback<OpenIddictApplicationDescriptor, CancellationToken>((value, _) => descriptors.Add(value))
            .ReturnsAsync(new object());
        applications.Setup(m => m.UpdateAsync(It.IsAny<object>(), It.IsAny<OpenIddictApplicationDescriptor>(), It.IsAny<CancellationToken>()))
            .Callback<object, OpenIddictApplicationDescriptor, CancellationToken>((_, value, _) => descriptors.Add(value))
            .Returns(ValueTask.CompletedTask);
        var service = new OpenIddictSeedHostedService(applications.Object, scopes.Object,
            Configuration(), factory.CreateLogger<OpenIddictSeedHostedService>());
        var previous = Console.Out;
        using var raw = new StringWriter();
        try
        {
            Console.SetOut(raw);
            await service.StartAsync(TestContext.Current.CancellationToken);
        }
        finally { Console.SetOut(previous); }
        Assert.Equal("", raw.ToString());
        Assert.Equal(3, descriptors.Count);
        Assert.All(descriptors, descriptor => Assert.Contains(descriptor.RedirectUris, uri => uri.ToString().Contains(Secret)));
        applications.Verify(m => m.CreateAsync(It.IsAny<OpenIddictApplicationDescriptor>(), It.IsAny<CancellationToken>()), Times.Exactly(existing ? 0 : 3));
        applications.Verify(m => m.UpdateAsync(It.IsAny<object>(), It.IsAny<OpenIddictApplicationDescriptor>(), It.IsAny<CancellationToken>()), Times.Exactly(existing ? 3 : 0));
        applications.Verify(m => m.DeleteAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Exactly(existing ? 1 : 0));
        scopes.Verify(m => m.CreateAsync(It.IsAny<OpenIddictScopeDescriptor>(), It.IsAny<CancellationToken>()), Times.Exactly(existing ? 0 : 1));
        Assert.Single(Lines(output));
        Assert.Contains("auth.seed.completed", output.ToString());
        Assert.DoesNotContain(Secret, output.ToString());
        if (candidate)
        {
            using var value = JsonDocument.Parse(output.ToString());
            var properties = value.RootElement.GetProperty("properties");
            Assert.Equal(existing ? 0 : 4, properties.GetProperty("createdCount").GetInt32());
            Assert.Equal(existing ? 3 : 0, properties.GetProperty("updatedCount").GetInt32());
            Assert.Equal(existing ? 1 : 0, properties.GetProperty("removedCount").GetInt32());
        }
    }

    [Fact]
    public async Task AuthSeedFailure_ShouldPropagateOriginalException_WithoutCompletion()
    {
        using var output = new StringWriter();
        using var logger = CreateLogger(output, true);
        using var factory = LoggerFactory.Create(builder => builder.AddSerilog(logger));
        var scopes = new Mock<IOpenIddictScopeManager>();
        var failure = new InvalidOperationException(Secret);
        scopes.Setup(m => m.CreateAsync(It.IsAny<OpenIddictScopeDescriptor>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        var service = new OpenIddictSeedHostedService(new Mock<IOpenIddictApplicationManager>().Object, scopes.Object,
            Configuration(), factory.CreateLogger<OpenIddictSeedHostedService>());
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(TestContext.Current.CancellationToken));
        Assert.Same(failure, actual);
        Assert.Equal("", output.ToString());
    }

    [Fact]
    public void RegisteredMigrationIds_ShouldSurviveSafePolicy()
    {
        using var output = new StringWriter();
        using var logger = CreateLogger(output, true);
        foreach (var id in SchemaMigrationRegistry.All.Select(m => m.MigrationId).Prepend(SchemaMigrationLedger.BaselineMigrationId))
        {
            output.GetStringBuilder().Clear();
            logger.ForContext("EventCode", "dbmigrate.schema.applied").ForContext("migrationId", id)
                .ForContext("databaseScope", "main").Information("Committed");
            using var value = JsonDocument.Parse(output.ToString());
            Assert.Equal(id, value.RootElement.GetProperty("properties").GetProperty("migrationId").GetString());
        }
    }

    [Fact]
    public void Ledger_ShouldOnlySummarizeCommittedChanges_AndKeepChecksumGuard()
    {
        using var db = new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "main", ConnectionString = "Data Source=:memory:", DbType = DbType.Sqlite,
            IsAutoCloseConnection = false, InitKeyType = InitKeyType.Attribute
        });
        var main = db.GetConnectionScope("main");
        foreach (var type in DbMigrateEntityRegistry.GetEntityTypesForConfig("Main"))
            main.CodeFirst.InitTables(type);
        using var output = new StringWriter();
        using var logger = CreateLogger(output, true);
        var previous = Log.Logger;
        try
        {
            Log.Logger = logger;
            SchemaMigrationLedger.EnsureBaseline(db, TimeProvider.System, ["main"]);
            Assert.Single(Lines(output));
            SchemaMigrationLedger.EnsureBaseline(db, TimeProvider.System, ["main"]);
            Assert.Single(Lines(output));
            Assert.Equal(1, main.Queryable<SchemaMigrationRecord>().Count());
            main.Updateable<SchemaMigrationRecord>().SetColumns(record => record.Checksum == Secret)
                .Where(record => record.MigrationId == SchemaMigrationLedger.BaselineMigrationId).ExecuteCommand();
            Assert.Throws<InvalidOperationException>(() => SchemaMigrationLedger.EnsureBaseline(db, TimeProvider.System, ["main"]));
            Assert.Single(Lines(output));
        }
        finally { Log.Logger = previous; }
        Assert.Contains("dbmigrate.schema.applied", output.ToString());
        Assert.DoesNotContain(Secret, output.ToString());
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["OpenIddict:Server:Issuer"] = $"https://localhost/{Secret}/" }).Build();

    private static string[] Lines(StringWriter output) => output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static Serilog.Core.Logger CreateLogger(TextWriter output, bool candidate)
    {
        var configuration = new LoggerConfiguration();
        if (candidate)
            RuntimeLoggingConfiguration.Configure(configuration, new ConfigurationBuilder().Build(), "Production", "dbmigrate", output, output);
        else configuration.Enrich.FromLogContext().WriteTo.Sink(new LegacySink(output));
        return configuration.CreateLogger();
    }

    private sealed class LegacySink(TextWriter output) : Serilog.Core.ILogEventSink
    {
        private readonly Serilog.Formatting.Display.MessageTemplateTextFormatter _formatter =
            new("{Level} {Message:lj} {Properties:j} {Exception}{NewLine}");
        public void Emit(Serilog.Events.LogEvent value) => _formatter.Format(value, output);
    }
}
