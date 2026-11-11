using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Radish.Api.Tests.ErrorHandling;
using Radish.Repository;
using Serilog.Context;
using Serilog.Core.Enrichers;
using Serilog.Events;
using Xunit;

namespace Radish.Api.Tests;

internal static class RewardOutputAssertions
{
    internal static readonly string[] Identity = ["SourceContext", "SourceCategory", "EventCode", "Diagnostic"];
    internal const string Secret = "REWARD_FINAL_OUTPUT_PRIVATE_SENTINEL";

    internal static IDisposable Pollute() => LogContext.Push(
        new PropertyEnricher("SourceContext", typeof(ReliableOutboxRepository).FullName!),
        new PropertyEnricher("EventCode", "outbox.dead_letter"), new PropertyEnricher("SourceCategory", "job"),
        new PropertyEnricher("Diagnostic", true), new PropertyEnricher("failureKind", "database"),
        new PropertyEnricher("operationId", "12345678-1234-4234-8234-123456789abc"),
        new PropertyEnricher("traceId", "11111111111111111111111111111111"), new PropertyEnricher("spanId", "1111111111111111"),
        new PropertyEnricher("statusCode", 503), new PropertyEnricher("privatePayload", Secret),
        new PropertyEnricher("rewardDomain", "coin"), new PropertyEnricher("rewardOperation", "initialize"),
        new PropertyEnricher("completionKind", "failure"), new PropertyEnricher("outcome", "failed"),
        new PropertyEnricher("attempt", 99), new PropertyEnricher("delayMs", 999),
        new PropertyEnricher("processedCount", 999), new PropertyEnricher("rejectedCount", 999),
        new PropertyEnricher("failedCount", 999), new PropertyEnricher("skippedCount", 999));

    internal static void Check(HttpBoundaryCapture capture, Type source, string code, LogEventLevel level,
        string? failure, params (string Key, object Value)[] fields)
    {
        var summary = Assert.Single(capture.Events);
        Assert.Equal(code, summary.Code);
        Assert.Equal(capture.Candidate && level == LogEventLevel.Information ? "Info" : level.ToString(), summary.Level);
        Assert.Equal(failure, summary.FailureKind);
        Assert.False(summary.Diagnostic);
        var expected = fields.ToDictionary(x => x.Key, x => x.Value);
        if (failure != null) expected.Add("failureKind", failure);
        var activity = Activity.Current;
        Assert.NotNull(activity);
        if (capture.Candidate)
        {
            using var json = JsonDocument.Parse(capture.Output.ToString());
            var root = json.RootElement;
            var properties = root.GetProperty("properties");
            Assert.Equal(expected.Keys.Order(), properties.EnumerateObject().Select(p => p.Name).Order());
            Assert.Equal("application", root.GetProperty("sourceCategory").GetString());
            Assert.False(root.TryGetProperty("operationId", out _));
            Assert.Equal(activity.TraceId.ToHexString(), root.GetProperty("traceId").GetString());
            Assert.Equal(activity.SpanId.ToHexString(), root.GetProperty("spanId").GetString());
            foreach (var (key, value) in expected)
            {
                var actual = properties.GetProperty(key);
                AssertValue(value, value is string ? actual.GetString() : actual.GetInt32());
            }
            Assert.DoesNotContain(Secret, capture.Output.ToString());
        }
        else
        {
            var value = Assert.Single(capture.Sink.Events);
            Assert.Equal(Identity.Concat(expected.Keys).Order(), value.Properties.Keys.Order());
            Assert.Equal(source.FullName, ReadScalar(value, "SourceContext"));
            Assert.Equal("application", ReadScalar(value, "SourceCategory"));
            Assert.Equal(activity.TraceId, value.TraceId);
            Assert.Equal(activity.SpanId, value.SpanId);
            Assert.Null(value.Exception);
            foreach (var (key, expectedValue) in expected) AssertValue(expectedValue, ReadScalar(value, key));
            Assert.DoesNotContain(Secret, value.RenderMessage() + string.Join(" ", value.Properties.Values));
        }
        capture.AssertSafe();
        capture.Clear();
    }

    internal static object? ReadScalar(LogEvent value, string key) => Assert.IsType<ScalarValue>(value.Properties[key]).Value;

    private static void AssertValue(object expected, object? actual)
    {
        if (expected is ValueTuple<int, int> range) Assert.InRange(Assert.IsType<int>(actual), range.Item1, range.Item2);
        else Assert.Equal(expected, actual);
    }
}
