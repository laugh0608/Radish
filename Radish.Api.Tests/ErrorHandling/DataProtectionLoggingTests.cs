using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Radish.Extension.Log;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class DataProtectionLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void ActualGenerators_ShouldMapAllCalledIdentitiesAndDiscardSensitiveInputs(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        var factory = new DataProtectionLoggerFactory(capture.Factory);
        Assert.Equal(78, DataProtectionFrameworkEvents.All.Length);
        foreach (var row in DataProtectionFrameworkEvents.All)
        {
            DataProtectionFrameworkEvents.Generate(row, factory.CreateLogger(row.Source));
            if (candidate && environment == "Production" && row.Level < LogLevel.Warning)
                Assert.Empty(capture.Events);
            else
            {
                var entry = Assert.Single(capture.Events);
                Assert.Equal(row.Code, entry.Code);
                Assert.Equal(row.Level < LogLevel.Warning, entry.Diagnostic);
                Assert.Equal(candidate && row.Level < LogLevel.Warning ? "Info" : row.Level == LogLevel.Trace ? "Verbose" : row.Level.ToString(), entry.Level);
            }
            DataProtectionFrameworkEvents.AssertSafe(capture);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void ProtectedSources_ShouldRejectPoisonStateUnknownAndMismatchedIdentities(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        var factory = new DataProtectionLoggerFactory(capture.Factory);
        var sources = typeof(DataProtectionLogIdentity).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Select(f => (string)f.GetRawConstantValue()!).ToArray();
        Assert.Equal(20, sources.Length);
        foreach (var source in sources)
        foreach (var identity in new[] { new EventId(0), new EventId(24, "wrong"), new EventId(999, "CreatingKey") })
        {
            factory.CreateLogger(source).Log(LogLevel.Warning, identity, new DataProtectionPoisonState(),
                new DataProtectionUnreadableException(), (_, _) => throw new InvalidOperationException("Formatter accessed"));
            var entry = Assert.Single(capture.Events);
            Assert.Equal(DataProtectionLogIdentity.Code(source, null, null), entry.Code);
            Assert.Equal("other", entry.FailureKind);
            DataProtectionFrameworkEvents.AssertSafe(capture);
            capture.Clear();
        }
        // The two source calls without named identities remain unknown, never template-matched.
        foreach (var (source, level) in new[] { (DataProtectionLogIdentity.TypeForwardingActivator, LogLevel.Debug), (DataProtectionLogIdentity.FileSystemXmlRepository, LogLevel.Trace) })
        {
            factory.CreateLogger(source).Log(level, new EventId(0), new DataProtectionPoisonState(), null, (_, _) => throw new InvalidOperationException());
            if (candidate && environment == "Production") Assert.Empty(capture.Events);
            else Assert.Equal(DataProtectionLogIdentity.Code(source, null, null), Assert.Single(capture.Events).Code);
            capture.Clear();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyWriteAndAuditSinks_ShouldRebuildDirectSerilogEventsAfterContextMerge(bool audit)
    {
        using var capture = new HttpBoundaryCapture(false, "Production", audit: audit);
        using var scope = capture.SensitiveScope();
        using var activity = new Activity("data-protection-test").SetIdFormat(ActivityIdFormat.W3C).Start();
        var source = DataProtectionLogIdentity.XmlKeyManager;
        capture.Logger.ForContext("SourceContext", source)
            .ForContext("EventId", new { Id = 24, Name = "ExceptionOccurredWhileProcessingTheKeyElement" }, true)
            .ForContext("EventCode", "runtime.started").ForContext("Diagnostic", false)
            .ForContext("keyId", HttpBoundaryCapture.Secret).ForContext("purposes", HttpBoundaryCapture.Secret)
            .Error(new DataProtectionUnreadableException(), "Secret XML {Xml}", HttpBoundaryCapture.Secret);
        var entry = Assert.Single(capture.Sink.Events);
        Assert.Equal(activity.TraceId, entry.TraceId);
        Assert.Equal(activity.SpanId, entry.SpanId);
        Assert.Equal("data_protection.key_manager.key_processing_failed", Assert.Single(capture.Events).Code);
        DataProtectionFrameworkEvents.AssertSafe(capture);
    }

    [Fact]
    public void Sink_ShouldRequireTypedUniqueIdentityAndPreserveTimestampSeverityAndTypedTrace()
    {
        var output = new HttpBoundaryTestSink();
        var sink = new DataProtectionLogSink(output);
        var validId = new LogEventProperty("Id", new ScalarValue(24));
        var validName = new LogEventProperty("Name", new ScalarValue("ExceptionOccurredWhileProcessingTheKeyElement"));
        var variants = new LogEventPropertyValue[]
        {
            new ScalarValue(24), new ScalarValue("24"),
            new StructureValue(new[] { validId }), new StructureValue(new[] { validName }),
            new StructureValue(new[] { validId, validName, validId }),
            new StructureValue(new[] { validId, validName, validName }),
            new StructureValue(new[] { new LogEventProperty("Id", new ScalarValue(24L)), validName }),
            new StructureValue(new[] { validId, new LogEventProperty("Name", new ScalarValue(null)) })
        };
        var timestamp = DateTimeOffset.UnixEpoch;
        var trace = ActivityTraceId.CreateRandom();
        var span = ActivitySpanId.CreateRandom();
        foreach (var identity in variants)
        {
            sink.Emit(new LogEvent(timestamp, LogEventLevel.Fatal, new DataProtectionUnreadableException(),
                new MessageTemplateParser().Parse(HttpBoundaryCapture.Secret), new[]
                {
                    new LogEventProperty("SourceContext", new ScalarValue(DataProtectionLogIdentity.XmlKeyManager)),
                    new LogEventProperty("EventId", identity),
                    new LogEventProperty("traceId", new ScalarValue(HttpBoundaryCapture.Secret))
                }, trace, span));
            var entry = output.Events.Last();
            Assert.Equal(timestamp, entry.Timestamp);
            Assert.Equal(LogEventLevel.Fatal, entry.Level);
            Assert.Equal(trace, entry.TraceId); Assert.Equal(span, entry.SpanId);
            Assert.Null(entry.Exception);
            Assert.Equal("data_protection.key_manager.unknown", ((ScalarValue)entry.Properties["EventCode"]).Value);
            Assert.DoesNotContain("traceId", entry.Properties.Keys);
        }
        var unrelated = new LogEvent(timestamp, LogEventLevel.Warning, null, new MessageTemplateParser().Parse("Unrelated"),
            new[] { new LogEventProperty("SourceContext", new ScalarValue(DataProtectionLogIdentity.XmlKeyManager + ".Custom")) });
        sink.Emit(unrelated);
        Assert.Same(unrelated, output.Events.Last());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OutputFailures_ShouldRetainWriteToConsumptionAndAuditToPropagation(bool audit)
    {
        var sink = new FailingSink();
        var configuration = new LoggerConfiguration().MinimumLevel.Verbose();
        if (audit) configuration.AuditTo.Sink(new DataProtectionLogSink(sink));
        else configuration.WriteTo.Sink(new DataProtectionLogSink(sink));
        using var logger = configuration.CreateLogger();
        using var factory = new DataProtectionLoggerFactory(new Serilog.Extensions.Logging.SerilogLoggerFactory(logger, dispose: false));
        var source = factory.CreateLogger(DataProtectionLogIdentity.XmlKeyManager);
        void Write() => source.Log(LogLevel.Error, new EventId(24, "ExceptionOccurredWhileProcessingTheKeyElement"),
            new DataProtectionPoisonState(), new DataProtectionUnreadableException(), (_, _) => throw new InvalidOperationException());
        if (audit) Assert.Contains(sink.Failure, Assert.Throws<AggregateException>(Write).InnerExceptions);
        else Write();
        Assert.Equal(1, sink.Calls);
    }

    private sealed class FailingSink : ILogEventSink
    {
        internal readonly InvalidOperationException Failure = new("Test output failure");
        internal int Calls;
        public void Emit(LogEvent logEvent)
        {
            Calls++;
            Assert.Null(logEvent.Exception);
            Assert.Equal("data_protection.key_manager.key_processing_failed", ((ScalarValue)logEvent.Properties["EventCode"]).Value);
            throw Failure;
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void ScopeFailureKind_ShouldNotBeInheritedByProtectedEvents(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.Factory.CreateLogger("scope").BeginScope(new Dictionary<string, object> { ["failureKind"] = "io", ["EventCode"] = "runtime.started" });
        var factory = new DataProtectionLoggerFactory(capture.Factory);
        var logger = factory.CreateLogger(DataProtectionLogIdentity.NullXmlEncryptor);
        logger.LogWarning(new EventId(44, "EncryptingUsingNullEncryptor"), "{Secret}", HttpBoundaryCapture.Secret);
        Assert.Null(Assert.Single(capture.Events).FailureKind);
        DataProtectionFrameworkEvents.AssertSafe(capture);
    }
}
