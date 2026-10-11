using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using Radish.Common.LogTool;
using Radish.Extension.Log;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class OpenIddictCoreLoggingTests
{
    [Fact]
    public void Inventory_ShouldMatchInstalledCoreAndRegisterExactly21Codes()
    {
        var installed = OpenIddictCompiledEvents.Read("OpenIddict.Core");
        Assert.Equal(18, installed.Count);
        Assert.Equal(installed.Order(), OpenIddictCoreEvents.All.Select(row => row.Id).Order());
        var codes = OpenIddictCoreEvents.All.Select(row => row.Code).Concat(OpenIddictCoreEvents.Sources.Select(OpenIddictCoreEvents.Unknown)).ToArray();
        Assert.Equal(21, codes.Distinct().Count());
        Assert.All(codes, code => Assert.True(RuntimeLogPolicy.IsRegisteredEventCode(code)));
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void IdentitiesScopesAndPoisonInputs_ShouldRespectModesAndDiagnostics(bool candidate, string environment)
    {
        foreach (var diagnostics in new[] { false, true })
        {
            using var capture = new HttpBoundaryCapture(candidate, environment, "auth", diagnostics);
            using var fixture = new OpenIddictLoggingFixture(capture);
            foreach (var row in OpenIddictCoreEvents.All)
            {
                var logger = fixture.Factory.CreateLogger(row.Source);
                using var scope = logger.BeginScope(new Dictionary<string, object>
                {
                    ["SourceContext"] = HttpBoundaryCapture.Secret, ["EventId"] = 6163,
                    ["EventCode"] = "oidc.core.application.client_secret_verification_failed", ["failureKind"] = "io",
                    ["payload"] = HttpBoundaryCapture.Secret
                });
                foreach (var failure in new[] { false, true })
                {
                    logger.Log(row.Level, new EventId(row.Id), new HealthPoisonState(), failure ? new HealthUnreadableException() : null,
                        (_, _) => throw new InvalidOperationException("Must not format original input"));
                    if (!candidate || row.Level >= LogLevel.Warning || (environment == "Development" && diagnostics))
                    {
                        var entry = Assert.Single(capture.Events);
                        Assert.Equal(row.Code, entry.Code);
                        Assert.Equal(failure ? "other" : null, entry.FailureKind);
                        Assert.Equal(row.Level < LogLevel.Warning, entry.Diagnostic);
                        Assert.Equal(candidate && row.Level < LogLevel.Warning ? "Info" : row.Level.ToString(), entry.Level);
                        if (candidate) Assert.Contains("\"sourceCategory\":\"application\"", capture.Output.ToString());
                        else Assert.Equal("application", ((ScalarValue)Assert.Single(capture.Sink.Events).Properties["SourceCategory"]).Value);
                    }
                    else Assert.Empty(capture.Events);
                    OpenIddictLoggingFixture.AssertSafe(capture);
                    capture.Clear();
                }
            }
            foreach (var source in OpenIddictCoreEvents.Sources)
            foreach (var level in new[] { LogLevel.Trace, LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Critical })
            {
                var logger = fixture.Factory.CreateLogger(source);
                using var scope = logger.BeginScope(new Dictionary<string, object> { ["EventId"] = 6163, ["failureKind"] = "io" });
                logger.Log(level, default, new HealthPoisonState(), null, (_, _) => throw new InvalidOperationException());
                if (!candidate || level >= LogLevel.Warning || (environment == "Development" && diagnostics))
                {
                    var entry = Assert.Single(capture.Events);
                    Assert.Equal(OpenIddictCoreEvents.Unknown(source), entry.Code);
                    Assert.Null(entry.FailureKind);
                    Assert.Equal(level < LogLevel.Warning, entry.Diagnostic);
                    Assert.Equal(level == LogLevel.Critical ? (candidate ? "Error" : "Fatal") : level < LogLevel.Warning && candidate ? "Info" : level == LogLevel.Trace ? "Verbose" : level.ToString(), entry.Level);
                }
                else Assert.Empty(capture.Events);
                OpenIddictLoggingFixture.AssertSafe(capture);
                capture.Clear();
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfiguredOutputs_ShouldAdaptAll21Codes(bool audit)
    {
        using var capture = new HttpBoundaryCapture(false, "Production", "auth", audit: audit);
        using var fixture = new OpenIddictLoggingFixture(capture);
        foreach (var row in OpenIddictCoreEvents.All)
            fixture.Factory.CreateLogger(row.Source).Log(row.Level, new EventId(row.Id), new HealthPoisonState(), null, (_, _) => throw new InvalidOperationException());
        foreach (var source in OpenIddictCoreEvents.Sources)
            fixture.Factory.CreateLogger(source).LogWarning(new EventId(9999, "future"), "{value}", HttpBoundaryCapture.Secret);
        Assert.Equal(OpenIddictCoreEvents.All.Select(row => row.Code).Concat(OpenIddictCoreEvents.Sources.Select(OpenIddictCoreEvents.Unknown)), capture.Codes);
        OpenIddictLoggingFixture.AssertSafe(capture);
    }

    [Fact]
    public void Sink_ShouldRequireExactIdentityAndPreserveTypedTraceTimeAndLevel()
    {
        using var sink = new HttpBoundaryTestSink();
        var adapter = new OpenIddictLogSink(sink);
        foreach (var row in OpenIddictCoreEvents.All)
        foreach (var kind in new[] { "valid", "name", "null-name", "duplicate-id", "duplicate-name", "wrong-type", "wrong-source-id", "no-id" })
        {
            var identity = new List<LogEventProperty>();
            if (kind != "no-id") identity.Add(new("Id", new ScalarValue(kind == "wrong-type" ? (object)row.Id.ToString() : kind == "wrong-source-id" ? 6000 : row.Id)));
            if (kind is "name" or "duplicate-name") identity.Add(new("Name", new ScalarValue("unexpected")));
            if (kind == "null-name") identity.Add(new("Name", new ScalarValue(null)));
            if (kind == "duplicate-id") identity.Add(new("Id", new ScalarValue(row.Id)));
            if (kind == "duplicate-name") identity.Add(new("Name", new ScalarValue("unexpected")));
            var input = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, new HealthUnreadableException(),
                new MessageTemplateParser().Parse(HttpBoundaryCapture.Secret), new[]
                {
                    new LogEventProperty("SourceContext", new ScalarValue(row.Source)), new LogEventProperty("EventId", new StructureValue(identity)),
                    new LogEventProperty("payload", new ScalarValue(new HealthPoisonValue()))
                }, ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom());
            adapter.Emit(input);
            var output = Assert.Single(sink.Events);
            Assert.Equal(kind == "valid" ? row.Code : OpenIddictCoreEvents.Unknown(row.Source), ((ScalarValue)output.Properties["EventCode"]).Value);
            Assert.Equal(input.Timestamp, output.Timestamp);
            Assert.Equal(input.Level, output.Level);
            Assert.Equal(input.TraceId, output.TraceId);
            Assert.Equal(input.SpanId, output.SpanId);
            Assert.Equal("application", ((ScalarValue)output.Properties["SourceCategory"]).Value);
            Assert.Null(output.Exception);
            Assert.DoesNotContain(HttpBoundaryCapture.Secret, output.RenderMessage() + string.Join(" ", output.Properties));
            sink.Events.Clear();
        }
    }

    [Fact]
    public void NeighbourSources_ShouldPassThroughUnchanged()
    {
        using var sink = new HttpBoundaryTestSink();
        var adapter = new OpenIddictLogSink(sink);
        foreach (var source in new[] { OpenIddictCoreEvents.Token + "`1", OpenIddictCoreEvents.Application + ".Custom", "OpenIddict.Core.OpenIddictScopeManager", "OpenIddict.EntityFrameworkCore.OpenIddictEntityFrameworkCoreTokenStore", "Radish.Auth.OpenIddict.ValidateIdleSessionTokenRequestHandler" })
        {
            var input = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Warning, null, new MessageTemplateParser().Parse("untouched"),
                new[] { new LogEventProperty("SourceContext", new ScalarValue(source)) });
            adapter.Emit(input);
            Assert.Same(input, Assert.Single(sink.Events));
            sink.Events.Clear();
        }
    }
}
