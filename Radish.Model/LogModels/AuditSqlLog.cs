using SqlSugar;

namespace Radish.Model.LogModels;

/// <summary>数据库日志模型类</summary>
/// <remarks>按月分表(自带分表支持: 年、季、月、周、日)</remarks>
[Tenant(configId: "log")]
[SplitTable(SplitType.Month)]
[SugarTable($@"{nameof(AuditSqlLog)}_{{year}}{{month}}{{day}}")]
public class AuditSqlLog : BaseLog
{
}