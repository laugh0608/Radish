using Microsoft.Extensions.Logging;
using Radish.Common.OptionTool;

namespace Radish.Extension.AopExtension;

/// <summary>SQL 只输出受控操作类别、参数数量与耗时；不读取 SQL 正文或参数值。</summary>
public sealed class SqlSugarAop(
    ILogger<SqlSugarAop> logger,
    SqlAopLogOptions options,
    bool diagnosticsEnabled)
{
    public void Executing(string user, string table, string operate, int parameterCount)
    {
        if (!diagnosticsEnabled || !options.Enabled ||
            Contains(options.SkipUsers, user) || Contains(options.SkipTables, table)) return;
        var enabled = operate.ToUpperInvariant() switch
        {
            "QUERY" => options.LogQuery,
            "INSERT" => options.LogInsert,
            "UPDATE" => options.LogUpdate,
            "DELETE" => options.LogDelete,
            _ => false
        };
        if (enabled) Write("database.diagnostic", LogLevel.Information, operate, parameterCount, 0, true);
    }

    public void Executed(string operate, int parameterCount, TimeSpan elapsed)
    {
        // 普通诊断的 Enabled、CRUD 和跳过名单均不能屏蔽慢查询。
        if (options.SlowQueryEnabled && elapsed.TotalMilliseconds >= options.SlowQueryThresholdMs)
            Write("database.slow", LogLevel.Warning, operate, parameterCount, elapsed.TotalMilliseconds);
    }

    public void ConnectionChecked(TimeSpan elapsed)
    {
        if (options.SlowConnectionEnabled && elapsed.TotalMilliseconds >= options.SlowConnectionThresholdMs)
            Write("database.slow", LogLevel.Warning, "CONNECT", 0, elapsed.TotalMilliseconds);
    }

    private void Write(string code, LogLevel level, string operate, int parameterCount, double duration, bool diagnostic = false)
    {
        var operation = operate.ToUpperInvariant() switch
        {
            "QUERY" => "select", "INSERT" => "insert", "UPDATE" => "update",
            "DELETE" => "delete", "CONNECT" => "connect", _ => "other"
        };
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["EventCode"] = code, ["SourceCategory"] = "database", ["Diagnostic"] = diagnostic,
            ["LogSource"] = "AopSql"
        });
        logger.Log(level, "Database {operation}; parameters={parameterCount}; duration={durationMs} ms",
            operation, parameterCount, duration);
    }

    private static bool Contains(IEnumerable<string>? values, string target) =>
        values?.Any(value => string.Equals(value?.Trim(), target.Trim(), StringComparison.OrdinalIgnoreCase)) == true;
}
