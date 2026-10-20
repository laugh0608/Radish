using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Radish.Common;
using Radish.Common.OptionTool;
using Radish.Extension.Log;
using Serilog;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class HealthLoggerFactoryTests
{
    [Fact]
    public void Factory_ShouldPreserveOtherCategoriesScopesProvidersAndOwnership()
    {
        var inner = new RecordingFactory();
        var factory = new HealthCheckLoggerFactory(inner);
        Assert.Same(inner.Logger, factory.CreateLogger("other"));
        Assert.Same(inner.Logger, factory.CreateLogger(HealthFrameworkEvents.Source + ".Custom"));
        var health = factory.CreateLogger(HealthFrameworkEvents.Source);
        Assert.NotSame(inner.Logger, health);
        var scope = new object();
        Assert.Same(inner.Logger.Scope, health.BeginScope(scope));
        Assert.Same(scope, inner.Logger.ScopeState);
        inner.Logger.Enabled = false;
        Assert.False(health.IsEnabled(LogLevel.Error));
        health.Log(LogLevel.Error, new EventId(105, "HealthCheckData"), new HealthPoisonState(), new HealthUnreadableException(), (_, _) => throw new InvalidOperationException());
        Assert.Equal(0, inner.Logger.Calls);
        var provider = new RecordingProvider();
        factory.AddProvider(provider);
        Assert.Same(provider, inner.Provider);
        factory.Dispose(); factory.Dispose();
        Assert.Equal(1, inner.Disposals);
    }

    [Fact]
    public void Registration_ShouldRetainOriginalFactoryCreationAndDisposeExactlyOnce()
    {
        var services = new ServiceCollection();
        var inner = new RecordingFactory();
        var creates = 0;
        services.AddSingleton<ILoggerFactory>(_ => { creates++; return inner; });
        services.ProtectHealthCheckLogging();
        using (var provider = services.BuildServiceProvider())
        {
            var factory = provider.GetRequiredService<ILoggerFactory>();
            Assert.IsType<HealthCheckLoggerFactory>(factory);
            Assert.Same(factory, provider.GetRequiredService<ILoggerFactory>());
            Assert.Equal(1, creates);
        }
        Assert.Equal(1, inner.Disposals);
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().ProtectHealthCheckLogging());
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void SharedHostRegistration_ShouldProtectBothBranchesWithoutStartingHost(bool candidate, string environment)
    {
        var key = Guid.NewGuid().ToString("N");
        var sink = new HttpBoundaryTestSink();
        HttpBoundaryTestSinkExtensions.Sinks[key] = sink;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RadishLogging:Enabled"] = candidate.ToString(), ["RadishLogging:Mode"] = environment,
            ["RadishLogging:Diagnostics"] = (environment == "Development").ToString(),
            ["Serilog:Using:0"] = "Radish.Api.Tests", ["Serilog:WriteTo:0:Name"] = "HttpBoundaryCapture", ["Serilog:WriteTo:0:Args:key"] = key
        }).Build();
        var previous = Log.Logger;
        var previousConfig = AppSettingsTool.Configuration;
        using var output = new StringWriter();
        using var emergency = new StringWriter();
        try
        {
            AppSettingsTool.Configuration = config;
            using var session = new RuntimeLoggingSession(config, environment, "api", output, emergency);
            var host = new HostBuilder().ConfigureServices(services => services.Configure<SerilogOptions>(options =>
            {
                options.MinimumLevel = "Verbose"; options.Console.Enable = false; options.File.Enable = false; options.Database.Enable = false;
            })).AddSerilogSetup(session).Build();
            using (host)
            {
                var factory = host.Services.GetRequiredService<ILoggerFactory>();
                Assert.IsType<HealthCheckLoggerFactory>(factory);
                factory.CreateLogger(HealthFrameworkEvents.Source).Log(LogLevel.Error, new EventId(105, "HealthCheckData"), new HealthPoisonState(),
                    new HealthUnreadableException(), (_, _) => throw new InvalidOperationException());
            }
            if (candidate)
            {
                Assert.Contains("health.check_data", output.ToString());
                Assert.Equal("", emergency.ToString());
                // dispose:false 的候选 logger 仍由 session 拥有，宿主释放后还可写入。
                session.Logger!.ForContext("EventCode", "runtime.started").Information("After host disposal");
                Assert.Contains("runtime.started", output.ToString());
            }
            else Assert.Equal("health.check_data", ((Serilog.Events.ScalarValue)Assert.Single(sink.Events).Properties["EventCode"]).Value);
            Assert.DoesNotContain(HttpBoundaryCapture.Secret, output.ToString());
        }
        finally
        {
            Serilog.Debugging.SelfLog.Disable();
            Log.Logger = previous;
            AppSettingsTool.Configuration = previousConfig;
            HttpBoundaryTestSinkExtensions.Sinks.TryRemove(key, out _);
        }
    }

    private sealed class RecordingFactory : ILoggerFactory
    {
        internal readonly RecordingLogger Logger = new();
        internal int Disposals;
        internal ILoggerProvider? Provider;
        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => Logger;
        public void AddProvider(ILoggerProvider provider) => Provider = provider;
        public void Dispose() => Disposals++;
    }
    private sealed class RecordingProvider : ILoggerProvider
    {
        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => throw new NotSupportedException();
        public void Dispose() { }
    }
    private sealed class RecordingLogger : Microsoft.Extensions.Logging.ILogger
    {
        internal bool Enabled = true;
        internal int Calls;
        internal object? ScopeState;
        internal readonly IDisposable Scope = new RecordingProvider();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull { ScopeState = state; return Scope; }
        public bool IsEnabled(LogLevel logLevel) => Enabled;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Calls++;
    }
}
