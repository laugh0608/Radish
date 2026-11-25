using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Radish.Api.Tests.ErrorHandling;
using Radish.Service;
using Serilog.Context;
using Serilog.Core.Enrichers;
using Serilog.Events;
using Xunit;

namespace Radish.Api.Tests;

internal static class ShopOutputAssertions
{
    internal const string Secret = "SHOP_FINAL_OUTPUT_PRIVATE_SENTINEL";
    internal static readonly string[] Identity = ["SourceContext", "SourceCategory", "EventCode", "Diagnostic"];
    internal sealed record Expected(Type Source, string Code, LogEventLevel Level, string? Failure,
        params (string Key, object Value)[] Fields);

    internal static IDisposable Pollute(HttpBoundaryCapture capture, bool mel)
    {
        var properties = new Dictionary<string, object>
        {
            ["SourceContext"] = typeof(RewardRuntimeLog).FullName!, ["SourceCategory"] = "job",
            ["EventCode"] = "reward.failed", ["EventId"] = new EventId(99, "OtherEvent"), ["Diagnostic"] = true,
            ["failureKind"] = "database", ["operationId"] = "01234567-89ab-4cde-8f01-23456789abcd",
            ["traceId"] = "11111111111111111111111111111111", ["spanId"] = "1111111111111111",
            ["privatePayload"] = Secret, ["statusCode"] = 503, ["purchaseStage"] = "fulfillment",
            ["attempt"] = 99, ["delayMs"] = 999, ["processedCount"] = 999, ["rewardDomain"] = "coin"
        };
        return mel ? capture.Factory.CreateLogger("shop.scope").BeginScope(properties)!
            : LogContext.Push(properties.Select(p => new PropertyEnricher(p.Key, p.Value)).ToArray());
    }

    internal static void Check(HttpBoundaryCapture capture, params Expected[] expected)
    {
        var summaries = capture.Events;
        Assert.Equal(expected.Length, summaries.Length);
        var legacy = capture.Sink.Events.ToArray();
        var lines = capture.Output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var activity = Activity.Current;
        Assert.NotNull(activity);
        for (var i = 0; i < expected.Length; i++)
        {
            var item = expected[i];
            Assert.Equal(item.Code, summaries[i].Code);
            Assert.Equal(capture.Candidate && item.Level == LogEventLevel.Information ? "Info" : item.Level.ToString(), summaries[i].Level);
            Assert.Equal(item.Failure, summaries[i].FailureKind);
            Assert.False(summaries[i].Diagnostic);
            var fields = item.Fields.ToDictionary(p => p.Key, p => p.Value);
            if (item.Failure != null) fields.Add("failureKind", item.Failure);
            if (capture.Candidate)
            {
                using var json = JsonDocument.Parse(lines[i]);
                var root = json.RootElement;
                var properties = root.GetProperty("properties");
                Assert.Equal(fields.Keys.Order(), properties.EnumerateObject().Select(p => p.Name).Order());
                Assert.Equal("application", root.GetProperty("sourceCategory").GetString());
                Assert.Equal(activity.TraceId.ToHexString(), root.GetProperty("traceId").GetString());
                Assert.Equal(activity.SpanId.ToHexString(), root.GetProperty("spanId").GetString());
                Assert.False(root.TryGetProperty("operationId", out _));
                foreach (var (key, value) in fields)
                    Assert.Equal(value, value is string ? properties.GetProperty(key).GetString() : (object)properties.GetProperty(key).GetInt32());
            }
            else
            {
                var value = legacy[i];
                Assert.Equal(Identity.Concat(fields.Keys).Order(), value.Properties.Keys.Order());
                Assert.Equal(item.Source.FullName, ReadScalar(value, "SourceContext"));
                Assert.Equal("application", ReadScalar(value, "SourceCategory"));
                Assert.Equal(activity.TraceId, value.TraceId);
                Assert.Equal(activity.SpanId, value.SpanId);
                Assert.Null(value.Exception);
                foreach (var (key, expectedValue) in fields) Assert.Equal(expectedValue, ReadScalar(value, key));
                Assert.DoesNotContain(Secret, value.RenderMessage() + string.Join(" ", value.Properties.Values));
            }
        }
        Assert.DoesNotContain(Secret, capture.Output.ToString());
        Assert.Equal("", capture.Emergency.ToString());
        capture.Clear();
    }

    internal static async Task AssertAuditFailure(Func<Task> call, Exception expected)
    {
        var failure = await Assert.ThrowsAsync<AggregateException>(call);
        Assert.Same(expected, Assert.Single(failure.Flatten().InnerExceptions));
    }

    internal static void AssertOutputFailed(HttpBoundaryCapture capture)
    {
        Assert.Empty(capture.Events);
        if (capture.Candidate)
        {
            using var json = JsonDocument.Parse(capture.Emergency.ToString());
            Assert.Equal("pipeline.output_failed", json.RootElement.GetProperty("eventCode").GetString());
            Assert.DoesNotContain(Secret, capture.Emergency.ToString());
        }
        else Assert.Equal("", capture.Emergency.ToString());
    }

    internal static object? ReadScalar(LogEvent value, string key) => Assert.IsType<ScalarValue>(value.Properties[key]).Value;
}
