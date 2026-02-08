using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Repository;

/// <summary>排行榜仓储</summary>
/// <remarks>
/// 提供排行榜聚合查询方法
/// </remarks>
public class LeaderboardRepository : BaseRepository<User>, ILeaderboardRepository
{
    public LeaderboardRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    #region 购买数量排行榜

    /// <inheritdoc />
    public async Task<List<(long UserId, int TotalQuantity)>> GetPurchaseCountRankingAsync(int pageIndex, int pageSize)
    {
        var result = await DbProtectedClient.Queryable<Order>()
            .Where(o => o.Status == OrderStatus.Completed && !o.IsDeleted)
            .GroupBy(o => o.UserId)
            .Select(o => new
            {
                UserId = o.UserId,
                TotalQuantity = SqlFunc.AggregateSum(o.Quantity)
            })
            .OrderByDescending(x => x.TotalQuantity)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return result.Select(x => (x.UserId, x.TotalQuantity)).ToList();
    }

    /// <inheritdoc />
    public async Task<int> GetPurchaseCountRankingTotalAsync()
    {
        return await DbProtectedClient.Queryable<Order>()
            .Where(o => o.Status == OrderStatus.Completed && !o.IsDeleted)
            .GroupBy(o => o.UserId)
            .CountAsync();
    }

    /// <inheritdoc />
    public async Task<int> GetUserPurchaseCountRankAsync(long userId)
    {
        // 先获取用户的购买总数
        var userTotal = await DbProtectedClient.Queryable<Order>()
            .Where(o => o.UserId == userId && o.Status == OrderStatus.Completed && !o.IsDeleted)
            .SumAsync(o => o.Quantity);

        if (userTotal == 0) return 0;

        // 计算排名（比该用户购买数量多的用户数 + 1）
        var rank = await DbProtectedClient.Queryable<Order>()
            .Where(o => o.Status == OrderStatus.Completed && !o.IsDeleted)
            .GroupBy(o => o.UserId)
            .Having(o => SqlFunc.AggregateSum(o.Quantity) > userTotal)
            .CountAsync();

        return rank + 1;
    }

    #endregion

    #region 发帖数量排行榜

    /// <inheritdoc />
    public async Task<List<(long UserId, int PostCount)>> GetPostCountRankingAsync(int pageIndex, int pageSize)
    {
        var result = await DbProtectedClient.Queryable<Post>()
            .Where(p => p.IsPublished && p.IsEnabled && !p.IsDeleted)
            .GroupBy(p => p.AuthorId)
            .Select(p => new
            {
                UserId = p.AuthorId,
                PostCount = SqlFunc.AggregateCount(p.Id)
            })
            .OrderByDescending(x => x.PostCount)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return result.Select(x => (x.UserId, x.PostCount)).ToList();
    }

    /// <inheritdoc />
    public async Task<int> GetPostCountRankingTotalAsync()
    {
        return await DbProtectedClient.Queryable<Post>()
            .Where(p => p.IsPublished && p.IsEnabled && !p.IsDeleted)
            .GroupBy(p => p.AuthorId)
            .CountAsync();
    }

    /// <inheritdoc />
    public async Task<int> GetUserPostCountRankAsync(long userId)
    {
        // 先获取用户的发帖数
        var userCount = await DbProtectedClient.Queryable<Post>()
            .Where(p => p.AuthorId == userId && p.IsPublished && p.IsEnabled && !p.IsDeleted)
            .CountAsync();

        if (userCount == 0) return 0;

        // 计算排名
        var rank = await DbProtectedClient.Queryable<Post>()
            .Where(p => p.IsPublished && p.IsEnabled && !p.IsDeleted)
            .GroupBy(p => p.AuthorId)
            .Having(p => SqlFunc.AggregateCount(p.Id) > userCount)
            .CountAsync();

        return rank + 1;
    }

    #endregion

    #region 评论数量排行榜

    /// <inheritdoc />
    public async Task<List<(long UserId, int CommentCount)>> GetCommentCountRankingAsync(int pageIndex, int pageSize)
    {
        var result = await DbProtectedClient.Queryable<Comment>()
            .Where(c => c.IsEnabled && !c.IsDeleted)
            .GroupBy(c => c.AuthorId)
            .Select(c => new
            {
                UserId = c.AuthorId,
                CommentCount = SqlFunc.AggregateCount(c.Id)
            })
            .OrderByDescending(x => x.CommentCount)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return result.Select(x => (x.UserId, x.CommentCount)).ToList();
    }

    /// <inheritdoc />
    public async Task<int> GetCommentCountRankingTotalAsync()
    {
        return await DbProtectedClient.Queryable<Comment>()
            .Where(c => c.IsEnabled && !c.IsDeleted)
            .GroupBy(c => c.AuthorId)
            .CountAsync();
    }

