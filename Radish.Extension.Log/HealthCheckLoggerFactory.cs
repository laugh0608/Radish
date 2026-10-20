using System.Collections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radish.Common.LogTool;

namespace Radish.Extension.Log;

/// <summary>只保护健康服务的 ILogger 输入，避免 Serilog 绑定任意 Data 时提前格式化或覆盖来源。</summary>
public sealed class HealthCheckLoggerFactory(ILoggerFactory inner) : ILoggerFactory
{
    private int _disposed;

    public ILogger CreateLogger(string categoryName)
    {
        var logger = inner.CreateLogger(categoryName);
        return categoryName == HealthCheckLogIdentity.Source ? new HealthCheckLogger(logger) : logger;
    }

    public void AddProvider(ILoggerProvider provider) => inner.AddProvider(provider);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0) inner.Dispose();
    }

    private sealed class HealthCheckLogger(ILogger logger) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!logger.IsEnabled(logLevel)) return;
            var code = HealthCheckLogIdentity.Code(eventId.Id, eventId.Name);
            // 不读取原始 state，也不调用 formatter；105 的任意 Data 在这一边界停止。
            var safeState = new HealthCheckState(code, logLevel < LogLevel.Warning,
                exception == null ? null : RuntimeFailureSummary.Classify(exception));
            logger.Log(logLevel, code == "health.unknown" ? new EventId(0, "HealthCheckUnknown") : eventId, safeState, null,
                static (value, _) => value.ToString());
        }
    }

    private sealed class HealthCheckState : IReadOnlyList<KeyValuePair<string, object?>>
    {
        private readonly KeyValuePair<string, object?>[] _properties;

        public HealthCheckState(string code, bool diagnostic, string? failureKind)
        {
            var properties = new List<KeyValuePair<string, object?>>
            {
                new("{OriginalFormat}", "Health check event {EventCode}."),
                new("SourceContext", HealthCheckLogIdentity.Source),
                new("EventCode", code),
                new("SourceCategory", "infrastructure"),
                new("Diagnostic", diagnostic),
                new("failureKind", failureKind)
            };
            _properties = properties.ToArray();
        }

        public int Count => _properties.Length;
        public KeyValuePair<string, object?> this[int index] => _properties[index];
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => ((IEnumerable<KeyValuePair<string, object?>>)_properties).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public override string ToString() => "Health check event.";
    }
}

public static class HealthCheckLoggingServiceCollectionExtensions
{
    /// <summary>装饰 UseSerilog 已注册的工厂，保留它的创建逻辑、provider 与 logger 释放所有权。</summary>
    public static void ProtectHealthCheckLogging(this IServiceCollection services)
    {
        var registration = services.LastOrDefault(d => d.ServiceType == typeof(ILoggerFactory) && !d.IsKeyedService);
        if (registration is not { Lifetime: ServiceLifetime.Singleton, ImplementationFactory: { } factory })
            throw new InvalidOperationException("Health check logging protection requires the Serilog singleton factory registration.");

        var index = services.IndexOf(registration);
        services[index] = ServiceDescriptor.Singleton<ILoggerFactory>(provider =>
            new HealthCheckLoggerFactory((ILoggerFactory)factory(provider)));
    }
}
