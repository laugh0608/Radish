using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Radish.Common;
using Radish.Common.CoreTool;
using Radish.Common.LogTool;
using Radish.Common.OptionTool;
using Serilog;
using Serilog.Debugging;
using Serilog.Configuration;
using Serilog.Events;

namespace Radish.Extension.Log;

public static class SerilogSetup
{
    public static IHostBuilder AddSerilogSetup(this IHostBuilder host, RuntimeLoggingSession runtimeLogging)
    {
        if (host == null) throw new ArgumentNullException(nameof(host));

        if (runtimeLogging.Logger != null)
        {
            host.UseSerilog(runtimeLogging.Logger, dispose: false);
        }
        else
        {
            host.UseSerilog((context, services, loggerConfiguration) =>
            {
                // 使用 root provider 的 IOptions，保留旧链的配置与释放所有权。
                var options = services.GetRequiredService<IOptions<SerilogOptions>>().Value;
                ConfigureLegacy(loggerConfiguration, AppSettingsTool.Configuration, services, options);
                ConfigureSerilogSelfLog();
            });
        }

        host.ConfigureServices((_, services) =>
        {
            services.ProtectOpenIddictServerLogging();
            services.ProtectHealthCheckLogging();
        });
        return host;
    }

    /// <summary>旧链共用接线；配置与代码 sink 均在上下文合并后接受安全事件。</summary>
    public static void ConfigureLegacy(LoggerConfiguration logger, IConfiguration configuration,
        IServiceProvider services, SerilogOptions options)
    {
        var minimumLevel = Enum.TryParse<LogEventLevel>(options.MinimumLevel, true, out var level)
            ? level : LogEventLevel.Information;
        logger.ReadFrom.Configuration(new SerilogConfigurationView(configuration, "root"))
            .MinimumLevel.Is(minimumLevel)
            .Filter.ByExcluding(RuntimeProcess.OwnsStartupFailure)
            .Enrich.FromLogContext()
            .Enrich.With<SensitiveQueryStringLogEnricher>()
            .WriteTo.Sink(LoggerSinkConfiguration.Wrap(sink => new HttpServerFrameworkLogSink(new HttpExceptionFrameworkLogSink(new SignalRFrameworkLogSink(new OutboundHttpFrameworkLogSink(new AuthenticationLogSink(new RoutingLogSink(new HealthCheckLogSink(new OpenIddictLogSink(sink)))))))), sinks =>
                sinks.Logger(outputs => outputs
                    .ReadFrom.Configuration(new SerilogConfigurationView(configuration, "WriteTo"))
                    .WriteToConsole(options).WriteToFile(options).WriteToDatabase(services, options))));
        if (configuration.GetSection("Serilog:AuditTo").GetChildren().Any())
        {
            // 审计 sink 保持异常向调用方传播的语义。
            logger.AuditTo.Sink(LoggerSinkConfiguration.Wrap(sink => new HttpServerFrameworkLogSink(new HttpExceptionFrameworkLogSink(new SignalRFrameworkLogSink(new OutboundHttpFrameworkLogSink(new AuthenticationLogSink(new RoutingLogSink(new HealthCheckLogSink(new OpenIddictLogSink(sink)))))))), sinks =>
                sinks.Logger(outputs => outputs
                    .ReadFrom.Configuration(new SerilogConfigurationView(configuration, "AuditTo")))));
        }
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
