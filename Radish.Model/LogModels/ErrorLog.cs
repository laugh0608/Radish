using SqlSugar;

namespace Radish.Model.LogModels;

/// <summary>
/// Error/Fatal 级别日志模型类
/// </summary>
/// <remarks>按月分表(自带分表支持: 年、季、月、周、日)</remarks>
[Tenant(configId: "Log")] // 使用 Log 数据库
[SplitTable(SplitType.Month)] // 按月分表
[SugarTable($@"{nameof(ErrorLog)}_{{year}}{{month}}{{day}}")] // 标准格式：ErrorLog_20251220
public class ErrorLog : BaseLog
{
    /// <summary>
    /// 异常堆栈信息
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDataType = "longtext,text,clob")]
    public string? Exception { get; set; }
}
