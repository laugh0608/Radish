using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Api.Services;
using Radish.Common.OptionTool;
using Radish.Extension.Log;
using Serilog;
using Serilog.Configuration;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;
using HangfireLevel = Hangfire.Logging.LogLevel;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class HangfireOutputLoggingTests
{
    internal const string Secret = "HANGFIRE_OUTPUT_PRIVATE_SENTINEL";
    internal const string AmbientOperation = "01234567-89ab-4cde-8f01-23456789abcd";

    public static IEnumerable<object[]> OutputRoutes()
    {
        foreach (var environment in new[] { "Development", "Production" })
        {
            yield return [false, environment, false];
            yield return [false, environment, true];
            yield return [true, environment, false];
        }
    }

    public static IEnumerable<object[]> RetryRoutes()
    {
        foreach (var route in OutputRoutes())
        foreach (var retry in new[]
        {
            new object[] { 0, 2, false, 60, "job.retrying" },
            [1, 2, false, 300, "job.retrying"], [2, 2, false, 0, "job.failed"],
            [2, 2, true, 0, "job.failed"], [0, 0, false, 0, "job.failed"],
            [0, 2, false, 0, "job.retrying"]
        }) yield return route.Concat(retry).ToArray();
    }

    [Theory]
    [MemberData(nameof(OutputRoutes))]
    public void Provider_ShouldRebuildFinalOutputsAfterScopesAndEnrichers(bool candidate, string environment, bool audit)
    {
        using var capture = new HangfireOutputCapture(candidate, environment, audit, enricher: new HangfirePayloadEnricher());
        using var scope = capture.SensitiveScope();
        var provider = new HangfireRuntimeLogProvider().GetLogger(Secret);
        Func<string> message = () => throw new InvalidOperationException("Message factory must not run");
        Assert.True(provider.Log(HangfireLevel.Error, null));
        Assert.Empty(capture.Codes);
        foreach (var level in Enum.GetValues<HangfireLevel>())
        {
            Assert.True(provider.Log(level, message, new HangfireUnreadableException()));
            var diagnostic = level < HangfireLevel.Warn;
            if (candidate && environment == "Production" && diagnostic)
                Assert.Empty(capture.Codes);
            else
            {
                Assert.Equal(level >= HangfireLevel.Error ? "hangfire.runtime_failed"
                    : level == HangfireLevel.Warn ? "hangfire.runtime_warning" : "hangfire.diagnostic", Assert.Single(capture.Codes));
                Assert.Equal(new Dictionary<string, string?> { ["failureKind"] = "other" }, capture.Properties());
                Assert.Null(capture.Correlation("operationId"));
                Assert.Equal(diagnostic, capture.Diagnostic());
                if (candidate)
                {
                    var value = Assert.Single(capture.JsonEvents());
                    Assert.Equal(level == HangfireLevel.Fatal, value.GetProperty("isFatal").GetBoolean());
                    Assert.Equal(level >= HangfireLevel.Error ? "Error" : level == HangfireLevel.Warn ? "Warning" : "Info",
                        value.GetProperty("level").GetString());
                }
                else
                {
                    var value = Assert.Single(capture.Sink.Events);
                    Assert.Equal(HangfireLogSink.ProviderSource, ((ScalarValue)value.Properties["SourceContext"]).Value);
                    Assert.Equal(level == HangfireLevel.Fatal ? LogEventLevel.Fatal : level == HangfireLevel.Error ? LogEventLevel.Error
                        : level == HangfireLevel.Warn ? LogEventLevel.Warning : LogEventLevel.Debug, value.Level);
                }
                capture.AssertSafe();
            }
            capture.Clear();
        }
        var automaticRetry = new HangfireRuntimeLogProvider().GetLogger(typeof(AutomaticRetryAttribute).FullName!);
        Assert.False(automaticRetry.Log(HangfireLevel.Fatal, message, new HangfireUnreadableException()));
        Assert.Empty(capture.Codes);
    }

    [Theory]
    [MemberData(nameof(OutputRoutes))]
    public void ProviderWithoutException_ShouldNotInheritAnAmbientFailure(bool candidate, string environment, bool audit)
    {
        using var capture = new HangfireOutputCapture(candidate, environment, audit, enricher: new HangfirePayloadEnricher());
        using var scope = capture.SensitiveScope();
        Assert.True(new HangfireRuntimeLogProvider().GetLogger(Secret).Log(HangfireLevel.Error, () => Secret));
        Assert.Equal("hangfire.runtime_failed", Assert.Single(capture.Codes));
        Assert.Empty(capture.Properties());
        Assert.Null(capture.Correlation("operationId"));
        capture.AssertSafe();
    }

    [Theory]
    [MemberData(nameof(RetryRoutes))]
    public void StateObserver_ShouldKeepActualRetryDecisionsAndProducerCorrelation(bool candidate, string environment, bool audit,
        int attempt, int limit, bool delete, int delay, string eventCode)
    {
        using var capture = new HangfireOutputCapture(candidate, environment, audit, enricher: new HangfirePayloadEnricher());
        using var scope = capture.SensitiveScope();
        var count = attempt.ToString();
        var connection = new Mock<IStorageConnection>();
        connection.Setup(c => c.GetJobParameter(Secret, "RetryCount")).Returns(() => count);
        connection.Setup(c => c.SetJobParameter(Secret, "RetryCount", It.IsAny<string>()))
            .Callback<string, string, string>((_, _, value) => count = value);
        var transaction = new Mock<IWriteOnlyTransaction>();
        var failure = new InvalidOperationException(Secret);
        var failed = new FailedState(failure);
        var context = StateContext(failed, connection.Object, transaction.Object);
        var retry = new AutomaticRetryAttribute
        {
            Attempts = limit, DelaysInSeconds = delay == 0 ? [0] : [60, 300], LogEvents = false,
            OnAttemptsExceeded = delete ? AttemptsExceededAction.Delete : AttemptsExceededAction.Fail
        };
        var before = DateTime.UtcNow;
        retry.OnStateElection(context);
        var elected = context.CandidateState;
        var countAfterElection = count;
        var observer = new HangfireRuntimeStateFilter();
        Assert.True(observer.Order > retry.Order);
        observer.OnStateElection(context);
        Assert.Same(elected, context.CandidateState);
        Assert.Same(failure, failed.Exception);
        Assert.Equal(countAfterElection, count);
        transaction.Verify(t => t.Commit(), Times.Never);
        if (eventCode == "job.retrying")
        {
            Assert.Equal((attempt + 1).ToString(), count);
            if (delay == 0) Assert.IsType<EnqueuedState>(elected);
            else Assert.InRange(Assert.IsType<ScheduledState>(elected).EnqueueAt, before.AddSeconds(delay), DateTime.UtcNow.AddSeconds(delay));
        }
        else if (delete) Assert.IsType<DeletedState>(elected);
        else Assert.Same(failed, elected);
        Assert.Equal(eventCode, Assert.Single(capture.Codes));
        Assert.Equal(new Dictionary<string, string?>
        {
            ["failureKind"] = "invalid-operation", ["outcome"] = eventCode == "job.retrying" ? "retrying" : "failed"
        }, capture.Properties());
        var expected = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("radish:hangfire:" + Secret)).AsSpan(0, 16)).ToString("D");
        Assert.Equal(expected, capture.Correlation("operationId"));
        Assert.NotEqual(AmbientOperation, expected);
        capture.AssertSafe();
    }

    [Theory]
    [MemberData(nameof(OutputRoutes))]
    public void NormalStates_ShouldRemainQuiet(bool candidate, string environment, bool audit)
    {
        using var capture = new HangfireOutputCapture(candidate, environment, audit);
        foreach (var state in new IState[] { new ScheduledState(TimeSpan.FromMinutes(1)), new EnqueuedState(), new SucceededState(null, 0, 0) })
        {
            var context = StateContext(state);
            new HangfireRuntimeStateFilter().OnStateElection(context);
            Assert.Same(state, context.CandidateState);
        }
        Assert.Empty(capture.Codes);
    }

    [Theory]
    [MemberData(nameof(OutputRoutes))]
    public void UnknownEvents_ShouldDropAllPayloadAndKeepSeverity(bool candidate, string environment, bool audit)
    {
        using var capture = new HangfireOutputCapture(candidate, environment, audit);
        foreach (var source in new[] { HangfireLogSink.ProviderSource, HangfireLogSink.StateSource })
        foreach (var level in Enum.GetValues<LogEventLevel>())
        {
            capture.Logger.Write(RawEvent(source, Secret, level));
            if (candidate && environment == "Production" && level < LogEventLevel.Warning)
                Assert.Empty(capture.Codes);
            else
            {
                Assert.Equal(source == HangfireLogSink.ProviderSource ? "hangfire.runtime_unknown" : "job.state_unknown",
                    Assert.Single(capture.Codes));
                Assert.Empty(capture.Properties());
                Assert.Null(capture.Correlation("operationId"));
                Assert.Equal(level < LogEventLevel.Warning, capture.Diagnostic());
                if (!candidate) Assert.Equal(level, Assert.Single(capture.Sink.Events).Level);
                else Assert.Equal(level == LogEventLevel.Fatal, Assert.Single(capture.JsonEvents()).GetProperty("isFatal").GetBoolean());
                capture.AssertSafe();
            }
            capture.Clear();
        }
    }

    [Theory]
    [InlineData(HangfireLogSink.ProviderSource, "job.failed", "hangfire.runtime_unknown")]
    [InlineData(HangfireLogSink.StateSource, "hangfire.runtime_failed", "job.state_unknown")]
    [InlineData(HangfireLogSink.ProviderSource, null, "hangfire.runtime_unknown")]
    [InlineData(HangfireLogSink.StateSource, null, "job.state_unknown")]
    public void CrossSourceAndMissingCodes_ShouldUseOwnUnknownEvent(string source, string? code, string expected)
    {
        var sink = new HangfireCaptureSink();
        new HangfireLogSink(sink).Emit(RawEvent(source, code));
        var value = Assert.Single(sink.Events);
        Assert.Equal(expected, ((ScalarValue)value.Properties["EventCode"]).Value);
        Assert.Equal(4, value.Properties.Count);
        Assert.Null(value.Exception);
        Assert.DoesNotContain(Secret, value.RenderMessage());
    }

    [Fact]
    public void Sink_ShouldRejectWrongTypesAndCrossEventFields()
    {
        var target = new HangfireCaptureSink();
        var sink = new HangfireLogSink(target);
        foreach (var invalid in new LogEventPropertyValue[]
        {
            new ScalarValue(null), new ScalarValue(42), new ScalarValue(true), new ScalarValue(Secret),
            new ScalarValue(new HangfireUnreadableScalar()), new SequenceValue([new ScalarValue(Secret)])
        })
        {
            var value = RawEvent(HangfireLogSink.StateSource, "job.retrying");
            foreach (var key in new[] { "failureKind", "outcome", "operationId" })
                value.AddOrUpdateProperty(new(key, invalid));
            sink.Emit(value);
            Assert.Equal(4, target.Events.Last().Properties.Count);
        }
        foreach (var operation in new[] { AmbientOperation.ToUpperInvariant(), "{" + AmbientOperation + "}", Secret })
        {
            var value = RawEvent(HangfireLogSink.StateSource, "job.retrying");
            value.AddOrUpdateProperty(new("operationId", new ScalarValue(operation)));
            value.AddOrUpdateProperty(new("outcome", new ScalarValue("failed")));
            sink.Emit(value);
            Assert.False(target.Events.Last().Properties.ContainsKey("operationId"));
            Assert.False(target.Events.Last().Properties.ContainsKey("outcome"));
        }
        var valid = RawEvent(HangfireLogSink.StateSource, "job.failed");
        valid.AddOrUpdateProperty(new("operationId", new ScalarValue(AmbientOperation)));
        valid.AddOrUpdateProperty(new("outcome", new ScalarValue("failed")));
        sink.Emit(valid);
        Assert.Equal(new[] { "Diagnostic", "EventCode", "SourceCategory", "SourceContext", "failureKind", "operationId", "outcome" },
            target.Events.Last().Properties.Keys.Order(StringComparer.Ordinal));
        var provider = RawEvent(HangfireLogSink.ProviderSource, "hangfire.runtime_failed");
        sink.Emit(provider);
        Assert.Equal(new[] { "Diagnostic", "EventCode", "SourceCategory", "SourceContext", "failureKind" },
            target.Events.Last().Properties.Keys.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Sink_ShouldKeepTimestampAndTypedTraceWithoutStringCorrelations(bool typed)
    {
        using var activity = new Activity("hangfire-output").SetIdFormat(ActivityIdFormat.W3C).Start();
        var trace = ActivityTraceId.CreateRandom();
        var span = ActivitySpanId.CreateRandom();
        var original = RawEvent(HangfireLogSink.ProviderSource, "hangfire.runtime_failed", LogEventLevel.Fatal,
            typed ? trace : default, typed ? span : default);
        original.AddOrUpdateProperty(new("traceId", new ScalarValue("11111111111111111111111111111111")));
        original.AddOrUpdateProperty(new("spanId", new ScalarValue("2222222222222222")));
        var target = new HangfireCaptureSink();
        new HangfireLogSink(target).Emit(original);
        var value = Assert.Single(target.Events);
        Assert.Equal(original.Timestamp, value.Timestamp);
        Assert.Equal(LogEventLevel.Fatal, value.Level);
        Assert.Equal(typed ? trace : activity.TraceId, value.TraceId);
        Assert.Equal(typed ? span : activity.SpanId, value.SpanId);
        Assert.False(value.Properties.ContainsKey("traceId"));
        Assert.False(value.Properties.ContainsKey("spanId"));
    }

    [Fact]
    public void Sink_ShouldLeaveAdjacentAndMalformedSourcesUntouched()
    {
        var target = new HangfireCaptureSink();
        var sink = new HangfireLogSink(target);
        foreach (var source in new[] { "Hangfire.Server.Worker", HangfireLogSink.ProviderSource + ".Other", HangfireLogSink.StateSource.ToLowerInvariant() })
        {
            var value = RawEvent(source, "job.failed");
            sink.Emit(value);
            Assert.Same(value, target.Events.Last());
        }
        var malformed = RawEvent(HangfireLogSink.ProviderSource, "hangfire.runtime_failed");
        malformed.AddOrUpdateProperty(new("SourceContext", new ScalarValue(42)));
        sink.Emit(malformed);
        Assert.Same(malformed, target.Events.Last());
        malformed.RemovePropertyIfPresent("SourceContext");
        sink.Emit(malformed);
        Assert.Same(malformed, target.Events.Last());
    }

    [Theory]
    [InlineData(false, "Warning", HangfireLevel.Info, false)]
    [InlineData(true, "Warning", HangfireLevel.Info, false)]
    [InlineData(false, "Error", HangfireLevel.Warn, false)]
    [InlineData(true, "Error", HangfireLevel.Warn, false)]
    [InlineData(false, "Error", HangfireLevel.Error, true)]
    [InlineData(true, "Error", HangfireLevel.Error, true)]
    public void LegacyMinimumLevel_ShouldKeepProviderQueryAndReturnBehavior(bool audit, string minimum, HangfireLevel level, bool enabled)
    {
        using var capture = new HangfireOutputCapture(false, "Production", audit, minimum: minimum);
        var provider = new HangfireRuntimeLogProvider().GetLogger(Secret);
        Assert.Equal(enabled, provider.Log(level, null));
        Assert.Empty(capture.Codes);
        Assert.Equal(enabled, provider.Log(level, () => throw new InvalidOperationException(Secret)));
        Assert.Equal(enabled ? 1 : 0, capture.Codes.Length);
        capture.AssertSafe();
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void CandidateDiagnosticsDisabled_ShouldSuppressDiagnosticOutputWithoutChangingProviderReturn(string environment)
    {
        using var capture = new HangfireOutputCapture(true, environment, diagnostics: false);
        Assert.True(new HangfireRuntimeLogProvider().GetLogger(Secret).Log(HangfireLevel.Info, () => Secret));
        Assert.Empty(capture.Codes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfiguredLegacyFailures_ShouldKeepWriteConsumptionAndAuditPropagation(bool audit)
    {
        var failure = new IOException(Secret);
        foreach (var state in new[] { false, true })
        {
            using var capture = new HangfireOutputCapture(false, "Production", audit, emitFailure: failure);
            var context = StateContext(new FailedState(new InvalidOperationException(Secret)));
            var elected = context.CandidateState;
            void Emit()
            {
                if (state) new HangfireRuntimeStateFilter().OnStateElection(context);
                else Assert.True(new HangfireRuntimeLogProvider().GetLogger(Secret).Log(HangfireLevel.Error, () => Secret));
            }
            if (audit) Assert.Contains(failure, Assert.Throws<AggregateException>(Emit).Flatten().InnerExceptions);
            else Emit();
            Assert.Same(elected, context.CandidateState);
            Assert.Equal(1, capture.Sink.Attempts);
        }
    }

    [Fact]
    public void CandidateOutputFailure_ShouldUseExistingEmergencyChannel()
    {
        using var broken = new HangfireBrokenWriter();
        using var capture = new HangfireOutputCapture(true, "Production", output: broken);
        Assert.True(new HangfireRuntimeLogProvider().GetLogger(Secret).Log(HangfireLevel.Error, () => Secret));
        using var json = JsonDocument.Parse(capture.Emergency.ToString());
        Assert.Equal("pipeline.output_failed", json.RootElement.GetProperty("eventCode").GetString());
        Assert.DoesNotContain(Secret, capture.Emergency.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfiguredSinkDisposal_ShouldKeepOriginalFailureBehavior(bool audit)
    {
        var failure = new IOException(Secret);
        var baseline = new HangfireCaptureSink { DisposeFailure = failure };
        var configuration = new LoggerConfiguration();
        if (audit) configuration.AuditTo.Sink(baseline);
        else configuration.WriteTo.Sink(baseline);
        var plain = configuration.CreateLogger();
        var expected = Record.Exception(plain.Dispose);
        var capture = new HangfireOutputCapture(false, "Production", audit, disposeFailure: failure);
        var actual = Record.Exception(capture.Dispose);
        Assert.Equal(expected?.GetType(), actual?.GetType());
        Assert.Equal(baseline.Disposals, capture.Sink.Disposals);
        Assert.Equal(1, capture.Sink.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfiguredSinkOwnership_ShouldReleaseSinkOnce(bool audit)
    {
        HangfireCaptureSink sink;
        using (var capture = new HangfireOutputCapture(false, "Production", audit))
        {
            sink = capture.Sink;
            Assert.True(new HangfireRuntimeLogProvider().GetLogger(Secret).Log(HangfireLevel.Error, () => Secret));
            Assert.Equal(0, sink.Disposals);
        }
        Assert.Equal(1, sink.Disposals);
    }

    private static ElectStateContext StateContext(IState state, IStorageConnection? connection = null, IWriteOnlyTransaction? transaction = null) =>
        new(new ApplyStateContext(Mock.Of<JobStorage>(), connection ?? Mock.Of<IStorageConnection>(),
            transaction ?? Mock.Of<IWriteOnlyTransaction>(), new BackgroundJob(Secret,
                Job.FromExpression(() => JobTarget.NoOp()), DateTime.UtcNow), state, ProcessingState.StateName));

    private static class JobTarget
    {
        public static void NoOp() { }
    }

    private static LogEvent RawEvent(string source, string? code, LogEventLevel level = LogEventLevel.Warning,
        ActivityTraceId trace = default, ActivitySpanId span = default) =>
        new(new DateTimeOffset(2026, 11, 4, 13, 14, 15, TimeSpan.FromHours(8)), level, new HangfireUnreadableException(),
            new MessageTemplateParser().Parse(Secret),
            [new("SourceContext", new ScalarValue(source)), new("EventCode", new ScalarValue(code)),
                new("failureKind", new ScalarValue("io")), new("operationId", new ScalarValue(AmbientOperation)),
                new("outcome", new ScalarValue("retrying")), new("jobKind", new ScalarValue("post-lottery")),
                new("payload", new ScalarValue(Secret)), new("count", new ScalarValue(42))], trace, span);
}

public sealed class HangfireCaptureSink : ILogEventSink, IDisposable
{
    public readonly List<LogEvent> Events = new();
    public Exception? EmitFailure;
    public Exception? DisposeFailure;
    public int Attempts;
    public int Disposals;
    public void Emit(LogEvent value) { Attempts++; if (EmitFailure != null) throw EmitFailure; Events.Add(value); }
    public void Dispose() { Disposals++; if (DisposeFailure != null) throw DisposeFailure; }
}

public static class HangfireCaptureExtensions
{
    public static readonly ConcurrentDictionary<string, ILogEventSink> Sinks = new();
    public static LoggerConfiguration HangfireOutputCapture(this LoggerSinkConfiguration config, string key) => config.Sink(Sinks[key]);
    public static LoggerConfiguration HangfireOutputCapture(this LoggerAuditSinkConfiguration config, string key) => config.Sink(Sinks[key]);
}

internal sealed class HangfireOutputCapture : IDisposable
{
    private readonly Serilog.ILogger _previous = Log.Logger;
    private readonly string _key = Guid.NewGuid().ToString("N");
    private readonly bool _candidate;
    private readonly ILoggerFactory _factory;
    public readonly StringWriter Output = new();
    public readonly StringWriter Emergency = new();
    public readonly HangfireCaptureSink Sink = new();
    public readonly Logger Logger;

    public HangfireOutputCapture(bool candidate, string environment, bool audit = false, bool diagnostics = true,
        ILogEventEnricher? enricher = null, string minimum = "Verbose", TextWriter? output = null,
        Exception? emitFailure = null, Exception? disposeFailure = null)
    {
        _candidate = candidate;
        Sink.EmitFailure = emitFailure; Sink.DisposeFailure = disposeFailure;
        HangfireCaptureExtensions.Sinks[_key] = Sink;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RadishLogging:Mode"] = environment, ["RadishLogging:Diagnostics"] = (diagnostics && environment == "Development").ToString(),
            ["Serilog:Using:0"] = "Radish.Api.Tests", [$"Serilog:{(audit ? "AuditTo" : "WriteTo")}:0:Name"] = "HangfireOutputCapture",
            [$"Serilog:{(audit ? "AuditTo" : "WriteTo")}:0:Args:key"] = _key
        }).Build();
        var setup = new LoggerConfiguration();
        if (candidate) RuntimeLoggingConfiguration.Configure(setup, config, environment, "api", output ?? Output, Emergency);
        else
        {
            using var services = new ServiceCollection().BuildServiceProvider();
            SerilogSetup.ConfigureLegacy(setup, config, services, new SerilogOptions
            {
                MinimumLevel = minimum, Console = new() { Enable = false }, File = new() { Enable = false }, Database = new() { Enable = false }
            });
        }
        if (enricher != null) setup.Enrich.With(enricher);
        Logger = setup.CreateLogger();
        _factory = new Serilog.Extensions.Logging.SerilogLoggerFactory(Logger, dispose: false);
        Log.Logger = Logger;
    }

    public JsonElement[] JsonEvents() => Output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line =>
    { using var json = JsonDocument.Parse(line); return json.RootElement.Clone(); }).ToArray();
    public string[] Codes => _candidate ? JsonEvents().Select(e => e.GetProperty("eventCode").GetString()!).ToArray()
        : Sink.Events.Select(e => (string)((ScalarValue)e.Properties["EventCode"]).Value!).ToArray();
    public Dictionary<string, string?> Properties() => _candidate
        ? Assert.Single(JsonEvents()).GetProperty("properties").EnumerateObject().ToDictionary(p => p.Name,
            p => p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.GetRawText())
        : Assert.Single(Sink.Events).Properties.Where(p => p.Key is not ("EventCode" or "SourceContext" or "SourceCategory" or "Diagnostic" or "operationId"))
            .ToDictionary(p => p.Key, p => ((ScalarValue)p.Value).Value?.ToString());
    public string? Correlation(string key) => _candidate
        ? Assert.Single(JsonEvents()).TryGetProperty(key, out var value) ? value.GetString() : null
        : Assert.Single(Sink.Events).Properties.TryGetValue(key, out var property) ? ((ScalarValue)property).Value as string : null;
    public bool Diagnostic() => _candidate ? Assert.Single(JsonEvents()).GetProperty("diagnostic").GetBoolean()
        : ((ScalarValue)Assert.Single(Sink.Events).Properties["Diagnostic"]).Value is true;
    public IDisposable? SensitiveScope() => _factory.CreateLogger("scope").BeginScope(new Dictionary<string, object>
    {
        ["payload"] = HangfireOutputLoggingTests.Secret, ["operationId"] = HangfireOutputLoggingTests.AmbientOperation,
        ["jobKind"] = "post-lottery", ["count"] = 42, ["outcome"] = "succeeded", ["failureKind"] = "io",
        ["SourceContext"] = HangfireOutputLoggingTests.Secret, ["EventCode"] = "runtime.failed", ["Diagnostic"] = true,
        ["SourceCategory"] = "database", ["nested"] = new { body = HangfireOutputLoggingTests.Secret }
    });
    public void Clear() { Sink.Events.Clear(); Output.GetStringBuilder().Clear(); }
    public void AssertSafe()
    {
        Assert.Equal("", Emergency.ToString());
        Assert.DoesNotContain(HangfireOutputLoggingTests.Secret, Output.ToString());
        foreach (var value in Sink.Events)
        {
            Assert.Null(value.Exception);
            Assert.DoesNotContain(HangfireOutputLoggingTests.Secret, value.RenderMessage() + string.Join(" ", value.Properties.Values));
            Assert.Equal("job", ((ScalarValue)value.Properties["SourceCategory"]).Value);
        }
    }
    public void Dispose()
    {
        try { _factory.Dispose(); Logger.Dispose(); }
        finally { Log.Logger = _previous; HangfireCaptureExtensions.Sinks.TryRemove(_key, out _); Output.Dispose(); Emergency.Dispose(); }
    }
}

internal sealed class HangfirePayloadEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent value, ILogEventPropertyFactory factory)
    {
        foreach (var (key, property) in new Dictionary<string, object?>
        {
            ["enriched"] = new { body = HangfireOutputLoggingTests.Secret }, ["failureKind"] = "io",
            ["operationId"] = HangfireOutputLoggingTests.AmbientOperation, ["jobKind"] = "post-lottery", ["count"] = 42
        }) value.AddPropertyIfAbsent(factory.CreateProperty(key, property, destructureObjects: true));
    }
}

internal sealed class HangfireUnreadableException : Exception
{
    public override string Message => throw new InvalidOperationException("Unexpected exception message read");
    public override string? StackTrace => throw new InvalidOperationException("Unexpected exception stack read");
    public override string ToString() => throw new InvalidOperationException("Unexpected exception rendering");
}

internal sealed class HangfireUnreadableScalar
{
    public override string ToString() => throw new InvalidOperationException("Unexpected scalar rendering");
}

internal sealed class HangfireBrokenWriter : StringWriter
{
    public override void WriteLine(string? value) => throw new IOException(HangfireOutputLoggingTests.Secret);
}
