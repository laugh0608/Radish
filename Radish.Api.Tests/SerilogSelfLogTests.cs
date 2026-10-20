using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Radish.Common;
using Radish.Common.LogTool;
using Radish.Common.OptionTool;
using Radish.Extension.Log;
using Serilog;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class SerilogSelfLogTests
{
    internal const string Secret = "SELFLOG_PRIVATE_SENTINEL";
    private static readonly RuntimeLogSource Source = new("fixture", "api", "fixture", "fixture");
    public static TheoryData<bool, string> Modes => new()
    {
        { false, "Production" }, { true, "Production" }, { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(Modes))]
    public void Session_ShouldCaptureBeforeConfigurationRead_IndependentOfMainLogFilters(bool candidate, string environment)
    {
        using var output = new StringWriter();
        using var emergency = new StringWriter();
        var config = Configuration(candidate, environment);
        var wrapped = new Mock<IConfiguration>();
        wrapped.Setup(c => c.GetSection(It.IsAny<string>())).Returns((string key) =>
        {
            if (key == "RadishLogging:Enabled") SelfLog.WriteLine("{0}\n{1}", Secret, new IOException(Secret));
            return config.GetSection(key);
        });
        using (var session = new RuntimeLoggingSession(wrapped.Object, environment, "auth", output, emergency))
        {
            Assert.Equal(candidate, session.Logger != null);
            AssertSummary(Assert.Single(Lines(emergency)), 1, environment, "auth");
            using var summary = JsonDocument.Parse(Lines(emergency)[0]);
            Assert.Equal("bootstrap", summary.RootElement.GetProperty("deploymentId").GetString());
            Assert.Equal("process-" + Environment.ProcessId, summary.RootElement.GetProperty("instanceId").GetString());
            Assert.Equal("unversioned", summary.RootElement.GetProperty("release").GetString());
            Assert.Equal("", output.ToString());
        }
        var finished = emergency.ToString();
        SelfLog.WriteLine(Secret);
        Assert.Equal(finished, emergency.ToString());
    }

    [Fact]
    public void ConstructorFailure_ShouldPreserveOriginalExceptionAndRestoreActiveReceiver()
    {
        using var outerOutput = new StringWriter();
        using var failedOutput = new StringWriter();
        using var outer = new RuntimeLoggingSession(Configuration(false, "Production"), "Production", "api", emergency: outerOutput);
        var failure = new InvalidOperationException(Secret);
        var config = new Mock<IConfiguration>();
        config.Setup(c => c.GetSection("RadishLogging:Enabled")).Callback(() => SelfLog.WriteLine(Secret)).Throws(failure);
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() =>
            new RuntimeLoggingSession(config.Object, "Production", "auth", emergency: failedOutput)));
        SelfLog.WriteLine(Secret);
        AssertSummary(Assert.Single(Lines(failedOutput)), 1, "Production", "auth");
        AssertSummary(Assert.Single(Lines(outerOutput)), 1, "Production", "api");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void HostBuildAndDispose_ShouldRetainReceiverWithoutCreatingSelfLogFiles(bool candidate, string environment)
    {
        using var output = new StringWriter();
        using var emergency = new StringWriter();
        var config = Configuration(candidate, environment);
        var previousConfig = AppSettingsTool.Configuration;
        var previousLogger = Log.Logger;
        var debugPath = Path.Combine(LogContextTool.BaseLogs, LogContextTool.ProjectName, LogContextTool.SerilogDebug);
        var before = Snapshot(debugPath);
        try
        {
            AppSettingsTool.Configuration = config;
            using (var session = new RuntimeLoggingSession(config, environment, "gateway", output, emergency))
            {
                using (var host = new HostBuilder().ConfigureServices(services =>
                {
                    services.Configure<SerilogOptions>(options =>
                    {
                        options.Console.Enable = false; options.File.Enable = false; options.Database.Enable = false;
                    });
                    services.AddSingleton<DisposeDiagnostic>();
                }).AddSerilogSetup(session).Build())
                {
                    _ = host.Services.GetRequiredService<DisposeDiagnostic>();
                    Assert.Empty(Lines(emergency));
                }
                AssertSummary(Assert.Single(Lines(emergency)), 1, environment, "gateway");
            }
            SelfLog.WriteLine(Secret);
            Assert.Single(Lines(emergency));
            Assert.Equal(before, Snapshot(debugPath));
        }
        finally
        {
            AppSettingsTool.Configuration = previousConfig;
            Log.Logger = previousLogger;
        }
    }

    [Theory]
    [InlineData("Production", "create")]
    [InlineData("Development", "create")]
    [InlineData("Production", "dispose")]
    [InlineData("Development", "dispose")]
    [InlineData("Production", "create-failed")]
    [InlineData("Development", "create-failed")]
    [InlineData("Production", "dispose-failed")]
    [InlineData("Development", "dispose-failed")]
    public void LegacyConfiguredSink_ShouldCaptureInitializationAndDisposalDiagnostics(string environment, string phase)
    {
        using var emergency = new StringWriter();
        var config = Configuration(false, environment);
        config["Serilog:Using:0"] = "Radish.Api.Tests";
        config["Serilog:WriteTo:0:Name"] = "SelfLogLifecycleProbe";
        config["Serilog:WriteTo:0:Args:phase"] = phase;
        var previousConfig = AppSettingsTool.Configuration;
        var previousLogger = Log.Logger;
        try
        {
            AppSettingsTool.Configuration = config;
            using (var session = new RuntimeLoggingSession(config, environment, "api", emergency: emergency))
            {
                var builder = new HostBuilder().ConfigureServices(services => services.Configure<SerilogOptions>(options =>
                {
                    options.Console.Enable = false; options.File.Enable = false; options.Database.Enable = false;
                })).AddSerilogSetup(session);
                if (phase == "create-failed")
                {
                    var failure = Record.Exception(() => builder.Build());
                    Assert.NotNull(failure);
                    Assert.Contains(Secret, failure.ToString());
                }
                else if (phase == "dispose-failed")
                {
                    var host = builder.Build();
                    var failure = Record.Exception(host.Dispose);
                    Assert.NotNull(failure);
                    Assert.Contains(Secret, failure.ToString());
                }
                else
                {
                    using var host = builder.Build();
                }
                AssertSummary(Assert.Single(Lines(emergency)), 1, environment, "api");
            }
            SelfLog.WriteLine(Secret);
            Assert.Single(Lines(emergency));
        }
        finally
        {
            AppSettingsTool.Configuration = previousConfig;
            Log.Logger = previousLogger;
        }
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void WriteToAndAuditTo_ShouldKeepOriginalFailureSemantics(bool candidate, string environment)
    {
        using var emergency = new StringWriter();
        using var session = new RuntimeLoggingSession(Configuration(candidate, environment), environment, "api", TextWriter.Null, emergency);
        var failure = new IOException(Secret);
        using (var logger = new LoggerConfiguration().WriteTo.Sink(new FailingSink(failure)).CreateLogger())
            logger.Error("{Secret}", Secret); // WriteTo 消费 sink 异常并发出 SelfLog。
        AssertSummary(Assert.Single(Lines(emergency)), 1, environment, "api");
        using var audit = new LoggerConfiguration().AuditTo.Sink(new FailingSink(failure)).CreateLogger();
        var exception = Assert.Throws<AggregateException>(() => audit.Error("{Secret}", Secret));
        Assert.Contains(failure, exception.InnerExceptions);
        Assert.DoesNotContain(Secret, emergency.ToString());
    }

    [Fact]
    public void ActualOutputFailure_ShouldRemainErrorAndNotSuppressSelfDiagnostic()
    {
        using var emergency = new StringWriter();
        var config = Configuration(true, "Production");
        using var session = new RuntimeLoggingSession(config, "Production", "api", new BrokenWriter(), emergency);
        session.Logger!.Error("{Secret}", Secret);
        SelfLog.WriteLine(Secret);
        var lines = Lines(emergency);
        Assert.Equal(2, lines.Length);
        using var failure = JsonDocument.Parse(lines[0]);
        Assert.Equal("pipeline.output_failed", failure.RootElement.GetProperty("eventCode").GetString());
        Assert.Equal("Error", failure.RootElement.GetProperty("level").GetString());
        AssertSummary(lines[1], 1, "Production", "api");
        Assert.DoesNotContain(Secret, emergency.ToString());
    }

    [Fact]
    public void ConcurrentDiagnostics_ShouldBeBoundedAndAccumulateAcrossMinuteBoundary()
    {
        using var output = new StringWriter();
        var clock = new Clock();
        using var session = new SerilogSelfLogSession(Source, "Production", output, clock);
        Parallel.For(0, 200, _ => SelfLog.WriteLine(Secret));
        AssertSummary(Assert.Single(Lines(output)), 1, "Production", "api");
        clock.Timestamp = 59;
        SelfLog.WriteLine(Secret);
        Assert.Single(Lines(output));
        clock.Timestamp = 60;
        SelfLog.WriteLine(Secret);
        AssertSummary(Lines(output)[1], 201, "Production", "api");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmergencyFailure_ShouldRetainCountWithoutRecursiveOutput(bool failFlush)
    {
        var clock = new Clock();
        using var output = new RecoverableWriter { FailWrite = !failFlush, FailFlush = failFlush };
        using var session = new SerilogSelfLogSession(Source, "Production", output, clock);
        SelfLog.WriteLine(Secret);
        SelfLog.WriteLine(Secret);
        Assert.Equal(1, output.Attempts);
        output.FailWrite = output.FailFlush = false;
        clock.Timestamp = 60;
        SelfLog.WriteLine(Secret);
        Assert.Equal(2, output.Attempts);
        AssertSummary(Lines(output).Last(), 3, "Production", "api");
        Assert.DoesNotContain(Secret, output.ToString());
    }

    [Fact]
    public void ReentrantWriter_ShouldNotRecurseEvenIfClockAdvancesDuringWrite()
    {
        var clock = new Clock();
        using var output = new RecoverableWriter();
        output.DuringWrite = () => { clock.Timestamp += 60; SelfLog.WriteLine(Secret); };
        using var session = new SerilogSelfLogSession(Source, "Production", output, clock);
        SelfLog.WriteLine(Secret);
        Assert.Equal(1, output.Attempts);
        output.DuringWrite = null;
        SelfLog.WriteLine(Secret);
        Assert.Equal(2, output.Attempts);
        AssertSummary(Lines(output)[1], 2, "Production", "api");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void OverlappingSessions_ShouldNotDisableNewerReceiverOrRestoreDisposedReceiver(bool candidate, bool disposeOlderFirst)
    {
        using var olderOutput = new StringWriter();
        using var newerOutput = new StringWriter();
        var config = Configuration(candidate, "Production");
        var previousLogger = Log.Logger;
        try
        {
            using var older = new RuntimeLoggingSession(config, "Production", "api", emergency: olderOutput);
            using var newer = new RuntimeLoggingSession(config, "Production", "auth", emergency: newerOutput);
            if (disposeOlderFirst)
            {
                older.Dispose();
                SelfLog.WriteLine(Secret);
                AssertSummary(Assert.Single(Lines(newerOutput)), 1, "Production", "auth");
                newer.Dispose();
                SelfLog.WriteLine(Secret);
                Assert.Empty(Lines(olderOutput));
                Assert.Single(Lines(newerOutput));
            }
            else
            {
                newer.Dispose();
                SelfLog.WriteLine(Secret);
                AssertSummary(Assert.Single(Lines(olderOutput)), 1, "Production", "api");
                Assert.Empty(Lines(newerOutput));
            }
        }
        finally
        {
            // 此处仅验证 SelfLog 注册的交错释放；业务全局 logger 仍按单宿主作用域使用。
            Log.Logger = previousLogger;
        }
    }

    private static IConfigurationRoot Configuration(bool candidate, string environment) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RadishLogging:Enabled"] = candidate.ToString(), ["RadishLogging:Mode"] = environment,
            ["RadishLogging:Diagnostics"] = "false", ["RadishLogging:MinimumLevel"] = "Error",
            ["Serilog:MinimumLevel"] = "Fatal"
        }).Build();

    private static string[] Lines(StringWriter writer) => writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static void AssertSummary(string line, int count, string mode, string service)
    {
        using var doc = JsonDocument.Parse(line);
        var value = doc.RootElement;
        Assert.Equal("pipeline.self_diagnostic", value.GetProperty("eventCode").GetString());
        Assert.Equal("Warning", value.GetProperty("level").GetString());
        Assert.Equal("pipeline", value.GetProperty("sourceCategory").GetString());
        Assert.Equal(mode, value.GetProperty("mode").GetString());
        Assert.Equal(service, value.GetProperty("service").GetString());
        Assert.False(value.GetProperty("diagnostic").GetBoolean());
        Assert.Single(value.GetProperty("properties").EnumerateObject());
        Assert.Equal(count, value.GetProperty("properties").GetProperty("count").GetInt32());
        Assert.DoesNotContain(Secret, line);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(line) <= RuntimeLogPolicy.MaxEventBytes);
    }

    private static string[] Snapshot(string directory) => Directory.Exists(directory)
        ? Directory.GetFiles(directory).OrderBy(p => p).Select(p => $"{p}:{new FileInfo(p).Length}:{File.GetLastWriteTimeUtc(p).Ticks}").ToArray()
        : [];

    private sealed class DisposeDiagnostic : IDisposable
    {
        public void Dispose() => SelfLog.WriteLine(Secret);
    }

    private sealed class FailingSink(Exception failure) : ILogEventSink
    {
        public void Emit(LogEvent logEvent) => throw failure;
    }

    private sealed class BrokenWriter : StringWriter
    {
        public override void WriteLine(string? value) => throw new IOException(Secret);
    }

    private sealed class Clock : TimeProvider
    {
        public long Timestamp;
        public override long TimestampFrequency => 1;
        public override long GetTimestamp() => Timestamp;
    }

    private sealed class RecoverableWriter : StringWriter
    {
        public int Attempts;
        public bool FailWrite;
        public bool FailFlush;
        public Action? DuringWrite;
        public override void WriteLine(string? value)
        {
            Attempts++;
            DuringWrite?.Invoke();
            if (FailWrite) throw new IOException(Secret);
            base.WriteLine(value);
        }
        public override void Flush()
        {
            if (FailFlush) throw new IOException(Secret);
            base.Flush();
        }
    }
}

public static class SelfLogLifecycleProbeExtensions
{
    public static LoggerConfiguration SelfLogLifecycleProbe(this Serilog.Configuration.LoggerSinkConfiguration sinks, string phase)
    {
        if (phase is "create" or "create-failed") SelfLog.WriteLine(SerilogSelfLogTests.Secret);
        if (phase == "create-failed") throw new InvalidOperationException(SerilogSelfLogTests.Secret);
        return sinks.Sink(new Probe(phase));
    }

    private sealed class Probe(string phase) : ILogEventSink, IDisposable
    {
        public void Emit(LogEvent logEvent) { }
        public void Dispose()
        {
            if (phase is "dispose" or "dispose-failed") SelfLog.WriteLine(SerilogSelfLogTests.Secret);
            if (phase == "dispose-failed") throw new IOException(SerilogSelfLogTests.Secret);
        }
    }
}
