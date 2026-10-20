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
public sealed class AuthenticationFrameworkLoggingTests
{
    public static IEnumerable<object[]> Events() => AuthenticationFrameworkEvents.All.Select((_, index) => new object[] { index });

    [Theory]
    [MemberData(nameof(Events))]
    public void ExactIdentities_ShouldMatchInstalledFrameworkAndPreserveEventEnvelope(int index)
    {
        var row = AuthenticationFrameworkEvents.All[index];
        var metadata = row.Generator.GetCustomAttribute<LoggerMessageAttribute>()!;
        Assert.Equal(row.Id, metadata.EventId);
        Assert.Equal(row.Name, metadata.EventName);
        Assert.Equal(row.Level, metadata.Level);
        var original = Event(row.Source, Identity(row.Id, row.Name));
        var result = Adapt(original);
        Assert.NotSame(original, result);
        Assert.Equal(original.Timestamp, result.Timestamp);
        Assert.Equal(original.Level, result.Level);
        Assert.Equal(original.TraceId, result.TraceId);
        Assert.Equal(original.SpanId, result.SpanId);
        Assert.Equal(row.Code, Code(result));
        Assert.Equal("other", ((ScalarValue)result.Properties["failureKind"]).Value);
        Assert.Null(result.Exception);
        Assert.DoesNotContain(HttpBoundaryCapture.Secret, result.RenderMessage() + string.Join(" ", result.Properties));
        Assert.IsType<UnreadableAuthenticationException>(original.Exception);
        Assert.True(original.Properties.ContainsKey("payload"));
    }

