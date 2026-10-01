using Radish.Common.LogTool;
using Serilog;

namespace Radish.Service;

/// <summary>币与经验发放的安全运行事件；调用点仅传固定领域 / 操作名，不接收业务载荷。</summary>
internal static class RewardRuntimeLog
{
    public static void Retry(string domain, int attempt, int delayMs) =>
        Logger(domain, "reward.retrying").ForContext("attempt", attempt).ForContext("delayMs", delayMs)
            .Warning("Reward concurrency retry scheduled");

    public static void Failure(string domain, string operation, Exception exception) =>
        Logger(domain, "reward.failed").ForContext("rewardOperation", operation)
            .ForContext("failureKind", RuntimeFailureSummary.Classify(exception))
            .Error("Reward operation consumed a failure");

    public static void Conflict(Exception exception) =>
        Logger("experience", "reward.conflict").ForContext("rewardOperation", "grant-once")
            .ForContext("failureKind", RuntimeFailureSummary.Classify(exception))
            .Warning("Reward key conflict has no existing transaction");

    public static void CacheFallback(string operation, Exception exception) =>
        Logger("experience", "reward.cache_fallback").ForContext("rewardOperation", operation)
            .ForContext("failureKind", RuntimeFailureSummary.Classify(exception))
            .Warning("Reward level configuration cache operation failed");

    public static void Batch(string domain, int succeeded, int rejected, int failed, string? failureKind)
    {
        if (succeeded == 0 && rejected == 0 && failed == 0) return;
        var logger = Logger(domain, failed > 0 ? "reward.batch_failed" : "reward.batch_completed")
            .ForContext("processedCount", succeeded).ForContext("rejectedCount", rejected)
            .ForContext("failedCount", failed)
            .ForContext("outcome", rejected + failed > 0 ? (succeeded > 0 ? "partial" : "failed") : "succeeded");
        if (failureKind != null) logger = logger.ForContext("failureKind", failureKind);
        if (failed > 0) logger.Error("Reward batch consumed failures");
        else logger.Information("Reward batch returned results");
    }

    private static ILogger Logger(string domain, string code) => Log.ForContext("SourceCategory", "application")
        .ForContext("EventCode", code).ForContext("rewardDomain", domain);
}
