using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SqlSugar;

namespace Radish.Model.LogModels;

/// <summary>拒收尝试的日桶；只包含受信来源与固定原因，重放可重复计数。</summary>
[Tenant("Log")]
[SugarTable("runtime_log_rejection_bucket")]
[SugarIndex("ix_runtime_log_rejection_day", nameof(DayUtc), OrderByType.Asc)]
public sealed class RuntimeLogRejectionBucket
{
    [SugarColumn(ColumnName = "id", IsPrimaryKey = true, Length = 64)]
    public string Id { get; set; } = string.Empty;
    [SugarColumn(ColumnName = "deployment_id", Length = 64)]
    public string DeploymentId { get; set; } = string.Empty;
    [SugarColumn(ColumnName = "service", Length = 64)]
    public string Service { get; set; } = string.Empty;
    [SugarColumn(ColumnName = "reason", Length = 32)]
    public string Reason { get; set; } = string.Empty;
    [SugarColumn(ColumnName = "day_utc")]
    public DateTime DayUtc { get; set; }
    [SugarColumn(ColumnName = "attempt_count")]
    public long AttemptCount { get; set; }
    public static RuntimeLogRejectionBucket Create(string deployment, string service, string reason, DateTime utc)
    {
        var day = utc.Date;
        var key = string.Join('|', deployment, service, reason, day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        return new RuntimeLogRejectionBucket
        {
            Id = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key))), DeploymentId = deployment,
            Service = service, Reason = reason, DayUtc = day, AttemptCount = 1
        };
    }
}
