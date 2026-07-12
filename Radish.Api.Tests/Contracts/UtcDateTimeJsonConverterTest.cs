using System;
using System.Text.Json;
using Xunit;

namespace Radish.Api.Tests.Contracts;

public sealed class UtcDateTimeJsonConverterTest
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    [Fact]
    public void Read_ShouldNormalizeExplicitOffsetToUtc()
    {
        var value = JsonSerializer.Deserialize<DateTime>("\"2026-07-12T08:30:00+08:00\"", Options);

        Assert.Equal(new DateTime(2026, 7, 12, 0, 30, 0, DateTimeKind.Utc), value);
    }

    [Fact]
    public void Read_ShouldRejectDateTimeWithoutOffset()
    {
        var exception = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<DateTime>("\"2026-07-12 08:30:00\"", Options));

        Assert.Contains("Z 或 offset", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_ShouldKeepDateOnlyCompatibility()
    {
        var value = JsonSerializer.Deserialize<DateTime>("\"2026-07-12\"", Options);

        Assert.Equal(new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc), value);
    }

    [Fact]
    public void Write_ShouldAlwaysEmitUtcRoundTripValue()
    {
        var json = JsonSerializer.Serialize(
            new DateTime(2026, 7, 12, 8, 30, 0, DateTimeKind.Local),
            Options);

        Assert.EndsWith("Z\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void DateOnlyContract_ShouldUseCalendarDateWithoutTimeZone()
    {
        var date = new DateOnly(2026, 7, 12);

        var json = JsonSerializer.Serialize(date, Options);
        var parsed = JsonSerializer.Deserialize<DateOnly>(json, Options);

        Assert.Equal("\"2026-07-12\"", json);
        Assert.Equal(date, parsed);
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<DateOnly>("\"2026-07-12T00:00:00Z\"", Options));
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new UtcDateTimeJsonConverter());
        return options;
    }
}
