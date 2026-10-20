using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Radish.Extension.Log;
using Radish.Model;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class HostProgramLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void Producers_ShouldKeepControlledFieldsAndDropForeignScopes(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, diagnostics: false);
        using var scope = capture.SensitiveScope();
        using var code = LogContext.PushProperty("EventCode", "runtime.failed");
        using var source = LogContext.PushProperty("SourceContext", HttpBoundaryCapture.Secret);
        using var failure = LogContext.PushProperty("failureKind", "io");
        using var count = LogContext.PushProperty("createdCount", 999);
        using var diagnostic = LogContext.PushProperty("Diagnostic", true);
        using var category = LogContext.PushProperty("SourceCategory", "database");
        using var activity = new Activity("program-test").SetIdFormat(ActivityIdFormat.W3C).Start();
        HostProgramLog.RecurringJobRegistered(capture.Logger, "outbox-dispatch");
        Assert.Equal("job.recurring_registration_returned", Assert.Single(capture.Events).Code);
        Assert.Equal("outbox-dispatch", Properties(capture)["jobKind"]);
        Assert.False(Properties(capture).ContainsKey("createdCount"));
        Assert.False(Properties(capture).ContainsKey("failureKind"));
        capture.AssertSafe();
        capture.Clear();
        foreach (var reason in Enum.GetValues<WikiBuiltInSyncSkipReason>().Append((WikiBuiltInSyncSkipReason)999))
        {
            HostProgramLog.BuiltInSyncSkipped(capture.Logger, reason);
            Assert.Equal("document.builtin_sync_skipped", Assert.Single(capture.Events).Code);
            Assert.Single(Properties(capture), p => p.Key == "builtInSyncSkipReason");
            Assert.False(Properties(capture).ContainsKey("createdCount"));
            capture.AssertSafe();
            capture.Clear();
        }
        HostProgramLog.BuiltInSyncCompleted(capture.Logger, new WikiBuiltInSyncSummary
        {
            MarkdownFileCount = 1, DescriptorCount = 2, GeneratedNodeCount = 3, SyncedCount = 4,
            CreatedCount = 5, UpdatedCount = 6, RestoredCount = 7, ParentAdjustedCount = 8, SoftDeletedCount = 9, SkippedCount = 10
        });
        Assert.Equal("document.builtin_sync_completed", Assert.Single(capture.Events).Code);
        Assert.Equal("5", Properties(capture)["createdCount"]);
        Assert.Equal("10", Properties(capture)["skippedCount"]);
        Assert.False(Properties(capture).ContainsKey("failureKind"));
        Assert.False(capture.Events[0].Diagnostic);
        capture.AssertSafe();
        capture.Clear();
        HostProgramLog.BuiltInSyncFailed(capture.Logger, new StartupUnreadableException());
        Assert.Equal("document.builtin_sync_failed", Assert.Single(capture.Events).Code);
        Assert.Equal("other", capture.Events[0].FailureKind);
        Assert.Equal("Error", capture.Events[0].Level);
        Assert.False(Properties(capture).ContainsKey("createdCount"));
        if (candidate)
        {
            using var json = JsonDocument.Parse(capture.Output.ToString());
            Assert.Equal(activity.TraceId.ToHexString(), json.RootElement.GetProperty("traceId").GetString());
        }
        else Assert.Equal(activity.TraceId, Assert.Single(capture.Sink.Events).TraceId);
        capture.AssertSafe();
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void Gateway_ShouldHaveOneStopOwnerBeforeHostDisposal(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway", diagnostics: false);
        using var scope = capture.SensitiveScope();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RadishLogging:Enabled"] = candidate.ToString(), ["RadishLogging:Mode"] = environment,
            ["RadishLogging:Diagnostics"] = "false"
        }).Build();
        using var session = new RuntimeLoggingSession(configuration, environment, "gateway", capture.Output, capture.Emergency);
        using var lifetime = new StartupTestLifetime();
        using var host = new HostBuilder().UseSerilog(session.Logger ?? capture.Logger, dispose: !candidate).Build();
        session.AttachGatewayLifetime(lifetime, capture.Logger);
        lifetime.Started.Cancel();
        Assert.Equal(candidate ? new[] { "runtime.started" } : Array.Empty<string>(), capture.Codes);
        capture.Clear();
        lifetime.Stopping.Cancel();
        Assert.Empty(capture.Events);
        lifetime.Stopped.Cancel();
        lifetime.Stopped.Cancel();
        Assert.Equal("runtime.stopped", Assert.Single(capture.Events).Code);
        Assert.False(capture.Events[0].Diagnostic);
        Assert.False(Properties(capture).ContainsKey("failureKind"));
        capture.AssertSafe();
        host.Dispose();
        Assert.Equal(candidate ? 0 : 1, capture.Sink.Disposals);
        Assert.Single(capture.Events); // No post-RunAsync extra event.
        session.Dispose();
        Assert.Single(capture.Events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Gateway_DisposedRegistrationOrMissingStopNotification_ShouldNotEmit(bool candidate)
    {
        using var capture = new HttpBoundaryCapture(false, "Production");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["RadishLogging:Enabled"] = candidate.ToString() }).Build();
        using var session = new RuntimeLoggingSession(configuration, "Production", "gateway", capture.Output, capture.Emergency);
        using var lifetime = new StartupTestLifetime();
        session.AttachGatewayLifetime(lifetime, capture.Logger);
        lifetime.Stopping.Cancel();
        Assert.Empty(capture.Sink.Events);
        Assert.Equal("", capture.Output.ToString());
        session.Dispose();
        lifetime.Stopped.Cancel();
        Assert.Empty(capture.Sink.Events);
        Assert.Equal("", capture.Output.ToString());
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void UnknownEvent_ShouldKeepSeverityWithoutOriginalPayload(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, diagnostics: false);
        foreach (var level in Enum.GetValues<LogEventLevel>())
        {
            capture.Logger.ForContext("SourceContext", HostProgramLog.Source).ForContext("EventCode", HttpBoundaryCapture.Secret)
                .Write(level, new StartupUnreadableException(), "Private {Value}", HttpBoundaryCapture.Secret);
            if (candidate && level < LogEventLevel.Warning) Assert.Empty(capture.Events);
            else
            {
                Assert.Equal("runtime.program_unknown", Assert.Single(capture.Events).Code);
                Assert.Equal(level < LogEventLevel.Warning, capture.Events[0].Diagnostic);
            }
            capture.AssertSafe();
            capture.Clear();
        }
    }

    [Fact]
    public void Sink_ShouldRejectWrongTypesCrossEventFieldsAndAdjacentSources()
    {
        var capture = new HttpBoundaryTestSink();
        var sink = new HostProgramLogSink(capture);
        var parser = new MessageTemplateParser();
        var now = DateTimeOffset.UtcNow;
        var trace = ActivityTraceId.CreateRandom();
        var span = ActivitySpanId.CreateRandom();
        foreach (var code in new[] { "job.recurring_registration_returned", "document.builtin_sync_skipped", "document.builtin_sync_completed", "document.builtin_sync_failed", "runtime.stopped" })
        foreach (var invalid in new object[] { -1, 1L, 1.5, "1", new StartupPoisonValue() })
        {
            sink.Emit(new LogEvent(now, LogEventLevel.Error, new StartupUnreadableException(), parser.Parse("Raw {payload}"),
            [new("SourceContext", new ScalarValue(HostProgramLog.Source)), new("EventCode", new ScalarValue(code)),
                new("jobKind", new ScalarValue(new StartupPoisonValue())), new("builtInSyncSkipReason", new ScalarValue(HttpBoundaryCapture.Secret)),
                new("createdCount", new ScalarValue(invalid)), new("failureKind", new ScalarValue(HttpBoundaryCapture.Secret)),
                new("payload", new ScalarValue(new StartupPoisonValue())), new("traceId", new ScalarValue(HttpBoundaryCapture.Secret))], trace, span));
            var result = capture.Events.Last();
            Assert.Null(result.Exception);
            Assert.Equal(now, result.Timestamp);
            Assert.Equal(LogEventLevel.Error, result.Level);
            Assert.Equal(trace, result.TraceId);
            Assert.Equal(span, result.SpanId);
            Assert.Equal(4, result.Properties.Count);
            Assert.DoesNotContain(HttpBoundaryCapture.Secret, result.RenderMessage());
        }
        var adjacent = new LogEvent(now, LogEventLevel.Information, null, parser.Parse("Unrelated"),
            [new("SourceContext", new ScalarValue(HostProgramLog.Source + ".Other"))]);
        sink.Emit(adjacent);
        Assert.Same(adjacent, capture.Events.Last());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfiguredLegacyOutputs_ShouldRebuildPayload(bool audit)
    {
        using var capture = new HttpBoundaryCapture(false, "Production", audit: audit);
        using var scope = capture.SensitiveScope();
        HostProgramLog.BuiltInSyncFailed(capture.Logger, new StartupUnreadableException());
        HostProgramLog.RecurringJobRegistered(capture.Logger, "outbox-dispatch");
        Assert.Equal(new[] { "document.builtin_sync_failed", "job.recurring_registration_returned" }, capture.Codes);
        capture.AssertSafe();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StopOutputFailure_ShouldKeepWriteConsumptionAuditPropagationAndFinalOwnership(bool audit)
    {
        var failure = new IOException(HttpBoundaryCapture.Secret);
        var configuration = new LoggerConfiguration();
        var sink = new ProgramThrowingSink(failure);
        if (audit) configuration.AuditTo.Sink(new HostProgramLogSink(sink));
        else configuration.WriteTo.Sink(new HostProgramLogSink(sink));
        using var logger = configuration.CreateLogger();
        using var lifetime = new StartupTestLifetime();
        using var registration = HostProgramLog.RegisterGatewayStopped(lifetime, logger);
        using var error = new StringWriter();
        var exit = await RuntimeProcess.RunAsync("gateway", () => { lifetime.Stopped.Cancel(); return Task.CompletedTask; }, error);
        Assert.Equal(audit ? 1 : 0, exit);
        Assert.Equal(1, sink.Attempts);
        if (audit)
        {
            using var json = JsonDocument.Parse(error.ToString());
            Assert.Equal("runtime.failed", json.RootElement.GetProperty("eventCode").GetString());
            Assert.Equal("aggregate", json.RootElement.GetProperty("properties").GetProperty("failureKind").GetString());
        }
        else Assert.Equal("", error.ToString());
        Assert.DoesNotContain(HttpBoundaryCapture.Secret, error.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposalFailureAfterStop_ShouldStillProduceFinalProcessFailure(bool candidate)
    {
        using var capture = new HttpBoundaryCapture(candidate, "Production", "gateway");
        using var error = new StringWriter();
        using var lifetime = new StartupTestLifetime();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["RadishLogging:Enabled"] = candidate.ToString() }).Build();
        var exit = await RuntimeProcess.RunAsync("gateway", () =>
        {
            using var session = new RuntimeLoggingSession(configuration, "Production", "gateway", capture.Output, capture.Emergency);
            using var host = new HostBuilder().UseSerilog(session.Logger ?? capture.Logger, dispose: !candidate)
                .ConfigureServices(services => services.AddSingleton<FailingHostDisposable>()).Build();
            host.Services.GetRequiredService<FailingHostDisposable>();
            session.AttachGatewayLifetime(lifetime, capture.Logger);
            lifetime.Stopped.Cancel();
            return Task.CompletedTask; // Disposing the built host still fails after the stopped notification.
        }, error);
        Assert.Equal(1, exit);
        Assert.Equal("runtime.stopped", Assert.Single(capture.Events).Code);
        using var json = JsonDocument.Parse(error.ToString());
        Assert.Equal("runtime.failed", json.RootElement.GetProperty("eventCode").GetString());
        Assert.DoesNotContain(HttpBoundaryCapture.Secret, error.ToString());
        capture.AssertSafe();
    }

    [Fact]
    public void CandidateOutputFailure_ShouldUseEmergencyChannel()
    {
        using var output = new StartupBrokenWriter();
        using var emergency = new StringWriter();
        var configuration = new LoggerConfiguration();
        RuntimeLoggingConfiguration.Configure(configuration, new ConfigurationBuilder().Build(), "Production", "api", output, emergency);
        using var logger = configuration.CreateLogger();
        HostProgramLog.BuiltInSyncFailed(logger, new StartupUnreadableException());
        using var json = JsonDocument.Parse(emergency.ToString());
        Assert.Equal("pipeline.output_failed", json.RootElement.GetProperty("eventCode").GetString());
        Assert.DoesNotContain(HttpBoundaryCapture.Secret, emergency.ToString());
    }

    internal static Dictionary<string, string?> Properties(HttpBoundaryCapture capture)
    {
        if (!capture.Candidate) return Assert.Single(capture.Sink.Events).Properties.ToDictionary(p => p.Key,
            p => p.Value is ScalarValue scalar ? scalar.Value?.ToString() : null);
        using var json = JsonDocument.Parse(capture.Output.ToString());
        return json.RootElement.GetProperty("properties").EnumerateObject().ToDictionary(p => p.Name,
            p => p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.GetRawText());
    }
}

internal sealed class ProgramThrowingSink(Exception failure) : ILogEventSink
{
    public int Attempts;
    public void Emit(LogEvent value) { Attempts++; throw failure; }
}

internal sealed class FailingHostDisposable : IDisposable
{
    public void Dispose() => throw new IOException(HttpBoundaryCapture.Secret);
}
