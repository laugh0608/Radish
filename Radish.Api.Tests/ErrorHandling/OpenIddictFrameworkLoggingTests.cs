using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using Radish.Common.LogTool;
using Radish.Extension.Log;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class OpenIddictFrameworkLoggingTests
{
    [Fact]
    public void FrozenInventory_ShouldMatchInstalledNumericCallsAndAuthenticationGenerators()
    {
        var installed = OpenIddictCompiledEvents.Read("OpenIddict.Server");
        installed.UnionWith(OpenIddictCompiledEvents.Read("OpenIddict.Server.AspNetCore"));
        Assert.Equal(178, installed.Count);
        Assert.Equal(installed.Order(), OpenIddictEvents.All.Where(e => e.Name == null).Select(e => e.Id).Order());
        Assert.DoesNotContain(6072, installed); // 当前目标未编译无 ECDSA 的备选分支。
        foreach (var row in OpenIddictEvents.All.Where(e => e.Name != null))
        {
            var metadata = Generator(row).GetCustomAttribute<LoggerMessageAttribute>()!;
            Assert.Equal(row.Id, metadata.EventId);
            Assert.Equal(row.Name, metadata.EventName);
            Assert.Equal(row.Level, metadata.Level);
        }
        var codes = OpenIddictEvents.All.Select(e => e.Code).Concat(new[] { "oidc.server.unknown", "oidc.handler.unknown" }).ToArray();
        Assert.Equal(185, codes.Distinct().Count());
        Assert.All(codes, code => Assert.True(RuntimeLogPolicy.IsRegisteredEventCode(code)));
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void IdentitiesAndRealBaseGenerators_ShouldRespectFourModesAndDiagnostics(bool candidate, string environment)
    {
        foreach (var diagnostics in new[] { false, true })
        {
            using var capture = new HttpBoundaryCapture(candidate, environment, "auth", diagnostics);
            using var fixture = new OpenIddictLoggingFixture(capture);
            using var scope = capture.SensitiveScope();
            foreach (var row in OpenIddictEvents.All)
            {
                var logger = fixture.Factory.CreateLogger(row.Source);
                if (row.Name == null)
                    logger.Log(row.Level, new EventId(row.Id), new HealthPoisonState(), new HealthUnreadableException(), (_, _) => throw new InvalidOperationException());
                else
                    Generator(row).Invoke(null, Generator(row).GetParameters().Select(p => p.ParameterType == typeof(Microsoft.Extensions.Logging.ILogger) ? (object)logger : HttpBoundaryCapture.Secret).ToArray());
                if (!candidate || row.Level >= LogLevel.Warning || (environment == "Development" && diagnostics))
                {
                    var entry = Assert.Single(capture.Events);
                    Assert.Equal(row.Code, entry.Code);
                    Assert.Equal(row.Level < LogLevel.Warning, entry.Diagnostic);
                    Assert.Equal(candidate && row.Level < LogLevel.Warning ? "Info" : row.Level == LogLevel.Trace ? "Verbose" : row.Level.ToString(), entry.Level);
                    Assert.Equal(row.Name == null ? "other" : null, entry.FailureKind);
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
    public void ConfiguredWriteAndAudit_ShouldCoverAll185Codes(bool audit)
    {
        using var capture = new HttpBoundaryCapture(false, "Production", "auth", audit: audit);
        using var fixture = new OpenIddictLoggingFixture(capture);
        using var scope = capture.SensitiveScope();
        foreach (var row in OpenIddictEvents.All)
            fixture.Factory.CreateLogger(row.Source).Log(row.Level, new EventId(row.Id, row.Name), new HealthPoisonState(), null, (_, _) => throw new InvalidOperationException());
        foreach (var source in new[] { OpenIddictEvents.Server, OpenIddictEvents.Handler })
            fixture.Factory.CreateLogger(source).LogWarning(new EventId(9999, "future"), "{private}", HttpBoundaryCapture.Secret);
        Assert.Equal(OpenIddictEvents.All.Select(e => e.Code).Concat(new[] { "oidc.server.unknown", "oidc.handler.unknown" }), capture.Codes);
        OpenIddictLoggingFixture.AssertSafe(capture);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void MessagesClaimsAndConflictingScopes_ShouldStopBeforeBridgeFormatting(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var fixture = new OpenIddictLoggingFixture(capture);
        using var scope = fixture.ServerLogger.BeginScope(new Dictionary<string, object>
        {
            ["SourceContext"] = HttpBoundaryCapture.Secret, ["EventId"] = 6000, ["EventCode"] = "oidc.server.token_validation_failed",
            ["failureKind"] = "io", ["payload"] = HttpBoundaryCapture.Secret
        });
        var request = new OpenIddictRequest { ClientSecret = HttpBoundaryCapture.Secret, State = HttpBoundaryCapture.Secret, LoginHint = HttpBoundaryCapture.Secret };
        using (var json = JsonDocument.Parse("{\"secret\":\"" + HttpBoundaryCapture.Secret + "\"}"))
            request.SetParameter("custom", new OpenIddictParameter(json.RootElement));
        // 实际协议对象含已释放 JsonElement：原桥接在 ToString 时会失败并丢弃事件。
        fixture.ServerLogger.LogInformation(6030, "Request {Request}", request);
        fixture.ServerLogger.LogTrace(6001, "Token {Token}; claims {Claims}", HttpBoundaryCapture.Secret, new HealthPoisonValue());
        Assert.Equal(candidate && environment == "Production" ? Array.Empty<string>() :
            new[] { "oidc.server.authorization_request_extracted", "oidc.server.token_validated" }, capture.Codes);
        Assert.All(capture.Events, e => Assert.Null(e.FailureKind));
        capture.Clear();
        foreach (var source in new[] { OpenIddictEvents.Server, OpenIddictEvents.Handler })
        foreach (var identity in new[] { default, new EventId(6000, "wrong"), new EventId(6072), new EventId(9999, HttpBoundaryCapture.Secret) })
        {
            fixture.Factory.CreateLogger(source).Log(LogLevel.Critical, identity, new HealthPoisonState(), new HealthUnreadableException(), (_, _) => throw new InvalidOperationException());
            var entry = Assert.Single(capture.Events);
            Assert.Equal(source == OpenIddictEvents.Server ? "oidc.server.unknown" : "oidc.handler.unknown", entry.Code);
            Assert.Equal(candidate ? "Error" : "Fatal", entry.Level);
            Assert.Equal("other", entry.FailureKind);
            OpenIddictLoggingFixture.AssertSafe(capture);
            capture.Clear();
        }
    }

    [Fact]
    public void Sink_ShouldPreserveEnvelopeAndStrictlyRejectMalformedIdentities()
    {
        var sink = new HttpBoundaryTestSink();
        var adapter = new OpenIddictLogSink(sink);
        foreach (var row in OpenIddictEvents.All)
        {
            var original = Event(row.Source, Identity(row.Id, row.Name));
            adapter.Emit(original);
            var entry = Assert.Single(sink.Events);
            Assert.Equal(row.Code, ((ScalarValue)entry.Properties["EventCode"]).Value);
            Assert.Equal(original.Timestamp, entry.Timestamp);
            Assert.Equal(original.Level, entry.Level);
            Assert.Equal(original.TraceId, entry.TraceId);
            Assert.Equal(original.SpanId, entry.SpanId);
            Assert.Null(entry.Exception);
            Assert.DoesNotContain(HttpBoundaryCapture.Secret, entry.RenderMessage() + string.Join(" ", entry.Properties));
            sink.Events.Clear();
        }
        foreach (var source in new[] { OpenIddictEvents.Server, OpenIddictEvents.Handler })
        foreach (var identity in new LogEventPropertyValue[]
        {
            new ScalarValue(6000), Identity("6000", null), Identity(null, null), Identity(6000, ""),
            new StructureValue(new[] { new LogEventProperty("Id", new ScalarValue(6000)), new LogEventProperty("Name", new ScalarValue(null)) }),
            new StructureValue(new[] { new LogEventProperty("Id", new ScalarValue(6000)), new LogEventProperty("Id", new ScalarValue(6000)) }),
            new StructureValue(new[] { new LogEventProperty("Id", new ScalarValue(7)), new LogEventProperty("Name", new ScalarValue("AuthenticationSchemeNotAuthenticatedWithFailure")), new LogEventProperty("Name", new ScalarValue("AuthenticationSchemeNotAuthenticatedWithFailure")) })
        })
        {
            adapter.Emit(Event(source, identity));
            Assert.Equal(source == OpenIddictEvents.Server ? "oidc.server.unknown" : "oidc.handler.unknown", ((ScalarValue)Assert.Single(sink.Events).Properties["EventCode"]).Value);
            sink.Events.Clear();
        }
        foreach (var source in new[] { OpenIddictEvents.Server + ".Custom", "OpenIddict.Core.OpenIddictScopeManager", "OpenIddict.Validation.OpenIddictValidationDispatcher", "OpenIddict.Client.OpenIddictClientDispatcher", "Radish.Auth.OpenIddict.ValidateIdleSessionTokenRequestHandler" })
        {
            var original = Event(source, Identity(6000, null));
            adapter.Emit(original);
            Assert.Same(original, Assert.Single(sink.Events));
            sink.Events.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void UnknownSeverity_ShouldKeepWarningsAndFailuresVisible(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth", diagnostics: false);
        using var fixture = new OpenIddictLoggingFixture(capture);
        foreach (var source in new[] { OpenIddictEvents.Server, OpenIddictEvents.Handler })
        {
            foreach (var level in new[] { LogLevel.Trace, LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Critical })
                fixture.Factory.CreateLogger(source).Log(level, default, new HealthPoisonState(), null, (_, _) => throw new InvalidOperationException());
            Assert.Equal(candidate ? 3 : 6, capture.Events.Length);
            Assert.Equal(new[] { "Warning", "Error", candidate ? "Error" : "Fatal" }, capture.Events.TakeLast(3).Select(e => e.Level));
            OpenIddictLoggingFixture.AssertSafe(capture);
            capture.Clear();
        }
    }

    private static MethodInfo Generator(OpenIddictEvent row) => Assembly.Load("Microsoft.AspNetCore.Authentication")
        .GetType("Microsoft.Extensions.Logging.LoggingExtensions", true)!.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        .Single(m => m.GetCustomAttribute<LoggerMessageAttribute>()?.EventId == row.Id);
    private static StructureValue Identity(object? id, string? name)
    {
        var properties = new List<LogEventProperty> { new("Id", new ScalarValue(id)) };
        if (name != null) properties.Add(new("Name", new ScalarValue(name)));
        return new StructureValue(properties);
    }
    private static LogEvent Event(string source, LogEventPropertyValue identity) => new(DateTimeOffset.UtcNow, LogEventLevel.Fatal,
        new HealthUnreadableException(), new MessageTemplateParser().Parse(HttpBoundaryCapture.Secret + " {payload}"), new[]
        {
            new LogEventProperty("SourceContext", new ScalarValue(source)), new LogEventProperty("EventId", identity),
            new LogEventProperty("payload", new ScalarValue(new HealthPoisonValue()))
        }, ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom());
}
