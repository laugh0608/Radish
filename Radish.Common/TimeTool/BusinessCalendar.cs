using Microsoft.Extensions.Options;
using Radish.Common.OptionTool;

namespace Radish.Common.TimeTool;

/// <summary>
/// 统一按系统业务时区计算自然日及其 UTC 边界。
/// </summary>
public sealed class BusinessCalendar
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _timeZone;

    public BusinessCalendar(TimeProvider timeProvider, IOptions<TimeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);

        _timeProvider = timeProvider;
        _timeZone = TimeZoneResolver.ResolveOrUtc(options.Value.DefaultTimeZoneId);
    }

    /// <summary>系统业务时区。</summary>
    public TimeZoneInfo TimeZone => _timeZone;

    /// <summary>获取当前业务自然日。</summary>
    public DateOnly GetCurrentDate()
    {
        return GetDate(_timeProvider.GetUtcNow());
    }

    /// <summary>将 UTC 时刻归属到系统业务自然日。</summary>
    public DateOnly GetDate(DateTimeOffset utcInstant)
    {
        var businessTime = TimeZoneInfo.ConvertTime(utcInstant.ToUniversalTime(), _timeZone);
        return DateOnly.FromDateTime(businessTime.DateTime);
    }

    /// <summary>获取业务自然日对应的 UTC 半开区间。</summary>
    public (DateTime StartUtc, DateTime EndUtc) GetUtcRange(DateOnly date)
    {
        return (ConvertStartToUtc(date), ConvertStartToUtc(date.AddDays(1)));
    }

    /// <summary>获取从当前时刻到下一个业务日开始的剩余时间。</summary>
    public TimeSpan GetTimeUntilNextDate()
    {
        var nowUtc = _timeProvider.GetUtcNow();
        var nextDate = GetDate(nowUtc).AddDays(1);
        var nextStartUtc = ConvertStartToUtc(nextDate);
        var remaining = nextStartUtc - nowUtc.UtcDateTime;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.FromSeconds(1);
    }

    private DateTime ConvertStartToUtc(DateOnly date)
    {
        var localStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (_timeZone.IsInvalidTime(localStart) && DateOnly.FromDateTime(localStart) == date)
        {
            localStart = localStart.AddMinutes(1);
        }

        if (DateOnly.FromDateTime(localStart) != date)
        {
            throw new InvalidOperationException($"业务时区 {_timeZone.Id} 不存在自然日 {date:yyyy-MM-dd}。");
        }

        if (_timeZone.IsAmbiguousTime(localStart))
        {
            var earliestOffset = _timeZone.GetAmbiguousTimeOffsets(localStart).Max();
            return new DateTimeOffset(localStart, earliestOffset).UtcDateTime;
        }

        return TimeZoneInfo.ConvertTimeToUtc(localStart, _timeZone);
    }
}
