using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Api.ErrorHandling;
using Radish.Common.AttributeTool;
using Radish.Common.OptionTool;
using Radish.DbMigrate;
using Radish.Extension.AopExtension;
using Radish.Extension.Log;
using Radish.Repository.UnitOfWorks;
using Serilog;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class ProducerLoggingTests
{
    private const string Secret = "PRODUCER_PRIVATE_SENTINEL";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SlowSql_ShouldIgnoreDiagnosticFilters_AndExcludePayload(bool candidate)
    {
        using var output = new StringWriter();
        using var logger = CreateLogger(output, candidate);
        using var factory = LoggerFactory.Create(builder => builder.AddSerilog(logger));
        var options = new SqlAopLogOptions
        {
            Enabled = false, LogQuery = false, LogUpdate = false,
            SkipTables = [Secret], SkipUsers = [Secret]
        };
        var sql = new SqlSugarAop(factory.CreateLogger<SqlSugarAop>(), options, false);
        sql.Executing(Secret, Secret, "Query", 2);
        sql.Executed("Query", 2, TimeSpan.FromMilliseconds(999));
        Assert.Equal("", output.ToString());
        sql.Executed("Query", 2, TimeSpan.FromMilliseconds(1000));
        sql.Executed("Update", 3, TimeSpan.FromMilliseconds(1001));
        sql.ConnectionChecked(TimeSpan.FromMilliseconds(500));
        Assert.Equal(3, Lines(output).Length);
        Assert.DoesNotContain(Secret, output.ToString());
        options.SlowQueryEnabled = false;
        options.SlowConnectionEnabled = false;
        sql.Executed("Query", 2, TimeSpan.FromSeconds(10));
        sql.ConnectionChecked(TimeSpan.FromSeconds(10));
        Assert.Equal(3, Lines(output).Length);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void SqlDiagnostics_ShouldRequireExplicitDevelopmentGate(bool enabled, int count)
    {
        using var output = new StringWriter();
        using var logger = CreateLogger(output, true, "Development", true);
        using var factory = LoggerFactory.Create(builder => builder.AddSerilog(logger));
        var sql = new SqlSugarAop(factory.CreateLogger<SqlSugarAop>(), new SqlAopLogOptions { Enabled = true }, enabled);
        sql.Executing(Secret, Secret, "Query", 2);
        Assert.Equal(count, Lines(output).Length);
        Assert.DoesNotContain(Secret, output.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApiPipeline_ShouldOwnOneSafeError_AfterTransactionRollback(bool candidate)
    {
        using var output = new StringWriter();
        using var logger = CreateLogger(output, candidate);
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(logger, dispose: false);
        builder.Services.AddSingleton<ApiExceptionHandler>();
        await using var app = builder.Build();
        var unit = new Mock<IUnitOfWorkManage>();
        var failure = new InvalidOperationException(Secret);
        var proxy = new ProxyGenerator().CreateInterfaceProxyWithTarget<ITransactionalService>(
            new TransactionalService(failure), new TranAop(unit.Object));
        app.UseApiExceptionHandler();
        app.Run(async _ => await proxy.ResultAsync());
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test/" + Secret;
        context.Response.Body = new MemoryStream();
        context.RequestServices = app.Services;
        await ((IApplicationBuilder)app).Build()(context);
        unit.Verify(u => u.BeginTran(It.IsAny<MethodInfo>()), Times.Once);
        unit.Verify(u => u.RollbackTran(It.IsAny<MethodInfo>()), Times.Once);
        unit.Verify(u => u.CommitTran(It.IsAny<MethodInfo>()), Times.Never);
        Assert.Equal(500, context.Response.StatusCode);
        var failures = Lines(output).Where(line => line.Contains("http.failed", StringComparison.Ordinal)).ToArray();
        Assert.Single(failures);
        Assert.DoesNotContain(Secret, output.ToString());
        Assert.DoesNotContain("runtime.unclassified", output.ToString());
    }

    [Theory]
    [InlineData("sync")]
    [InlineData("task")]
    [InlineData("result")]
    public async Task Transaction_ShouldPreserveOriginalFailureAndRollback(string kind)
    {
        var unit = new Mock<IUnitOfWorkManage>();
        var failure = new InvalidOperationException(Secret);
        var proxy = new ProxyGenerator().CreateInterfaceProxyWithTarget<ITransactionalService>(
            new TransactionalService(failure), new TranAop(unit.Object));
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            if (kind == "sync") proxy.Sync();
            else if (kind == "task") await proxy.TaskAsync();
            else await proxy.ResultAsync();
        });
        Assert.Same(failure, actual);
        unit.Verify(u => u.RollbackTran(It.IsAny<MethodInfo>()), Times.Once);
        unit.Verify(u => u.CommitTran(It.IsAny<MethodInfo>()), Times.Never);
    }

    [Fact]
    public async Task ProcessBoundary_ShouldKeepFailureNonzeroAndNeverRenderException()
    {
        using var error = new StringWriter();
        var code = await RuntimeProcess.RunAsync("api", () => throw new InvalidOperationException(Secret), error);
        Assert.Equal(1, code);
        Assert.Single(Lines(error));
        using var value = JsonDocument.Parse(error.ToString());
        Assert.Equal("runtime.failed", value.RootElement.GetProperty("eventCode").GetString());
        Assert.True(value.RootElement.GetProperty("isFatal").GetBoolean());
        Assert.DoesNotContain(Secret, error.ToString());
        Assert.Equal(0, await RuntimeProcess.RunAsync("api", () => Task.CompletedTask, error));
        Assert.Single(Lines(error));
        await Assert.ThrowsAsync<HostAbortedException>(() => RuntimeProcess.RunAsync("api", () => throw new HostAbortedException(), error));
    }

    [Fact]
    public async Task MigrationCommand_ShouldSeparateResultsAndPropagateFailures()
    {
        using var output = new StringWriter();
        using var logger = CreateLogger(output, true);
        using var results = new StringWriter();
        var previous = Console.Out;
        try
        {
            Console.SetOut(results);
            await DbMigrateCommand.RunAsync([Secret], logger, () =>
                DbMigrateRunner.RunAsync(new ServiceCollection().BuildServiceProvider(), new ConfigurationBuilder().Build(), "Production", [Secret]));
        }
        finally { Console.SetOut(previous); }
        Assert.Contains("Radish.DbMigrate 用法:", results.ToString());
        Assert.DoesNotContain("eventId", results.ToString());
        Assert.Equal(2, Lines(output).Length);
        Assert.DoesNotContain(Secret, output.ToString());
        output.GetStringBuilder().Clear();
        var failure = new InvalidOperationException(Secret);
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DbMigrateCommand.RunAsync(["verify"], logger, () => throw failure));
        Assert.Same(failure, actual);
        Assert.Single(Lines(output)); // 只有 started；失败由进程最终边界记录。
        Assert.DoesNotContain("dbmigrate.completed", output.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HostingFailure_ShouldHaveOneFinalError(bool candidate)
    {
        using var output = new StringWriter();
        using var logger = CreateLogger(output, candidate);
        var result = await RuntimeProcess.RunAsync("api", async () =>
        {
            using var host = new HostBuilder().ConfigureLogging(builder => builder.AddSerilog(logger))
                .ConfigureServices(services => services.AddHostedService<FailingHostedService>()).Build();
            await host.StartAsync();
        }, output);
        Assert.Equal(1, result);
        Assert.Single(Lines(output), line => line.Contains("runtime.failed", StringComparison.Ordinal));
        Assert.DoesNotContain("Hosting failed", output.ToString());
        Assert.DoesNotContain("runtime.unclassified", output.ToString());
        Assert.Contains("runtime.failed", output.ToString());
        Assert.DoesNotContain(Secret, output.ToString());
    }

    [Theory]
    [InlineData("doctor", 0)]
    [InlineData("verify", 1)]
    public async Task DoctorAndVerify_ShouldPreserveReportAndExitOutcome(string command, int expectedExit)
    {
        using var report = new StringWriter();
        using var diagnostic = new StringWriter();
        using var services = new ServiceCollection().BuildServiceProvider();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Databases:0:ConnId"] = "Main", ["Databases:0:Enabled"] = "false",
            ["Databases:0:ConnectionString"] = Secret, ["Databases:0:DbType"] = "2"
        }).Build();
        var previous = Console.Out;
        var previousConfig = Radish.Common.AppSettingsTool.Configuration;
        try
        {
            Console.SetOut(report);
            Radish.Common.AppSettingsTool.Configuration = configuration;
            var result = await RuntimeProcess.RunAsync("dbmigrate", () => DbMigrateRunner.RunAsync(
                services, configuration, "Production", [command]), diagnostic);
            Assert.Equal(expectedExit, result);
        }
        finally
        {
            Console.SetOut(previous);
            Radish.Common.AppSettingsTool.Configuration = previousConfig;
        }
        Assert.Contains("[Doctor] 结论", report.ToString());
        Assert.DoesNotContain("eventId", report.ToString());
        Assert.DoesNotContain(Secret, report.ToString() + diagnostic);
        Assert.Equal(expectedExit, Lines(diagnostic).Length);
    }

    private sealed class FailingHostedService : IHostedService
    {
        public Task StartAsync(System.Threading.CancellationToken token) => throw new InvalidOperationException(Secret);
        public Task StopAsync(System.Threading.CancellationToken token) => Task.CompletedTask;
    }

    [Fact]
    public void LegacySqlFilter_ShouldKeepSlowQueriesWhenSelectDiagnosticsAreDisabled()
    {
        var sink = new EventSink();
        using var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        var sql = logger.ForContext("LogSource", "AopSql");
        sql.ForContext("operation", "select").Information("diagnostic");
        sql.ForContext("operation", "insert").Information("diagnostic");
        sql.ForContext("operation", "select").Warning("slow");
        sql.ForContext("operation", "connect").Warning("slow");
        var selected = sink.Events.FilterSqlLog(includeSelectQueries: false).ToArray();
        Assert.Equal(3, selected.Length);
        Assert.Equal(2, selected.Count(value => value.Level == Serilog.Events.LogEventLevel.Warning));
    }

    [Theory]
    [InlineData(409, 0)]
    [InlineData(503, 1)]
    public async Task BusinessError_ShouldPreserveResponseAndOnlyLogServerFailures(int status, int events)
    {
        using var output = new StringWriter();
        using var logger = CreateLogger(output, true);
        using var factory = LoggerFactory.Create(builder => builder.AddSerilog(logger));
        var handler = new ApiExceptionHandler(factory.CreateLogger<ApiExceptionHandler>());
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();
        Assert.True(await handler.TryHandleAsync(context,
            new Radish.Common.Exceptions.BusinessException("public business message", status), default));
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal(events, Lines(output).Length);
    }

    private sealed class EventSink : Serilog.Core.ILogEventSink
    {
        public List<Serilog.Events.LogEvent> Events { get; } = [];
        public void Emit(Serilog.Events.LogEvent value) => Events.Add(value);
    }

    private static Serilog.Core.Logger CreateLogger(TextWriter output, bool candidate, string mode = "Production", bool diagnostics = false)
    {
        var configuration = new LoggerConfiguration();
        if (candidate)
        {
            var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RadishLogging:Mode"] = mode, ["RadishLogging:Diagnostics"] = diagnostics.ToString()
            }).Build();
            RuntimeLoggingConfiguration.Configure(configuration, settings, mode, "api", output, output);
        }
        else configuration.MinimumLevel.Verbose().Enrich.FromLogContext()
            .Filter.ByExcluding(RuntimeProcess.OwnsStartupFailure).WriteTo.Sink(new LegacySink(output));
        return configuration.CreateLogger();
    }

    private static string[] Lines(StringWriter output) => output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private sealed class LegacySink(TextWriter output) : Serilog.Core.ILogEventSink
    {
        private readonly Serilog.Formatting.Display.MessageTemplateTextFormatter _formatter =
            new("{Level} {Message:lj} {Properties:j} {Exception}{NewLine}");
        public void Emit(Serilog.Events.LogEvent value) => _formatter.Format(value, output);
    }

    public interface ITransactionalService
    {
        void Sync();
        Task TaskAsync();
        Task<int> ResultAsync();
    }

    private sealed class TransactionalService(Exception exception) : ITransactionalService
    {
        [UseTran] public void Sync() => throw exception;
        [UseTran] public Task TaskAsync() => Task.FromException(exception);
        [UseTran] public Task<int> ResultAsync() => Task.FromException<int>(exception);
    }
}
