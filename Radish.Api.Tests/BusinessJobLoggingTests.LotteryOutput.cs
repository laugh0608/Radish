using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Api.Tests.ErrorHandling;
using Radish.Common.OptionTool;
using Radish.Extension.Log;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service.Jobs;
using Serilog;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests;

public sealed partial class BusinessJobLoggingTests
{
    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public async Task Lottery_ShouldWriteOnlyItsBatchFieldsAfterAmbientEnrichment(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var scope = PolluteSummaryScope(typeof(BusinessJobSummary).FullName, "shop-timeouts");
        using var activity = new Activity("lottery-batch").SetIdFormat(ActivityIdFormat.W3C).Start();
        var ioFailure = new IOException(HttpBoundaryCapture.Secret);

        Assert.Equal(2, await RunLotteryBatch(capture.Factory));
        AssertLotterySummary(capture, activity, "succeeded", 2, 0);

        capture.Clear();
        Assert.Equal(1, await RunLotteryBatch(capture.Factory, ioFailure));
        AssertLotterySummary(capture, activity, "partial", 1, 1, "io");

        capture.Clear();
        Assert.Equal(0, await RunLotteryBatch(capture.Factory, ioFailure, new TimeoutException(HttpBoundaryCapture.Secret)));
        AssertLotterySummary(capture, activity, "failed", 0, 2, "other");
    }

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public async Task Lottery_ShouldPreserveScanAndOutputFailureOwnership(bool candidate, string environment, bool audit)
    {
        using var writer = new HangfireBrokenWriter();
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit, output: writer);
        using var scope = PolluteSummaryScope(typeof(BusinessJobSummary).FullName, "shop-timeouts");
        var scanFailure = new IOException(HttpBoundaryCapture.Secret);
        var emptyRepository = new Mock<IBaseRepository<PostLottery>>(MockBehavior.Strict);
        emptyRepository.SetupSequence(r => r.QueryPageAsync(It.IsAny<Expression<Func<PostLottery, bool>>>(), 1, 1,
                It.IsAny<Expression<Func<PostLottery, object>>>(), OrderByType.Asc))
            .ReturnsAsync((new List<PostLottery>(), 0)).ThrowsAsync(scanFailure);
        var service = new Mock<IPostLotteryService>(MockBehavior.Strict);
        var scanJob = new PostLotteryJob(emptyRepository.Object, service.Object, capture.Factory.CreateLogger<PostLotteryJob>());

        Assert.Equal(0, await scanJob.ExecuteAutoDrawAsync(0));
        Assert.Same(scanFailure, await Assert.ThrowsAsync<IOException>(() => scanJob.ExecuteAutoDrawAsync(0)));
        service.VerifyNoOtherCalls();
        Assert.Empty(capture.Events);
        Assert.Equal("", capture.Emergency.ToString());

        var outputFailure = new IOException(HttpBoundaryCapture.Secret);
        capture.Sink.EmitFailure = outputFailure;
        var (job, draws) = LotteryBatch(capture.Factory, new IOException(HttpBoundaryCapture.Secret));
        if (audit)
        {
            var caught = await Assert.ThrowsAsync<AggregateException>(() => job.ExecuteAutoDrawAsync(999));
            Assert.Same(outputFailure, Assert.Single(caught.Flatten().InnerExceptions));
        }
        else
        {
            Assert.Equal(1, await job.ExecuteAutoDrawAsync(999));
        }

        VerifyLotteryDraws(draws);
        Assert.Empty(capture.Events);
        if (candidate)
        {
            using var emergency = JsonDocument.Parse(capture.Emergency.ToString());
            Assert.Equal("pipeline.output_failed", emergency.RootElement.GetProperty("eventCode").GetString());
            Assert.DoesNotContain(HttpBoundaryCapture.Secret, capture.Emergency.ToString());
        }
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Lottery_LegacyCodeConsoleShouldWriteOnlyTheFixedSummary(string environment)
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
            using var factory = LoggerFactory.Create(builder => builder.AddSerilog(logger));
            using var scope = PolluteSummaryScope(typeof(BusinessJobSummary).FullName, "shop-timeouts");

