using System.Diagnostics;
using Radish.Common.LogTool;
using Serilog;

namespace Radish.Service.Internal;

/// <summary>服务内清理的局部统计；不接收会话、令牌、路径或异常正文。</summary>
internal sealed class ServiceCleanupSummary(string jobKind) : IDisposable
{
    private readonly long _started = Stopwatch.GetTimestamp();
    private string? _failureKind;
    public int ProcessedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int RemovedDirectoryCount { get; set; }
    public int SettlementCount { get; set; }
    public int SkippedCount { get; set; }
    public bool Completed { get; set; }
    private int FailedCount { get; set; }

    public void RecordFailure(Exception exception)
    {
        FailedCount++;
        var kind = RuntimeFailureSummary.Classify(exception);
        _failureKind = _failureKind == null || _failureKind == kind ? kind : "other";
    }

    public void Dispose()
    {
        // 幂等结算没有返回实际变更数；仅结算重放 / 并发跳过不能视为新清理成果。
        if (UpdatedCount == 0 && RemovedDirectoryCount == 0 && FailedCount == 0) return;
        var changed = UpdatedCount > 0 || RemovedDirectoryCount > 0;
        var code = FailedCount > 0 ? "job.cleanup.failed"
            : Completed ? "job.cleanup.completed" : "job.cleanup.interrupted";
        var outcome = FailedCount > 0 ? (changed ? "partial" : "failed") : Completed ? "succeeded" : "partial";
        var logger = Log.ForContext("EventCode", code).ForContext("SourceCategory", "job")
            .ForContext("jobKind", jobKind).ForContext("outcome", outcome)
            .ForContext("processedCount", ProcessedCount).ForContext("updatedCount", UpdatedCount)
            .ForContext("removedDirectoryCount", RemovedDirectoryCount).ForContext("settlementCount", SettlementCount)
            .ForContext("skippedCount", SkippedCount).ForContext("failedCount", FailedCount)
            .ForContext("durationMs", Stopwatch.GetElapsedTime(_started).TotalMilliseconds);
        if (_failureKind != null) logger = logger.ForContext("failureKind", _failureKind);
        // 未消费的异常保持传播；这里只报告此前进度，不再记同一异常的 Error。
        if (FailedCount > 0) logger.Error("Service cleanup consumed failures");
        else logger.Information("Service cleanup progress recorded");
    }

    public static void RecordStandaloneFailure(string operation, Exception exception) =>
        Log.ForContext("EventCode", "upload.cleanup.failed").ForContext("SourceCategory", "application")
            .ForContext("cleanupOperation", operation)
            .ForContext("failureKind", RuntimeFailureSummary.Classify(exception))
            .Error("Upload cleanup operation consumed a failure");
}
