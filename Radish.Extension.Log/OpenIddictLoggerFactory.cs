using System.Collections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radish.Common.LogTool;

namespace Radish.Extension.Log;

/// <summary>仅在 OpenIddict Server 两个精确类别进入 Serilog 前丢弃原始日志输入。</summary>
public sealed class OpenIddictLoggerFactory(ILoggerFactory inner) : ILoggerFactory
{
    private int _disposed;

    public ILogger CreateLogger(string categoryName)
    {
        var logger = inner.CreateLogger(categoryName);
        return OpenIddictLogIdentity.IsSource(categoryName) ? new ServerLogger(logger, categoryName) : logger;
    }

    public void AddProvider(ILoggerProvider provider) => inner.AddProvider(provider);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0) inner.Dispose();
    }

    private sealed class ServerLogger(ILogger logger, string source) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!logger.IsEnabled(logLevel)) return;
            var code = OpenIddictLogIdentity.Code(source, eventId.Id, eventId.Name);
            // OpenIddictMessage / Claims 等值会在桥接绑定时被格式化；此处不读取原 state 或 formatter。
            var safeState = new ServerState(source, code, logLevel < LogLevel.Warning,
                exception == null ? null : RuntimeFailureSummary.Classify(exception));
            logger.Log(logLevel, code.EndsWith(".unknown", StringComparison.Ordinal) ? new EventId(0, "OpenIddictUnknown") : eventId,
                safeState, null, static (value, _) => value.ToString());
        }
    }

    private sealed class ServerState(string source, string code, bool diagnostic, string? failureKind)
        : IReadOnlyList<KeyValuePair<string, object?>>
    {
        private readonly KeyValuePair<string, object?>[] _properties =
        [
            new("{OriginalFormat}", "OpenIddict event {EventCode}."),
            new("SourceContext", source),
            new("EventCode", code),
            new("SourceCategory", "http"),
            new("Diagnostic", diagnostic),
            // 显式 null 防止 scope 中的 failureKind 补入无异常事件。
            new("failureKind", failureKind)
        ];

        public int Count => _properties.Length;
        public KeyValuePair<string, object?> this[int index] => _properties[index];
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => ((IEnumerable<KeyValuePair<string, object?>>)_properties).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public override string ToString() => "OpenIddict event.";
    }
}

public static class OpenIddictLoggingServiceCollectionExtensions
{
    /// <summary>保留 UseSerilog 的原工厂创建与 logger 所有权，只添加精确类别保护。</summary>
    public static void ProtectOpenIddictServerLogging(this IServiceCollection services)
    {
        var registration = services.LastOrDefault(d => d.ServiceType == typeof(ILoggerFactory) && !d.IsKeyedService);
        if (registration is not { Lifetime: ServiceLifetime.Singleton, ImplementationFactory: { } factory })
            throw new InvalidOperationException("OpenIddict logging protection requires the Serilog singleton factory registration.");

        services[services.IndexOf(registration)] = ServiceDescriptor.Singleton<ILoggerFactory>(provider =>
            new OpenIddictLoggerFactory((ILoggerFactory)factory(provider)));
    }
}
