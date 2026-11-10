using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Radish.Api.Tests.ErrorHandling;
using Radish.Common.OptionTool;
using Radish.Extension.Log;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Service.Jobs;
using Serilog;
using Serilog.Context;
using Serilog.Core.Enrichers;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Radish.Api.Tests;

public sealed partial class BusinessJobLoggingTests
{
    private static readonly string[] BatchFields =
        ["jobKind", "outcome", "processedCount", "updatedCount", "rewardCount", "skippedCount", "rejectedCount", "failedCount", "durationMs"];
    private static readonly string[] EventFields = ["SourceContext", "SourceCategory", "EventCode", "Diagnostic"];

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public async Task ShopCancel_ShouldRebuildItsFinalSummaryAfterAmbientEnrichment(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var logger = new ApplicationLogScope(capture.Logger);
        using var scope = PolluteSummaryScope();
        using var activity = new Activity("job-batch").SetIdFormat(ActivityIdFormat.W3C).Start();

        Assert.Equal(0, await CancellationJob(empty: true).CancelTimeoutOrdersAsync(0));
        Assert.Empty(capture.Events);
        Assert.Equal(2, await CancellationJob().CancelTimeoutOrdersAsync(0));
        var summary = Assert.Single(capture.Events);
        Assert.Equal("job.batch.failed", summary.Code);
        Assert.Equal("Error", summary.Level);
        Assert.Equal("io", summary.FailureKind);
        Assert.False(summary.Diagnostic);
        capture.AssertSafe();

        var fields = BatchFields.Append("failureKind");
        if (candidate)
        {
            using var output = JsonDocument.Parse(capture.Output.ToString());
            var value = output.RootElement;
            var properties = value.GetProperty("properties");
            Assert.Equal(fields.Order(), properties.EnumerateObject().Select(p => p.Name).Order());
            Assert.Equal("job", value.GetProperty("sourceCategory").GetString());
            Assert.Equal(activity.TraceId.ToHexString(), value.GetProperty("traceId").GetString());
            Assert.Equal(activity.SpanId.ToHexString(), value.GetProperty("spanId").GetString());
            Assert.False(value.TryGetProperty("operationId", out _));
            Assert.Equal("shop-timeouts", properties.GetProperty("jobKind").GetString());
            Assert.Equal("partial", properties.GetProperty("outcome").GetString());
            Assert.Equal(2, properties.GetProperty("processedCount").GetInt32());
            Assert.Equal(1, properties.GetProperty("updatedCount").GetInt32());
            Assert.Equal(1, properties.GetProperty("rejectedCount").GetInt32());
            Assert.Equal(1, properties.GetProperty("failedCount").GetInt32());
        }
        else
        {
            var value = Assert.Single(capture.Sink.Events);
            Assert.Equal(fields.Concat(EventFields).Order(), value.Properties.Keys.Order());
            Assert.Equal(typeof(BusinessJobSummary).FullName, Scalar(value, "SourceContext"));
            Assert.Equal("job", Scalar(value, "SourceCategory"));
            Assert.Equal(activity.TraceId, value.TraceId);
            Assert.Equal(activity.SpanId, value.SpanId);
            Assert.Equal("shop-timeouts", Scalar(value, "jobKind"));
            Assert.Equal("partial", Scalar(value, "outcome"));
            Assert.Equal(2, Scalar(value, "processedCount"));
            Assert.Equal(1, Scalar(value, "updatedCount"));
            Assert.Equal(1, Scalar(value, "rejectedCount"));
            Assert.Equal(1, Scalar(value, "failedCount"));
        }
    }

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public void CompletedAndRejectedSummaries_ShouldKeepTheirLevelsAndOmitAmbientFailureKind(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var logger = new ApplicationLogScope(capture.Logger);
        using var scope = PolluteSummaryScope();
        new BusinessJobSummary("shop-stats") { ProcessedCount = 2 }.Write();
        new BusinessJobSummary("comment-highlight") { RejectedCount = 1 }.Write();

        Assert.Collection(capture.Events,
            completed =>
            {
                Assert.Equal("job.batch.completed", completed.Code);
                Assert.Equal(candidate ? "Info" : "Information", completed.Level);
                Assert.Null(completed.FailureKind);
                Assert.False(completed.Diagnostic);
            },
            warning =>
            {
                Assert.Equal("job.batch.warning", warning.Code);
                Assert.Equal("Warning", warning.Level);
                Assert.Null(warning.FailureKind);
                Assert.False(warning.Diagnostic);
            });
        capture.AssertSafe();
    }

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public async Task ShopCancel_OutputFailureShouldPreserveEachRouteAndReleaseTheLock(bool candidate, string environment, bool audit)
    {
        using var writer = new HangfireBrokenWriter();
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit, output: writer);
        using var logger = new ApplicationLogScope(capture.Logger);
        using var scope = PolluteSummaryScope();
        var failure = new IOException(HttpBoundaryCapture.Secret);
        capture.Sink.EmitFailure = failure;
        if (audit)
        {
            var caught = await Assert.ThrowsAsync<AggregateException>(() => CancellationJob().CancelTimeoutOrdersAsync(0));
            Assert.Same(failure, Assert.Single(caught.Flatten().InnerExceptions));
        }
        else
        {
            Assert.Equal(2, await CancellationJob().CancelTimeoutOrdersAsync(0));
            if (candidate)
            {
                using var emergency = JsonDocument.Parse(capture.Emergency.ToString());
                Assert.Equal("pipeline.output_failed", emergency.RootElement.GetProperty("eventCode").GetString());
                Assert.DoesNotContain(HangfireOutputLoggingTests.Secret, capture.Emergency.ToString());
                Assert.DoesNotContain(HttpBoundaryCapture.Secret, capture.Emergency.ToString());
            }
        }

