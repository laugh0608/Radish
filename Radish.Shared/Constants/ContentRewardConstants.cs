namespace Radish.Shared.Constants;

public static class ContentRewardTargetTypes
{
    public const string Post = "Post";
    public const string Comment = "Comment";

    public static IReadOnlySet<string> All { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            Post,
            Comment
        };
}

public static class ContentRewardReasonCodes
{
    public const string Helpful = "Helpful";
    public const string Insightful = "Insightful";
    public const string WellWritten = "WellWritten";
    public const string Detailed = "Detailed";
    public const string Warm = "Warm";

    public static IReadOnlySet<string> All { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            Helpful,
            Insightful,
            WellWritten,
            Detailed,
            Warm
        };

    public static string GetLabelOrDefault(string? reasonCode, bool english) => (reasonCode, english) switch
    {
        (Helpful, true) => "helpful",
        (Insightful, true) => "insightful",
        (WellWritten, true) => "well-written",
        (Detailed, true) => "detailed",
        (Warm, true) => "heartwarming",
        (Helpful, false) => "很有帮助",
        (Insightful, false) => "很有启发",
        (WellWritten, false) => "写得真好",
        (Detailed, false) => "内容详实",
        (Warm, false) => "温暖了我",
        (_, true) => "recognizing your content",
        _ => "认可内容"
    };
}

public static class ContentRewardErrorCodes
{
    public const string InvalidArgument = "ContentReward.InvalidArgument";
    public const string Unavailable = "ContentReward.Unavailable";
    public const string TargetUnavailable = "ContentReward.TargetUnavailable";
    public const string SelfNotAllowed = "ContentReward.SelfNotAllowed";
    public const string AlreadyRewarded = "ContentReward.AlreadyRewarded";
    public const string InsufficientBalance = "ContentReward.InsufficientBalance";
    public const string DailyLimitExceeded = "ContentReward.DailyLimitExceeded";
    public const string AccountUnavailable = "ContentReward.AccountUnavailable";
    public const string Processing = "ContentReward.Processing";
    public const string IdempotencyConflict = "ContentReward.IdempotencyConflict";
    public const string ConcurrentConflict = "ContentReward.ConcurrentConflict";
    public const string ReplayUnavailable = "ContentReward.ReplayUnavailable";
}
