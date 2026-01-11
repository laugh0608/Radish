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
            // 获取配置选项（使用 IOptions 而非 IOptionsSnapshot，因为 Serilog 在 root provider 中初始化）
            var options = services.GetRequiredService<IOptions<SerilogOptions>>().Value;

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

        // 使用 FileStream 并允许共享读写,避免多进程或重启时文件被占用
        var fileStream = new FileStream(
            debugLogPath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite);
        var streamWriter = new StreamWriter(fileStream);

        SelfLog.Enable(TextWriter.Synchronized(streamWriter));
    }
}
