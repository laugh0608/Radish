using Radish.Common.OptionTool.Core;

namespace Radish.Common.OptionTool;

/// <summary>
/// Serilog 日志配置选项
/// </summary>
public sealed class SerilogOptions : IConfigurableOptions
{
    /// <summary>
    /// 最小日志级别
    /// </summary>
    /// <remarks>可选值: Verbose, Debug, Information, Warning, Error, Fatal</remarks>
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>
    /// 控制台输出配置
    /// </summary>
    public ConsoleWriteConfig Console { get; set; } = new();

    /// <summary>
    /// 文件输出配置
    /// </summary>
    public FileWriteConfig File { get; set; } = new();

    /// <summary>
    /// 数据库输出配置
    /// </summary>
    public DatabaseWriteConfig Database { get; set; } = new();
}

/// <summary>
/// 控制台输出配置
/// </summary>
public sealed class ConsoleWriteConfig
{
    /// <summary>
    /// 是否启用控制台输出
    /// </summary>
    public bool Enable { get; set; } = true;

    /// <summary>
    /// 是否输出应用日志到控制台
    /// </summary>
    public bool EnableApplicationLog { get; set; } = true;

    /// <summary>
    /// 是否输出 SQL 日志到控制台
    /// </summary>
    public bool EnableSqlLog { get; set; } = true;
}

/// <summary>
/// 文件输出配置
/// </summary>
public sealed class FileWriteConfig
{
    /// <summary>
    /// 是否启用文件输出
    /// </summary>
    public bool Enable { get; set; } = true;

    /// <summary>
    /// 是否输出应用日志到文件
    /// </summary>
    public bool EnableApplicationLog { get; set; } = true;

    /// <summary>
    /// 是否输出 SQL 日志到文件
    /// </summary>
    public bool EnableSqlLog { get; set; } = true;

    /// <summary>
    /// 文件保留天数
    /// </summary>
    public int RetainedFileCountLimit { get; set; } = 31;
}

/// <summary>
/// 数据库输出配置
/// </summary>
public sealed class DatabaseWriteConfig
{
    /// <summary>
    /// 是否启用数据库输出
    /// </summary>
    public bool Enable { get; set; } = false;

    /// <summary>
    /// 是否记录应用日志到数据库
    /// </summary>
    public bool EnableApplicationLog { get; set; } = true;

    /// <summary>
    /// 是否记录 SQL 日志到数据库
    /// </summary>
    public bool EnableSqlLog { get; set; } = true;

    /// <summary>
    /// 是否记录 SELECT 查询
    /// </summary>
    /// <remarks>false 时仅记录 INSERT/UPDATE/DELETE 操作</remarks>
    public bool LogSelectQueries { get; set; } = true;

    /// <summary>
    /// 批处理大小限制
    /// </summary>
    public int BatchSizeLimit { get; set; } = 500;

    /// <summary>
    /// 批处理周期(秒)
    /// </summary>
    public int PeriodSeconds { get; set; } = 1;

    /// <summary>
    /// 是否立即发送第一个事件
    /// </summary>
    public bool EagerlyEmitFirstEvent { get; set; } = true;

    /// <summary>
    /// 队列限制
    /// </summary>
    public int QueueLimit { get; set; } = 10000;
}
