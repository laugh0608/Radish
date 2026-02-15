using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace Radish.Common.TimeTool;

/// <summary>统一处理时区 ID 解析与降级</summary>
public static class TimeZoneResolver
{
    private static readonly Regex IanaPattern =
        new(@"^[A-Za-z][A-Za-z0-9._+-]*(/[A-Za-z0-9._+-]+)+$", RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, string> IanaToWindowsMap =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Etc/UTC"] = "UTC",
            ["UTC"] = "UTC",
            ["Asia/Shanghai"] = "China Standard Time",
            ["Asia/Tokyo"] = "Tokyo Standard Time",
            ["Asia/Singapore"] = "Singapore Standard Time",
            ["Europe/London"] = "GMT Standard Time",
            ["Europe/Berlin"] = "W. Europe Standard Time",
            ["America/New_York"] = "Eastern Standard Time",
            ["America/Los_Angeles"] = "Pacific Standard Time"
        });

    private static readonly IReadOnlyDictionary<string, string> WindowsToIanaMap =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["UTC"] = "UTC",
            ["China Standard Time"] = "Asia/Shanghai",
            ["Tokyo Standard Time"] = "Asia/Tokyo",
            ["Singapore Standard Time"] = "Asia/Singapore",
            ["GMT Standard Time"] = "Europe/London",
            ["W. Europe Standard Time"] = "Europe/Berlin",
            ["Eastern Standard Time"] = "America/New_York",
            ["Pacific Standard Time"] = "America/Los_Angeles"
        });

    /// <summary>解析时区，失败时回落到 fallbackId，再失败回落到 UTC</summary>
    public static TimeZoneInfo ResolveOrUtc(string? requestedId, string fallbackId = "Asia/Shanghai")
    {
        return TryResolve(requestedId, out var requestedTz)
            ? requestedTz
            : TryResolve(fallbackId, out var fallbackTz)
                ? fallbackTz
                : TimeZoneInfo.Utc;
    }

    /// <summary>返回可用于前端展示的规范化时区 ID（优先 IANA）</summary>
    public static string NormalizeToDisplayId(string? requestedId, string fallbackId = "Asia/Shanghai")
    {
        if (TryResolve(requestedId, out var resolved))
        {
            return ToIanaIdOrOriginal(resolved.Id);
        }

        if (TryResolve(fallbackId, out var fallback))
        {
            return ToIanaIdOrOriginal(fallback.Id);
        }

        return "UTC";
    }

    /// <summary>判断时区 ID 是否有效（支持 IANA 与 Windows）</summary>
    public static bool IsValidTimeZoneId(string? id)
    {
        return TryResolve(id, out _);
    }

    /// <summary>判断是否是 IANA 时区 ID 的合法形态（用于跨平台保底校验）</summary>
    public static bool IsLikelyIanaTimeZoneId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        return IanaPattern.IsMatch(id.Trim());
    }

    /// <summary>尝试解析时区（支持 IANA 与 Windows）</summary>
    public static bool TryResolve(string? id, out TimeZoneInfo timeZone)
    {
        timeZone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        if (TryFindById(id, out timeZone))
        {
            return true;
        }

        if (IanaToWindowsMap.TryGetValue(id, out var windowsId) && TryFindById(windowsId, out timeZone))
        {
            return true;
        }

        if (WindowsToIanaMap.TryGetValue(id, out var ianaId) && TryFindById(ianaId, out timeZone))
        {
            return true;
        }

        return false;
    }

    private static bool TryFindById(string id, out TimeZoneInfo timeZone)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch
        {
            timeZone = TimeZoneInfo.Utc;
            return false;
        }
    }

    private static string ToIanaIdOrOriginal(string zoneId)
    {
        return WindowsToIanaMap.TryGetValue(zoneId, out var iana) ? iana : zoneId;
    }
}
