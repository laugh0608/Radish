using Microsoft.Extensions.Hosting;
using Radish.Common.LogTool;
using Radish.Model;
using Serilog;

namespace Radish.Extension.Log;

/// <summary>Program 业务观察点只接受固定类别与数量，不读取配置原文或异常文本。</summary>
public static class HostProgramLog
{
    internal const string Source = "Radish.Extension.Log.HostProgramLog";

    private static ILogger Event(ILogger logger, string code, string category) => logger
        .ForContext("SourceContext", Source).ForContext("EventCode", code)
        .ForContext("SourceCategory", category).ForContext("Diagnostic", false);

    public static void RecurringJobRegistered(ILogger logger, string jobKind) =>
        Event(logger, "job.recurring_registration_returned", "job").ForContext("jobKind", jobKind)
            .Information("Recurring job registration returned.");

    public static void BuiltInSyncSkipped(ILogger logger, WikiBuiltInSyncSkipReason reason) =>
        Event(logger, "document.builtin_sync_skipped", "application")
            .ForContext("builtInSyncSkipReason", reason switch
            {
                WikiBuiltInSyncSkipReason.Disabled => "disabled",
                WikiBuiltInSyncSkipReason.BothTablesMissing => "both-tables-missing",
                WikiBuiltInSyncSkipReason.DocumentTableMissing => "document-table-missing",
                WikiBuiltInSyncSkipReason.RevisionTableMissing => "revision-table-missing",
                WikiBuiltInSyncSkipReason.DirectoryMissing => "directory-missing",
                WikiBuiltInSyncSkipReason.NoMarkdown => "no-markdown",
                _ => "unknown"
            }).Information("Built-in document startup sync skipped.");

    public static void BuiltInSyncCompleted(ILogger logger, WikiBuiltInSyncSummary summary) =>
        Event(logger, "document.builtin_sync_completed", "application")
            .ForContext("markdownFileCount", summary.MarkdownFileCount)
            .ForContext("descriptorCount", summary.DescriptorCount)
            .ForContext("generatedNodeCount", summary.GeneratedNodeCount)
            .ForContext("syncedCount", summary.SyncedCount)
            .ForContext("createdCount", summary.CreatedCount)
            .ForContext("updatedCount", summary.UpdatedCount)
            .ForContext("restoredCount", summary.RestoredCount)
            .ForContext("parentAdjustedCount", summary.ParentAdjustedCount)
            .ForContext("softDeletedCount", summary.SoftDeletedCount)
            .ForContext("skippedCount", summary.SkippedCount)
            .Information("Built-in document startup sync completed.");

    public static void BuiltInSyncFailed(ILogger logger, Exception exception) =>
        Event(logger, "document.builtin_sync_failed", "application")
            .ForContext("failureKind", RuntimeFailureSummary.Classify(exception))
            .Error("Built-in document startup sync failed.");

    /// <summary>仅代表停止通知；后续宿主释放仍可能失败。回调不拥有 logger。</summary>
    public static IDisposable RegisterGatewayStopped(IHostApplicationLifetime lifetime, ILogger logger) =>
        lifetime.ApplicationStopped.Register(() => Event(logger, "runtime.stopped", "lifecycle")
            .Information("Gateway stopped notification received."));
}
