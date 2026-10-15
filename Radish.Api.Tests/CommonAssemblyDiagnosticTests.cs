using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Radish.Common.LogTool;
using Radish.DbMigrate;
using Radish.Extension.Log;
using Serilog;
using Serilog.Context;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class CommonAssemblyDiagnosticTests
{
    internal const string Secret = "COMMON_ASSEMBLY_PRIVATE_SENTINEL";

    [Theory]
    [MemberData(nameof(SerilogSelfLogTests.Modes), MemberType = typeof(SerilogSelfLogTests))]
    public async Task ProcessScope_ShouldCoverBeforeDuringAndAfterSessionWithoutMainLoggerOrConfiguration(bool candidate, string environment)
    {
        using var error = new StringWriter();
        using var main = new StringWriter();
        using var scope = LogContext.PushProperty("service", Secret);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RadishLogging:Enabled"] = candidate.ToString(), ["RadishLogging:Mode"] = environment,
            ["RadishLogging:Diagnostics"] = "false", ["RadishLogging:MinimumLevel"] = "Error"
        }).Build();
        var result = await RuntimeProcess.RunAsync("auth", () =>
        {
            AssemblyDiagnosticReporter.AssemblyLoadFailed(new UnreadableAssemblyException());
            using (var session = new RuntimeLoggingSession(configuration, environment, "auth", main, error))
            {
                Assert.Equal(candidate, session.Logger != null);
                AssemblyDiagnosticReporter.AssemblyTypesFailed(new UnreadableAssemblyException());
            }
            AssemblyDiagnosticReporter.AssemblyLoadFailed(new IOException(Secret));
            return Task.CompletedTask;
        }, error);
        Assert.Equal(0, result);
        Assert.Equal("", main.ToString());
        var events = Events(error);
        Assert.Equal(3, events.Length);
        AssertSummary(events[0], "auth", "runtime.assembly_load_failed", "other");
        AssertSummary(events[1], "auth", "runtime.assembly_types_failed", "other");
        AssertSummary(events[2], "auth", "runtime.assembly_load_failed", "io");
    }

    [Fact]
    public async Task ProcessScope_ShouldReportBeforeConfigurationFailureWithoutChangingTheFinalBoundary()
    {
        using var error = new StringWriter();
        var configuration = new Mock<IConfiguration>();
        configuration.Setup(c => c.GetSection(It.IsAny<string>()))
            .Callback(() => AssemblyDiagnosticReporter.AssemblyLoadFailed(new UnreadableAssemblyException()))
            .Throws(new IOException(Secret));
        var result = await RuntimeProcess.RunAsync("api", () =>
        {
            using var session = new RuntimeLoggingSession(configuration.Object, "Production", "api", emergency: error);
            return Task.CompletedTask;
        }, error);
        Assert.Equal(1, result);
        var events = Events(error);
        Assert.Equal(2, events.Length);
        AssertSummary(events[0], "api", "runtime.assembly_load_failed", "other");
        Assert.Equal("runtime.failed", events[1].GetProperty("eventCode").GetString());
        Assert.True(events[1].GetProperty("isFatal").GetBoolean());
        Assert.DoesNotContain(Secret, error.ToString());
    }

    [Fact]
    public void Scopes_ShouldRestoreNestedOwnersAndNeverDisposeWritersOrInheritUnknownService()
    {
        using var unbound = new TrackingAssemblyWriter();
        using var outerWriter = new TrackingAssemblyWriter();
        using var innerWriter = new TrackingAssemblyWriter();
        var previous = Console.Error;
        try
        {
            Console.SetError(unbound);
            AssemblyDiagnosticReporter.AssemblyLoadFailed(new IOException(Secret));
            var outer = AssemblyDiagnosticReporter.BeginScope("api", outerWriter);
            var inner = AssemblyDiagnosticReporter.BeginScope("gateway", innerWriter);
            outer.Dispose(); // Disposing an older owner must not clear the live inner owner.
            AssemblyDiagnosticReporter.AssemblyTypesFailed(new IOException(Secret));
            inner.Dispose();
            inner.Dispose();
            AssemblyDiagnosticReporter.AssemblyLoadFailed(new IOException(Secret));
            using (AssemblyDiagnosticReporter.BeginScope(Secret, innerWriter))
                AssemblyDiagnosticReporter.AssemblyLoadFailed(new IOException(Secret));
            using (AssemblyDiagnosticReporter.BeginScope("api", outerWriter))
            {
                using (AssemblyDiagnosticReporter.BeginScope("dbmigrate", innerWriter))
                    AssemblyDiagnosticReporter.AssemblyLoadFailed(new IOException(Secret));
                AssemblyDiagnosticReporter.AssemblyLoadFailed(new IOException(Secret));
            }
            Assert.Equal(new[] { "runtime", "runtime" }, Events(unbound).Select(e => e.GetProperty("service").GetString()));
            Assert.Equal(new[] { "gateway", "runtime", "dbmigrate" }, Events(innerWriter).Select(e => e.GetProperty("service").GetString()));
            Assert.Equal("api", Assert.Single(Events(outerWriter)).GetProperty("service").GetString());
            Assert.Equal(0, unbound.Disposals + outerWriter.Disposals + innerWriter.Disposals);
        }
        finally { Console.SetError(previous); }
    }

    [Fact]
    public async Task Scopes_ShouldIsolateConcurrentHostsAndIgnoreDisposedInheritedContext()
    {
        using var shared = new StringWriter();
        var services = new[] { "api", "auth", "gateway", "dbmigrate" };
        await Task.WhenAll(services.Select(service => Task.Run(async () =>
        {
            await RuntimeProcess.RunAsync(service, async () =>
            {
                await Task.Yield();
                for (var i = 0; i < 20; i++) AssemblyDiagnosticReporter.AssemblyLoadFailed(new IOException(Secret));
            }, shared);
        })));
        var events = Events(shared);
        Assert.Equal(80, events.Length);
        Assert.Equal(80, events.Select(e => e.GetProperty("eventId").GetString()).Distinct().Count());
        foreach (var service in services) Assert.Equal(20, events.Count(e => e.GetProperty("service").GetString() == service));
        using var unbound = new StringWriter();
        var previous = Console.Error;
        try
        {
            Console.SetError(unbound);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task inherited;
            using (AssemblyDiagnosticReporter.BeginScope("api", shared))
                inherited = Task.Run(async () =>
                {
                    await release.Task;
                    AssemblyDiagnosticReporter.AssemblyTypesFailed(new IOException(Secret));
                }, TestContext.Current.CancellationToken);
            release.SetResult();
            await inherited;
            AssertSummary(Assert.Single(Events(unbound)), "runtime", "runtime.assembly_types_failed", "io");
            Assert.Equal(80, Events(shared).Length);
        }
        finally { Console.SetError(previous); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OutputFailure_ShouldRemainBoundedAndRecoverWithoutReplacingConsumedFailure(bool flush)
    {
        using var error = new FailingAssemblyWriter { FailWrites = !flush, FailFlushes = flush };
        using var scope = AssemblyDiagnosticReporter.BeginScope("api", error);
        for (var i = 0; i < 3; i++) AssemblyDiagnosticReporter.AssemblyLoadFailed(new UnreadableAssemblyException());
        Assert.Equal(4, error.WriteAttempts); // Three normal attempts plus one rate-limited emergency attempt.
        error.FailWrites = error.FailFlushes = false;
        error.GetStringBuilder().Clear();
        AssemblyDiagnosticReporter.AssemblyTypesFailed(new IOException(Secret));
        AssertSummary(Assert.Single(Events(error)), "api", "runtime.assembly_types_failed", "io");
    }

    [Fact]
    public void OutputFailure_ShouldEmitOnlyFixedEmergencySummaryWhenTheSecondWriteWorks()
    {
        using var error = new FailingAssemblyWriter { FailFirstWrite = true };
        using var scope = AssemblyDiagnosticReporter.BeginScope("api", error);
        AssemblyDiagnosticReporter.AssemblyLoadFailed(new IOException(Secret));
        var entry = Assert.Single(Events(error));
        Assert.Equal("pipeline.output_failed", entry.GetProperty("eventCode").GetString());
        Assert.Equal(1, entry.GetProperty("properties").GetProperty("count").GetInt32());
        Assert.DoesNotContain(Secret, error.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReentrantWriter_ShouldNotRecurseOrDeadlock(bool asyncCallback)
    {
        using var error = new ReentrantAssemblyWriter(asyncCallback);
        using var scope = AssemblyDiagnosticReporter.BeginScope("auth", error);
        AssemblyDiagnosticReporter.AssemblyLoadFailed(new IOException(Secret));
        AssemblyDiagnosticReporter.AssemblyTypesFailed(new IOException(Secret));
        Assert.Equal(2, error.Callbacks);
        Assert.Equal(2, Events(error).Length);
    }

    [Fact]
    public async Task ProcessBoundary_ShouldPreserveFatalExitAndToolAbortAndRestoreParentScope()
    {
        using var error = new StringWriter();
        using var outer = AssemblyDiagnosticReporter.BeginScope("gateway", error);
        var result = await RuntimeProcess.RunAsync("api", () =>
        {
            AssemblyDiagnosticReporter.AssemblyLoadFailed(new IOException(Secret));
            throw new DirectoryNotFoundException(Secret);
        }, error);
        Assert.Equal(1, result);
        Assert.Equal(new[] { "runtime.assembly_load_failed", "runtime.failed" }, Events(error).Select(e => e.GetProperty("eventCode").GetString()));
        Assert.True(Events(error)[1].GetProperty("isFatal").GetBoolean());
        error.GetStringBuilder().Clear();
        var abort = new HostAbortedException();
        Assert.Same(abort, await Assert.ThrowsAsync<HostAbortedException>(() => RuntimeProcess.RunAsync("api", () => throw abort, error)));
        Assert.Empty(Events(error));
        AssemblyDiagnosticReporter.AssemblyTypesFailed(new IOException(Secret));
        AssertSummary(Assert.Single(Events(error)), "gateway", "runtime.assembly_types_failed", "io");
    }

    [Fact]
    public async Task DbMigrateHelp_ShouldKeepCommandResultsOnStdoutAndAssemblyDiagnosticsOnStderr()
    {
        using var error = new StringWriter();
        using var results = new StringWriter();
        using var logger = DbMigrateCommand.CreateLogger(new ConfigurationBuilder().Build(), "Production", error);
        using var services = new ServiceCollection().BuildServiceProvider();
        var previous = Console.Out;
        try
        {
            Console.SetOut(results);
            var exit = await RuntimeProcess.RunAsync("dbmigrate", () => DbMigrateCommand.RunAsync(["help"], logger, () =>
            {
                AssemblyDiagnosticReporter.AssemblyLoadFailed(new IOException(Secret));
                return DbMigrateRunner.RunAsync(services, new ConfigurationBuilder().Build(), "Production", ["help"]);
            }), error);
            Assert.Equal(0, exit);
        }
        finally { Console.SetOut(previous); }
        Assert.Contains("Radish.DbMigrate 用法:", results.ToString());
        Assert.DoesNotContain("eventId", results.ToString());
        Assert.Equal(new[] { "dbmigrate.started", "runtime.assembly_load_failed", "dbmigrate.completed" }, Events(error).Select(e => e.GetProperty("eventCode").GetString()));
        Assert.DoesNotContain(Secret, results.ToString() + error.ToString());
    }

    internal static JsonElement[] Events(StringWriter writer) => writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => { using var json = JsonDocument.Parse(line); return json.RootElement.Clone(); }).ToArray();

    internal static void AssertSummary(JsonElement entry, string service, string code, string kind)
    {
        Assert.Equal(service, entry.GetProperty("service").GetString());
        Assert.Equal(code, entry.GetProperty("eventCode").GetString());
        Assert.Equal("Error", entry.GetProperty("level").GetString());
        Assert.False(entry.GetProperty("isFatal").GetBoolean());
        Assert.False(entry.GetProperty("diagnostic").GetBoolean());
        Assert.Equal("Production", entry.GetProperty("mode").GetString());
        Assert.Equal("infrastructure", entry.GetProperty("sourceCategory").GetString());
        Assert.Equal("bootstrap", entry.GetProperty("deploymentId").GetString());
        Assert.Equal("unversioned", entry.GetProperty("release").GetString());
        Assert.Equal("process-" + Environment.ProcessId, entry.GetProperty("instanceId").GetString());
        Assert.Equal(kind, entry.GetProperty("properties").GetProperty("failureKind").GetString());
        Assert.Single(entry.GetProperty("properties").EnumerateObject());
        Assert.DoesNotContain(Secret, entry.GetRawText());
        Assert.True(Encoding.UTF8.GetByteCount(entry.GetRawText()) <= RuntimeLogPolicy.MaxEventBytes);
    }
}

internal sealed class UnreadableAssemblyException : Exception
{
    public override string Message => throw new InvalidOperationException("Exception message read");
    public override string? StackTrace => throw new InvalidOperationException("Exception stack read");
    public override IDictionary Data => throw new InvalidOperationException("Exception data read");
    public override string ToString() => throw new InvalidOperationException("Exception formatted");
}

internal sealed class TrackingAssemblyWriter : StringWriter
{
    public int Disposals;
    protected override void Dispose(bool disposing) { Disposals++; base.Dispose(disposing); }
}

internal sealed class FailingAssemblyWriter : StringWriter
{
    public bool FailWrites, FailFlushes, FailFirstWrite;
    public int WriteAttempts;
    public override void WriteLine(string? value)
    {
        WriteAttempts++;
        if (FailWrites || (FailFirstWrite && WriteAttempts == 1)) throw new IOException(CommonAssemblyDiagnosticTests.Secret);
        base.WriteLine(value);
    }
    public override void Flush()
    {
        if (FailFlushes) throw new IOException(CommonAssemblyDiagnosticTests.Secret);
        base.Flush();
    }
}

internal sealed class ReentrantAssemblyWriter(bool asyncCallback) : StringWriter
{
    public int Callbacks;
    public override void WriteLine(string? value)
    {
        Callbacks++;
        if (asyncCallback)
        {
            var callback = Task.Run(() => AssemblyDiagnosticReporter.AssemblyTypesFailed(new IOException(CommonAssemblyDiagnosticTests.Secret)));
            if (!callback.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException("Reentrant report blocked");
        }
        else AssemblyDiagnosticReporter.AssemblyTypesFailed(new IOException(CommonAssemblyDiagnosticTests.Secret));
        base.WriteLine(value);
    }
}
