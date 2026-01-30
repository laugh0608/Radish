using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Radish.Common.LogTool;
using Radish.Common.OptionTool;
using Serilog;
using Serilog.Events;
using Serilog.Filters;
using Serilog.Sinks.PeriodicBatching;

namespace Radish.Extension.Log;

public static class LogConfigExtension
{
    /// <summary>
    /// 将日志输出到控制台
    /// </summary>
    public static LoggerConfiguration WriteToConsole(this LoggerConfiguration loggerConfiguration, SerilogOptions options)
    {
        if (!options.Console.Enable)
            return loggerConfiguration;

        // 输出应用日志
        if (options.Console.EnableApplicationLog)
        {
            loggerConfiguration = loggerConfiguration.WriteTo.Logger(lg =>
                lg.Filter.ByIncludingOnly(WithPropertyExt<string>(LogContextTool.LogSource, s => !LogContextTool.AopSql.Equals(s)))
                  .WriteTo.Console());
        }

        // 输出 SQL 日志
        if (options.Console.EnableSqlLog)
        {
            loggerConfiguration = loggerConfiguration.WriteTo.Logger(lg =>
                lg.Filter.ByIncludingOnly(Matching.WithProperty<string>(LogContextTool.LogSource, s => LogContextTool.AopSql.Equals(s)))
                  .WriteTo.Console());
        }

        return loggerConfiguration;
    }

    /// <summary>
    /// 将日志输出到 Logs 文件夹中的日志文件
    /// 自动将不同项目的日志输出到解决方案根目录的 Logs/{ProjectName}/ 文件夹下
    /// </summary>
    public static LoggerConfiguration WriteToFile(this LoggerConfiguration loggerConfiguration, SerilogOptions options)
    {
        if (!options.File.Enable)
            return loggerConfiguration;

        var projectName = LogContextTool.ProjectName;

        // 输出应用日志
        if (options.File.EnableApplicationLog)
        {
            loggerConfiguration = loggerConfiguration.WriteTo.Logger(lg =>
                lg.Filter.ByIncludingOnly(WithPropertyExt<string>(LogContextTool.LogSource, s => !LogContextTool.AopSql.Equals(s)))
                  .WriteTo.Async(s => s.File(
                      Path.Combine(LogContextTool.BaseLogs, projectName, @"Log.txt"),
                      rollingInterval: RollingInterval.Day,
                      outputTemplate: LogContextTool.FileMessageTemplate,
                      retainedFileCountLimit: options.File.RetainedFileCountLimit)));
        }

        // 输出 SQL 日志
        if (options.File.EnableSqlLog)
        {
            loggerConfiguration = loggerConfiguration.WriteTo.Logger(lg =>
                lg.Filter.ByIncludingOnly(Matching.WithProperty<string>(LogContextTool.LogSource, s => LogContextTool.AopSql.Equals(s)))
                  .WriteTo.Async(s => s.File(
                      Path.Combine(LogContextTool.BaseLogs, projectName, LogContextTool.AopSql, @"AopSql.txt"),
                      rollingInterval: RollingInterval.Day,
                      outputTemplate: LogContextTool.FileMessageTemplate,
                      retainedFileCountLimit: options.File.RetainedFileCountLimit)));
        }

        return loggerConfiguration;
    }

    /// <summary>
    /// 将日志输出到数据库
    /// </summary>
    public static LoggerConfiguration WriteToDatabase(this LoggerConfiguration loggerConfiguration, IServiceProvider serviceProvider, SerilogOptions options)
    {
        if (!options.Database.Enable)
            return loggerConfiguration;

        var batchingSink = new LogBatchingSink(
            serviceProvider.GetRequiredService<SqlSugar.ISqlSugarClient>(),
            options
        );

        var batchingOptions = new PeriodicBatchingSinkOptions
        {
            BatchSizeLimit = options.Database.BatchSizeLimit,
            Period = TimeSpan.FromSeconds(options.Database.PeriodSeconds),
            EagerlyEmitFirstEvent = options.Database.EagerlyEmitFirstEvent,
            QueueLimit = options.Database.QueueLimit
        };

        var periodicBatchingSink = new PeriodicBatchingSink(batchingSink, batchingOptions);

        return loggerConfiguration.WriteTo.Sink(periodicBatchingSink);
    }

    public static Func<LogEvent, bool> WithPropertyExt<T>(string propertyName, Func<T, bool> predicate)
    {
        // 如果不包含属性 也认为是 true
        return e =>
        {
            if (!e.Properties.TryGetValue(propertyName, out var propertyValue)) return true;

            return propertyValue is ScalarValue { Value: T value } && predicate(value);
        };
    }
}
