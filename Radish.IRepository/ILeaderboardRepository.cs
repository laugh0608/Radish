namespace Radish.IRepository;

/// <summary>排行榜仓储接口</summary>
/// <remarks>
/// 提供排行榜聚合查询方法
/// </remarks>
public interface ILeaderboardRepository
{
    /// <summary>
    /// 获取购买数量排行榜
    /// </summary>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>用户 ID 和购买总数量列表</returns>
    Task<List<(long UserId, int TotalQuantity)>> GetPurchaseCountRankingAsync(int pageIndex, int pageSize);

    /// <summary>
    /// 获取购买数量排行榜总数
    /// </summary>
    /// <returns>有购买记录的用户数</returns>
    Task<int> GetPurchaseCountRankingTotalAsync();

    /// <summary>
    /// 获取用户购买数量排名
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>排名（0 表示未上榜）</returns>
    Task<int> GetUserPurchaseCountRankAsync(long userId);

    /// <summary>
    /// 获取发帖数量排行榜
    /// </summary>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>用户 ID 和发帖数量列表</returns>
    Task<List<(long UserId, int PostCount)>> GetPostCountRankingAsync(int pageIndex, int pageSize);

    /// <summary>
    /// 获取发帖数量排行榜总数
    /// </summary>
    /// <returns>有发帖记录的用户数</returns>
    Task<int> GetPostCountRankingTotalAsync();

    /// <summary>
    /// 获取用户发帖数量排名
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>排名（0 表示未上榜）</returns>
    Task<int> GetUserPostCountRankAsync(long userId);

    /// <summary>
    /// 获取评论数量排行榜
    /// </summary>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>用户 ID 和评论数量列表</returns>
    Task<List<(long UserId, int CommentCount)>> GetCommentCountRankingAsync(int pageIndex, int pageSize);

    /// <summary>
    /// 获取评论数量排行榜总数
    /// </summary>
    /// <returns>有评论记录的用户数</returns>
    Task<int> GetCommentCountRankingTotalAsync();

    /// <summary>
    /// 获取用户评论数量排名
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>排名（0 表示未上榜）</returns>
    Task<int> GetUserCommentCountRankAsync(long userId);

    /// <summary>
    /// 获取人气排行榜（帖子+评论点赞数）
    /// </summary>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>用户 ID 和总点赞数列表</returns>
    Task<List<(long UserId, int TotalLikes)>> GetPopularityRankingAsync(int pageIndex, int pageSize);

    /// <summary>
    /// 获取人气排行榜总数
    /// </summary>
    /// <returns>有点赞记录的用户数</returns>
    Task<int> GetPopularityRankingTotalAsync();

    /// <summary>
    /// 获取用户人气排名
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>排名（0 表示未上榜）</returns>
    Task<int> GetUserPopularityRankAsync(long userId);
}