            Assert.Equal(1, await RunLotteryBatch(factory, new IOException(HttpBoundaryCapture.Secret)));
            var line = Assert.Single(output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries));
            Assert.EndsWith("Business job batch consumed failures", line.TrimEnd());
            Assert.DoesNotContain(HttpBoundaryCapture.Secret, line);
        }
        finally { Console.SetOut(previous); }
    }

    private static void AssertLotterySummary(HttpBoundaryCapture capture, Activity activity, string outcome,
        int processedCount, int failedCount, string? failureKind = null)
    {
        var summary = Assert.Single(capture.Events);
        Assert.Equal(failedCount == 0 ? "job.batch.completed" : "job.batch.failed", summary.Code);
        Assert.Equal(failedCount > 0 ? "Error" : capture.Candidate ? "Info" : "Information", summary.Level);
        Assert.Equal(failureKind, summary.FailureKind);
        Assert.False(summary.Diagnostic);
        capture.AssertSafe();

        IEnumerable<string> fields = ["jobKind", "outcome", "processedCount", "failedCount", "durationMs"];
        if (failureKind != null) fields = fields.Append("failureKind");
        if (capture.Candidate)
        {
            using var output = JsonDocument.Parse(capture.Output.ToString());
            var value = output.RootElement;
            var properties = value.GetProperty("properties");
            Assert.Equal(fields.Order(), properties.EnumerateObject().Select(p => p.Name).Order());
            Assert.Equal("job", value.GetProperty("sourceCategory").GetString());
            Assert.Equal(activity.TraceId.ToHexString(), value.GetProperty("traceId").GetString());
            Assert.Equal(activity.SpanId.ToHexString(), value.GetProperty("spanId").GetString());
            Assert.False(value.TryGetProperty("operationId", out _));
            Assert.Equal("post-lottery", properties.GetProperty("jobKind").GetString());
            Assert.Equal(outcome, properties.GetProperty("outcome").GetString());
            Assert.Equal(processedCount, properties.GetProperty("processedCount").GetInt32());
            Assert.Equal(failedCount, properties.GetProperty("failedCount").GetInt32());
            Assert.InRange(properties.GetProperty("durationMs").GetDouble(), 0d, 31_536_000_000d);
        }
        else
        {
            var value = Assert.Single(capture.Sink.Events);
            Assert.Equal(fields.Concat(EventFields).Order(), value.Properties.Keys.Order());
            Assert.Equal(typeof(PostLotteryJob).FullName, Scalar(value, "SourceContext"));
            Assert.Equal("job", Scalar(value, "SourceCategory"));
            Assert.Equal(activity.TraceId, value.TraceId);
            Assert.Equal(activity.SpanId, value.SpanId);
            Assert.Equal("post-lottery", Scalar(value, "jobKind"));
            Assert.Equal(outcome, Scalar(value, "outcome"));
            Assert.Equal(processedCount, Scalar(value, "processedCount"));
            Assert.Equal(failedCount, Scalar(value, "failedCount"));
            Assert.InRange(Assert.IsType<double>(Scalar(value, "durationMs")), 0d, 31_536_000_000d);
        }
    }

    private static async Task<int> RunLotteryBatch(ILoggerFactory factory, Exception? firstFailure = null, Exception? secondFailure = null)
    {
        var (job, draws) = LotteryBatch(factory, firstFailure, secondFailure);
        var processed = await job.ExecuteAutoDrawAsync(999);
        VerifyLotteryDraws(draws);
        return processed;
    }

    private static (PostLotteryJob Job, Mock<IPostLotteryService> Draws) LotteryBatch(ILoggerFactory factory,
        Exception? firstFailure, Exception? secondFailure = null)
    {
        var repository = new Mock<IBaseRepository<PostLottery>>(MockBehavior.Strict);
        repository.Setup(r => r.QueryPageAsync(It.IsAny<Expression<Func<PostLottery, bool>>>(), 1, 100,
                It.IsAny<Expression<Func<PostLottery, object>>>(), OrderByType.Asc))
            .ReturnsAsync((new List<PostLottery> { new() { PostId = 1 }, new() { PostId = 1 }, new() { PostId = 2 } }, 3));
        var service = new Mock<IPostLotteryService>(MockBehavior.Strict);
        var first = service.Setup(s => s.AutoDrawByPostIdAsync(1));
        if (firstFailure == null) first.ReturnsAsync(new PostLotteryVo());
        else first.ThrowsAsync(firstFailure);
        var second = service.Setup(s => s.AutoDrawByPostIdAsync(2));
        if (secondFailure == null) second.ReturnsAsync(new PostLotteryVo());
        else second.ThrowsAsync(secondFailure);
        return (new PostLotteryJob(repository.Object, service.Object, factory.CreateLogger<PostLotteryJob>()), service);
    }

    private static void VerifyLotteryDraws(Mock<IPostLotteryService> service)
    {
        service.Verify(s => s.AutoDrawByPostIdAsync(1), Times.Once);
        service.Verify(s => s.AutoDrawByPostIdAsync(2), Times.Once);
        service.VerifyNoOtherCalls();
    }
}
