using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Radish.Common.LogTool;
using Radish.Extension.Log;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class HealthFrameworkLoggingTests
{
    [Fact]
    public void FrozenIdentities_ShouldMatchInstalledFrameworkAndSevenCodes()
    {
        var methods = HealthFrameworkEvents.LogType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.GetCustomAttribute<LoggerMessageAttribute>() != null).ToArray();
        Assert.Equal(7, methods.Length);
        foreach (var row in HealthFrameworkEvents.All.Where(e => e.Id != 105))
        {
            var metadata = HealthFrameworkEvents.Generator(row).GetCustomAttribute<LoggerMessageAttribute>()!;
            Assert.Equal(row.Id, metadata.EventId);
            Assert.Equal(row.Name, metadata.EventName);
            Assert.Equal(row.Level, metadata.Level);
        }
        var codes = HealthFrameworkEvents.All.Select(e => e.Code).Append("health.unknown").Distinct().ToArray();
        Assert.Equal(7, codes.Length);
        Assert.All(codes, c => Assert.True(RuntimeLogPolicy.IsRegisteredEventCode(c)));
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void RealGenerators_ShouldReachBothModesWithoutFormattingState(bool candidate, string environment)
    {
        foreach (var diagnostics in new[] { true, false })
        {
            using var capture = new HttpBoundaryCapture(candidate, environment, diagnostics: diagnostics);
            using var fixture = new HealthFixture(capture);
            using var scope = capture.SensitiveScope();
            foreach (var row in HealthFrameworkEvents.All)
            {
                HealthFrameworkEvents.Generate(row, fixture.Logger);
                if (!candidate || row.Level >= LogLevel.Warning || (environment == "Development" && diagnostics))
                {
                    var entry = Assert.Single(capture.Events);
                    Assert.Equal(row.Code, entry.Code);
                    Assert.Equal(row.Level < LogLevel.Warning, entry.Diagnostic);
                    Assert.Equal(candidate && row.Level < LogLevel.Warning ? "Info" : row.Level.ToString(), entry.Level);
                    Assert.Equal(row.Level >= LogLevel.Warning ? "other" : null, entry.FailureKind);
                }
                else Assert.Empty(capture.Events);
                HealthFrameworkEvents.AssertSafe(capture);
                capture.Clear();
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfiguredWriteAndAudit_ShouldCoverSevenCodes(bool audit)
    {
        using var capture = new HttpBoundaryCapture(false, "Production", audit: audit);
        using var fixture = new HealthFixture(capture);
        using var scope = capture.SensitiveScope();
        foreach (var row in HealthFrameworkEvents.All) HealthFrameworkEvents.Generate(row, fixture.Logger);
        fixture.Logger.LogWarning(new EventId(909, "future"), "{payload}", HttpBoundaryCapture.Secret);
        Assert.Equal(HealthFrameworkEvents.All.Select(e => e.Code).Append("health.unknown"), capture.Codes);
        HealthFrameworkEvents.AssertSafe(capture);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void DataReservedKeysAndPoisonFormatter_ShouldNeverReachBridge(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var fixture = new HealthFixture(capture);
        using var scope = fixture.Logger.BeginScope(new Dictionary<string, object>
        {
            ["SourceContext"] = HttpBoundaryCapture.Secret, ["EventCode"] = "health.check_failed", ["EventId"] = 104,
            ["failureKind"] = "io", ["payload"] = HttpBoundaryCapture.Secret
        });
        var value = new HealthPoisonValue();
        HealthFrameworkEvents.Data(fixture.Logger, new Dictionary<string, object>
        {
            ["SourceContext"] = HttpBoundaryCapture.Secret, ["EventId"] = 104, ["EventCode"] = "health.check_failed",
            ["HealthCheckName"] = HttpBoundaryCapture.Secret, ["payload"] = value
        });
        Assert.Equal(0, value.FormatCalls);
        Assert.Equal(candidate && environment == "Production" ? Array.Empty<string>() : new[] { "health.check_data" }, capture.Codes);
        Assert.All(capture.Events, e => Assert.Null(e.FailureKind));
        capture.Clear();
        foreach (var identity in new[] { new EventId(105, "HealthCheckData"), new EventId(105, "wrong"), new EventId(999, HttpBoundaryCapture.Secret), default })
        {
            fixture.Logger.Log(LogLevel.Error, identity, new HealthPoisonState(), new HealthUnreadableException(),
                (_, _) => throw new InvalidOperationException("Original formatter called"));
            var entry = Assert.Single(capture.Events);
            Assert.Equal(identity.Name == "HealthCheckData" ? "health.check_data" : "health.unknown", entry.Code);
            Assert.Equal("other", entry.FailureKind);
            Assert.Equal("Error", entry.Level);
            HealthFrameworkEvents.AssertSafe(capture);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void UnknownSeverity_ShouldRemainVisibleAccordingToOriginalLevel(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, diagnostics: false);
        using var fixture = new HealthFixture(capture);
        foreach (var level in new[] { LogLevel.Trace, LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Critical })
            fixture.Logger.Log(level, new EventId(555, HttpBoundaryCapture.Secret), new HealthPoisonState(), null, (_, _) => throw new InvalidOperationException());
        Assert.Equal(candidate ? 3 : 6, capture.Events.Length);
        Assert.All(capture.Events, e => Assert.Equal("health.unknown", e.Code));
        Assert.Equal(new[] { "Warning", "Error", candidate ? "Error" : "Fatal" }, capture.Events.TakeLast(3).Select(e => e.Level));
        HealthFrameworkEvents.AssertSafe(capture);
    }

    [Fact]
    public void Sink_ShouldPreserveEnvelopeRejectWrongIdentitiesAndLeaveNeighborsAlone()
    {
        var sink = new HttpBoundaryTestSink();
        var adapter = new HealthCheckLogSink(sink);
        foreach (var row in HealthFrameworkEvents.All)
        {
            foreach (var name in new[] { row.Name, row.Name + "Future" })
            {
                var original = Event(HealthFrameworkEvents.Source, Identity(row.Id, name));
                adapter.Emit(original);
                var entry = Assert.Single(sink.Events);
                Assert.Equal(name == row.Name ? row.Code : "health.unknown", ((ScalarValue)entry.Properties["EventCode"]).Value);
                Assert.Equal(original.Timestamp, entry.Timestamp);
                Assert.Equal(original.Level, entry.Level);
                Assert.Equal(original.TraceId, entry.TraceId);
                Assert.Equal(original.SpanId, entry.SpanId);
                Assert.Null(entry.Exception);
                Assert.DoesNotContain(HttpBoundaryCapture.Secret, entry.RenderMessage() + string.Join(" ", entry.Properties));
                sink.Events.Clear();
            }
        }
        foreach (var malformed in new LogEventPropertyValue[] { new ScalarValue(105), Identity("105", "HealthCheckData"), Identity(null, "HealthCheckData"),
            new StructureValue(new[] { new LogEventProperty("Id", new ScalarValue(105)), new LogEventProperty("Id", new ScalarValue(105)), new LogEventProperty("Name", new ScalarValue("HealthCheckData")) }),
            new StructureValue(new[] { new LogEventProperty("Name", new ScalarValue("HealthCheckData")) }) })
        {
            adapter.Emit(Event(HealthFrameworkEvents.Source, malformed));
            Assert.Equal("health.unknown", ((ScalarValue)Assert.Single(sink.Events).Properties["EventCode"]).Value);
            sink.Events.Clear();
        }
        foreach (var source in new[] { HealthFrameworkEvents.Source + ".Custom", "Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckPublisherHostedService", "HealthChecks.Uris.UriHealthCheck" })
        {
            var original = Event(source, Identity(105, "HealthCheckData"));
            adapter.Emit(original);
            Assert.Same(original, Assert.Single(sink.Events));
            sink.Events.Clear();
        }
    }

    private static StructureValue Identity(object? id, string name) => new(new[] { new LogEventProperty("Id", new ScalarValue(id)), new LogEventProperty("Name", new ScalarValue(name)) });
    private static LogEvent Event(string source, LogEventPropertyValue identity) => new(DateTimeOffset.UtcNow, LogEventLevel.Fatal,
        new HealthUnreadableException(), new MessageTemplateParser().Parse(HttpBoundaryCapture.Secret + " {payload}"), new[]
        {
            new LogEventProperty("SourceContext", new ScalarValue(source)), new LogEventProperty("EventId", identity),
            new LogEventProperty("payload", new ScalarValue(new HealthPoisonValue()))
        }, ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom());
}
