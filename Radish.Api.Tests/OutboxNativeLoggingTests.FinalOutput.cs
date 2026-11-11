using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Api.Services;
using Radish.Api.Tests.ErrorHandling;
using Radish.Common.OptionTool;
using Radish.Extension.Log;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Repository;
using Radish.Service;
using Radish.Service.Internal;
using Serilog;
using Serilog.Context;
using Serilog.Core.Enrichers;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Radish.Api.Tests;

public sealed partial class OutboxNativeLoggingTests
{
    private static readonly string[] OutboxIdentityFields = ["SourceContext", "SourceCategory", "EventCode", "Diagnostic"];
    private const string AmbientOperation = "12345678-1234-4234-8234-123456789abc";

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public async Task OutboxFinalOutput_ShouldKeepPersistedStateAndProducerIdentity(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var global = new ApplicationLogScope(capture.Logger);
        using var scope = PollutedOutboxScope();
        using var activity = new Activity("outbox-state").SetIdFormat(ActivityIdFormat.W3C).Start();
        using var db = CreateDatabase();
        var repository = new ReliableOutboxRepository(db);
        var service = new ReliableOutboxService(repository);
        var processor = new Mock<IReliableTaskProcessor>();
        processor.Setup(p => p.ProcessAsync(It.IsAny<ReliableOutboxSnapshot>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException(Secret));
        var job = new ReliableOutboxExecutionJob(service, processor.Object, Mock.Of<IContentModerationCaseRepository>());
        var now = DateTime.UtcNow;

        foreach (var database in new[] { "main", "chat" })
        {
            var id = await AddOutputMessage(repository, database, now);
            await ClaimOutputMessage(repository, database, now);
            await job.ExecuteAsync(database, id, CancellationToken.None);
            var pending = await repository.QueryByIdAsync(database, id);
            Assert.Equal(ReliableOutboxStatuses.Pending, pending!.Status);
            Assert.Equal(1, pending.AttemptCount);
            Assert.Equal(nameof(IOException), pending.LastErrorCode);
            Assert.DoesNotContain(Secret, pending.LastErrorSummary!);
            AssertOutboxOutput(capture, activity, "outbox.retrying", database, id, 1, "io");
            await job.ExecuteAsync(database, id, CancellationToken.None);
            await repository.MarkFailedAsync(database, id, Secret, Secret, now, null);
            Assert.Single(capture.Events);
            capture.Clear();

            await ClaimOutputMessage(repository, database, now.AddHours(1));
            await job.ExecuteAsync(database, id, CancellationToken.None);
            var dead = await repository.QueryByIdAsync(database, id);
            Assert.Equal(ReliableOutboxStatuses.DeadLetter, dead!.Status);
            Assert.Equal(2, dead.AttemptCount);
            AssertOutboxOutput(capture, activity, "outbox.dead_letter", database, id, 2, "io");
            capture.Clear();

            var permanent = await AddOutputMessage(repository, database, now);
            await ClaimOutputMessage(repository, database, now);
            await service.MarkFailedAsync(database, permanent, new PermanentReliableTaskException(Secret), now);
            Assert.Equal(Secret, (await repository.QueryByIdAsync(database, permanent))!.LastErrorSummary);
            AssertOutboxOutput(capture, activity, "outbox.dead_letter", database, permanent, 1, "other");
            capture.Clear();

            // 直接调用仍受支持，缺省与无效分类都不能从外层 scope 恢复。
            foreach (var failure in new string?[] { null, Secret })
            {
                var direct = await AddOutputMessage(repository, database, now);
                await ClaimOutputMessage(repository, database, now);
                if (failure == null) await repository.MarkFailedAsync(database, direct, Secret, Secret, now, null);
                else await repository.MarkFailedAsync(database, direct, Secret, Secret, now, null, failure);
                AssertOutboxOutput(capture, activity, "outbox.dead_letter", database, direct, 1, null);
                capture.Clear();
            }
        }
    }

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public async Task OutboxFinalOutput_ShouldCountEnqueuesAndPreserveEnqueueFailure(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var scope = PollutedOutboxScope();
        using var activity = new Activity("outbox-dispatch").SetIdFormat(ActivityIdFormat.W3C).Start();
        using var db = CreateDatabase();
        var repository = new ReliableOutboxRepository(db);
        var client = new Mock<IBackgroundJobClient>();
        client.Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>())).Returns("enqueued");
        var dispatcher = new ReliableOutboxDispatcherJob(new ReliableOutboxService(repository), client.Object,
            capture.Factory.CreateLogger<ReliableOutboxDispatcherJob>());
        Assert.Equal(0, await dispatcher.DispatchAsync());
        Assert.Empty(capture.Events);
        var now = DateTime.UtcNow.AddMinutes(-1);
        var main = await AddOutputMessage(repository, "main", now);
        var chat = await AddOutputMessage(repository, "chat", now);
        Assert.Equal(2, await dispatcher.DispatchAsync());
        client.Verify(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Exactly(2));
        Assert.Equal(ReliableOutboxStatuses.Processing, (await repository.QueryByIdAsync("main", main))!.Status);
        Assert.Equal(ReliableOutboxStatuses.Processing, (await repository.QueryByIdAsync("chat", chat))!.Status);
        AssertOutboxOutput(capture, activity, "outbox.dispatched", count: 2);
        capture.Clear();