        capture.Sink.EmitFailure = null;
        var queries = 0;
        Assert.Equal(0, await CancellationJob(empty: true, queried: () => queries++).CancelTimeoutOrdersAsync(0));
        Assert.Equal(1, queries);
    }

    [Fact]
    public async Task LegacyCodeConsole_ShouldWriteOnlyTheFixedJobSummary()
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
            });
            using var logger = configuration.CreateLogger();
            using var global = new ApplicationLogScope(logger);
            using var scope = PolluteSummaryScope();
            Assert.Equal(2, await CancellationJob().CancelTimeoutOrdersAsync(0));
            Assert.Single(output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries));
            Assert.Contains("Business job batch consumed failures", output.ToString());
            Assert.DoesNotContain(HttpBoundaryCapture.Secret, output.ToString());
        }
        finally { Console.SetOut(previous); }
    }

    [Fact]
    public void Sink_ShouldRejectInvalidFieldsAndUnregisteredCodesWithoutClaimingOtherSources()
    {
        using var capture = new HttpBoundaryTestSink();
        var sink = new BusinessJobLogSink(capture);
        var timestamp = DateTimeOffset.UtcNow.AddDays(-1);
        var trace = ActivityTraceId.CreateRandom();
        var span = ActivitySpanId.CreateRandom();
        var template = new MessageTemplateParser().Parse(HttpBoundaryCapture.Secret);
        var adjacent = new LogEvent(timestamp, LogEventLevel.Warning, null, template,
            [new("SourceContext", new ScalarValue(typeof(PostLotteryJob).FullName)), new("EventCode", new ScalarValue("job.batch.warning"))],
            trace, span);
        sink.Emit(adjacent);
        Assert.Same(adjacent, Assert.Single(capture.Events));
        capture.Events.Clear();

        foreach (var code in new[] { "job.batch.failed", "job.cleanup.failed", null })
        {
            var raw = new LogEvent(timestamp, LogEventLevel.Error, new IOException(Secret), template,
            [
                new("SourceContext", new ScalarValue(typeof(BusinessJobSummary).FullName)),
                new("EventCode", new ScalarValue(code)), new("jobKind", new ScalarValue("post-lottery")),
                new("outcome", new ScalarValue("succeeded")), new("failureKind", new ScalarValue(Secret)),
                new("failedCount", new ScalarValue(-1)), new("processedCount", new ScalarValue(3L)),
                new("durationMs", new ScalarValue(double.PositiveInfinity))
            ], trace, span);
            sink.Emit(raw);
            var value = Assert.Single(capture.Events);
            Assert.Equal(code == "job.batch.failed" ? code : "runtime.unclassified", Scalar(value, "EventCode"));
            Assert.Equal(EventFields.Order(), value.Properties.Keys.Order());
            Assert.Equal(timestamp, value.Timestamp);
            Assert.Equal(LogEventLevel.Error, value.Level);
            Assert.Equal(trace, value.TraceId);
            Assert.Equal(span, value.SpanId);
            Assert.Null(value.Exception);
            Assert.DoesNotContain(HttpBoundaryCapture.Secret, value.RenderMessage());
            capture.Events.Clear();
        }
    }

    private static object? Scalar(LogEvent value, string field) => Assert.IsType<ScalarValue>(value.Properties[field]).Value;

    private static IDisposable PolluteSummaryScope() => LogContext.Push(
        new PropertyEnricher("probePrivate", HttpBoundaryCapture.Secret),
        new PropertyEnricher("SourceContext", typeof(PostLotteryJob).FullName!),
        new PropertyEnricher("SourceCategory", "http"),
        new PropertyEnricher("EventCode", "job.cleanup.failed"),
        new PropertyEnricher("jobKind", "post-lottery"),
        new PropertyEnricher("statusCode", 503),
        new PropertyEnricher("operationId", "12345678-1234-4234-8234-123456789abc"),
        new PropertyEnricher("traceId", "11111111111111111111111111111111"),
        new PropertyEnricher("spanId", "1111111111111111"),
        new PropertyEnricher("processedCount", 999),
        new PropertyEnricher("failureKind", "database"),
        new PropertyEnricher("Diagnostic", true));

    private static ShopJob CancellationJob(bool empty = false, Action? queried = null)
    {
        var orders = new Mock<IBaseRepository<Order>>();
        orders.Setup(r => r.QueryDistinctAsync(It.IsAny<Expression<Func<Order, long>>>(), It.IsAny<Expression<Func<Order, bool>>>()))
            .ReturnsAsync(() =>
            {
                queried?.Invoke();
                return empty ? [] : [1, 2, 3, 4];
            });
        var service = new Mock<IOrderService>(MockBehavior.Strict);
        const string reason = "订单超时自动取消（超过 30 分钟未支付）";
        service.Setup(s => s.CancelOrderBySystemAsync(1, reason)).ReturnsAsync(true);
        service.Setup(s => s.CancelOrderBySystemAsync(2, reason)).ReturnsAsync(false);
        service.Setup(s => s.CancelOrderBySystemAsync(3, reason)).ThrowsAsync(new InvalidOperationException(Secret));
        service.Setup(s => s.CancelOrderBySystemAsync(4, reason)).ThrowsAsync(new IOException(Secret));
        return Shop(orders.Object, service.Object);
    }
}
