using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Radish.Api.HealthChecks;
using Radish.Extension.Log;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class HostStartupLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void StartedNotification_ShouldEmitOneSafeSummaryPerHostAndKeepAllModes(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, diagnostics: false);
        using var scope = capture.SensitiveScope();
        using var eventScope = LogContext.PushProperty("EventCode", "runtime.failed");
        using var sourceScope = LogContext.PushProperty("SourceContext", HttpBoundaryCapture.Secret);
        using var diagnosticScope = LogContext.PushProperty("Diagnostic", true);
        using var categoryScope = LogContext.PushProperty("SourceCategory", "database");
        using var traceScope = LogContext.PushProperty("traceId", "aabbccddeeff00112233445566778899");
        using var extraScope = LogContext.PushProperty("failureKind", "other");
        using var activity = new Activity("startup-test").SetIdFormat(ActivityIdFormat.W3C).Start();
        foreach (var host in new[] { "auth", "api", "gateway" })
        {
            using var lifetime = new StartupTestLifetime();
            var addresses = new CountOnlyAddresses { Count = 0 };
            var startup = host switch
            {
                "auth" => HostStartupLog.Auth(environment, 2),
                "api" => HostStartupLog.Api(environment, 2, true),
                _ => HostStartupLog.Gateway(environment, 2, 3, true, true)
            };
            using var registration = startup.Register(lifetime, addresses, capture.Logger);
            if (host == "auth") startup.AuthConfigurationCompleted(false);
            Assert.Empty(capture.Events);
            addresses.Count = 4; // The actual bound count is available only after registration.
            lifetime.Started.Cancel();
            lifetime.Started.Cancel();
            lifetime.Stopping.Cancel();
            lifetime.Stopped.Cancel();
            Assert.Equal("runtime.startup_summary", Assert.Single(capture.Events).Code);
            Assert.False(capture.Events[0].Diagnostic);
            Assert.Equal(candidate ? "Info" : "Information", capture.Events[0].Level);
            var properties = Properties(capture);
            Assert.Equal(host, properties["startupHost"]);
            Assert.Equal(environment.ToLowerInvariant(), properties["startupEnvironment"]);
            Assert.Equal("4", properties["listenEndpointCount"]);
            Assert.Equal("2", properties["corsOriginCount"]);
            if (host == "gateway")
            {
                Assert.Equal("3", properties["healthTargetCount"]);
                Assert.Equal("enabled", properties["startupHttpsRedirection"]);
                Assert.Equal("removed", properties["startupDelegation"]);
                Assert.False(properties.ContainsKey("startupAuthentication"));
            }
            else Assert.Equal(host == "auth" ? "file-certificates" : "local-certificate", properties["startupAuthentication"]);
            Assert.False(properties.ContainsKey("failureKind"));
            if (candidate)
            {
                using var json = JsonDocument.Parse(capture.Output.ToString());
                Assert.Equal(activity.TraceId.ToHexString(), json.RootElement.GetProperty("traceId").GetString());
                Assert.Equal(activity.SpanId.ToHexString(), json.RootElement.GetProperty("spanId").GetString());
            }
            else
            {
                Assert.Equal(activity.TraceId, Assert.Single(capture.Sink.Events).TraceId);
                Assert.Equal(activity.SpanId, Assert.Single(capture.Sink.Events).SpanId);
            }
            capture.AssertSafe();
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void FailedOrDisposedStartup_ShouldNotClaimCompletion_AndDeferredAuthIsExplicit(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using (var failed = new StartupTestLifetime())
        {
            using var registration = HostStartupLog.Auth(environment, 0).Register(failed, new CountOnlyAddresses(), capture.Logger);
            // A failed host does not signal ApplicationStarted. No new host or listener is started here.
            failed.Stopping.Cancel();
            failed.Stopped.Cancel();
            Assert.Empty(capture.Events);
        }
        using (var disposed = new StartupTestLifetime())
        {
            var registration = HostStartupLog.Auth(environment, 0).Register(disposed, new CountOnlyAddresses(), capture.Logger);
            registration.Dispose();
            disposed.Started.Cancel();
            Assert.Empty(capture.Events);
        }
        foreach (var initialize in new[] { false, true })
        {
            using var lifetime = new StartupTestLifetime();
            var startup = HostStartupLog.Auth(environment, 0);
            using var registration = startup.Register(lifetime, new CountOnlyAddresses(), capture.Logger);
            if (initialize) startup.AuthConfigurationCompleted(true);
            lifetime.Started.Cancel();
            Assert.Equal(initialize ? "development-keys" : "not-initialized", Properties(capture)["startupAuthentication"]);
            startup.AuthConfigurationCompleted(false); // Later options initialization does not emit a second startup summary.
            Assert.Single(capture.Events);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void Snapshot_ShouldNotRereadFailingConfigurationOrResolveInvalidCertificatePaths(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        var provider = new FailingStartupConfiguration();
        var configuration = new ConfigurationRoot(new List<IConfigurationProvider> { provider });
        using (configuration)
        {
            Assert.False(ApiJwtRuntimeProfile.IsDeploymentJwtValidationEnabled(configuration));
            var api = HostStartupLog.Api(environment, 1, localSigningCertificateLoaded: false);
            var auth = HostStartupLog.Auth(environment, 1);
            auth.AuthConfigurationCompleted(true);
            configuration["OpenIddict:Encryption:SigningCertificatePath"] = "\0" + HttpBoundaryCapture.Secret;
            provider.Fail = true;
            Assert.Throws<IOException>(() => configuration["OpenIddict:Server:Issuer"]);
            var reads = provider.Reads;
            foreach (var startup in new[] { api, auth })
            {
                using var lifetime = new StartupTestLifetime();
                using var registration = startup.Register(lifetime, new CountOnlyAddresses(), capture.Logger);
                lifetime.Started.Cancel();
                Assert.Single(capture.Events);
                Assert.Equal(startup == api ? "authority" : "development-keys", Properties(capture)["startupAuthentication"]);
                Assert.Equal(reads, provider.Reads);
                capture.AssertSafe();
                capture.Clear();
            }
        }
    }

    [Theory]
    [InlineData("Staging", "staging")]
    [InlineData(HttpBoundaryCapture.Secret, "other")]
    public void EnvironmentAndGatewaySwitches_ShouldRemainClosedValues(string environment, string expected)
    {
        using var capture = new HttpBoundaryCapture(false, "Production");
        using var lifetime = new StartupTestLifetime();
        using var registration = HostStartupLog.Gateway(environment, 0, 0, false, false)
            .Register(lifetime, new CountOnlyAddresses(), capture.Logger);
        lifetime.Started.Cancel();
        Assert.Equal(expected, Properties(capture)["startupEnvironment"]);
        Assert.Equal("disabled", Properties(capture)["startupHttpsRedirection"]);
        Assert.Equal("retained", Properties(capture)["startupDelegation"]);
        capture.AssertSafe();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyConfiguredOutputs_ShouldRebuildAfterScopesAndPreserveRegistrationOwnership(bool audit)
    {
        using var capture = new HttpBoundaryCapture(false, "Production", audit: audit);
        using var scope = capture.SensitiveScope();
        using var lifetime = new StartupTestLifetime();
        var registration = HostStartupLog.Api("Production", 0, false).Register(lifetime, new CountOnlyAddresses(), capture.Logger);
        lifetime.Started.Cancel();
        registration.Dispose();
        Assert.Equal("runtime.startup_summary", Assert.Single(capture.Events).Code);
        Assert.Equal(0, capture.Sink.Disposals);
        capture.AssertSafe();
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void UnknownStartupEvents_ShouldRemainSafeAndKeepExistingSeverityGate(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, diagnostics: false);
        foreach (var level in new[] { LogEventLevel.Verbose, LogEventLevel.Debug, LogEventLevel.Information, LogEventLevel.Warning, LogEventLevel.Error, LogEventLevel.Fatal })
        {
            capture.Logger.ForContext("SourceContext", HostStartupLog.Source).ForContext("EventCode", HttpBoundaryCapture.Secret)
                .Write(level, new StartupUnreadableException(), "Private {Value}", HttpBoundaryCapture.Secret);
            if (candidate && level < LogEventLevel.Warning) Assert.Empty(capture.Events);
            else
            {
                Assert.Equal("runtime.startup_unknown", Assert.Single(capture.Events).Code);
                Assert.Equal(level < LogEventLevel.Warning, capture.Events[0].Diagnostic);
            }
            capture.AssertSafe();
            capture.Clear();
        }
    }

    [Fact]
    public void Sink_ShouldDropWrongTypesCrossHostPropertiesAndRawPayloadWithoutRendering()
    {
        var captured = new HttpBoundaryTestSink();
        var sink = new HostStartupLogSink(captured);
        var timestamp = DateTimeOffset.UtcNow;
        var trace = ActivityTraceId.CreateRandom();
        var span = ActivitySpanId.CreateRandom();
        var parser = new MessageTemplateParser();
        foreach (var host in new[] { "auth", "api", "gateway", HttpBoundaryCapture.Secret })
        foreach (var invalid in new LogEventPropertyValue[] { new ScalarValue(-1), new ScalarValue(1L), new ScalarValue(1.5), new ScalarValue("1"), new ScalarValue(new StartupPoisonValue()) })
        {
            sink.Emit(new LogEvent(timestamp, LogEventLevel.Error, new StartupUnreadableException(), parser.Parse("Private {payload}"),
            [
                new("SourceContext", new ScalarValue(HostStartupLog.Source)), new("EventCode", new ScalarValue("runtime.startup_summary")),
                new("startupHost", new ScalarValue(host)), new("startupEnvironment", new ScalarValue(new StartupPoisonValue())),
                new("startupAuthentication", new ScalarValue(host == "auth" ? "authority" : "file-certificates")),
                new("startupDelegation", new ScalarValue("retained")), new("startupHttpsRedirection", new ScalarValue("enabled")),
                new("healthTargetCount", invalid), new("listenEndpointCount", invalid), new("corsOriginCount", invalid),
                new("payload", new ScalarValue(new StartupPoisonValue())), new("traceId", new ScalarValue(HttpBoundaryCapture.Secret)),
                new("Diagnostic", new ScalarValue(true))
            ], trace, span));
            var result = captured.Events.Last();
            Assert.Equal(timestamp, result.Timestamp);
            Assert.Equal(LogEventLevel.Error, result.Level);
            Assert.Equal(trace, result.TraceId);
            Assert.Equal(span, result.SpanId);
            Assert.Null(result.Exception);
            foreach (var key in new[] { "startupEnvironment", "startupAuthentication", "healthTargetCount", "listenEndpointCount", "corsOriginCount", "payload", "traceId" })
                Assert.False(result.Properties.ContainsKey(key));
            Assert.Equal(host == "gateway", result.Properties.ContainsKey("startupDelegation"));
            Assert.DoesNotContain(HttpBoundaryCapture.Secret, result.RenderMessage() + string.Join(" ", result.Properties.Values));
        }
        var adjacent = new LogEvent(timestamp, LogEventLevel.Information, null, parser.Parse("Unrelated"),
            [new("SourceContext", new ScalarValue(HostStartupLog.Source + ".Other"))]);
        sink.Emit(adjacent);
        Assert.Same(adjacent, captured.Events.Last());
    }

    [Fact]
    public void CandidateLifecycle_ShouldRemainSeparateFromSummary()
    {
        using var output = new StringWriter();
        using var emergency = new StringWriter();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RadishLogging:Enabled"] = "true", ["RadishLogging:Mode"] = "Production"
        }).Build();
        using var session = new RuntimeLoggingSession(configuration, "Production", "api", output, emergency);
        using var lifetime = new StartupTestLifetime();
        session.AttachLifetime(lifetime);
        using var registration = HostStartupLog.Api("Production", 1, false).Register(lifetime, new CountOnlyAddresses(), session.Logger!);
        lifetime.Started.Cancel();
        lifetime.Stopped.Cancel();
        var codes = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line =>
        {
            using var json = JsonDocument.Parse(line);
            return json.RootElement.GetProperty("eventCode").GetString();
        }).OrderBy(code => code).ToArray();
        Assert.Equal(new[] { "runtime.started", "runtime.startup_summary", "runtime.stopped" }, codes);
        Assert.Equal("", emergency.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StartupOutputFailure_ShouldPreserveWriteConsumptionAndAuditPropagation(bool audit)
    {
        var sink = new StartupFailingSink();
        var configuration = new LoggerConfiguration();
        if (audit) configuration.AuditTo.Sink(new HostStartupLogSink(sink));
        else configuration.WriteTo.Sink(new HostStartupLogSink(sink));
        using var logger = configuration.CreateLogger();
        using var lifetime = new StartupTestLifetime();
        using var registration = HostStartupLog.Api("Production", 0, false)
            .Register(lifetime, new CountOnlyAddresses(), logger);
        if (audit) Assert.Contains(sink.Failure, Assert.Throws<AggregateException>(() => lifetime.Started.Cancel()).Flatten().InnerExceptions);
        else lifetime.Started.Cancel();
        Assert.Equal(1, sink.Calls);
    }

    [Fact]
    public void CandidateOutputFailure_ShouldUseExistingIndependentEmergencyChannel()
    {
        using var emergency = new StringWriter();
        using var output = new StartupBrokenWriter();
        var configuration = new LoggerConfiguration();
        RuntimeLoggingConfiguration.Configure(configuration, new ConfigurationBuilder().Build(), "Production", "api", output, emergency);
        using var logger = configuration.CreateLogger();
        using var lifetime = new StartupTestLifetime();
        using var registration = HostStartupLog.Api("Production", 0, false)
            .Register(lifetime, new CountOnlyAddresses(), logger);
        lifetime.Started.Cancel();
        using var json = JsonDocument.Parse(emergency.ToString());
        Assert.Equal("pipeline.output_failed", json.RootElement.GetProperty("eventCode").GetString());
        Assert.DoesNotContain(HttpBoundaryCapture.Secret, emergency.ToString());
    }

    private static Dictionary<string, string?> Properties(HttpBoundaryCapture capture)
    {
        if (!capture.Candidate) return Assert.Single(capture.Sink.Events).Properties.ToDictionary(p => p.Key,
            p => p.Value is ScalarValue scalar ? scalar.Value?.ToString() : null);
        using var json = JsonDocument.Parse(capture.Output.ToString());
        return json.RootElement.GetProperty("properties").EnumerateObject().ToDictionary(p => p.Name,
            p => p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.GetRawText());
    }
}

internal sealed class StartupTestLifetime : IHostApplicationLifetime, IDisposable
{
    public readonly CancellationTokenSource Started = new();
    public readonly CancellationTokenSource Stopping = new();
    public readonly CancellationTokenSource Stopped = new();
    public CancellationToken ApplicationStarted => Started.Token;
    public CancellationToken ApplicationStopping => Stopping.Token;
    public CancellationToken ApplicationStopped => Stopped.Token;
    public void StopApplication() => Stopping.Cancel();
    public void Dispose() { Started.Dispose(); Stopping.Dispose(); Stopped.Dispose(); }
}

internal sealed class CountOnlyAddresses : ICollection<string>
{
    public int Count { get; set; }
    public bool IsReadOnly => true;
    public IEnumerator<string> GetEnumerator() => throw new InvalidOperationException("Address content was read");
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public void Add(string item) => throw new NotSupportedException();
    public void Clear() => throw new NotSupportedException();
    public bool Contains(string item) => throw new NotSupportedException();
    public void CopyTo(string[] array, int arrayIndex) => throw new NotSupportedException();
    public bool Remove(string item) => throw new NotSupportedException();
}

internal sealed class FailingStartupConfiguration : ConfigurationProvider
{
    public bool Fail;
    public int Reads;
    public override bool TryGet(string key, out string? value)
    {
        Reads++;
        if (Fail) throw new IOException(HttpBoundaryCapture.Secret);
        return base.TryGet(key, out value);
    }
}

internal sealed class StartupUnreadableException : Exception
{
    public override string Message => throw new InvalidOperationException("Exception message read");
    public override string ToString() => throw new InvalidOperationException("Exception formatted");
}

internal sealed class StartupPoisonValue
{
    public override string ToString() => throw new InvalidOperationException("Payload formatted");
}

internal sealed class StartupFailingSink : ILogEventSink
{
    public readonly IOException Failure = new(HttpBoundaryCapture.Secret);
    public int Calls;
    public void Emit(LogEvent logEvent)
    {
        Calls++;
        Assert.Equal("runtime.startup_summary", ((ScalarValue)logEvent.Properties["EventCode"]).Value);
        throw Failure;
    }
}

internal sealed class StartupBrokenWriter : StringWriter
{
    public override void WriteLine(string? value) => throw new IOException(HttpBoundaryCapture.Secret);
}
