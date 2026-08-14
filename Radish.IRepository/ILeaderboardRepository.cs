using Radish.Model;

namespace Radish.IRepository;

/// <summary>用户排行榜的权威指标投影。</summary>
public sealed record UserLeaderboardMetric(long UserId, long Value);

/// <summary>排行榜公开展示所需的最小用户投影。</summary>
public sealed class LeaderboardUserProjection
{
    public long UserId { get; set; }

    public string? UserName { get; set; }

    public string? PublicId { get; set; }

    public long? PublicIndex { get; set; }
}

/// <summary>排行榜仓储接口。</summary>
/// <remarks>
/// 仓储统一负责参与资格、指标资格、稳定排序、分页总数与个人排名，
/// 避免 Service 在分页完成后再过滤用户。
/// </remarks>
public interface ILeaderboardRepository
{
    /// <summary>获取经验排行榜与应用完整资格后的总数。</summary>
    Task<(List<UserLeaderboardMetric> Items, int TotalCount)> GetExperienceRankingAsync(
        DateTime now,
        int pageIndex,
        int pageSize);

    /// <summary>获取用户在经验排行榜的确定性序号。</summary>
    Task<int> GetUserExperienceRankAsync(long userId, DateTime now);

    /// <summary>获取发帖排行榜与应用完整资格后的总数。</summary>
    Task<(List<UserLeaderboardMetric> Items, int TotalCount)> GetPostCountRankingAsync(
        int pageIndex,
        int pageSize);

    /// <summary>获取用户在发帖排行榜的确定性序号。</summary>
    Task<int> GetUserPostCountRankAsync(long userId);

    /// <summary>获取评论排行榜与应用完整资格后的总数。</summary>
    Task<(List<UserLeaderboardMetric> Items, int TotalCount)> GetCommentCountRankingAsync(
        int pageIndex,
        int pageSize);

    /// <summary>获取用户在评论排行榜的确定性序号。</summary>
    Task<int> GetUserCommentCountRankAsync(long userId);

    /// <summary>获取人气排行榜与应用完整资格后的总数。</summary>
    Task<(List<UserLeaderboardMetric> Items, int TotalCount)> GetPopularityRankingAsync(
        int pageIndex,
        int pageSize);

    /// <summary>获取用户在人气排行榜的确定性序号。</summary>
    Task<int> GetUserPopularityRankAsync(long userId);

    /// <summary>获取热门商品排行榜与应用完整资格后的总数。</summary>
    Task<(List<Product> Items, int TotalCount)> GetHotProductRankingAsync(
        int pageIndex,
        int pageSize);

    /// <summary>按共同参与资格装配一组用户。</summary>
    Task<List<LeaderboardUserProjection>> GetEligibleUsersAsync(
        IReadOnlyCollection<long> userIds);
}
