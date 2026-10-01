using System.Diagnostics;
using Radish.Common.LogTool;
using Serilog;

namespace Radish.Service.Jobs;

/// <summary>后台业务批次的本地计数，不接收业务标识、金额、返回正文或异常原文。</summary>
internal sealed class BusinessJobSummary(string jobKind)
{
    private readonly long _started = Stopwatch.GetTimestamp();
    private string? _failureKind;
    public int ProcessedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int RewardCount { get; set; }
    public int SkippedCount { get; set; }
    public int RejectedCount { get; set; }
    private int FailedCount { get; set; }

    public void RecordFailure(Exception exception)
    {
        FailedCount++;
        var kind = RuntimeFailureSummary.Classify(exception);
        _failureKind = _failureKind == null || _failureKind == kind ? kind : "other";
    }

    public void Write()
    {
        if (ProcessedCount == 0 && UpdatedCount == 0 && RewardCount == 0 && RejectedCount == 0 && FailedCount == 0) return;
        var hasWork = ProcessedCount > 0 || UpdatedCount > 0 || RewardCount > 0;
        var outcome = FailedCount > 0 || RejectedCount > 0 ? (hasWork ? "partial" : "failed") : "succeeded";
        var code = FailedCount > 0 ? "job.batch.failed" : RejectedCount > 0 ? "job.batch.warning" : "job.batch.completed";
        var logger = Log.ForContext("SourceCategory", "job").ForContext("EventCode", code)
            .ForContext("jobKind", jobKind).ForContext("outcome", outcome)
            .ForContext("processedCount", ProcessedCount).ForContext("updatedCount", UpdatedCount)
            .ForContext("rewardCount", RewardCount).ForContext("skippedCount", SkippedCount)
            .ForContext("rejectedCount", RejectedCount).ForContext("failedCount", FailedCount)
            .ForContext("durationMs", Stopwatch.GetElapsedTime(_started).TotalMilliseconds);
        if (_failureKind != null) logger = logger.ForContext("failureKind", _failureKind);
        if (FailedCount > 0) logger.Error("Business job batch consumed failures");
        else if (RejectedCount > 0) logger.Warning("Business job batch contains rejected results");
        else logger.Information("Business job batch completed");
    }
}
