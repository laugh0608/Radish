using Microsoft.Extensions.Options;
using Radish.Common.DbTool;
using Radish.Common.OptionTool;
using Radish.Model.LogModels;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;
using SqlSugar;

namespace Radish.Extension.SerilogExtension;

/// <summary>
/// 日志批处理 Sink - 将日志批量写入数据库
/// </summary>
public class LogBatchingSink : IBatchedLogEventSink
{
    private readonly ISqlSugarClient _db;
    private readonly SerilogOptions _options;

    public LogBatchingSink(ISqlSugarClient db, SerilogOptions options)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task EmitBatchAsync(IEnumerable<LogEvent> batch)
    {
        if (!batch.Any()) return;

        // 分离 SQL 日志和应用日志
        var sqlLogs = batch.FilterSqlLog(_options.Database.LogSelectQueries);
        var appLogs = batch.FilterApplicationLog();

        // 写入 SQL 日志
        if (_options.Database.EnableSqlLog && sqlLogs.Any())
        {
            await WriteSqlLogAsync(sqlLogs);
        }

        // 写入应用日志
        if (_options.Database.EnableApplicationLog && appLogs.Any())
        {
            await WriteApplicationLogsAsync(appLogs);
        }
    }

    public Task OnEmptyBatchAsync()
    {
        return Task.CompletedTask;
    }

    #region 写入应用日志

    private async Task WriteApplicationLogsAsync(IEnumerable<LogEvent> batch)
    {
        // 按日志级别分组
        var groups = batch.GroupBy(e => e.Level);

        foreach (var group in groups)
        {
            switch (group.Key)
            {
                case LogEventLevel.Information:
                    await WriteInformationLogAsync(group);
                    break;
                case LogEventLevel.Warning:
                    await WriteWarningLogAsync(group);
                    break;
                case LogEventLevel.Error:
                case LogEventLevel.Fatal:
                    await WriteErrorLogAsync(group);
                    break;
            }
        }
    }

    private async Task WriteInformationLogAsync(IEnumerable<LogEvent> batch)
    {
        var logs = batch.Select(MapToInformationLog).ToList();
        if (logs.Any())
        {
            // 注意: SqlSugar 在初始化时会将 ConfigId 转换为小写,所以这里使用 ToLower()
            var logDb = ((SqlSugarScope)_db).GetConnectionScope(SqlSugarConst.LogConfigId.ToLower());
            await logDb.Insertable(logs).SplitTable().ExecuteReturnSnowflakeIdAsync();
        }
    }

    private async Task WriteWarningLogAsync(IEnumerable<LogEvent> batch)
    {
        var logs = batch.Select(MapToWarningLog).ToList();
        if (logs.Any())
        {
            var logDb = ((SqlSugarScope)_db).GetConnectionScope(SqlSugarConst.LogConfigId.ToLower());
            await logDb.Insertable(logs).SplitTable().ExecuteReturnSnowflakeIdAsync();
        }
    }

    private async Task WriteErrorLogAsync(IEnumerable<LogEvent> batch)
    {
        var logs = batch.Select(MapToErrorLog).ToList();
        if (logs.Any())
        {
            var logDb = ((SqlSugarScope)_db).GetConnectionScope(SqlSugarConst.LogConfigId.ToLower());
            await logDb.Insertable(logs).SplitTable().ExecuteReturnSnowflakeIdAsync();
        }
    }

    #endregion

    #region 写入 SQL 日志

    private async Task WriteSqlLogAsync(IEnumerable<LogEvent> batch)
    {
        var logs = batch.Select(MapToAuditSqlLog).ToList();
        if (logs.Any())
        {
            var logDb = ((SqlSugarScope)_db).GetConnectionScope(SqlSugarConst.LogConfigId.ToLower());
            await logDb.Insertable(logs).SplitTable().ExecuteReturnSnowflakeIdAsync();
        }
    }

    #endregion

    #region 映射方法

    private InformationLog MapToInformationLog(LogEvent logEvent)
    {
        return new InformationLog
        {
            DateTime = logEvent.Timestamp.DateTime,
            Level = logEvent.Level.ToString(),
            Message = logEvent.RenderMessage(),
            MessageTemplate = logEvent.MessageTemplate.Text,
            Properties = SerializeProperties(logEvent.Properties)
        };
    }

    private WarningLog MapToWarningLog(LogEvent logEvent)
    {
        return new WarningLog
        {
            DateTime = logEvent.Timestamp.DateTime,
            Level = logEvent.Level.ToString(),
            Message = logEvent.RenderMessage(),
            MessageTemplate = logEvent.MessageTemplate.Text,
            Properties = SerializeProperties(logEvent.Properties)
        };
    }

    private ErrorLog MapToErrorLog(LogEvent logEvent)
    {
        return new ErrorLog
        {
            DateTime = logEvent.Timestamp.DateTime,
            Level = logEvent.Level.ToString(),
            Message = logEvent.RenderMessage(),
            MessageTemplate = logEvent.MessageTemplate.Text,
            Properties = SerializeProperties(logEvent.Properties),
            Exception = logEvent.Exception?.ToString()
        };
    }

    private AuditSqlLog MapToAuditSqlLog(LogEvent logEvent)
    {
        return new AuditSqlLog
        {
            DateTime = logEvent.Timestamp.DateTime,
            Level = logEvent.Level.ToString(),
            Message = logEvent.RenderMessage(),
            MessageTemplate = logEvent.MessageTemplate.Text,
            Properties = SerializeProperties(logEvent.Properties)
        };
    }

    private string SerializeProperties(IReadOnlyDictionary<string, LogEventPropertyValue> properties)
    {
        if (properties == null || !properties.Any())
            return string.Empty;

        try
        {
            return System.Text.Json.JsonSerializer.Serialize(properties);
        }
        catch
        {
            return string.Empty;
        }
    }

    #endregion
}
