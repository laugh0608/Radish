using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Radish.Extension.Log;
using Serilog;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

internal sealed record HealthFrameworkEvent(int Id, string Name, LogLevel Level, string Code, string Method);

internal static class HealthFrameworkEvents
{
    internal const string Source = "Microsoft.Extensions.Diagnostics.HealthChecks.DefaultHealthCheckService";
    internal static readonly Type LogType = Assembly.Load("Microsoft.Extensions.Diagnostics.HealthChecks").GetType(Source, true)!
        .GetNestedType("Log", BindingFlags.NonPublic)!;
    internal static readonly HealthFrameworkEvent[] All =
    [
        new(100, "HealthCheckProcessingBegin", LogLevel.Debug, "health.run_started", "HealthCheckProcessingBegin"),
        new(101, "HealthCheckProcessingEnd", LogLevel.Debug, "health.run_completed", "HealthCheckProcessingEnd"),
        new(102, "HealthCheckBegin", LogLevel.Debug, "health.check_started", "HealthCheckBegin"),
        new(103, "HealthCheckEnd", LogLevel.Debug, "health.check_completed", "HealthCheckEndHealthy"),
        new(103, "HealthCheckEnd", LogLevel.Warning, "health.check_completed", "HealthCheckEndDegraded"),
        new(103, "HealthCheckEnd", LogLevel.Error, "health.check_completed", "HealthCheckEndUnhealthy"),
        new(104, "HealthCheckError", LogLevel.Error, "health.check_failed", "HealthCheckError"),
        new(105, "HealthCheckData", LogLevel.Debug, "health.check_data", "HealthCheckData")
    ];

    internal static MethodInfo Generator(HealthFrameworkEvent row) => LogType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        .Single(m => m.Name == row.Method && m.GetCustomAttribute<LoggerMessageAttribute>() != null);

    internal static void Generate(HealthFrameworkEvent row, Microsoft.Extensions.Logging.ILogger logger)
    {
        if (row.Id == 105)
        {
            Data(logger, new Dictionary<string, object> { [HttpBoundaryCapture.Secret] = new HealthPoisonValue() });
            return;
        }
        var method = Generator(row);
        method.Invoke(null, method.GetParameters().Select(p => p.ParameterType == typeof(Microsoft.Extensions.Logging.ILogger) ? (object)logger :
            p.ParameterType == typeof(string) ? HttpBoundaryCapture.Secret :
            p.ParameterType == typeof(double) ? 12.5 :
            p.ParameterType == typeof(HealthStatus) ? (object)(row.Level == LogLevel.Error ? HealthStatus.Unhealthy : row.Level == LogLevel.Warning ? HealthStatus.Degraded : HealthStatus.Healthy) :
            p.ParameterType == typeof(Exception) ? new HealthUnreadableException() : throw new InvalidOperationException(p.ParameterType.Name)).ToArray());
    }

    internal static void Data(Microsoft.Extensions.Logging.ILogger logger, IReadOnlyDictionary<string, object> data)
    {
        var registration = new HealthCheckRegistration(HttpBoundaryCapture.Secret, new DelegateHealthCheck(_ => Task.FromResult(HealthCheckResult.Healthy())), null, null);
        var entry = new HealthReportEntry(HealthStatus.Healthy, HttpBoundaryCapture.Secret, TimeSpan.Zero, null, data);
        LogType.GetMethod("HealthCheckData", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new object[] { logger, registration, entry });
    }

    internal static void AssertSafe(HttpBoundaryCapture capture)
    {
        capture.AssertSafe();
        foreach (var entry in capture.Sink.Events)
            Assert.All(entry.Properties.Keys, key => Assert.Contains(key,
                new[] { "EventCode", "SourceContext", "SourceCategory", "Diagnostic", "failureKind" }));
    }
}

internal sealed class HealthFixture : IDisposable
{
    internal readonly ServiceProvider Services;
    internal readonly HealthCheckService Health;
    internal readonly ILoggerFactory Factory;

    internal HealthFixture(HttpBoundaryCapture capture, Action<IHealthChecksBuilder>? configure = null, Action<IServiceCollection>? services = null)
    {
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddSerilog(capture.Logger, dispose: false);
        collection.ProtectHealthCheckLogging();
        var builder = collection.AddHealthChecks();
        configure?.Invoke(builder);
        services?.Invoke(collection);
        Services = collection.BuildServiceProvider();
        Factory = Services.GetRequiredService<ILoggerFactory>();
        Health = Services.GetRequiredService<HealthCheckService>();
    }

    internal Microsoft.Extensions.Logging.ILogger Logger => Factory.CreateLogger(HealthFrameworkEvents.Source);
    public void Dispose() => Services.Dispose();
}

internal sealed class DelegateHealthCheck(Func<CancellationToken, Task<HealthCheckResult>> run) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) => run(cancellationToken);
}

internal sealed class HealthPoisonValue
{
    internal int FormatCalls;
    public override string ToString() { FormatCalls++; throw new InvalidOperationException("Raw health data must not be formatted"); }
}

internal sealed class HealthUnreadableException : Exception
{
    public override string Message => throw new InvalidOperationException("Raw health exception message must not be read");
    public override string ToString() => throw new InvalidOperationException("Raw health exception must not be formatted");
}

internal sealed class HealthPoisonState : IReadOnlyList<KeyValuePair<string, object?>>
{
    public int Count => throw new InvalidOperationException("Raw state count accessed");
    public KeyValuePair<string, object?> this[int index] => throw new InvalidOperationException("Raw state item accessed");
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => throw new InvalidOperationException("Raw state enumerated");
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public override string ToString() => throw new InvalidOperationException("Raw state formatted");
}
