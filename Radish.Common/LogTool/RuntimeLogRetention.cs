namespace Radish.Common.LogTool;

public static class RuntimeLogRetention
{
    public const string ExpiredReason = "retention-expired";
    public static IReadOnlyList<string> Levels { get; } = Array.AsReadOnly(new[] { "Info", "Warning", "Error" });

    public static DateTime Cutoff(string level, DateTime utcNow) => utcNow.AddDays(level switch
    {
        "Info" => -7,
        "Warning" => -30,
        "Error" => -90,
        _ => throw new ArgumentOutOfRangeException(nameof(level))
    });
}
