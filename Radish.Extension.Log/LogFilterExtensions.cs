using Radish.Common.LogTool;
using Serilog.Events;
using SqlSugar;

namespace Radish.Extension.Log;

/// <summary>
/// 日志过滤扩展方法
/// </summary>
public static class LogFilterExtensions
{
    /// <summary>
    /// 过滤出 SQL 日志
    /// </summary>
    /// <param name="batch">日志批次</param>
    /// <param name="includeSelectQueries">是否包含 SELECT 查询</param>
    public static IEnumerable<LogEvent> FilterSqlLog(this IEnumerable<LogEvent> batch, bool includeSelectQueries = true)
    {
        var sqlLogs = batch.Where(e => e.HasProperty(LogContextTool.LogSource, LogContextTool.AopSql));

        if (!includeSelectQueries)
        {
            // 仅保留 INSERT/UPDATE/DELETE 操作
            sqlLogs = sqlLogs.Where(e =>
            {
                if (!e.Properties.TryGetValue(LogContextTool.SugarActionType, out var actionTypeValue))
                    return false;

                if (actionTypeValue is ScalarValue { Value: SugarActionType actionType })
                {
                    return actionType != SugarActionType.Query && actionType != SugarActionType.UnKnown;
                }

                return false;
            });
        }

        return sqlLogs;
    }

    /// <summary>
    /// 过滤出应用日志(非 SQL 日志)
    /// </summary>
    public static IEnumerable<LogEvent> FilterApplicationLog(this IEnumerable<LogEvent> batch)
    {
        return batch.Where(e => !e.HasProperty(LogContextTool.LogSource, LogContextTool.AopSql));
    }

    /// <summary>
    /// 检查日志事件是否包含指定属性和值
    /// </summary>
    private static bool HasProperty(this LogEvent logEvent, string propertyName, string expectedValue)
    {
        if (!logEvent.Properties.TryGetValue(propertyName, out var propertyValue))
            return false;

        return propertyValue is ScalarValue { Value: string value } && value == expectedValue;
    }
}
