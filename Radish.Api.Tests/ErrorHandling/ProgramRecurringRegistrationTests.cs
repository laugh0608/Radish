using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.Configuration;
using Moq;
using Radish.Api.Services;
using Radish.Extension.Log;
using Serilog;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class ProgramRecurringRegistrationTests
{
    private const string Child = "RADISH_RECURRING_REGISTRATION_TEST_CHILD";
    private static readonly string[] Ids = ["reliable-outbox-dispatch", "cleanup-chat-reaction-operations",
        "cleanup-wiki-terminal-draft-payloads", "cleanup-notification-inbox", "cleanup-deleted-files", "cleanup-temp-files",
        "cleanup-recycle-bin", "cleanup-orphan-attachments", "cleanup-expired-upload-sessions", "cleanup-expired-access-tokens",
        "comment-highlight-stat", "retention-reward", "forum-post-lottery-auto-draw", "shop-cancel-timeout-orders",
        "shop-mark-expired-benefits", "shop-daily-stats"];
    private static readonly string[] Kinds = ["outbox-dispatch", "chat-reactions", "wiki-drafts", "notification-inbox",
        "file-deleted", "file-temp", "file-recycle", "file-orphan", "upload-sessions", "file-tokens", "comment-highlight",
        "retention-rewards", "post-lottery", "shop-timeouts", "shop-benefits", "shop-stats"];
    private static readonly string[] Schedules = ["*/1 * * * *", "15 4 * * *", "45 3 * * *", "30 3 * * *", "0 3 * * *",
        "0 * * * *", "0 4 * * *", "0 5 * * *", "*/15 * * * *", "0 7 * * *", "0 1 * * *", "0 2 * * 0", "*/1 * * * *",
        "*/10 * * * *", "0 0 * * *", "0 1 * * *"];

    [Fact]
    public async Task RealStaticRegistrations_ShouldKeepOrderArgumentsConditionsAndFailureBoundary()
    {
        if (Environment.GetEnvironmentVariable(Child) == "1") { await RunChild(); return; }
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(typeof(ProgramRecurringRegistrationTests).Assembly.Location);
        foreach (var arg in new[] { "-noLogo", "-noColor", "-class", typeof(ProgramRecurringRegistrationTests).FullName! }) start.ArgumentList.Add(arg);
        start.Environment[Child] = "1";
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); } }
        var text = await stdout + await stderr;
        Assert.True(process.ExitCode == 0, "Isolated registration test failed: " + text);
    }

    private static async Task RunChild()
    {
        // RecurringJob has a process-wide Lazy manager. Keep its real static path in a disposable test process.
        var writes = new List<(string Id, Dictionary<string, string> Hash)>();
        var connection = new Mock<IStorageConnection>();
        var transaction = new Mock<IWriteOnlyTransaction>();
        var storage = new Mock<JobStorage>();
        storage.Setup(s => s.GetConnection()).Returns(connection.Object);
        connection.Setup(c => c.AcquireDistributedLock(It.IsAny<string>(), It.IsAny<TimeSpan>())).Returns(Mock.Of<IDisposable>());
        connection.Setup(c => c.CreateWriteTransaction()).Returns(transaction.Object);
        transaction.Setup(t => t.SetRangeInHash(It.IsAny<string>(), It.IsAny<IEnumerable<KeyValuePair<string, string>>>()))
            .Callback<string, IEnumerable<KeyValuePair<string, string>>>((id, hash) => writes.Add((id, hash.ToDictionary(p => p.Key, p => p.Value))));
        Action commit = () => { };
        transaction.Setup(t => t.Commit()).Callback(() => commit());
        JobStorage.Current = storage.Object;
        GlobalConfiguration.Configuration.UseLogProvider(new HangfireRuntimeLogProvider());
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
        var previous = Log.Logger;
        try
        {
            foreach (var candidate in new[] { false, true })
            foreach (var environment in new[] { "Production", "Development" })
            {
                using var capture = new HttpBoundaryCapture(candidate, environment, diagnostics: false);
                Log.Logger = capture.Logger;
                var commits = 0;
                commit = () => { Assert.Equal(commits, capture.Events.Count(e => e.Code == "job.recurring_registration_returned")); commits++; };
                ApiRecurringJobRegistration.Register(Config(new() { ["Hangfire:Shop:DailyStats:Enable"] = "true" }), zone);
                Assert.Equal(16, commits);
                Assert.Equal(Ids.Select(id => "recurring-job:" + id), writes.Select(w => w.Id));
                Assert.Equal(16, capture.Events.Count(e => e.Code == "job.recurring_registration_returned"));
                Assert.Equal(Kinds, JobKinds(capture));
                for (var i = 0; i < 16; i++)
                {
                    Assert.Equal(Schedules[i], writes[i].Hash["Cron"]);
                    Assert.Equal(i < 4 ? TimeZoneInfo.Utc.Id : zone.Id, writes[i].Hash["TimeZoneId"]);
                }
                var jobs = writes.Select(w => InvocationData.DeserializePayload(w.Hash["Job"]).DeserializeJob()).ToArray();
                Assert.Equal(new[] { "DispatchAsync", "ExecuteAsync", "ExecuteAsync", "ExecuteAsync", "CleanupDeletedFilesAsync",
                    "CleanupTempFilesAsync", "CleanupRecycleBinAsync", "CleanupOrphanAttachmentsAsync", "CleanupExpiredSessionsAsync",
                    "CleanupExpiredTokensAsync", "ExecuteAsync", "ExecuteAsync", "ExecuteAutoDrawAsync", "CancelTimeoutOrdersAsync",
                    "MarkExpiredBenefitsAsync", "GenerateDailyStatsAsync" }, jobs.Select(j => j.Method.Name));
                Assert.Equal(new object[] { 50 }, jobs[0].Args);
                Assert.Equal(new object[] { 500 }, jobs[1].Args);
                Assert.Equal(new object[] { 90, 200 }, jobs[2].Args);
                Assert.Equal(new object[] { 200, 5000 }, jobs[3].Args);
                Assert.Equal(new object[] { 30 }, jobs[4].Args);
                Assert.Equal(new object[] { 2 }, jobs[5].Args);
                Assert.Equal(new object[] { 90 }, jobs[6].Args);
                Assert.Equal(new object[] { 24 }, jobs[7].Args);
                Assert.Equal(new object?[] { null }, jobs[10].Args);
                Assert.Equal(new object[] { 20 }, jobs[12].Args);
                Assert.Equal(new object[] { 30 }, jobs[13].Args);
                capture.AssertSafe();
                capture.Clear(); writes.Clear(); commits = 0;
                ApiRecurringJobRegistration.Register(Config(new()), zone);
                Assert.Equal(15, commits); // DailyStats remains disabled by default.
                capture.Clear(); writes.Clear(); commits = 0;
                var disabled = new Dictionary<string, string?>();
                foreach (var key in new[] { "WikiDraftPayloadCleanup", "NotificationInboxCleanup", "FileCleanup:DeletedFiles",
                    "FileCleanup:TempFiles", "FileCleanup:RecycleBin", "FileCleanup:OrphanAttachments", "CommentHighlight", "RetentionReward",
                    "PostLottery", "Shop:TimeoutOrders", "Shop:ExpiredBenefits", "Shop:DailyStats" }) disabled["Hangfire:" + key + ":Enable"] = "false";
                ApiRecurringJobRegistration.Register(Config(disabled), zone);
                Assert.Equal(new[] { "outbox-dispatch", "chat-reactions", "upload-sessions", "file-tokens" }, JobKinds(capture));
                capture.Clear(); writes.Clear(); commits = 0;
                var failure = new IOException(HttpBoundaryCapture.Secret);
                commit = () => { if (++commits == 3) throw failure; };
                Assert.Same(failure, Assert.Throws<IOException>(() => ApiRecurringJobRegistration.Register(Config(new()), zone)));
                Assert.Equal(3, commits);
                Assert.Equal(new[] { "outbox-dispatch", "chat-reactions" }, JobKinds(capture));
                capture.AssertSafe();
                capture.Clear(); writes.Clear(); commits = 0;
                using var error = new StringWriter();
                var exit = await RuntimeProcess.RunAsync("api", () => { ApiRecurringJobRegistration.Register(Config(new()), zone); return Task.CompletedTask; }, error);
                Assert.Equal(1, exit);
                using var json = JsonDocument.Parse(error.ToString());
                Assert.Equal("runtime.failed", json.RootElement.GetProperty("eventCode").GetString());
                Assert.Equal("io", json.RootElement.GetProperty("properties").GetProperty("failureKind").GetString());
                Assert.DoesNotContain(HttpBoundaryCapture.Secret, error.ToString());
                Assert.Equal(new[] { "outbox-dispatch", "chat-reactions" }, JobKinds(capture));
                capture.Clear(); writes.Clear(); commits = 0;
                commit = () => { commits++; };
                var configured = Config(new()
                {
                    ["Hangfire:WikiDraftPayloadCleanup:BatchSize"] = "-7", ["Document:Authoring:TerminalDraftRetentionDays"] = "-2",
                    ["Hangfire:WikiDraftPayloadCleanup:Schedule"] = "1 2 * * *"
                });
                ApiRecurringJobRegistration.Register(configured, zone);
                Assert.Equal(new object[] { -2, -7 }, InvocationData.DeserializePayload(writes[2].Hash["Job"]).DeserializeJob().Args);
                Assert.Equal("1 2 * * *", writes[2].Hash["Cron"]); // Logging adds no parameter clamps or schedule validation.
                capture.AssertSafe();
                capture.Clear(); writes.Clear(); commits = 0;
                Assert.Throws<ArgumentException>(() => ApiRecurringJobRegistration.Register(Config(new()
                    { ["Hangfire:WikiDraftPayloadCleanup:Schedule"] = HttpBoundaryCapture.Secret }), zone));
                Assert.Equal(2, commits);
                Assert.Equal(new[] { "outbox-dispatch", "chat-reactions" }, JobKinds(capture));
                capture.AssertSafe();
                capture.Clear(); writes.Clear(); commits = 0;
                Assert.Throws<InvalidOperationException>(() => ApiRecurringJobRegistration.Register(Config(new()
                    { ["Hangfire:WikiDraftPayloadCleanup:BatchSize"] = HttpBoundaryCapture.Secret }), zone));
                Assert.Equal(2, commits);
                Assert.Equal(new[] { "outbox-dispatch", "chat-reactions" }, JobKinds(capture));
                capture.AssertSafe();
                capture.Clear(); writes.Clear(); commits = 0;
                foreach (var audit in new[] { false, true })
                {
                    var failingSink = new ProgramThrowingSink(failure);
                    var logging = new LoggerConfiguration();
                    if (audit) logging.AuditTo.Sink(new HostProgramLogSink(failingSink));
                    else logging.WriteTo.Sink(new HostProgramLogSink(failingSink));
                    using var logger = logging.CreateLogger();
                    Log.Logger = logger;
                    if (audit) Assert.Contains(failure, Assert.Throws<AggregateException>(() =>
                        ApiRecurringJobRegistration.Register(Config(new()), zone)).Flatten().InnerExceptions);
                    else ApiRecurringJobRegistration.Register(Config(new()), zone);
                    Assert.Equal(audit ? 1 : 15, commits);
                    Assert.Equal(audit ? 1 : 15, failingSink.Attempts);
                    Log.Logger = capture.Logger;
                    writes.Clear(); commits = 0;
                }
            }
        }
        finally { Log.Logger = previous; }
    }

    private static IConfiguration Config(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static string?[] JobKinds(HttpBoundaryCapture capture) => capture.Candidate
        ? capture.Output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line =>
        {
            using var json = JsonDocument.Parse(line);
            return json.RootElement.GetProperty("eventCode").GetString() == "job.recurring_registration_returned"
                ? json.RootElement.GetProperty("properties").GetProperty("jobKind").GetString() : null;
        }).Where(kind => kind != null).ToArray()
        : capture.Sink.Events.Where(e => e.Properties.TryGetValue("EventCode", out var code) &&
            code is Serilog.Events.ScalarValue { Value: "job.recurring_registration_returned" })
            .Select(e => ((Serilog.Events.ScalarValue)e.Properties["jobKind"]).Value as string).ToArray();
}