        var first = await AddOutputMessage(repository, "main", now);
        var second = await AddOutputMessage(repository, "chat", now);
        var failure = new IOException(Secret);
        client.SetupSequence(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>())).Returns("first").Throws(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => dispatcher.DispatchAsync()));
        Assert.Empty(capture.Events);
        // 第二次入队失败时不虚构完成摘要，也不改变原领取和异常传播行为。
        Assert.Equal(ReliableOutboxStatuses.Processing, (await repository.QueryByIdAsync("main", first))!.Status);
        Assert.Equal(ReliableOutboxStatuses.Processing, (await repository.QueryByIdAsync("chat", second))!.Status);
    }

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public async Task OutboxFinalOutput_ShouldRemainQuietOnZeroRowsAndPreserveOutputFailure(bool candidate, string environment, bool audit)
    {
        using var writer = new HangfireBrokenWriter();
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit, output: writer);
        using var global = new ApplicationLogScope(capture.Logger);
        using var scope = PollutedOutboxScope();
        using var db = CreateDatabase();
        var repository = new ReliableOutboxRepository(db);
        var now = DateTime.UtcNow;
        foreach (var database in new[] { "main", "chat" })
        {
            var id = await AddOutputMessage(repository, database, now);
            await ClaimOutputMessage(repository, database, now);
            var connection = db.GetConnectionScope(database);
            var competingRows = 0;
            connection.Aop.OnLogExecuting = (sql, _) =>
            {
                if (!sql.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)) return;
                connection.Aop.OnLogExecuting = null;
                competingRows = database == "main"
                    ? connection.Updateable<ReliableOutboxMessage>().SetColumns(row => row.Status == ReliableOutboxStatuses.Succeeded)
                        .Where(row => row.Id == id && row.Status == ReliableOutboxStatuses.Processing).ExecuteCommand()
                    : connection.Updateable<ChatReliableOutboxMessage>().SetColumns(row => row.Status == ReliableOutboxStatuses.Succeeded)
                        .Where(row => row.Id == id && row.Status == ReliableOutboxStatuses.Processing).ExecuteCommand();
            };
            await repository.MarkFailedAsync(database, id, Secret, Secret, now, null);
            Assert.Equal(1, competingRows);
            var ignored = await repository.QueryByIdAsync(database, id);
            Assert.Equal(ReliableOutboxStatuses.Succeeded, ignored!.Status);
            Assert.Equal(0, ignored.AttemptCount);
            Assert.Empty(capture.Events);
        }
        Assert.Equal("", capture.Emergency.ToString());

        var sinkFailure = new IOException(Secret);
        capture.Sink.EmitFailure = sinkFailure;
        foreach (var database in new[] { "main", "chat" })
        {
            var id = await AddOutputMessage(repository, database, now);
            await ClaimOutputMessage(repository, database, now);
            if (audit)
            {
                var caught = await Assert.ThrowsAsync<AggregateException>(() => repository.MarkFailedAsync(database, id, Secret, Secret, now, null));
                Assert.Same(sinkFailure, Assert.Single(caught.Flatten().InnerExceptions));
            }
            else await repository.MarkFailedAsync(database, id, Secret, Secret, now, null);
            Assert.Equal(ReliableOutboxStatuses.DeadLetter, (await repository.QueryByIdAsync(database, id))!.Status);
        }
        var client = new Mock<IBackgroundJobClient>();
        client.Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>())).Returns("enqueued");
        var dispatcher = new ReliableOutboxDispatcherJob(new ReliableOutboxService(repository), client.Object,
            capture.Factory.CreateLogger<ReliableOutboxDispatcherJob>());
        var dispatched = await AddOutputMessage(repository, "main", now.AddMinutes(-1));
        if (audit)
        {
            var caught = await Assert.ThrowsAsync<AggregateException>(() => dispatcher.DispatchAsync());
            Assert.Same(sinkFailure, Assert.Single(caught.Flatten().InnerExceptions));
        }
        else Assert.Equal(1, await dispatcher.DispatchAsync());
        client.Verify(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Once);
        Assert.Equal(ReliableOutboxStatuses.Processing, (await repository.QueryByIdAsync("main", dispatched))!.Status);
        Assert.Empty(capture.Events);
        if (candidate)
        {
            using var json = JsonDocument.Parse(capture.Emergency.ToString());
            Assert.Equal("pipeline.output_failed", json.RootElement.GetProperty("eventCode").GetString());
            Assert.DoesNotContain(Secret, capture.Emergency.ToString());
        }
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task OutboxFinalOutput_LegacyCodeConsoleShouldUseFixedTemplates(string environment)
    {
        var previous = Console.Out;
        using var output = new StringWriter();
        using var services = new ServiceCollection().BuildServiceProvider();
        try
        {
            Console.SetOut(output);
            var configuration = new LoggerConfiguration();
            SerilogSetup.ConfigureLegacy(configuration, new ConfigurationBuilder().Build(), services, new SerilogOptions
            {
                Console = new() { Enable = true, EnableSqlLog = false },
                File = new() { Enable = false }, Database = new() { Enable = false }
            }, environment);
            using var logger = configuration.CreateLogger();
            using var factory = new Serilog.Extensions.Logging.SerilogLoggerFactory(logger, dispose: false);
            using var global = new ApplicationLogScope(logger);
            using var scope = PollutedOutboxScope();
            using var db = CreateDatabase();
            var repository = new ReliableOutboxRepository(db);
            var service = new ReliableOutboxService(repository);
            var now = DateTime.UtcNow.AddMinutes(-1);
            var id = await AddOutputMessage(repository, "main", now);
            var dispatcher = new ReliableOutboxDispatcherJob(service, Mock.Of<IBackgroundJobClient>(), factory.CreateLogger<ReliableOutboxDispatcherJob>());
            Assert.Equal(1, await dispatcher.DispatchAsync());
            await service.MarkFailedAsync("main", id, new IOException(Secret), now);
            await ClaimOutputMessage(repository, "main", now.AddHours(1));
            await service.MarkFailedAsync("main", id, new IOException(Secret), now);
            Assert.Collection(Lines(output),
                line => Assert.EndsWith("Outbox dispatch completed.", line.TrimEnd()),
                line => Assert.EndsWith("Outbox retry scheduled.", line.TrimEnd()),
                line => Assert.EndsWith("Outbox task moved to dead letter.", line.TrimEnd()));
            Assert.DoesNotContain(Secret, output.ToString());
        }
        finally { Console.SetOut(previous); }
    }

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public void OutboxFinalOutput_ShouldRejectUnknownAndCrossSourceEvents(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var scope = PollutedOutboxScope();
        foreach (var source in new[] { typeof(ReliableOutboxRepository), typeof(ReliableOutboxDispatcherJob) })
        foreach (var level in new[] { LogEventLevel.Information, LogEventLevel.Error })
        {
            var code = source == typeof(ReliableOutboxRepository) ? "outbox.dispatched" : "outbox.dead_letter";
            capture.Logger.ForContext("SourceContext", source.FullName).ForContext("EventCode", code)
                .Write(level, new IOException(Secret), Secret);
            if (candidate && environment == "Production" && level < LogEventLevel.Warning) Assert.Empty(capture.Events);
            else
            {
                var summary = Assert.Single(capture.Events);
                Assert.Equal("runtime.unclassified", summary.Code);
                Assert.Equal(level < LogEventLevel.Warning, summary.Diagnostic);
                if (candidate)
                {
                    using var json = JsonDocument.Parse(capture.Output.ToString());
                    Assert.Empty(json.RootElement.GetProperty("properties").EnumerateObject());
                    Assert.False(json.RootElement.TryGetProperty("operationId", out _));
                }
                else Assert.Equal(OutboxIdentityFields.Order(), Assert.Single(capture.Sink.Events).Properties.Keys.Order());
                capture.AssertSafe();
            }
            capture.Clear();
        }
    }

    [Fact]
    public void OutboxSink_ShouldValidateFieldsAndKeepOriginalTimestampAndTypedTrace()
    {
        using var output = new HttpBoundaryTestSink();
        var sink = new OutboxLogSink(output);
        var timestamp = DateTimeOffset.UtcNow.AddDays(-1);
        var trace = ActivityTraceId.CreateRandom();
        var span = ActivitySpanId.CreateRandom();
        LogEvent Create(string source, string code, LogEventLevel level) => new(timestamp, level, new IOException(Secret),
            new MessageTemplateParser().Parse(Secret),
            [new("SourceContext", new ScalarValue(source)), new("EventCode", new ScalarValue(code)),
                new("count", new ScalarValue(-1)), new("durationMs", new ScalarValue(double.PositiveInfinity)),
                new("attempt", new ScalarValue(1_000_001)), new("outcome", new ScalarValue("succeeded")),
                new("databaseScope", new ScalarValue(Secret)), new("failureKind", new ScalarValue(Secret)),
                new("operationId", new ScalarValue(Guid.NewGuid().ToString("N")))], trace, span);
        var adjacent = Create(typeof(ReliableOutboxRepository).FullName + ".Adjacent", "outbox.retrying", LogEventLevel.Warning);
        sink.Emit(adjacent);
        Assert.Same(adjacent, Assert.Single(output.Events));
        output.Events.Clear();
        foreach (var (source, code, level) in new[]
        {
            (typeof(ReliableOutboxDispatcherJob), "outbox.dispatched", LogEventLevel.Information),
            (typeof(ReliableOutboxRepository), "outbox.retrying", LogEventLevel.Warning),
            (typeof(ReliableOutboxRepository), "outbox.dead_letter", LogEventLevel.Error),
            (typeof(ReliableOutboxRepository), "outbox.retrying", LogEventLevel.Information)
        })
        {
            sink.Emit(Create(source.FullName!, code, level));
            var value = Assert.Single(output.Events);
            Assert.Equal(OutboxIdentityFields.Order(), value.Properties.Keys.Order());
            Assert.Equal(source == typeof(ReliableOutboxRepository) && level == LogEventLevel.Information
                ? "runtime.unclassified" : code, OutboxScalar(value, "EventCode"));
            Assert.Equal(timestamp, value.Timestamp);
            Assert.Equal(level, value.Level);
            Assert.Equal(trace, value.TraceId);
            Assert.Equal(span, value.SpanId);
            Assert.Null(value.Exception);
            Assert.DoesNotContain(Secret, value.RenderMessage());
            output.Events.Clear();
        }
    }

    private static async Task ClaimOutputMessage(ReliableOutboxRepository repository, string database, DateTime now) =>
        Assert.Single(await repository.ClaimDueAsync(database, 10, Secret, now, TimeSpan.FromMinutes(5)));

    private static Task<long> AddOutputMessage(ReliableOutboxRepository repository, string database, DateTime now) =>
        repository.AddAsync(new ReliableOutboxDraft(database, 0, ReliableTaskTypes.PostPublished, 1, Guid.NewGuid().ToString("N"),
            "Post", Secret, Secret, now, MaxAttempts: 2));

    private static IDisposable PollutedOutboxScope() => LogContext.Push(
        new PropertyEnricher("SourceContext", typeof(ServiceCleanupSummary).FullName!),
        new PropertyEnricher("EventCode", "job.cleanup.failed"), new PropertyEnricher("SourceCategory", "http"),
        new PropertyEnricher("Diagnostic", true), new PropertyEnricher("failureKind", "database"),
        new PropertyEnricher("operationId", AmbientOperation), new PropertyEnricher("traceId", "11111111111111111111111111111111"),
        new PropertyEnricher("spanId", "1111111111111111"), new PropertyEnricher("statusCode", 503),
        new PropertyEnricher("privatePayload", Secret), new PropertyEnricher("attempt", 99),
        new PropertyEnricher("outcome", "succeeded"), new PropertyEnricher("databaseScope", "chat"),
        new PropertyEnricher("count", 999), new PropertyEnricher("durationMs", 999d));

    private static object? OutboxScalar(LogEvent value, string key) => Assert.IsType<ScalarValue>(value.Properties[key]).Value;

    private static void AssertOutboxOutput(HttpBoundaryCapture capture, Activity activity, string code,
        string? database = null, long id = 0, int count = 0, string? failure = null)
    {
        var dispatch = code == "outbox.dispatched";
        var level = dispatch ? (capture.Candidate ? "Info" : "Information") : code == "outbox.retrying" ? "Warning" : "Error";
        var summary = Assert.Single(capture.Events);
        Assert.Equal(code, summary.Code);
        Assert.Equal(level, summary.Level);
        Assert.Equal(failure, summary.FailureKind);
        Assert.False(summary.Diagnostic);
        var operation = dispatch ? null : new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"radish:outbox:{database}:{id.ToString(CultureInfo.InvariantCulture)}")).AsSpan(0, 16)).ToString("D");
        var fields = dispatch ? new[] { "count", "durationMs" } : failure == null
            ? ["databaseScope", "attempt", "outcome"] : ["databaseScope", "attempt", "outcome", "failureKind"];
        if (capture.Candidate)
        {
            using var json = JsonDocument.Parse(capture.Output.ToString());
            var root = json.RootElement;
            var properties = root.GetProperty("properties");
            Assert.Equal("job", root.GetProperty("sourceCategory").GetString());
            Assert.Equal(activity.TraceId.ToHexString(), root.GetProperty("traceId").GetString());
            Assert.Equal(activity.SpanId.ToHexString(), root.GetProperty("spanId").GetString());
            Assert.Equal(operation, root.TryGetProperty("operationId", out var correlation) ? correlation.GetString() : null);
            Assert.Equal(fields.Order(), properties.EnumerateObject().Select(p => p.Name).Order());
            Assert.Equal(count, properties.GetProperty(dispatch ? "count" : "attempt").GetInt32());
            if (dispatch) Assert.InRange(properties.GetProperty("durationMs").GetDouble(), 0, 31_536_000_000d);
            else
            {
                Assert.Equal(database, properties.GetProperty("databaseScope").GetString());
                Assert.Equal(code == "outbox.retrying" ? "retrying" : "failed", properties.GetProperty("outcome").GetString());
            }
            Assert.DoesNotContain(Secret, capture.Output.ToString());
        }
        else
        {
            var value = Assert.Single(capture.Sink.Events);
            var expected = OutboxIdentityFields.Concat(fields).Concat(dispatch ? [] : new[] { "operationId" });
            Assert.Equal(expected.Order(), value.Properties.Keys.Order());
            Assert.Equal((dispatch ? typeof(ReliableOutboxDispatcherJob) : typeof(ReliableOutboxRepository)).FullName, OutboxScalar(value, "SourceContext"));
            Assert.Equal("job", OutboxScalar(value, "SourceCategory"));
            Assert.Equal(activity.TraceId, value.TraceId);
            Assert.Equal(activity.SpanId, value.SpanId);
            Assert.Equal(count, OutboxScalar(value, dispatch ? "count" : "attempt"));
            if (dispatch) Assert.InRange(Assert.IsType<double>(OutboxScalar(value, "durationMs")), 0, 31_536_000_000d);
            else
            {
                Assert.Equal(operation, OutboxScalar(value, "operationId"));
                Assert.Equal(database, OutboxScalar(value, "databaseScope"));
                Assert.Equal(code == "outbox.retrying" ? "retrying" : "failed", OutboxScalar(value, "outcome"));
            }
            Assert.DoesNotContain(Secret, value.RenderMessage() + string.Join(" ", value.Properties.Values));
        }
        capture.AssertSafe();
    }
}
