using Microsoft.Extensions.Hosting;
using Serilog;

namespace Radish.Extension.Log;

/// <summary>只保存已选择的运行状态；启动通知不重新读取配置、地址内容或证书。</summary>
public sealed class HostStartupLog
{
    internal const string Source = "Radish.Extension.Log.HostStartupLog";
    private readonly string _host;
    private readonly string _environment;
    private readonly int _corsOriginCount;
    private readonly int _healthTargetCount;
    private readonly string? _httpsRedirection;
    private readonly string? _delegation;
    private string _authentication;

    private HostStartupLog(string host, string environment, int corsOriginCount, string authentication,
        int healthTargetCount = 0, string? httpsRedirection = null, string? delegation = null)
    {
        _host = host;
        _environment = environment.ToLowerInvariant() switch
        {
            "development" => "development",
            "production" => "production",
            "staging" => "staging",
            _ => "other"
        };
        _corsOriginCount = corsOriginCount;
        _authentication = authentication;
        _healthTargetCount = healthTargetCount;
        _httpsRedirection = httpsRedirection;
        _delegation = delegation;
    }

    public static HostStartupLog Auth(string environment, int corsOriginCount) =>
        new("auth", environment, corsOriginCount, "not-initialized");

    public static HostStartupLog Api(string environment, int corsOriginCount, bool localSigningCertificateLoaded) =>
        new("api", environment, corsOriginCount, localSigningCertificateLoaded ? "local-certificate" : "authority");

    public static HostStartupLog Gateway(string environment, int corsOriginCount, int healthTargetCount,
        bool httpsRedirection, bool delegationRemoved) =>
        new("gateway", environment, corsOriginCount, "not-applicable", healthTargetCount,
            httpsRedirection ? "enabled" : "disabled", delegationRemoved ? "removed" : "retained");

    /// <summary>在既有 OpenIddict 配置委托主体完成时记录选择，不触发 options 或证书初始化。</summary>
    public void AuthConfigurationCompleted(bool developmentKeys)
    {
        if (_host != "auth") throw new InvalidOperationException("Only Auth has an OpenIddict startup state.");
        Volatile.Write(ref _authentication, developmentKeys ? "development-keys" : "file-certificates");
    }

    /// <summary>地址只读取启动通知时的 Count；注册方在宿主退出后释放回调。</summary>
    public IDisposable Register(IHostApplicationLifetime lifetime, ICollection<string> addresses, ILogger logger)
    {
        var sourceLogger = logger.ForContext("SourceContext", Source);
        return lifetime.ApplicationStarted.Register(() =>
        {
            var summary = sourceLogger.ForContext("EventCode", "runtime.startup_summary")
                .ForContext("SourceCategory", "lifecycle")
                .ForContext("Diagnostic", false)
                .ForContext("startupHost", _host)
                .ForContext("startupEnvironment", _environment)
                .ForContext("listenEndpointCount", addresses.Count)
                .ForContext("corsOriginCount", _corsOriginCount);
            if (_host == "gateway")
                summary = summary.ForContext("healthTargetCount", _healthTargetCount)
                    .ForContext("startupHttpsRedirection", _httpsRedirection)
                    .ForContext("startupDelegation", _delegation);
            else
                summary = summary.ForContext("startupAuthentication", Volatile.Read(ref _authentication));
            summary.Information("Host startup summary.");
        });
    }
}
