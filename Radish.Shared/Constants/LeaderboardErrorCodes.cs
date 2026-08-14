namespace Radish.Shared.Constants;

/// <summary>公开排行榜错误契约。</summary>
public static class LeaderboardErrorCodes
{
    public const string TypeUnavailable = "Leaderboard.TypeUnavailable";
    public const string UserRankUnavailable = "Leaderboard.UserRankUnavailable";

    public static string ResolveMessageKey(string errorCode) => errorCode switch
    {
        TypeUnavailable => "error.leaderboard.type_unavailable",
        UserRankUnavailable => "error.leaderboard.user_rank_unavailable",
        _ => throw new ArgumentOutOfRangeException(
            nameof(errorCode),
            errorCode,
            "Unknown leaderboard error code.")
    };
}
