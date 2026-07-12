using System;
using Microsoft.Extensions.Options;
using Radish.Common.OptionTool;
using Radish.Common.TimeTool;
using Xunit;

namespace Radish.Api.Tests.Common;

public sealed class BusinessCalendarTest
{
    [Fact]
    public void GetCurrentDate_ShouldUseConfiguredBusinessTimeZone()
    {
        var timeProvider = new FixedUtcTimeProvider(new DateTimeOffset(2026, 7, 11, 16, 30, 0, TimeSpan.Zero));
        var calendar = CreateCalendar(timeProvider, "Asia/Shanghai");

        var currentDate = calendar.GetCurrentDate();
        var (startUtc, endUtc) = calendar.GetUtcRange(currentDate);

        Assert.Equal(new DateOnly(2026, 7, 12), currentDate);
        Assert.Equal(new DateTime(2026, 7, 11, 16, 0, 0, DateTimeKind.Utc), startUtc);
        Assert.Equal(new DateTime(2026, 7, 12, 16, 0, 0, DateTimeKind.Utc), endUtc);
        Assert.Equal(TimeSpan.FromHours(23.5), calendar.GetTimeUntilNextDate());
    }

    [Theory]
    [InlineData(2026, 3, 8, 23)]
    [InlineData(2026, 11, 1, 25)]
    public void GetUtcRange_ShouldRespectDaylightSavingBoundaries(int year, int month, int day, int expectedHours)
    {
        var calendar = CreateCalendar(
            new FixedUtcTimeProvider(DateTimeOffset.UnixEpoch),
            "America/New_York");

        var (startUtc, endUtc) = calendar.GetUtcRange(new DateOnly(year, month, day));

        Assert.Equal(TimeSpan.FromHours(expectedHours), endUtc - startUtc);
    }

    [Fact]
    public void GetUtcRange_ShouldUseFirstValidInstantWhenMidnightIsSkipped()
    {
        var calendar = CreateCalendar(
            new FixedUtcTimeProvider(DateTimeOffset.UnixEpoch),
            "America/Sao_Paulo");

        var (startUtc, endUtc) = calendar.GetUtcRange(new DateOnly(2018, 11, 4));

        Assert.Equal(new DateTime(2018, 11, 4, 3, 0, 0, DateTimeKind.Utc), startUtc);
        Assert.Equal(TimeSpan.FromHours(23), endUtc - startUtc);
    }

    private static BusinessCalendar CreateCalendar(TimeProvider timeProvider, string timeZoneId)
    {
        return new BusinessCalendar(
            timeProvider,
            Options.Create(new TimeOptions { DefaultTimeZoneId = timeZoneId }));
    }

    private sealed class FixedUtcTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
