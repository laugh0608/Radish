using Radish.Common.OptionTool.Core;

namespace Radish.Common.OptionTool;

/// <summary>SqlSugar AOP 日志配置</summary>
public sealed class SqlAopLogOptions : IConfigurableOptions
{
    /// <summary>是否启用普通 SQL 开发诊断（仍需 Development 与 Diagnostics）</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>是否记录查询日志</summary>
    public bool LogQuery { get; set; } = true;

    /// <summary>是否记录新增日志</summary>
    public bool LogInsert { get; set; } = true;

    /// <summary>是否记录更新日志</summary>
    public bool LogUpdate { get; set; } = true;

    /// <summary>是否记录删除日志</summary>
    public bool LogDelete { get; set; } = true;

    /// <summary>慢查询独立启用，不受普通 SQL 诊断筛选影响</summary>
    public bool SlowQueryEnabled { get; set; } = true;

    public int SlowQueryThresholdMs { get; set; } = 1000;

    public bool SlowConnectionEnabled { get; set; } = true;

    public int SlowConnectionThresholdMs { get; set; } = 500;

    /// <summary>直接跳过日志记录的表名</summary>
    public List<string> SkipTables { get; set; } = [];

    /// <summary>直接跳过日志记录的操作人</summary>
    public List<string> SkipUsers { get; set; } = [];
}
