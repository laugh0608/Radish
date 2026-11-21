using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Radish.Common.LogTool;
using Radish.Common.OptionTool;
using Radish.DbMigrate;
using Radish.Model.DtoModels;
using Radish.Model.LogModels;
using Radish.Repository;
using Xunit;

namespace Radish.Api.Tests;

public sealed class RuntimeLogConsoleTests
{
    [Fact]
    public async Task CursorStaysOnCapturedRowsForLateInsertWithSameTimestamp()
    {
        using var test = new RuntimeLogIngestTestSupport();
        var repository = Create(test);
        var now = test.Clock.GetUtcNow().UtcDateTime;
        var a = "00000000-0000-4000-8000-000000000010";
        var b = "00000000-0000-4000-8000-000000000011";
        var late = "00000000-0000-4000-8000-000000000009";
        var service = test.Service(repository);
        await service.IngestAsync(RuntimeLogIngestTestSupport.Body(test.Event(a) + "\n" + test.Event(b)), CancellationToken.None);
        var filter = new RuntimeLogPageFilter("local", now.AddMinutes(-1), now.AddMinutes(1), ["api"], "Warning",
            null, null, null, "runtime", 1);
        var (rows, snapshot) = await repository.QueryAsync(filter, null, CancellationToken.None);
        Assert.Equal(b, rows[0].EventId);
        await service.IngestAsync(RuntimeLogIngestTestSupport.Body(test.Event(late)), CancellationToken.None);
        var page = await repository.QueryAsync(filter, snapshot with { LastTime = now, LastId = b }, CancellationToken.None);
        Assert.Equal(a, Assert.Single(page.Rows).EventId);
    }

    [Fact]
    public async Task TerminalAlertDeduplicatesReplayAndOperationAndTracksActor()
    {
        using var test = new RuntimeLogIngestTestSupport();
        var repository = Create(test);
        var policy = new RuntimeLogPolicy(new RuntimeLogSource("local", "api", "test", "test"), clock: test.Clock);
        var operationId = Guid.NewGuid().ToString("D");
        var first = policy.Create(JsonSerializer.SerializeToElement(new { eventCode = "outbox.dead_letter", level = "Error", operationId }))!;
        var second = first with { EventId = Guid.NewGuid().ToString("D") };
        var body = RuntimeLogIngestTestSupport.Body(first.ToJsonLine() + "\n" + first.ToJsonLine() + "\n" + second.ToJsonLine());
        Assert.Equal(200, (await test.Service(repository).IngestAsync(body, CancellationToken.None)).StatusCode);
        var alert = Assert.Single((await repository.GetAlertsAsync("local", CancellationToken.None)).Rows);
        Assert.True(await repository.TransitionAsync("local", alert.Id, 1, "acknowledge", "7", CancellationToken.None));
        Assert.False(await repository.TransitionAsync("local", alert.Id, 1, "acknowledge", "8", CancellationToken.None));
        Assert.Equal("7", Assert.Single(test.Db.GetConnectionScope("log").Queryable<RuntimeLogAlertAction>().ToList()).ActorId);
        Assert.Equal(0, (await repository.GetAlertsAsync("local", CancellationToken.None)).Open);
    }

    [Fact]
    public async Task PipelineNeedsNewInputTwoOutputAdvancesAndDrainToRecover()
    {
        using var test = new RuntimeLogIngestTestSupport();
        var repository = Create(test);
        var now = test.Clock.GetUtcNow().UtcDateTime;
        Task Sample(int index, int written, int files, int http, int chunks, int input) =>
            repository.UpdatePipelineAsync("local", new RuntimeLogPipelineSample(0, written, files, http, chunks, now.AddSeconds(index), input),
                CancellationToken.None);
        async Task<string> State() => (await repository.GetPipelineAsync("local", CancellationToken.None))!.State;
        await Sample(0, 0, 1, 1, 0, 1);
        await Sample(1, 1, 1, 1, 1, 2);
        await Sample(2, 1, 1, 1, 0, 2);
        Assert.Equal("Faulted", await State());
        await Sample(3, 1, 2, 2, 0, 2);
        Assert.Equal("Faulted", await State());
        await Sample(4, 1, 3, 2, 1, 3);
        Assert.Equal("Faulted", await State());
        await Sample(5, 1, 3, 3, 0, 3);
        Assert.Equal("Healthy", await State());
        var row = Assert.Single((await repository.GetAlertsAsync("local", CancellationToken.None)).Rows);
        Assert.Equal("Resolved", row.State);
        Assert.Equal(1, row.KnownFailures);
        Assert.Equal("recover", Assert.Single(test.Db.GetConnectionScope("log").Queryable<RuntimeLogAlertAction>().ToList()).Action);
    }

    private static RuntimeLogIngestRepository Create(RuntimeLogIngestTestSupport test)
    {
        RuntimeLogConsoleSchemaMigration.Instance.Apply(test.Db.GetConnectionScope("log"), test.Services);
        return new RuntimeLogIngestRepository(test.Db, test.Options, test.Clock, new RuntimeLogConsoleOptions { Enabled = true });
    }
}
