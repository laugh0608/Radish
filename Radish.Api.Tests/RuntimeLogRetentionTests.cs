using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Radish.Common.LogTool;
using Radish.IRepository;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests;

public sealed class RuntimeLogRetentionTests
{
    [Fact]
    public async Task Prune_ShouldUseStrictUtcCutoffsAndOnlyDeleteEnabledDeploymentInBatches()
    {
        using var test = new RuntimeLogIngestTestSupport();
        var now = test.Clock.GetUtcNow().UtcDateTime;
        var db = test.Db.GetConnectionScope("log");
        db.Ado.ExecuteCommand("CREATE TABLE AuditLog_20240101 (marker TEXT); INSERT INTO AuditLog_20240101 VALUES ('keep');");
        db.Ado.ExecuteCommand("CREATE TABLE InformationLog_20240101 (marker TEXT); INSERT INTO InformationLog_20240101 VALUES ('keep');");
        var retention = new RuntimeLogRetentionService((IRuntimeLogRetentionRepository)test.Repository, test.Options);
        var fixtures = RuntimeLogRetention.Levels.SelectMany(level =>
        {
            var cutoff = RuntimeLogRetention.Cutoff(level, now);
            var oldCount = level == "Info" ? 201 : 1;
            return Enumerable.Range(0, oldCount).Select(_ => EventAt(test, level, cutoff.AddMilliseconds(-1)))
                .Concat([EventAt(test, level, cutoff), EventAt(test, level, cutoff.AddMilliseconds(1))]);
        }).ToArray();
        var accepted = await test.Service().IngestAsync(RuntimeLogIngestTestSupport.Body(string.Join('\n', fixtures) + '\n'), CancellationToken.None);
        Assert.Equal(fixtures.Length, accepted.Summary.VoAccepted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => retention.PruneAsync(now, CancellationToken.None));
        Assert.Equal(fixtures.Length, db.Ado.GetInt("SELECT COUNT(*) FROM runtime_log_event"));
        db.Ado.ExecuteCommand("""
            INSERT INTO runtime_log_event
            SELECT 'other-row','other-deployment',event_id,event_code,service,level,occurred_at_utc,observed_at_utc,
                ingested_at_utc,trace_id,operation_id,content_hash,canonical_json
            FROM runtime_log_event ORDER BY occurred_at_utc LIMIT 1
            """);

        test.Options.RetentionEnabled = true;
        var deleted = await retention.PruneAsync(now, CancellationToken.None);
        Assert.Equal(201L, deleted["Info"]);
        Assert.Equal(1L, deleted["Warning"]);
        Assert.Equal(1L, deleted["Error"]);
        Assert.Equal(6, db.Ado.GetInt("SELECT COUNT(*) FROM runtime_log_event WHERE deployment_id='local'"));
        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(*) FROM runtime_log_event WHERE deployment_id='other-deployment'"));
        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(*) FROM AuditLog_20240101"));
        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(*) FROM InformationLog_20240101"));
        Assert.All(await retention.PruneAsync(now, CancellationToken.None), entry => Assert.Equal(0, entry.Value));
    }

    [Fact]
    public async Task Ingest_ShouldRejectExpiredReplayButKeepExactCutoffAndNewerEvents()
    {
        using var test = new RuntimeLogIngestTestSupport();
        test.Options.RetentionEnabled = true;
        var cutoff = RuntimeLogRetention.Cutoff("Info", test.Clock.GetUtcNow().UtcDateTime);
        using var body = RuntimeLogIngestTestSupport.Body(string.Join('\n', new[]
        {
            EventAt(test, "Info", cutoff.AddMilliseconds(-1)), EventAt(test, "Info", cutoff),
            EventAt(test, "Info", cutoff.AddMilliseconds(1)), ""
        }));
        var result = await test.Service().IngestAsync(body, CancellationToken.None);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(2, result.Summary.VoAccepted);
        Assert.Equal(1, result.Summary.VoRejected);
        Assert.Equal(1, test.Db.GetConnectionScope("log").Ado.GetInt(
            "SELECT COUNT(*) FROM runtime_log_rejection_bucket WHERE reason='retention-expired'"));
    }

    private static string EventAt(RuntimeLogIngestTestSupport test, string level, DateTime timestamp)
    {
        var json = JsonNode.Parse(test.Event(level: level))!;
        var utc = timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);
        json["occurredAtUtc"] = utc;
        json["observedAtUtc"] = utc;
        return json.ToJsonString();
    }
}
