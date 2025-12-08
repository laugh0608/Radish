using Radish.Common.LogTool;
using Serilog;
using Serilog.Events;
using Serilog.Filters;

namespace Radish.Extension.SerilogExtension;

public static class LogConfigExtension
{
    /// <summary>
    /// 将日志输出到控制台
    /// </summary>
    /// <param name="loggerConfiguration"></param>
    /// <returns></returns>
    public static LoggerConfiguration WriteToConsole(this LoggerConfiguration loggerConfiguration)
    {
        // 识别普通日志
        loggerConfiguration = loggerConfiguration.WriteTo.Logger(lg =>
            lg
            .Filter.ByIncludingOnly(WithPropertyExt<string>(LogContextTool.LogSource, s => !LogContextTool.AopSql.Equals(s)))
            .WriteTo.Console());

        // 识别 AopSql 的日志
        loggerConfiguration = loggerConfiguration.WriteTo.Logger(lg =>
            lg
            .Filter.ByIncludingOnly(Matching.WithProperty<string>(LogContextTool.LogSource, s => LogContextTool.AopSql.Equals(s)))
            .WriteTo.Console());

        return loggerConfiguration;
    }

    /// <summary>
    /// 将日志输出到 Log 文件夹中的日志文件
    /// 自动将不同项目的日志输出到解决方案根目录的 Log/{ProjectName}/ 文件夹下
    /// </summary>
    /// <param name="loggerConfiguration"></param>
    /// <returns></returns>
    public static LoggerConfiguration WriteToFile(this LoggerConfiguration loggerConfiguration)
    {
        // 获取项目名称，用于区分不同项目的日志
        var projectName = LogContextTool.ProjectName;

        // 识别普通日志 - 输出到 Log/{ProjectName}/Log.txt
        loggerConfiguration = loggerConfiguration.WriteTo.Logger(lg =>
            lg
            .Filter.ByIncludingOnly(WithPropertyExt<string>(LogContextTool.LogSource, s => !LogContextTool.AopSql.Equals(s)))
            .WriteTo.Async(s => s.File(
                Path.Combine(LogContextTool.BaseLogs, projectName, @"Log.txt"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: LogContextTool.FileMessageTemplate,
                retainedFileCountLimit: 31)));

        // 识别 AopSql 的日志 - 输出到 Log/{ProjectName}/AopSql/AopSql.txt
        loggerConfiguration = loggerConfiguration.WriteTo.Logger(lg =>
            lg
            .Filter.ByIncludingOnly(Matching.WithProperty<string>(LogContextTool.LogSource, s => LogContextTool.AopSql.Equals(s)))
            .WriteTo.Async(s => s.File(
                Path.Combine(LogContextTool.BaseLogs, projectName, LogContextTool.AopSql, @"AopSql.txt"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: LogContextTool.FileMessageTemplate,
                retainedFileCountLimit: 31)));

        return loggerConfiguration;
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