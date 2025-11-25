using Microsoft.Extensions.Hosting;
using Radish.Common;
using Radish.Common.CoreTool;
using Radish.Common.LogTool;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;

namespace Radish.Extension.SerilogExtension;

public static class SerilogSetup
{
    public static IHostBuilder AddSerilogSetup(this IHostBuilder host)
    {
        if (host == null) throw new ArgumentNullException(nameof(host));

        var loggerConfiguration = new LoggerConfiguration()
            .ReadFrom.Configuration(AppSettingsTool.Configuration)
            .Enrich.FromLogContext()
            // 输出到控制台
            .WriteToConsole()
            // 将日志保存到文件中
            .WriteToFile();
            // 配置日志库
            // .WriteToLogBatching();

        // 配置Seq日志中心
        // var option = App.GetOptions<SeqOptions>();
        // if (option.Enabled)
        // {
        //     var address = option.Address;
        //     var apiKey = option.ApiKey;
        //     if (!address.IsNullOrEmpty())
        //     {
        //         loggerConfiguration =
        //             loggerConfiguration.WriteTo.Seq(address, restrictedToMinimumLevel: LogEventLevel.Verbose,
        //                 apiKey: apiKey, eventBodyLimitBytes: 10485760);
        //     }
        // }

        Log.Logger = loggerConfiguration.CreateLogger();

        // Serilog 内部日志
        var debugLogDir = LogContextTool.Combine(LogContextTool.SerilogDebug);
        if (!Directory.Exists(debugLogDir))
        {
            Directory.CreateDirectory(debugLogDir);
        }
        var file = File.CreateText(LogContextTool.Combine(LogContextTool.SerilogDebug, $"Serilog{DateTime.Now:yyyyMMdd}.txt"));
        SelfLog.Enable(TextWriter.Synchronized(file));

        host.UseSerilog();
        return host;
    }
}