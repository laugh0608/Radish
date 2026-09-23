using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Radish.Common.LogTool;
using Serilog;

namespace Radish.Extension.Log;

/// <summary>正式切换前仅显式启用；与旧 sink 互斥，不叠加 ReadFrom.Configuration。</summary>
public static class RuntimeLoggingConfiguration
{
    public static bool IsEnabled(IConfiguration configuration) => configuration.GetValue<bool>("RadishLogging:Enabled");

    public static RuntimeLogOutput Configure(LoggerConfiguration logger, IConfiguration configuration,
        string environmentName, string service, TextWriter? output = null, TextWriter? emergency = null)
    {
        var section = configuration.GetSection("RadishLogging");
        var mode = section["Mode"] ?? "Production";
        if (mode == "Development" && environmentName != Environments.Development)
            throw new ArgumentException("Development logging requires a Development host.");
        var source = new RuntimeLogSource(section["DeploymentId"] ?? "local", service,
            section["InstanceId"] ?? Environment.MachineName, section["Release"] ?? "unversioned");
        var policy = new RuntimeLogPolicy(source, mode, section["MinimumLevel"] ?? "Info", section.GetValue<bool>("Diagnostics"));
        var writer = new RuntimeLogOutput(policy, source, output ?? Console.Out, emergency ?? Console.Error, mode);
        logger.MinimumLevel.Verbose().Enrich.FromLogContext()
            .Filter.ByExcluding(RuntimeProcess.OwnsStartupFailure)
            .WriteTo.Sink(new RuntimeSerilogSink(writer));
        return writer;
    }
}