    /// <inheritdoc />
    public async Task<int> GetUserCommentCountRankAsync(long userId)
    {
        // 先获取用户的评论数
        var userCount = await DbProtectedClient.Queryable<Comment>()
            .Where(c => c.AuthorId == userId && c.IsEnabled && !c.IsDeleted)
            .CountAsync();

        if (userCount == 0) return 0;

        // 计算排名
        var rank = await DbProtectedClient.Queryable<Comment>()
            .Where(c => c.IsEnabled && !c.IsDeleted)
            .GroupBy(c => c.AuthorId)
            .Having(c => SqlFunc.AggregateCount(c.Id) > userCount)
            .CountAsync();

        return rank + 1;
    }

    #endregion

    #region 人气排行榜

    /// <inheritdoc />
    public async Task<List<(long UserId, int TotalLikes)>> GetPopularityRankingAsync(int pageIndex, int pageSize)
    {
        // 获取帖子点赞数
        var postLikes = DbProtectedClient.Queryable<Post>()
            .Where(p => p.IsPublished && p.IsEnabled && !p.IsDeleted)
            .GroupBy(p => p.AuthorId)
            .Select(p => new
            {
                UserId = p.AuthorId,
                LikeCount = SqlFunc.AggregateSum(p.LikeCount)
            });

        // 获取评论点赞数
        var commentLikes = DbProtectedClient.Queryable<Comment>()
            .Where(c => c.IsEnabled && !c.IsDeleted)
            .GroupBy(c => c.AuthorId)
            .Select(c => new
            {
                UserId = c.AuthorId,
                LikeCount = SqlFunc.AggregateSum(c.LikeCount)
            });

        // 合并两个查询结果
        var postLikesList = await postLikes.ToListAsync();
        var commentLikesList = await commentLikes.ToListAsync();

        // 在内存中合并计算
        var combined = postLikesList
            .Concat(commentLikesList)
            .GroupBy(x => x.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalLikes = g.Sum(x => x.LikeCount)
            })
            .OrderByDescending(x => x.TotalLikes)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return combined.Select(x => (x.UserId, x.TotalLikes)).ToList();
    }

    /// <inheritdoc />
    public async Task<int> GetPopularityRankingTotalAsync()
    {
        // 获取有帖子或评论的用户数
        var postAuthors = await DbProtectedClient.Queryable<Post>()
            .Where(p => p.IsPublished && p.IsEnabled && !p.IsDeleted && p.LikeCount > 0)
            .Select(p => p.AuthorId)
            .Distinct()
            .ToListAsync();

        var commentAuthors = await DbProtectedClient.Queryable<Comment>()
            .Where(c => c.IsEnabled && !c.IsDeleted && c.LikeCount > 0)
            .Select(c => c.AuthorId)
            .Distinct()
            .ToListAsync();

        return postAuthors.Union(commentAuthors).Count();
    }

    /// <inheritdoc />
    public async Task<int> GetUserPopularityRankAsync(long userId)
    {
        // 获取用户的帖子点赞数
        var postLikes = await DbProtectedClient.Queryable<Post>()
            .Where(p => p.AuthorId == userId && p.IsPublished && p.IsEnabled && !p.IsDeleted)
            .SumAsync(p => p.LikeCount);

        // 获取用户的评论点赞数
        var commentLikes = await DbProtectedClient.Queryable<Comment>()
            .Where(c => c.AuthorId == userId && c.IsEnabled && !c.IsDeleted)
            .SumAsync(c => c.LikeCount);

        var userTotalLikes = postLikes + commentLikes;
        if (userTotalLikes == 0) return 0;

        // 获取所有用户的点赞数并计算排名
        var postLikesList = await DbProtectedClient.Queryable<Post>()
            .Where(p => p.IsPublished && p.IsEnabled && !p.IsDeleted)
            .GroupBy(p => p.AuthorId)
            .Select(p => new
            {
                UserId = p.AuthorId,
                LikeCount = SqlFunc.AggregateSum(p.LikeCount)
            })
            .ToListAsync();

        var commentLikesList = await DbProtectedClient.Queryable<Comment>()
            .Where(c => c.IsEnabled && !c.IsDeleted)
            .GroupBy(c => c.AuthorId)
            .Select(c => new
            {
                UserId = c.AuthorId,
                LikeCount = SqlFunc.AggregateSum(c.LikeCount)
            })
            .ToListAsync();

        var combined = postLikesList
            .Concat(commentLikesList)
            .GroupBy(x => x.UserId)
            .Select(g => g.Sum(x => x.LikeCount))
            .Count(total => total > userTotalLikes);

        return combined + 1;
    }

    #endregion
}
