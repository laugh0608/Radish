using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Radish.Extension.Log;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class YarpConfigurationFrameworkLoggingTests
{
    public static IEnumerable<object[]> Events() => YarpConfigurationFrameworkEvents.All.Select((_, index) => new object[] { index });

    [Theory]
    [MemberData(nameof(Events))]
    public void RealGeneratorIdentity_AndAdaptedEnvelope_ShouldMatchFrozenContract(int index)
    {
        var row = YarpConfigurationFrameworkEvents.All[index];
        var probe = new EventProbe(row);
        YarpConfigurationFrameworkEvents.Generate(row, probe);
        Assert.Equal(1, probe.Matches);
        var original = Event(row.Source, Identity(row.Id, row.Name));
        var result = Adapt(original);
        Assert.NotSame(original, result);
        Assert.Equal(row.Code, Code(result));
        Assert.Equal(original.Level, result.Level);
        Assert.Equal(original.Timestamp, result.Timestamp);
        Assert.Equal(original.TraceId, result.TraceId);
        Assert.Equal(original.SpanId, result.SpanId);
        Assert.Null(result.Exception);
        Assert.Equal("other", ((ScalarValue)result.Properties["failureKind"]).Value);
        Assert.DoesNotContain(HttpBoundaryCapture.Secret, result.RenderMessage() + string.Join(" ", result.Properties));
        Assert.IsType<UnreadableException>(original.Exception);
    }

    [Fact]
    public void Inventory_ShouldCoverInstalledGeneratorsAndExactlyEighteenCodes()
    {
        Assert.Equal(15, YarpConfigurationFrameworkEvents.All.Length);
        Assert.Equal(3, YarpConfigurationFrameworkEvents.Sources.Length);
        Assert.Equal(18, YarpConfigurationFrameworkEvents.All.Select(e => e.Code).Concat(YarpConfigurationFrameworkEvents.Sources.Select(e => e.UnknownCode)).Distinct().Count());
        foreach (var group in YarpConfigurationFrameworkEvents.All.Where(e => e.Field.Length > 0).GroupBy(e => e.Source))
        {
            var actual = YarpConfigurationFrameworkEvents.LogType(group.First()).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
                .Where(f => typeof(Delegate).IsAssignableFrom(f.FieldType)).Select(f => f.Name).Order();
            Assert.Equal(group.Select(e => e.Field).Order(), actual);
        }
        foreach (var row in YarpConfigurationFrameworkEvents.All)
        {
            var loggerType = Assert.Single(YarpConfigurationFrameworkEvents.OwnerType(row).GetConstructors().SelectMany(c => c.GetParameters()),
                p => p.ParameterType.IsGenericType && p.ParameterType.GetGenericTypeDefinition() == typeof(ILogger<>)).ParameterType;
            Assert.Equal(row.Source, loggerType.GetGenericArguments()[0].FullName);
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void RealGenerators_ShouldReachSafeOutputsWithExistingDiagnosticsPolicy(bool candidate, string environment)
    {
        foreach (var diagnostics in new[] { true, false })
        {
            using var capture = new HttpBoundaryCapture(candidate, environment, "gateway", diagnostics);
            using var scope = capture.SensitiveScope();
            foreach (var row in YarpConfigurationFrameworkEvents.All)
            {
                var probe = new EventProbe(row, capture.Factory.CreateLogger(row.Source));
                YarpConfigurationFrameworkEvents.Generate(row, probe);
                Assert.Equal(1, probe.Matches);
                if (!candidate || row.Level >= LogLevel.Warning || (environment == "Development" && diagnostics))
                {
                    var logged = Assert.Single(capture.Events);
                    Assert.Equal(row.Code, logged.Code);
                    Assert.Equal(row.Level < LogLevel.Warning, logged.Diagnostic);
                    Assert.Equal(candidate && row.Level < LogLevel.Warning ? "Info" : row.Level == LogLevel.Trace ? "Verbose" : row.Level.ToString(), logged.Level);
                }
                else Assert.Empty(capture.Events);
                TransportFrameworkLoggingTests.AssertSafe(capture);
                capture.Clear();
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfiguredWriteAndAuditOutputs_ShouldReceiveEverySource(bool audit)
    {
        using var capture = new HttpBoundaryCapture(false, "Production", "gateway", audit: audit);
        using var scope = capture.SensitiveScope();
        foreach (var row in YarpConfigurationFrameworkEvents.All)
            YarpConfigurationFrameworkEvents.Generate(row, new EventProbe(row, capture.Factory.CreateLogger(row.Source)));
        Assert.Equal(YarpConfigurationFrameworkEvents.All.Select(e => e.Code), capture.Codes);
        TransportFrameworkLoggingTests.AssertSafe(capture);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void UnknownEvents_ShouldRetainFailuresWithoutRawPayloads(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway", diagnostics: false);
        using var scope = capture.SensitiveScope();
        foreach (var source in YarpConfigurationFrameworkEvents.Sources)
        foreach (var identity in new[] { default(EventId), new EventId(999, "Future") })
        {
            foreach (var level in new[] { LogLevel.Warning, LogLevel.Error, LogLevel.Critical, LogLevel.Information, LogLevel.Debug, LogLevel.Trace })
                capture.Factory.CreateLogger(source.Source).Log(level, identity, new UnreadableException(), "{payload}", HttpBoundaryCapture.Secret);
            Assert.Equal(candidate ? 3 : 6, capture.Events.Length);
            Assert.Equal(new[] { "Warning", "Error", candidate ? "Error" : "Fatal" }, capture.Events.Take(3).Select(e => e.Level));
            Assert.All(capture.Events, e => Assert.Equal(source.UnknownCode, e.Code));
            TransportFrameworkLoggingTests.AssertSafe(capture);
            capture.Clear();
        }
    }

    [Fact]
    public void MalformedIdentityAndNeighborSources_ShouldNotBeGuessed()
    {
        foreach (var row in YarpConfigurationFrameworkEvents.All)
        {
            var unknown = YarpConfigurationFrameworkEvents.Sources.Single(e => e.Source == row.Source).UnknownCode;
            foreach (var identity in new LogEventPropertyValue[]
            {
                Identity(row.Id, null), Identity(row.Id, row.Name.ToLowerInvariant()), Identity(row.Id + 1000, row.Name),
                new ScalarValue(1), new StructureValue([new("Id", new ScalarValue("100")), new("Name", new ScalarValue(row.Name))]),
                new StructureValue([new("Id", new ScalarValue(row.Id)), new("Name", new ScalarValue(new UnreadableValue()))])
            }) Assert.Equal(unknown, Code(Adapt(Event(row.Source, identity))));
            var neighbor = Event(row.Source + ".Custom", Identity(row.Id, row.Name));
            Assert.Same(neighbor, Adapt(neighbor));
        }
        foreach (var source in new[] { "System.Net.Http.HttpClient.Other.ClientHandler", "System.Net.Http.HttpClient.api-service.clienthandler",
            "Yarp.ReverseProxy.Configuration.ClusterValidators.ProxyHttpRequestValidator", "Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets.Client", "System.Net.Http" })
        {
            var original = Event(source, Identity(100, "RequestStart"));
            Assert.Same(original, Adapt(original));
        }
    }

    private sealed class EventProbe(OutboundFrameworkEvent row, ILogger? next = null) : ILogger
    {
        internal int Matches;
        public bool IsEnabled(LogLevel level) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => next?.BeginScope(state);
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Assert.Equal(row.Id, id.Id);
            Assert.Equal(row.Name, id.Name);
            Assert.Equal(row.Level, level);
            Matches++;
            next?.Log(level, id, state, exception, formatter);
        }
    }
    private static LogEvent Adapt(LogEvent original)
    {
        var sink = new HttpBoundaryTestSink();
        new OutboundHttpFrameworkLogSink(sink).Emit(original);
        return Assert.Single(sink.Events);
    }
    private static string Code(LogEvent value) => (string)((ScalarValue)value.Properties["EventCode"]).Value!;
    private static StructureValue Identity(int id, string? name) => new([new("Id", new ScalarValue(id)), new("Name", new ScalarValue(name))]);
    private static LogEvent Event(string source, LogEventPropertyValue identity) => new(DateTimeOffset.UtcNow, LogEventLevel.Fatal,
        new UnreadableException(), new MessageTemplateParser().Parse(HttpBoundaryCapture.Secret + " {payload}"),
        [new("SourceContext", new ScalarValue(source)), new("EventId", identity), new("payload", new ScalarValue(new UnreadableValue()))],
        ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom());
    private sealed class UnreadableException : Exception
    {
        public override string Message => throw new InvalidOperationException("Must not read exception message");
        public override string ToString() => throw new InvalidOperationException("Must not render exception");
    }
    private sealed class UnreadableValue
    {
        public override string ToString() => throw new InvalidOperationException("Must not render payload");
    }
}
