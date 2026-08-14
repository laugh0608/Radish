namespace Radish.Shared.CustomEnum;

/// <summary>排行榜公开访问策略。</summary>
public static class LeaderboardPublicPolicy
{
    /// <summary>判断排行榜类型是否允许公开读取。</summary>
    public static bool IsPublicType(LeaderboardType type)
    {
        return type is LeaderboardType.Experience
            or LeaderboardType.PostCount
            or LeaderboardType.CommentCount
            or LeaderboardType.Popularity
            or LeaderboardType.HotProduct;
    }

    /// <summary>判断排行榜类型是否支持用户个人排名。</summary>
    public static bool SupportsUserRank(LeaderboardType type)
    {
        return type is LeaderboardType.Experience
            or LeaderboardType.PostCount
            or LeaderboardType.CommentCount
            or LeaderboardType.Popularity;
    }
}
