using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Radish.Common;
using Radish.Common.CoreTool;
using Radish.Common.LogTool;
using Radish.Common.OptionTool;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;

namespace Radish.Extension.SerilogExtension;

public static class SerilogSetup
{
    public static IHostBuilder AddSerilogSetup(this IHostBuilder host)
    {
        if (host == null) throw new ArgumentNullException(nameof(host));

        host.UseSerilog((context, services, loggerConfiguration) =>
        {
            // 获取配置选项
            var options = services.GetRequiredService<IOptionsSnapshot<SerilogOptions>>().Value;

            // 解析最小日志级别
            var minimumLevel = Enum.TryParse<LogEventLevel>(options.MinimumLevel, true, out var level)
                ? level
                : LogEventLevel.Information;

            loggerConfiguration
                .ReadFrom.Configuration(AppSettingsTool.Configuration)
                .MinimumLevel.Is(minimumLevel)
                .Enrich.FromLogContext()
                .WriteToConsole(options)
                .WriteToFile(options)
                .WriteToDatabase(services, options);

            // 配置 Serilog 内部日志
            ConfigureSerilogSelfLog();
        });

        return host;
    }

    private static void ConfigureSerilogSelfLog()
    {
        var projectName = LogContextTool.ProjectName;
        var debugLogDir = Path.Combine(LogContextTool.BaseLogs, projectName, LogContextTool.SerilogDebug);

        if (!Directory.Exists(debugLogDir))
        {
            Directory.CreateDirectory(debugLogDir);
        }

        var debugLogPath = Path.Combine(debugLogDir, $"Serilog{DateTime.Now:yyyyMMdd}.txt");
        var file = File.CreateText(debugLogPath);
        SelfLog.Enable(TextWriter.Synchronized(file));
    }
}