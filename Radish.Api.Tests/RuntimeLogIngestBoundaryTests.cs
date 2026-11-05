using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Radish.Common.LogTool;
using Radish.IRepository;
using Radish.Model.LogModels;
using Xunit;

namespace Radish.Api.Tests;

public sealed class RuntimeLogIngestBoundaryTests
{
    [Theory]
    [InlineData("schemaVersion", "\"PRIVATE\"")]
    [InlineData("schemaVersion", "2")]
    [InlineData("eventId", "\"PRIVATE\"")]
    [InlineData("deploymentId", "\"PRIVATE\"")]
    [InlineData("service", "\"PRIVATE\"")]
    [InlineData("occurredAtUtc", "\"2026-02-30T00:00:00.000Z\"")]
    [InlineData("occurredAtUtc", "\"1969-01-01T00:00:00.000Z\"")]
    [InlineData("observedAtUtc", "\"2026-01-01T00:00:00+08:00\"")]
    [InlineData("mode", "\"Development\"")]
    [InlineData("diagnostic", "true")]
    [InlineData("isFatal", "true")]
    [InlineData("message", "\"PRIVATE\"")]
    [InlineData("messageTemplate", "\"PRIVATE\"")]
    [InlineData("properties", "{\"count\":\"PRIVATE\"}")]
    [InlineData("properties", "{\"unknown\":\"PRIVATE\"}")]
    [InlineData("properties", "{\"count\":1e99}")]
    [InlineData("traceId", "\"00000000000000000000000000000000\"")]
    [InlineData("containerId", "\"PRIVATE\"")]
    [InlineData("release", "\"PRIVATE\\n\"")]
    [InlineData("unknown", "\"PRIVATE\"")]
    public async Task InvalidSemanticRecord_ShouldRejectWithoutStoringPayload(string key, string jsonValue)
    {
        using var test = new RuntimeLogIngestTestSupport();
        var value = JsonNode.Parse(test.Event())!.AsObject();
        value[key] = JsonNode.Parse(jsonValue);
        var result = await test.Service().IngestAsync(RuntimeLogIngestTestSupport.Body(value.ToJsonString()), CancellationToken.None);
        Assert.Equal(200, result.StatusCode); Assert.Equal(1, result.Summary.VoRejected);
        Assert.Equal(0, test.Db.GetConnectionScope("log").Queryable<RuntimeLogEventRecord>().Count());
        Assert.DoesNotContain("PRIVATE", string.Join(' ', test.Db.GetConnectionScope("log").Queryable<RuntimeLogRejectionBucket>().ToList().Select(b => b.Service + b.Reason)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n\n")]
    [InlineData("{\"message\":\"\\ud800\"}")]
    [InlineData("{")]
    [InlineData("{\"a\":1,\"a\":2}")]
    [InlineData("{\"properties\":{\"count\":1,\"count\":2}}")]
    [InlineData("{\"a\":{\"b\":{\"c\":{\"d\":{}}}}}")]
    public async Task MalformedEnvelope_ShouldNotAcknowledge(string text)
    {
        using var test = new RuntimeLogIngestTestSupport();
        var result = await test.Service().IngestAsync(RuntimeLogIngestTestSupport.Body(text), CancellationToken.None);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(0, test.Db.GetConnectionScope("log").Queryable<RuntimeLogRejectionBucket>().Count());
    }

    [Fact]
    public async Task InvalidUtf8AndOversizeLine_ShouldFailBeforeStorage()
    {
        using var test = new RuntimeLogIngestTestSupport();
        var utf8 = await test.Service().IngestAsync(new MemoryStream([0xff, 0xfe]), CancellationToken.None);
        var longLine = await test.Service().IngestAsync(RuntimeLogIngestTestSupport.Body(new string('x', 8193)), CancellationToken.None);
        Assert.Equal(400, utf8.StatusCode); Assert.Equal(413, longLine.StatusCode);
    }

    [Fact]
    public async Task TransportBudgets_ShouldAcceptBoundaryAndRejectBeyondIt()
    {
        using var test = new RuntimeLogIngestTestSupport();
        var line = test.Event().PadRight(8191) + "\n";
        var payload = string.Concat(Enumerable.Repeat(line, 2048));
        var repository = new AcceptRepository();
        var accepted = await test.Service(repository).IngestAsync(RuntimeLogIngestTestSupport.Body(payload), CancellationToken.None);
        Assert.Equal(200, accepted.StatusCode); Assert.Equal(2048, accepted.Summary.VoAccepted);
        var exceeded = await test.Service(repository).IngestAsync(RuntimeLogIngestTestSupport.Body(payload + "\n"), CancellationToken.None);
        Assert.Equal(413, exceeded.StatusCode); Assert.Equal(2000, exceeded.Summary.VoAccepted);
        var count = await test.Service(repository).IngestAsync(RuntimeLogIngestTestSupport.Body(new string('\n', 32769)), CancellationToken.None);
        Assert.Equal(413, count.StatusCode);
    }

    [Fact]
    public async Task LaterSyntaxFailure_ShouldRetainOnlyCommittedPrefix()
    {
        using var test = new RuntimeLogIngestTestSupport();
        var payload = string.Join('\n', Enumerable.Range(0, 201).Select(_ => test.Event())) + "\n{";
        var result = await test.Service().IngestAsync(RuntimeLogIngestTestSupport.Body(payload), CancellationToken.None);
        Assert.Equal(400, result.StatusCode); Assert.Equal(200, result.Summary.VoAccepted);
        Assert.Equal(200, test.Db.GetConnectionScope("log").Queryable<RuntimeLogEventRecord>().Count());
    }

    [Fact]
    public async Task CancellationAndConcurrency_ShouldReleaseSlotsAndBoundWaiting()
    {
        using var test = new RuntimeLogIngestTestSupport();
        using var cancellation = new CancellationTokenSource();
        var firstStream = new BlockingStream(); var secondStream = new BlockingStream();
        var first = test.Service().IngestAsync(firstStream, cancellation.Token);
        var second = test.Service().IngestAsync(secondStream, cancellation.Token);
        await Task.WhenAll(firstStream.Entered.Task, secondStream.Entered.Task);
        var busy = await test.Service().IngestAsync(RuntimeLogIngestTestSupport.Body(test.Event()), CancellationToken.None);
        Assert.Equal(429, busy.StatusCode);
        cancellation.Cancel();
        Assert.Equal(499, (await first).StatusCode); Assert.Equal(499, (await second).StatusCode);
        Assert.Equal(200, (await test.Service().IngestAsync(RuntimeLogIngestTestSupport.Body(test.Event()), CancellationToken.None)).StatusCode);
    }

    [Fact]
    public async Task SlowBody_ShouldTimeoutAndReleaseSlot()
    {
        using var test = new RuntimeLogIngestTestSupport(); test.Options.RequestTimeoutSeconds = 1;
        var result = await test.Service().IngestAsync(new BlockingStream(), CancellationToken.None);
        Assert.Equal(408, result.StatusCode);
        Assert.Equal(1, test.State.Snapshot().Failed);
    }

    [Fact]
    public void DiagnosticOutput_ShouldBeRateLimitedSafeAndReentrant()
    {
        var writer = new ReentrantWriter();
        var diagnostics = new RuntimeLogIngestDiagnostics(writer: writer);
        writer.Callback = () => diagnostics.Report("ingest", new Exception("PRIVATE"));
        diagnostics.Report("ingest", new Exception("PRIVATE"));
        diagnostics.Report("ingest", new Exception("PRIVATE"));
        Assert.Equal(1, writer.Calls);
        Assert.DoesNotContain("PRIVATE", writer.ToString());
        var failing = new RuntimeLogIngestDiagnostics(writer: new FailingWriter());
        failing.Report("ingest", new Exception("PRIVATE"));
    }

    private sealed class AcceptRepository : IRuntimeLogIngestRepository
    {
        public Task<IReadOnlyList<RuntimeLogEventRecord?>> CommitAsync(IReadOnlyList<RuntimeLogEventRecord> events,
            IReadOnlyList<RuntimeLogRejectionBucket> rejections, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<RuntimeLogEventRecord?>>(events.Cast<RuntimeLogEventRecord?>().ToList());
    }
    private sealed class BlockingStream : MemoryStream
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult(); await Task.Delay(Timeout.Infinite, cancellationToken); return 0;
        }
    }
    private sealed class ReentrantWriter : StringWriter
    {
        public Action? Callback { get; set; }
        public int Calls { get; private set; }
        public override void WriteLine(string? value) { Calls++; base.WriteLine(value); Callback?.Invoke(); }
    }
    private sealed class FailingWriter : StringWriter
    {
        public override void WriteLine(string? value) => throw new IOException("PRIVATE");
    }
}
