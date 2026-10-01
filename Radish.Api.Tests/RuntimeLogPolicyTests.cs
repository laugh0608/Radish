using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Radish.Common.LogTool;
using Xunit;

namespace Radish.Api.Tests;

public class RuntimeLogPolicyTests
{
    private static readonly RuntimeLogSource Source = new("l1-test", "api", "fixture", "test-latest");
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-09-19T00:00:00.123Z");
    }

    public static IEnumerable<object[]> Fixtures()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "LoggingFixtures/runtime-events.json")));
        foreach (var fixture in document.RootElement.EnumerateArray())
            yield return new object[] { fixture.GetProperty("name").GetString()!, fixture.GetRawText() };
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void SharedFixtures_ShouldApplyTheSameContractAsNode(string name, string json)
    {
        using var document = JsonDocument.Parse(json);
        var fixture = document.RootElement;
        fixture.TryGetProperty("options", out var options);
        string GetOption(string key, string fallback) => options.ValueKind == JsonValueKind.Object && options.TryGetProperty(key, out var value)
            ? value.GetString()! : fallback;
        var diagnostics = options.ValueKind == JsonValueKind.Object && options.TryGetProperty("diagnostics", out var flag) && flag.GetBoolean();
        var policy = new RuntimeLogPolicy(Source, GetOption("mode", "Production"), GetOption("minimumLevel", "Info"), diagnostics, new FixedClock());
        var value = policy.Create(fixture.GetProperty("input"));
        var expected = fixture.GetProperty("expected");
        if (expected.ValueKind == JsonValueKind.Null) { Assert.Null(value); return; }
        Assert.NotNull(value);
        var serialized = value.ToJsonLine();
        using var output = JsonDocument.Parse(serialized);
        foreach (var field in expected.EnumerateObject())
            Assert.True(JsonElement.DeepEquals(field.Value, output.RootElement.GetProperty(field.Name)), $"{name}: {field.Name}");
        Assert.True(Guid.TryParse(value.EventId, out _));
        Assert.Equal("2026-09-19T00:00:00.123Z", value.OccurredAtUtc);
        Assert.Equal(value.OccurredAtUtc, value.ObservedAtUtc);
        Assert.DoesNotContain("SENTINEL_SECRET", serialized);
        Assert.True(Encoding.UTF8.GetByteCount(serialized) <= RuntimeLogPolicy.MaxEventBytes);
        Assert.Equal(value.EventId, output.RootElement.GetProperty("eventId").GetString());
    }

    [Fact]
    public void Serialization_ShouldEnforceUtf8BudgetBeforeDockerFraming()
    {
        using var input = JsonDocument.Parse("{}");
        var value = new RuntimeLogPolicy(Source).Create(input.RootElement)!;
        Assert.Throws<InvalidOperationException>(() => (value with { Message = new string('汉', 3000) }).ToJsonLine());
    }

    [Fact]
    public void Configuration_ShouldRejectProductionDiagnosticsAndUnknownValues()
    {
        using var invalid = JsonDocument.Parse("{\"diagnostic\":\"true\"}");
        Assert.Throws<ArgumentException>(() => new RuntimeLogPolicy(Source).Create(invalid.RootElement));
        Assert.Throws<ArgumentException>(() => new RuntimeLogPolicy(Source, diagnostics: true));
        Assert.Throws<ArgumentException>(() => new RuntimeLogPolicy(Source, mode: "test-latest"));
        Assert.Throws<ArgumentException>(() => new RuntimeLogPolicy(Source, minimumLevel: "Debug"));
        Assert.Throws<ArgumentException>(() => new RuntimeLogPolicy(Source with { Service = "unknown" }));
        Assert.Throws<ArgumentException>(() => new RuntimeLogPolicy(Source with { InstanceId = "node\n" }));
    }

    [Fact]
    public void LargeRawPayload_ShouldNeverBeSerialized_AndOccurrencesHaveDifferentIds()
    {
        var policy = new RuntimeLogPolicy(Source);
        using var input = JsonDocument.Parse(JsonSerializer.Serialize(new { message = new string('x', 1_000_000), exception = "SENTINEL_SECRET" }));
        var first = policy.Create(input.RootElement)!;
        var second = policy.Create(input.RootElement)!;
        Assert.True(first.Redacted);
        Assert.True(first.ToJsonLine().Length < 2048);
        Assert.NotEqual(first.EventId, second.EventId);
    }
}
