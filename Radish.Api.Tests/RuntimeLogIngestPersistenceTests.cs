using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Radish.DbMigrate;
using Radish.IRepository;
using Radish.Model.LogModels;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests;

public sealed class RuntimeLogIngestPersistenceTests
{
    [Fact]
    public async Task ReplayAndConflict_ShouldKeepOriginalIdTimeContentAndSafeRejections()
    {
        using var test = new RuntimeLogIngestTestSupport();
        var line = test.Event();
        var first = await test.Service().IngestAsync(RuntimeLogIngestTestSupport.Body(line), CancellationToken.None);
        var replay = await test.Service().IngestAsync(RuntimeLogIngestTestSupport.Body(line + "\n" + line), CancellationToken.None);
        var conflict = await test.Service().IngestAsync(RuntimeLogIngestTestSupport.Body(line.Replace("Warning", "Error")), CancellationToken.None);
        Assert.Equal(200, first.StatusCode); Assert.Equal(1, first.Summary.VoAccepted);
        Assert.Equal(2, replay.Summary.VoDuplicate); Assert.Equal(0, replay.Summary.VoAccepted);
        Assert.Equal(1, conflict.Summary.VoRejected);
        var db = test.Db.GetConnectionScope("log");
        var stored = Assert.Single(db.Queryable<RuntimeLogEventRecord>().ToList());
        Assert.Equal("Warning", stored.Level);
        Assert.Equal(test.Clock.GetUtcNow().UtcDateTime.Ticks, stored.OccurredAtUtc.Ticks);
        Assert.Equal(test.Clock.GetUtcNow().UtcDateTime.Ticks, stored.IngestedAtUtc.Ticks);
        Assert.Equal("content-conflict", Assert.Single(db.Queryable<RuntimeLogRejectionBucket>().ToList()).Reason);
        Assert.Empty(RuntimeLogIngestSchemaMigration.Instance.Verify(db, test.Services));
    }

    [Fact]
    public async Task LaterTransactionFailure_ShouldPreserveCommittedBatchAndReplayWithoutDuplicates()
    {
        using var test = new RuntimeLogIngestTestSupport();
        var lines = string.Join('\n', Enumerable.Range(0, 201).Select(_ => test.Event()));
        var fail = new FailSecondCommit(test.Repository);
        var result = await test.Service(fail).IngestAsync(RuntimeLogIngestTestSupport.Body(lines), CancellationToken.None);
        Assert.Equal(503, result.StatusCode); Assert.Equal(200, result.Summary.VoAccepted);
        var retry = await test.Service().IngestAsync(RuntimeLogIngestTestSupport.Body(lines), CancellationToken.None);
        Assert.Equal(200, retry.StatusCode); Assert.Equal(200, retry.Summary.VoDuplicate); Assert.Equal(1, retry.Summary.VoAccepted);
        Assert.Equal(201, test.Db.GetConnectionScope("log").Queryable<RuntimeLogEventRecord>().Count());
        Assert.DoesNotContain("PRIVATE", test.Emergency.ToString());
    }

    [Fact]
    public async Task TransactionFailureAfterInsert_ShouldRollBackEventsAndRejectionsTogether()
    {
        using var test = new RuntimeLogIngestTestSupport();
        var db = test.Db.GetConnectionScope("log");
        db.Ado.ExecuteCommand("CREATE TRIGGER fail_bucket BEFORE INSERT ON runtime_log_rejection_bucket BEGIN SELECT RAISE(ABORT, 'PRIVATE'); END");
        var result = await test.Service().IngestAsync(RuntimeLogIngestTestSupport.Body(test.Event() + "\n{}"), CancellationToken.None);
        Assert.Equal(503, result.StatusCode); Assert.Equal(0, result.Summary.VoAccepted);
        Assert.Equal(0, db.Queryable<RuntimeLogEventRecord>().Count());
        Assert.Equal(0, db.Queryable<RuntimeLogRejectionBucket>().Count());
        Assert.DoesNotContain("PRIVATE", test.Emergency.ToString());
    }

