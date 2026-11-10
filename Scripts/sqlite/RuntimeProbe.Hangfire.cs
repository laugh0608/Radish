using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Hangfire.Storage.SQLite;
using Microsoft.Extensions.Configuration;
using Radish.Api.Services;

namespace Radish.SqliteDiagnostics;

public static partial class SqliteNativeRuntimeProbe
{
    private static void CheckHangfire(string directory)
    {
        var storage = new SQLiteStorage(Path.Combine(directory, "hangfire.db"), new SQLiteStorageOptions
        {
            QueuePollInterval = TimeSpan.FromMilliseconds(100)
        });
        try
        {
            JobStorage.Current = storage;
            using var connection = storage.GetConnection();
            var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hangfire:Shop:DailyStats:Enable"] = "true"
            }).Build();
            ApiRecurringJobRegistration.Register(configuration, zone);
            var jobs = connection.GetRecurringJobs();
            var expectedIds = new[] { "reliable-outbox-dispatch", "cleanup-chat-reaction-operations", "cleanup-wiki-terminal-draft-payloads",
                "cleanup-notification-inbox", "cleanup-deleted-files", "cleanup-temp-files", "cleanup-recycle-bin", "cleanup-orphan-attachments",
                "cleanup-expired-upload-sessions", "cleanup-expired-access-tokens", "comment-highlight-stat", "retention-reward",
                "forum-post-lottery-auto-draw", "shop-cancel-timeout-orders", "shop-mark-expired-benefits", "shop-daily-stats" };
            var schedules = new[] { "*/1 * * * *", "15 4 * * *", "45 3 * * *", "30 3 * * *", "0 3 * * *", "0 * * * *", "0 4 * * *",
                "0 5 * * *", "*/15 * * * *", "0 7 * * *", "0 1 * * *", "0 2 * * 0", "*/1 * * * *", "*/10 * * * *", "0 0 * * *", "0 1 * * *" };
            var methods = new[] { "DispatchAsync", "ExecuteAsync", "ExecuteAsync", "ExecuteAsync", "CleanupDeletedFilesAsync", "CleanupTempFilesAsync",
                "CleanupRecycleBinAsync", "CleanupOrphanAttachmentsAsync", "CleanupExpiredSessionsAsync", "CleanupExpiredTokensAsync", "ExecuteAsync",
                "ExecuteAsync", "ExecuteAutoDrawAsync", "CancelTimeoutOrdersAsync", "MarkExpiredBenefitsAsync", "GenerateDailyStatsAsync" };
            var arguments = new[] { "[50]", "[500]", "[90,200]", "[200,5000]", "[30]", "[2]", "[90]", "[24]", "[]", "[]", "[null]", "[]", "[20]", "[30]", "[]", "[]" };
            Require(jobs.Count == expectedIds.Length, "Recurring registration count mismatch");
            for (var index = 0; index < expectedIds.Length; index++)
            {
                var job = jobs.Single(item => item.Id == expectedIds[index]);
                Require(job.Cron == schedules[index] && job.TimeZoneId == (index < 4 ? TimeZoneInfo.Utc.Id : zone.Id)
                    && job.Job.Method.Name == methods[index] && JsonSerializer.Serialize(job.Job.Args) == arguments[index], "Recurring registration contract mismatch: " + job.Id);
                new RecurringJobManager(storage).RemoveIfExists(job.Id);
            }
            const string queue = "sqlite-native-probe";
            var client = new BackgroundJobClient(storage);
            var marker = Path.Combine(directory, "success.txt");
            var retryMarker = Path.Combine(directory, "retry.txt");
            var successId = client.Create(Job.FromExpression(() => SqliteProbeJobs.Succeed(marker)), new EnqueuedState(queue));
            var retryId = client.Create(Job.FromExpression(() => SqliteProbeJobs.Retry(retryMarker)), new EnqueuedState(queue));
            using (var worker = new BackgroundJobServer(new BackgroundJobServerOptions
            {
                Queues = new[] { queue }, WorkerCount = 1, SchedulePollingInterval = TimeSpan.FromMilliseconds(100), ShutdownTimeout = TimeSpan.FromSeconds(5)
            }, storage))
            {
                var timer = Stopwatch.StartNew();
                while (timer.Elapsed < TimeSpan.FromSeconds(20))
                {
                    if (connection.GetStateData(successId)?.Name == "Succeeded" && connection.GetStateData(retryId)?.Name == "Succeeded") break;
                    Thread.Sleep(50);
                }
                var success = connection.GetStateData(successId);
                var retry = connection.GetStateData(retryId);
                File.WriteAllText(Path.Combine(directory, "job-states.json"), JsonSerializer.Serialize(new { success, retry }, new JsonSerializerOptions { WriteIndented = true }));
                Require(success?.Name == "Succeeded" && retry?.Name == "Succeeded", "Probe execution/retry did not succeed");
            }
            Require(File.ReadAllText(marker) == "success" && File.ReadAllText(retryMarker) == "first\nretry", "Probe execution markers mismatch");
            File.WriteAllText(Path.Combine(directory, "hangfire-result.json"), JsonSerializer.Serialize(new { registered = jobs.Count, successId, retryId, retryAttempts = 2 }));
        }
        finally
        {
            (storage as IDisposable)?.Dispose();
        }
    }
}

[Queue("sqlite-native-probe")]
public static class SqliteProbeJobs
{
    public static void Succeed(string marker) => File.WriteAllText(marker, "success");

    [AutomaticRetry(Attempts = 1, DelaysInSeconds = new[] { 0 })]
    public static void Retry(string marker)
    {
        if (!File.Exists(marker))
        {
            File.WriteAllText(marker, "first");
            throw new InvalidOperationException("planned SQLite probe retry");
        }
        File.AppendAllText(marker, "\nretry");
    }
}
