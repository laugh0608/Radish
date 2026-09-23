using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Radish.Extension.Log;
using Serilog;

namespace Radish.DbMigrate;

/// <summary>诊断记录命令类别与结果，不把原始参数或异常消息写入日志。</summary>
internal static class DbMigrateCommand
{
    public static Serilog.Core.Logger CreateLogger(IConfiguration configuration, string environmentName, TextWriter output)
    {
        var logger = new LoggerConfiguration();
        RuntimeLoggingConfiguration.Configure(logger, configuration, environmentName, "dbmigrate", output);
        return logger.CreateLogger();
    }

    public static async Task RunAsync(string[] args, ILogger logger, Func<Task> command)
    {
        var mode = args.FirstOrDefault()?.ToLowerInvariant() ?? "apply";
        var knownMode = mode is "apply" or "doctor" or "verify" or "init" or "seed" ? mode : "help";
        var events = logger.ForContext("SourceCategory", "database").ForContext("command", knownMode);
        var started = Stopwatch.GetTimestamp();
        events.ForContext("EventCode", "dbmigrate.started").Information("Migration command started");
        await command();
        events.ForContext("EventCode", "dbmigrate.completed")
            .ForContext("durationMs", Stopwatch.GetElapsedTime(started).TotalMilliseconds)
            .Information("Migration command completed");
        // 失败交给最外层 RuntimeProcess 记录；不在此捕获后重复记录。
    }
}