    [Fact]
    public void FrozenInventory_ShouldCoverExactlyTwentyFourCodes()
    {
        Assert.Equal(18, AuthenticationFrameworkEvents.All.Length);
        Assert.Equal(4, AuthenticationFrameworkEvents.Sources.Length);
        var codes = AuthenticationFrameworkEvents.All.Select(e => e.Code)
            .Concat(AuthenticationFrameworkEvents.Sources.Select(e => e.UnknownCode))
            .Concat(new[] { "auth.jwt_failure_observed", "auth.jwt_challenge_observed" }).ToArray();
        Assert.Equal(24, codes.Distinct().Count());
        Assert.All(codes, code => Assert.True(Radish.Common.LogTool.RuntimeLogPolicy.IsRegisteredEventCode(code)));
        foreach (var group in AuthenticationFrameworkEvents.All.GroupBy(e => e.Assembly))
        {
            var actual = Assembly.Load(group.Key).GetType("Microsoft.Extensions.Logging.LoggingExtensions", true)!
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(m => m.GetCustomAttribute<LoggerMessageAttribute>()).Where(a => a != null).Select(a => a!.EventId).Order();
            // Authentication 基类还定义 remote handler 的九个事件，JWT / Cookie 并不调用。
            var remote = group.Key == "Microsoft.AspNetCore.Authentication" ? new[] { 4, 5, 6, 14, 15, 16, 17, 18, 19 } : Array.Empty<int>();
            Assert.Equal(group.Select(e => e.Id).Distinct().Concat(remote).Order(), actual);
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void RealGenerators_ShouldReachSafeOutputsAtOriginalSeverity(bool candidate, string environment)
    {
        foreach (var diagnostics in new[] { true, false })
        {
            using var capture = new HttpBoundaryCapture(candidate, environment, diagnostics: diagnostics);
            using var scope = capture.SensitiveScope();
            foreach (var row in AuthenticationFrameworkEvents.All)
            {
                InvokeGenerator(row, capture);
                if (!candidate || row.Level >= LogLevel.Warning || (environment == "Development" && diagnostics))
                {
                    var logged = Assert.Single(capture.Events);
                    Assert.Equal(row.Code, logged.Code);
                    Assert.Equal(row.Level < LogLevel.Warning, logged.Diagnostic);
                    Assert.Equal(candidate && row.Level < LogLevel.Warning ? "Info" : row.Level == LogLevel.Trace ? "Verbose" : row.Level.ToString(), logged.Level);
                    Assert.Equal(row.Generator.GetParameters().Any(p => p.ParameterType == typeof(Exception)) ? "other" : null, logged.FailureKind);
                }
                else Assert.Empty(capture.Events);
                AssertSafe(capture);
                capture.Clear();
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfiguredWriteAndAuditOutputs_ShouldBothReceiveAllRealGenerators(bool audit)
    {
        using var capture = new HttpBoundaryCapture(false, "Production", audit: audit);
        using var scope = capture.SensitiveScope();
        foreach (var row in AuthenticationFrameworkEvents.All) InvokeGenerator(row, capture);
        Assert.Equal(AuthenticationFrameworkEvents.All.Select(e => e.Code), capture.Codes);
        AssertSafe(capture);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void UnknownAndDirectZeroEvents_ShouldKeepWarningsAndFailuresVisible(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, diagnostics: false);
        using var scope = capture.SensitiveScope();
        foreach (var source in AuthenticationFrameworkEvents.Sources)
        foreach (var identity in new[] { default(EventId), new EventId(991, "FutureEvent") })
        {
            var logger = capture.Factory.CreateLogger(source.Source);
            foreach (var level in new[] { LogLevel.Warning, LogLevel.Error, LogLevel.Critical, LogLevel.Information, LogLevel.Debug, LogLevel.Trace })
                logger.Log(level, identity, new UnreadableAuthenticationException(), "{payload}", HttpBoundaryCapture.Secret);
            Assert.Equal(candidate ? 3 : 6, capture.Events.Length);
            Assert.Equal(new[] { "Warning", "Error", candidate ? "Error" : "Fatal" }, capture.Events.Take(3).Select(e => e.Level));
            Assert.All(capture.Events, e => Assert.Equal(source.UnknownCode, e.Code));
            AssertSafe(capture);
            capture.Clear();
        }
    }

    [Fact]
    public void WrongIdentityOrNeighborCategory_ShouldNeverGuessFromText()
    {
        foreach (var source in AuthenticationFrameworkEvents.Sources)
        {
            foreach (var row in AuthenticationFrameworkEvents.All.Where(e => e.Source == source.Source))
            {
                foreach (var name in new string?[] { null, row.Name.ToLowerInvariant(), row.Name + "Future" })
                    Assert.Equal(source.UnknownCode, Code(Adapt(Event(source.Source, Identity(row.Id, name)))));
                Assert.Equal(source.UnknownCode, Code(Adapt(Event(source.Source, Identity(row.Id + 1000, row.Name)))));
            }
            foreach (var malformed in new LogEventPropertyValue[] { new ScalarValue(1), new ScalarValue(null),
                new StructureValue(new[] { new LogEventProperty("Id", new ScalarValue("1")), new LogEventProperty("Name", new ScalarValue("Future")) }),
                new StructureValue(new[] { new LogEventProperty("Id", new ScalarValue(1)), new LogEventProperty("Name", new ScalarValue(new UnreadableAuthenticationValue())) }) })
                Assert.Equal(source.UnknownCode, Code(Adapt(Event(source.Source, malformed))));
            var neighbor = Event(source.Source + ".Custom", Identity(1, "Future"));
            Assert.Same(neighbor, Adapt(neighbor));
        }
        foreach (var source in new[] { "Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectHandler", "OpenIddict.Server.OpenIddictServerDispatcher", "Microsoft.AspNetCore.Authorization.DefaultAuthorizationServiceImpl" })
        {
            var original = Event(source, Identity(1, "Future"));
            Assert.Same(original, Adapt(original));
        }
    }

    internal static void AssertSafe(HttpBoundaryCapture capture)
    {
        capture.AssertSafe();
        foreach (var entry in capture.Sink.Events)
            Assert.All(entry.Properties.Keys, key => Assert.Contains(key,
                new[] { "EventCode", "SourceCategory", "SourceContext", "Diagnostic", "failureKind" }));
    }

    private static void InvokeGenerator(AuthenticationFrameworkEvent row, HttpBoundaryCapture capture) =>
        AuthenticationFrameworkEvents.Generate(row, capture.Factory.CreateLogger(row.Source));

    private static LogEvent Adapt(LogEvent original)
    {
        var sink = new HttpBoundaryTestSink();
        new AuthenticationLogSink(sink).Emit(original);
        return Assert.Single(sink.Events);
    }
    private static string Code(LogEvent value) => (string)((ScalarValue)value.Properties["EventCode"]).Value!;
    private static StructureValue Identity(int id, string? name) => new(new[]
    {
        new LogEventProperty("Id", new ScalarValue(id)), new LogEventProperty("Name", new ScalarValue(name))
    });
    private static LogEvent Event(string source, LogEventPropertyValue identity) => new(DateTimeOffset.UtcNow, LogEventLevel.Fatal,
        new UnreadableAuthenticationException(), new MessageTemplateParser().Parse(HttpBoundaryCapture.Secret + " {payload}"), new[]
        {
            new LogEventProperty("SourceContext", new ScalarValue(source)), new LogEventProperty("EventId", identity),
            new LogEventProperty("payload", new ScalarValue(new UnreadableAuthenticationValue()))
        }, ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom());

    private sealed class UnreadableAuthenticationValue
    {
        public override string ToString() => throw new InvalidOperationException("Must not render authentication payload");
    }
}
