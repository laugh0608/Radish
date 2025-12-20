using SqlSugar;

namespace Radish.Model.LogModels;

/// <summary>
/// Warning 级别日志模型类
/// </summary>
/// <remarks>按月分表(自带分表支持: 年、季、月、周、日)</remarks>
[Tenant(configId: "Log")] // 使用 Log 数据库
[SplitTable(SplitType.Month)] // 按月分表
[SugarTable($@"{nameof(WarningLog)}_{{year}}{{month}}{{day}}")] // 标准格式：WarningLog_20251220
public class WarningLog : BaseLog
{
    // 继承 BaseLog 的所有字段即可
    // - Id: long (Snowflake ID)
    // - DateTime: DateTime (分表字段)
    // - Level: string
    // - Message: string (longtext)
    // - MessageTemplate: string (longtext)
    // - Properties: string (longtext)
}
