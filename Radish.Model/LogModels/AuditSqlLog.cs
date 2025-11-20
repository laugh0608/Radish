using SqlSugar;

namespace Radish.Model.LogModels;

/// <summary>SQL 审计日志模型类</summary>
/// <remarks>按月分表(自带分表支持: 年、季、月、周、日)</remarks>
[Tenant(configId: "Log")] // 就是在 appsetting 中配置的 "ConnId": "Log"
[SplitTable(SplitType.Month)] // 目前是按月分表
[SugarTable($@"{nameof(AuditSqlLog)}_{{year}}{{month}}{{day}}")] // [SugarTable("AuditSqlLog_20231201")]，标准格式，不能更改
public class AuditSqlLog : BaseLog
{
}