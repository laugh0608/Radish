using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Radish.Common.LogTool;
using Serilog;
using Serilog.Core;

namespace Radish.Extension.Log;

/// <summary>配置加载后的引导与运行共享同一个 logger；宿主退出时释放，不创建第二套 sink。</summary>
public sealed class RuntimeLoggingSession : IDisposable
{
    private readonly Serilog.ILogger _previous;
    private readonly List<IDisposable> _registrations = [];
    private readonly SerilogSelfLogSession _selfLog;
    private bool _disposed;
    public Logger? Logger { get; }

    public RuntimeLoggingSession(IConfiguration configuration, string environmentName, string service,
        TextWriter? output = null, TextWriter? emergency = null)
    {
        _previous = Serilog.Log.Logger;
        // 配置可能尚未读完或已经失效，自诊断不依赖日志开关、配置来源字段或业务输出。
        _selfLog = new SerilogSelfLogSession(
            new RuntimeLogSource("bootstrap", service, "process-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture), "unversioned"),
            environmentName == Environments.Development ? "Development" : "Production", emergency ?? Console.Error);
        try
        {
            if (!RuntimeLoggingConfiguration.IsEnabled(configuration)) return;
            var loggerConfiguration = new LoggerConfiguration();
            RuntimeLoggingConfiguration.Configure(loggerConfiguration, configuration, environmentName, service, output, emergency);
            Logger = loggerConfiguration.CreateLogger();
            Serilog.Log.Logger = Logger;
        }
        catch
        {
            _selfLog.Dispose();
            throw;
        }
    }

    public void AttachLifetime(IHostApplicationLifetime lifetime)
    {
        if (Logger == null) return;
        _registrations.Add(lifetime.ApplicationStarted.Register(() => WriteLifecycle("runtime.started")));
        _registrations.Add(lifetime.ApplicationStopped.Register(() => WriteLifecycle("runtime.stopped")));
    }

    /// <summary>Gateway 两条链只在停止通知时记录一次；旧链 logger 仍归宿主释放。</summary>
    public void AttachGatewayLifetime(IHostApplicationLifetime lifetime, Serilog.ILogger legacyLogger)
    {
        if (Logger != null)
            _registrations.Add(lifetime.ApplicationStarted.Register(() => WriteLifecycle("runtime.started")));
        _registrations.Add(HostProgramLog.RegisterGatewayStopped(lifetime, Logger ?? legacyLogger));
    }

    private void WriteLifecycle(string code) => Logger?.ForContext("EventCode", code)
        .ForContext("SourceCategory", "lifecycle").Information("Runtime lifecycle event");

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            foreach (var registration in _registrations) registration.Dispose();
            if (Logger == null) return;
            if (ReferenceEquals(Serilog.Log.Logger, Logger)) Serilog.Log.Logger = _previous;
            Logger.Dispose();
        }
        finally
        {
            // 旧链 logger 由宿主先释放；候选 logger 由上方释放。两者均保留释放阶段的自诊断。
            _selfLog.Dispose();
        }
    }
}
