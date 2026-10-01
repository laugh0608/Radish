using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Radish.Common.LogTool;
using Radish.Extension.Log;
using Serilog;
using Serilog.Events;
using Xunit;

namespace Radish.Api.Tests;

[CollectionDefinition("Runtime logging global state", DisableParallelization = true)]
public class RuntimeLoggingCollection;

[Collection("Runtime logging global state")]
public class RuntimeLoggingAdapterTests
{
    private static readonly RuntimeLogSource Source = new("adapter-test", "api", "fixture", "test-latest");
    private const string Secret = "ADAPTER_PRIVATE_SENTINEL";
    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(x => x.Key, x => (string?)x.Value)).Build();

    [Fact]
    public void Serilog_ShouldNeverRenderPayloadOrException_AndPreserveSeverityAndTrace()
    {
        using var stream = new StringWriter();
        using var emergency = new StringWriter();
        var configuration = new LoggerConfiguration();
        RuntimeLoggingConfiguration.Configure(configuration, Configuration(), "Production", "api", stream, emergency);
        using var logger = configuration.CreateLogger();
        using var activity = new Activity("safe-test").SetIdFormat(ActivityIdFormat.W3C).Start();
        logger.ForContext("EventCode", "http.failed").ForContext("SourceCategory", "http")
            .ForContext("statusCode", 500).ForContext("body", new { password = Secret }, destructureObjects: true)
            .Fatal(new InvalidOperationException(Secret), "Bearer {Token}", Secret);
        using var json = JsonDocument.Parse(stream.ToString());
        var result = json.RootElement;
        Assert.Equal("Error", result.GetProperty("level").GetString());
        Assert.True(result.GetProperty("isFatal").GetBoolean());
        Assert.Equal("http.failed", result.GetProperty("eventCode").GetString());
        Assert.Equal(500, result.GetProperty("properties").GetProperty("statusCode").GetInt32());
        Assert.Single(result.GetProperty("properties").EnumerateObject());
        Assert.Equal(activity.TraceId.ToHexString(), result.GetProperty("traceId").GetString());
        Assert.DoesNotContain(Secret, stream.ToString() + emergency);
    }

    [Theory]
    [InlineData("Production", false, 2)]
    [InlineData("Development", false, 2)]
    [InlineData("Development", true, 5)]
    public void Diagnostics_ShouldBeExplicit_WithoutLosingUnknownWarnings(string mode, bool diagnostics, int count)
    {
        using var stream = new StringWriter();
        var configuration = new LoggerConfiguration();
        RuntimeLoggingConfiguration.Configure(configuration, Configuration(("RadishLogging:Mode", mode),
            ("RadishLogging:Diagnostics", diagnostics.ToString())), mode, "auth", stream, TextWriter.Null);
        using var logger = configuration.CreateLogger();
        logger.Debug("Debug {Secret}", Secret);
        logger.Information("Framework request {Secret}", Secret);
        logger.ForContext("EventCode", "unregistered-code").Information("Unregistered {Secret}", Secret);
        logger.Warning("Unknown warning {Secret}", Secret);
        logger.ForContext("EventCode", "runtime.started").Information("Started");
        var lines = stream.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(count, lines.Length);
        Assert.DoesNotContain(Secret, stream.ToString());
        Assert.Contains(lines, line => line.Contains("\"level\":\"Warning\""));
    }

    [Fact]
    public void Configuration_ShouldRejectDevelopmentOnPublicHost_AndValidateMinimumLevel()
    {
        Assert.Throws<ArgumentException>(() => RuntimeLoggingConfiguration.Configure(new LoggerConfiguration(),
            Configuration(("RadishLogging:Mode", "Development")), "Production", "api"));
        Assert.Throws<ArgumentException>(() => RuntimeLoggingConfiguration.Configure(new LoggerConfiguration(),
            Configuration(("RadishLogging:MinimumLevel", "Debug")), "Development", "api"));
        Assert.Throws<ArgumentException>(() => RuntimeLoggingConfiguration.Configure(new LoggerConfiguration(),
            Configuration(("RadishLogging:Diagnostics", "true")), "Production", "api"));
    }

    [Fact]
    public void OutputFailure_ShouldNotEscape_AndEmergencyIsRateLimitedWithCounts()
    {
        var clock = new Clock();
        using var emergency = new StringWriter();
        var writer = new RuntimeLogOutput(new RuntimeLogPolicy(Source), Source, new BrokenWriter(), emergency, clock: clock);
        var input = JsonSerializer.SerializeToElement(new { eventCode = "runtime.started", message = Secret });
        for (var i = 0; i < 10; i++) writer.Write(input);
        Assert.Equal(10, writer.FailureCount);
        Assert.Single(emergency.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries));
        clock.Timestamp += 60;
        writer.Write(input);
        var lines = emergency.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        using var json = JsonDocument.Parse(lines[1]);
        Assert.Equal(10, json.RootElement.GetProperty("properties").GetProperty("count").GetInt32());
        Assert.DoesNotContain(Secret, emergency.ToString());
        var bothBroken = new RuntimeLogOutput(new RuntimeLogPolicy(Source), Source, new BrokenWriter(), new BrokenWriter());
        bothBroken.Write(input);
        Assert.Equal(1, bothBroken.FailureCount);
    }

    [Fact]
    public void InvalidInput_ShouldUseEmergency_AndConcurrentWritesRemainJsonLines()
    {
        using var stream = new StringWriter();
        using var emergency = new StringWriter();
        var writer = new RuntimeLogOutput(new RuntimeLogPolicy(Source), Source, stream, emergency);
        writer.Write(JsonSerializer.SerializeToElement(new { level = Secret }));
        Assert.Equal(1, writer.FailureCount);
        var input = JsonSerializer.SerializeToElement(new { eventCode = "runtime.started" });
        System.Threading.Tasks.Parallel.For(0, 200, _ => writer.Write(input));
        var ids = stream.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line)!["eventId"].GetString()).ToArray();
        Assert.Equal(200, ids.Distinct().Count());
        Assert.DoesNotContain(Secret, emergency.ToString());
    }

    [Fact]
    public void BootstrapAndMicrosoftLogger_ShouldShareOneSink_WithoutOldDependencies()
    {
        using var stream = new StringWriter();
        using var emergency = new StringWriter();
        var config = Configuration(("RadishLogging:Enabled", "true"), ("Serilog:Database:Enable", "true"),
            ("Serilog:File:Enable", "true"), ("Serilog:MinimumLevel", "Fatal"));
        var original = Log.Logger;
        using (var session = new RuntimeLoggingSession(config, "Production", "gateway", stream, emergency))
        {
            Log.ForContext("EventCode", "runtime.started").Information("Bootstrap {Secret}", Secret);
            using var host = new HostBuilder().AddSerilogSetup(session).Build();
            var logger = host.Services.GetRequiredService<ILogger<RuntimeLoggingAdapterTests>>();
            using (logger.BeginScope(new Dictionary<string, object> { ["EventCode"] = "http.failed", ["statusCode"] = 503 }))
                logger.LogError(new Exception(Secret), "{Secret}", Secret);
            var lifetime = new Lifetime();
            session.AttachLifetime(lifetime);
            lifetime.Started.Cancel();
            lifetime.Stopped.Cancel();
            Serilog.Debugging.SelfLog.WriteLine(Secret);
        }
        Assert.Same(original, Log.Logger);
        var lines = stream.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, lines.Length);
        using var json = JsonDocument.Parse(lines[1]);
        Assert.Equal("http.failed", json.RootElement.GetProperty("eventCode").GetString());
        Assert.Equal(503, json.RootElement.GetProperty("properties").GetProperty("statusCode").GetInt32());
        using var selfLog = JsonDocument.Parse(emergency.ToString());
        Assert.Equal("pipeline.output_failed", selfLog.RootElement.GetProperty("eventCode").GetString());
        Assert.DoesNotContain(Secret, stream.ToString() + emergency);
    }

    [Fact]
    public void DisabledSession_ShouldLeaveExistingLoggerUntouched()
    {
        var original = Log.Logger;
        using var session = new RuntimeLoggingSession(Configuration(), "Production", "api");
        Assert.Null(session.Logger);
        Assert.Same(original, Log.Logger);
    }

    private sealed class Clock : TimeProvider
    {
        public long Timestamp;
        public override long TimestampFrequency => 1;
        public override long GetTimestamp() => Timestamp;
    }

    private sealed class BrokenWriter : StringWriter
    {
        public override void WriteLine(string? value) => throw new IOException(Secret);
    }

    private sealed class Lifetime : IHostApplicationLifetime
    {
        public CancellationTokenSource Started { get; } = new();
        public CancellationTokenSource Stopped { get; } = new();
        public CancellationToken ApplicationStarted => Started.Token;
        public CancellationToken ApplicationStopped => Stopped.Token;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public void StopApplication() => Stopped.Cancel();
    }
}