    [Fact]
    public async Task ConcurrentReplay_ShouldUseDatabaseConstraint()
    {
        using var test = new RuntimeLogIngestTestSupport();
        var line = test.Event();
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
            await test.Service().IngestAsync(RuntimeLogIngestTestSupport.Body(line), CancellationToken.None))));
        Assert.All(results, result => Assert.Equal(200, result.StatusCode));
        Assert.Equal(1, results.Sum(r => r.Summary.VoAccepted));
        Assert.Equal(1, results.Sum(r => r.Summary.VoDuplicate));
        Assert.Equal(1, test.Db.GetConnectionScope("log").Queryable<RuntimeLogEventRecord>().Count());
    }

    [Fact]
    public async Task Rejections_ShouldAggregateExpireAndNeverPersistOriginalPayload()
    {
        using var test = new RuntimeLogIngestTestSupport();
        var db = test.Db.GetConnectionScope("log");
        db.Insertable(RuntimeLogRejectionBucket.Create("local", "log-collector", "invalid-event", test.Clock.GetUtcNow().UtcDateTime.AddDays(-9))).ExecuteCommand();
        var result = await test.Service().IngestAsync(RuntimeLogIngestTestSupport.Body("{\"PRIVATE\":\"PRIVATE\"}\n{\"PRIVATE\":0}\n" + test.Event()), CancellationToken.None);
        Assert.Equal(200, result.StatusCode); Assert.Equal(2, result.Summary.VoRejected);
        var bucket = Assert.Single(db.Queryable<RuntimeLogRejectionBucket>().ToList());
        Assert.Equal(2, bucket.AttemptCount);
        db.Updateable<RuntimeLogRejectionBucket>().SetColumns(x => x.AttemptCount == 9_007_199_254_740_991L).Where(x => x.Id == bucket.Id).ExecuteCommand();
        await test.Service().IngestAsync(RuntimeLogIngestTestSupport.Body("{\"PRIVATE\":0}"), CancellationToken.None);
        Assert.Equal(9_007_199_254_740_991L, db.Queryable<RuntimeLogRejectionBucket>().Single().AttemptCount);
        Assert.DoesNotContain("PRIVATE", string.Join(' ', db.Queryable<RuntimeLogEventRecord>().ToList().Select(e => e.CanonicalJson)));
    }

    [Fact]
    public async Task LockedDatabase_ShouldReturnRetryableFailureAndRecover()
    {
        using var test = new RuntimeLogIngestTestSupport();
        using var connection = new SqliteConnection(test.Db.GetConnectionScope("log").CurrentConnectionConfig.ConnectionString);
        connection.Open();
        using (var transaction = connection.BeginTransaction(deferred: false))
        {
            var locked = await test.Service().IngestAsync(RuntimeLogIngestTestSupport.Body(test.Event()), CancellationToken.None);
            Assert.Equal(503, locked.StatusCode);
            Assert.Equal(0, locked.Summary.VoAccepted);
            transaction.Rollback();
        }
        var recovered = await test.Service().IngestAsync(RuntimeLogIngestTestSupport.Body(test.Event()), CancellationToken.None);
        Assert.Equal(200, recovered.StatusCode);
        Assert.Equal(1, recovered.Summary.VoAccepted);
    }

    [Fact]
    public void Migration_ShouldUpgradeExistingLedgerReenterAndRejectIndexDrift()
    {
        using var test = new RuntimeLogIngestTestSupport(migrate: false);
        var db = test.Db.GetConnectionScope("log");
        // 模拟已有 Log baseline；新 migration 之前没有新表。
        db.CodeFirst.InitTables<SchemaMigrationRecord>();
        var baselineChecksum = SchemaMigrationLedger.BaselineChecksum;
        db.Insertable(new SchemaMigrationRecord
        {
            MigrationId = SchemaMigrationLedger.BaselineMigrationId, Scope = "Log", Checksum = baselineChecksum, AppliedAtUtc = test.Clock.GetUtcNow().UtcDateTime
        }).ExecuteCommand();
        SchemaMigrationLedger.ApplyPending(test.Db, test.Services, ["log"]);
        SchemaMigrationLedger.ApplyPending(test.Db, test.Services, ["log"]);
        Assert.Empty(RuntimeLogIngestSchemaMigration.Instance.Verify(db, test.Services));
        Assert.Equal(1, db.Queryable<SchemaMigrationRecord>().Count(x => x.MigrationId == RuntimeLogIngestSchemaMigration.Instance.MigrationId));
        db.Ado.ExecuteCommand("DROP INDEX ux_runtime_log_identity");
        db.Ado.ExecuteCommand("CREATE INDEX ux_runtime_log_identity ON runtime_log_event (deployment_id,event_id)");
        Assert.Contains(RuntimeLogIngestSchemaMigration.Instance.Verify(db, test.Services), issue => issue.Contains("ux_runtime_log_identity", StringComparison.Ordinal));
        Assert.Throws<InvalidOperationException>(() => RuntimeLogIngestSchemaMigration.Instance.Apply(db, test.Services));
    }

    [Fact]
    public void NewDatabaseRegistry_ShouldContainBothTablesOnlyInLog()
    {
        Assert.Contains(typeof(RuntimeLogEventRecord), DbMigrateEntityRegistry.GetEntityTypesForConfig("Log"));
        Assert.Contains(typeof(RuntimeLogRejectionBucket), DbMigrateEntityRegistry.GetEntityTypesForConfig("Log"));
        Assert.DoesNotContain(typeof(RuntimeLogEventRecord), DbMigrateEntityRegistry.GetEntityTypesForConfig("Main"));
    }

    private sealed class FailSecondCommit(IRuntimeLogIngestRepository repository) : IRuntimeLogIngestRepository
    {
        private int _calls;
        public Task<IReadOnlyList<RuntimeLogEventRecord?>> CommitAsync(IReadOnlyList<RuntimeLogEventRecord> events,
            IReadOnlyList<RuntimeLogRejectionBucket> rejections, CancellationToken token) => ++_calls == 2
                ? throw new IOException("PRIVATE database failure") : repository.CommitAsync(events, rejections, token);
    }
}
