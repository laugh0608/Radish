using System;
using System.Collections.Generic;
using System.IO;
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
public sealed class OpenIddictLoggerFactoryTests
{
    [Fact]
    public void Wrapper_ShouldDelegateNonTargetsScopesLevelsProvidersAndSingleDisposal()
    {
        var inner = new RecordingFactory();
        var services = new ServiceCollection();
        var creates = 0;
        services.AddSingleton<ILoggerFactory>(_ => { creates++; return inner; });
        services.ProtectOpenIddictServerLogging();
        using (var provider = services.BuildServiceProvider())
        {
            var factory = provider.GetRequiredService<ILoggerFactory>();
            Assert.IsType<OpenIddictLoggerFactory>(factory);
            Assert.Same(factory, provider.GetRequiredService<ILoggerFactory>());
            Assert.Equal(1, creates);
            foreach (var source in new[] { "other", OpenIddictEvents.Server + ".Custom", "OpenIddict.Core.OpenIddictTokenManager", HealthFrameworkEvents.Source })
                Assert.Same(inner.Logger, factory.CreateLogger(source));
            foreach (var source in new[] { OpenIddictEvents.Server, OpenIddictEvents.Handler })
            {
                var logger = factory.CreateLogger(source);
                Assert.NotSame(inner.Logger, logger);
                var state = new object();
                Assert.Same(inner.Logger.Scope, logger.BeginScope(state));
                Assert.Same(state, inner.Logger.ScopeState);
                Assert.False(logger.IsEnabled(LogLevel.Error));
                logger.Log(LogLevel.Error, default, new HealthPoisonState(), new HealthUnreadableException(), (_, _) => throw new InvalidOperationException());
            }
            Assert.Equal(0, inner.Logger.Calls);
            var providerToAdd = new EmptyProvider();
            factory.AddProvider(providerToAdd);
            Assert.Same(providerToAdd, inner.Provider);
            factory.Dispose(); factory.Dispose();
        }
        Assert.Equal(1, inner.Disposals);
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().ProtectOpenIddictServerLogging());
        var invalid = new ServiceCollection();
        invalid.AddSingleton<ILoggerFactory>(inner);
        Assert.Throws<InvalidOperationException>(() => invalid.ProtectOpenIddictServerLogging());
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void SharedHostBuildAndDispose_ShouldProtectServerAndPreserveLoggerOwnership(bool candidate, string environment)
    {
        var key = Guid.NewGuid().ToString("N");
        var sink = new HttpBoundaryTestSink();
        HttpBoundaryTestSinkExtensions.Sinks[key] = sink;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
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
            AppSettingsTool.Configuration = configuration;
            using var session = new RuntimeLoggingSession(configuration, environment, "auth", output, emergency);
            var host = new HostBuilder().ConfigureServices(services => services.Configure<SerilogOptions>(options =>
            {
                options.MinimumLevel = "Verbose"; options.Console.Enable = false; options.File.Enable = false; options.Database.Enable = false;
            })).AddSerilogSetup(session).Build();
            using (host)
            {
                var factory = host.Services.GetRequiredService<ILoggerFactory>();
                Assert.IsType<HealthCheckLoggerFactory>(factory); // 原健康保护仍在同一注册链上。
                foreach (var source in new[] { OpenIddictEvents.Server, OpenIddictEvents.Handler })
                    factory.CreateLogger(source).Log(LogLevel.Error, new EventId(9999), new HealthPoisonState(), new HealthUnreadableException(), (_, _) => throw new InvalidOperationException());
            }
            if (candidate)
            {
                Assert.Contains("oidc.server.unknown", output.ToString());
                Assert.Contains("oidc.handler.unknown", output.ToString());
                Assert.Equal("", emergency.ToString());
                session.Logger!.ForContext("EventCode", "runtime.started").Information("After host disposal");
                Assert.Contains("runtime.started", output.ToString());
            }
            else
            {
                Assert.Equal(2, sink.Events.Count);
                Assert.Equal(1, sink.Disposals); // 旧链仍随宿主释放根 logger 和它拥有的 sink。
            }
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
    private sealed class EmptyProvider : ILoggerProvider
    {
        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => throw new NotSupportedException();
        public void Dispose() { }
    }
    private sealed class RecordingLogger : Microsoft.Extensions.Logging.ILogger
    {
        internal int Calls;
        internal object? ScopeState;
        internal readonly IDisposable Scope = new EmptyProvider();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull { ScopeState = state; return Scope; }
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Calls++;
    }
}
