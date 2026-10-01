using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Debugging;

namespace Radish.Extension.Log;

/// <summary>配置加载后的引导与运行共享同一个 logger；宿主退出时释放，不创建第二套 sink。</summary>
public sealed class RuntimeLoggingSession : IDisposable
{
    private readonly Serilog.ILogger _previous;
    private readonly List<IDisposable> _registrations = [];
    private bool _disposed;
    public Logger? Logger { get; }

    public RuntimeLoggingSession(IConfiguration configuration, string environmentName, string service,
        TextWriter? output = null, TextWriter? emergency = null)
    {
        _previous = Serilog.Log.Logger;
        if (!RuntimeLoggingConfiguration.IsEnabled(configuration)) return;
        var loggerConfiguration = new LoggerConfiguration();
        var writer = RuntimeLoggingConfiguration.Configure(loggerConfiguration, configuration, environmentName, service, output, emergency);
        Logger = loggerConfiguration.CreateLogger();
        Serilog.Log.Logger = Logger;
        SelfLog.Enable(_ => writer.ReportFailure());
    }

    public void AttachLifetime(IHostApplicationLifetime lifetime)
    {
        if (Logger == null) return;
        _registrations.Add(lifetime.ApplicationStarted.Register(() => WriteLifecycle("runtime.started")));
        _registrations.Add(lifetime.ApplicationStopped.Register(() => WriteLifecycle("runtime.stopped")));
    }

    private void WriteLifecycle(string code) => Logger?.ForContext("EventCode", code)
        .ForContext("SourceCategory", "lifecycle").Information("Runtime lifecycle event");

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var registration in _registrations) registration.Dispose();
        if (Logger == null) return;
        if (ReferenceEquals(Serilog.Log.Logger, Logger))
        {
            SelfLog.Disable();
            Serilog.Log.Logger = _previous;
        }
        Logger.Dispose();
    }
}
