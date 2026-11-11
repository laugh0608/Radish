using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Radish.Api.Tests.ErrorHandling;
using Radish.Common.OptionTool;
using Radish.Extension.ExperienceExtension;
using Radish.Extension.Log;
using Radish.Service;
using Serilog;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;
using static Radish.Api.Tests.RewardOutputAssertions;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class RewardOutputLoggingTests
{
    private sealed record EventCase(Type Source, string Code, LogEventLevel Level, string? Failure,
        params (string Key, object Value)[] Fields);

    private static IEnumerable<EventCase> RegisteredEvents()
    {
        foreach (var domain in new[] { "coin", "experience" })
        {
            yield return new(typeof(RewardRuntimeLog), "reward.retrying", LogEventLevel.Warning, null,
                ("rewardDomain", domain), ("attempt", 1), ("delayMs", 100));
            foreach (var outcome in new[] { "succeeded", "partial", "failed" })
                yield return new(typeof(RewardRuntimeLog), "reward.batch_completed", LogEventLevel.Information, null,
                    ("rewardDomain", domain), ("outcome", outcome), ("processedCount", outcome == "failed" ? 0 : 1),
                    ("rejectedCount", outcome == "succeeded" ? 0 : 1), ("failedCount", 0));
            foreach (var outcome in new[] { "partial", "failed" })
                yield return new(typeof(RewardRuntimeLog), "reward.batch_failed", LogEventLevel.Error, "io",
                    ("rewardDomain", domain), ("outcome", outcome), ("processedCount", outcome == "failed" ? 0 : 1),
                    ("rejectedCount", 0), ("failedCount", 1));
        }
        foreach (var operation in new[] { "grant", "grant-once", "initialize" })
            yield return new(typeof(RewardRuntimeLog), "reward.failed", LogEventLevel.Error, "io",
                ("rewardDomain", "experience"), ("rewardOperation", operation));
        yield return new(typeof(RewardRuntimeLog), "reward.conflict", LogEventLevel.Warning, "io",
            ("rewardDomain", "experience"), ("rewardOperation", "grant-once"));
        foreach (var operation in new[] { "cache-read", "cache-write", "cache-invalidate" })
        {
            yield return new(typeof(RewardRuntimeLog), "reward.cache_fallback", LogEventLevel.Warning, "io",
                ("rewardDomain", "experience"), ("rewardOperation", operation));
            yield return new(typeof(ExperienceCalculator), "experience.calculator_cache_fallback", LogEventLevel.Warning, "io",
                ("rewardOperation", operation));
        }
        yield return new(typeof(CoinService), "coin.transfer_completion_recovered", LogEventLevel.Warning, "io");
        foreach (var completion in new[] { "success", "failure" })
            yield return new(typeof(OperationIdempotencyService), "idempotency.completion_missing", LogEventLevel.Warning, null,
                ("completionKind", completion));
        yield return new(typeof(CoinRewardService), "reward.existence_check_failed", LogEventLevel.Error, "io");
        yield return new(typeof(ExperienceService), "experience.rank_query_failed", LogEventLevel.Error, "io");
        yield return new(typeof(ExperienceService), "experience.governance_snapshot_invalid", LogEventLevel.Warning, "io");
        yield return new(typeof(ExperienceService), "experience.leaderboard_incomplete", LogEventLevel.Warning, null, ("skippedCount", 2));
    }

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public void RegisteredEvents_ShouldKeepOnlyTheirOwnFieldsAcrossFinalRoutes(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var scope = Pollute();
        using var activity = new Activity("reward-contract").SetIdFormat(ActivityIdFormat.W3C).Start();
        foreach (var item in RegisteredEvents())
        {
            capture.Logger.Write(Raw(item));
            Check(capture, item.Source, item.Code, item.Level, item.Failure, item.Fields);
        }
    }

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public void UnknownOrMismatchedIdentity_ShouldKeepSafeSeverityAndNoBusinessFields(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var scope = Pollute();
        foreach (var item in RegisteredEvents())
        {
            var missing = Raw(item);
            missing.RemovePropertyIfPresent("EventCode");
            capture.Logger.Write(missing);
            AssertUnknown(capture, item.Level);
            var unknown = Raw(item);
            unknown.AddOrUpdateProperty(new("EventCode", new ScalarValue(Secret)));
            capture.Logger.Write(unknown);
            AssertUnknown(capture, item.Level);
            var wrongSource = Raw(item);
            wrongSource.AddOrUpdateProperty(new("SourceContext", new ScalarValue(
                item.Source == typeof(CoinService) ? typeof(CoinRewardService).FullName : typeof(CoinService).FullName)));
            capture.Logger.Write(wrongSource);
            AssertUnknown(capture, item.Level);
            // 每条已登记事件也验证错误等级，不能凭事件码改写原严重程度。
            var wrongLevel = new LogEvent(wrongSource.Timestamp, LogEventLevel.Debug, wrongSource.Exception,
                wrongSource.MessageTemplate, Raw(item).Properties.Select(p => new LogEventProperty(p.Key, p.Value)));
            capture.Logger.Write(wrongLevel);
            AssertUnknown(capture, LogEventLevel.Debug);
            foreach (var field in item.Fields.Where(f => f.Key is "rewardDomain" or "rewardOperation" or "outcome" or "completionKind"))
            {
                var invalid = Raw(item);
                invalid.AddOrUpdateProperty(new(field.Key, new ScalarValue(Secret)));
                capture.Logger.Write(invalid);
                AssertUnknown(capture, item.Level);
            }
        }
    }

    [Fact]
    public void Adapter_ShouldKeepTimestampAndTypedTraceAndRejectInvalidNumericFields()
    {
        using var output = new HttpBoundaryTestSink();
        var sink = new RewardLogSink(output);
        using var activity = new Activity("reward-typed-trace").SetIdFormat(ActivityIdFormat.W3C).Start();
        var timestamp = DateTimeOffset.UtcNow.AddDays(-1);
        var trace = ActivityTraceId.CreateRandom();
        var span = ActivitySpanId.CreateRandom();
        foreach (var item in RegisteredEvents())
        {
            var value = Raw(item);
            var keys = item.Fields.Where(f => f.Value is int).Select(f => f.Key).ToArray();
            foreach (var key in keys) value.AddOrUpdateProperty(new(key, new ScalarValue(-1)));
            value.AddOrUpdateProperty(new("failureKind", new ScalarValue(Secret)));
            value = new(timestamp, item.Level, value.Exception, value.MessageTemplate,
                value.Properties.Select(p => new LogEventProperty(p.Key, p.Value)), trace, span);
            sink.Emit(value);
            var result = Assert.Single(output.Events);
            Assert.Equal(item.Code, ReadScalar(result, "EventCode"));
            Assert.Equal(timestamp, result.Timestamp);
            Assert.Equal(item.Level, result.Level);
            Assert.Equal(trace, result.TraceId);
            Assert.Equal(span, result.SpanId);
            Assert.Null(result.Exception);
            Assert.False(result.Properties.ContainsKey("failureKind"));
            Assert.Equal(Identity.Concat(item.Fields.Where(f => f.Value is not int).Select(f => f.Key)).Order(), result.Properties.Keys.Order());
            output.Events.Clear();
        }
        var retry = RegisteredEvents().First();
        foreach (var invalid in new object[] { 1_000_001, 1L, double.NaN, "1" })
        {
            var value = Raw(retry);
            value.AddOrUpdateProperty(new("attempt", new ScalarValue(invalid)));
            value.AddOrUpdateProperty(new("delayMs", new ScalarValue(invalid)));
            sink.Emit(value);
            var result = Assert.Single(output.Events);
            Assert.False(result.Properties.ContainsKey("attempt"));
            Assert.False(result.Properties.ContainsKey("delayMs"));
            output.Events.Clear();
        }
        var adjacent = Raw(retry);
        adjacent.AddOrUpdateProperty(new("SourceContext", new ScalarValue(typeof(RewardRuntimeLog).FullName + ".Adjacent")));
        sink.Emit(adjacent);
        Assert.Same(adjacent, Assert.Single(output.Events));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void CodeConsole_ShouldUseFixedTemplatesForEveryRegisteredEvent(string environment)
    {
        var previous = Console.Out;
        using var output = new StringWriter();
        using var services = new ServiceCollection().BuildServiceProvider();
        try
        {
            Console.SetOut(output);
            var config = new LoggerConfiguration();
            SerilogSetup.ConfigureLegacy(config, new ConfigurationBuilder().Build(), services, new SerilogOptions
            { Console = new() { Enable = true }, File = new() { Enable = false }, Database = new() { Enable = false } }, environment);
            using var logger = config.CreateLogger();
            using var scope = Pollute();
            foreach (var item in RegisteredEvents()) logger.Write(Raw(item));
            var text = output.ToString();
            Assert.Equal(RegisteredEvents().Count(), text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
            Assert.DoesNotContain(Secret, text);
            Assert.DoesNotContain("Unrecognized", text);
            Assert.Contains("Reward batch returned results.", text);
            Assert.Contains("Transfer completion recording returned after retry.", text);
            Assert.Contains("Experience calculator cache operation failed.", text);
        }
        finally { Console.SetOut(previous); }
    }

    private static LogEvent Raw(EventCase item)
    {
        var properties = new Dictionary<string, object>
        {
            ["SourceContext"] = item.Source.FullName!, ["EventCode"] = item.Code, ["SourceCategory"] = "job", ["Diagnostic"] = true,
            ["failureKind"] = "io", ["privatePayload"] = Secret, ["operationId"] = "12345678-1234-4234-8234-123456789abc",
            ["rewardDomain"] = "coin", ["rewardOperation"] = "initialize", ["completionKind"] = "failure", ["outcome"] = "failed",
            ["attempt"] = 99, ["delayMs"] = 999, ["processedCount"] = 999, ["failedCount"] = 999, ["rejectedCount"] = 999,
            ["skippedCount"] = 999, ["statusCode"] = 503
        };
        foreach (var (key, value) in item.Fields) properties[key] = value;
        return new LogEvent(DateTimeOffset.UtcNow.AddDays(-1), item.Level, new IOException(Secret), new MessageTemplateParser().Parse(Secret),
            properties.Select(p => new LogEventProperty(p.Key, new ScalarValue(p.Value))),
            Activity.Current?.TraceId ?? default, Activity.Current?.SpanId ?? default);
    }

    private static void AssertUnknown(HttpBoundaryCapture capture, LogEventLevel level)
    {
        if (capture.Candidate && capture.Environment == "Production" && level < LogEventLevel.Warning) Assert.Empty(capture.Events);
        else
        {
            var summary = Assert.Single(capture.Events);
            Assert.Equal("runtime.unclassified", summary.Code);
            Assert.Equal(level < LogEventLevel.Warning, summary.Diagnostic);
            Assert.Null(summary.FailureKind);
            if (capture.Candidate)
            {
                using var json = JsonDocument.Parse(capture.Output.ToString());
                Assert.Empty(json.RootElement.GetProperty("properties").EnumerateObject());
                Assert.False(json.RootElement.TryGetProperty("operationId", out _));
                Assert.DoesNotContain(Secret, capture.Output.ToString());
            }
            else
            {
                var value = Assert.Single(capture.Sink.Events);
                Assert.Equal(level, value.Level);
                Assert.Equal(Identity.Order(), value.Properties.Keys.Order());
                Assert.Null(value.Exception);
                Assert.DoesNotContain(Secret, value.RenderMessage());
            }
        }
        capture.Clear();
    }
}
