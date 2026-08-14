using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using SqlSugar;

namespace Radish.Repository;

/// <summary>公开排行榜权威聚合仓储。</summary>
public class LeaderboardRepository : BaseRepository<User>, ILeaderboardRepository
{
    public LeaderboardRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    /// <inheritdoc />
    public Task<(List<UserLeaderboardMetric> Items, int TotalCount)> GetExperienceRankingAsync(
        DateTime now,
        int pageIndex,
        int pageSize)
    {
        return ExecuteDbOperationAsync(() =>
            QueryMetricPageAsync(BuildExperienceRankingQuery(now), pageIndex, pageSize));
    }

    /// <inheritdoc />
    public Task<int> GetUserExperienceRankAsync(long userId, DateTime now)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var target = await BuildExperienceRankingQuery(now)
                .Where(item => item.UserId == userId)
                .FirstAsync();
            return target == null
                ? 0
                : await CountOrdinalRankAsync(BuildExperienceRankingQuery(now), target);
        });
    }

    /// <inheritdoc />
    public Task<(List<UserLeaderboardMetric> Items, int TotalCount)> GetPostCountRankingAsync(
        int pageIndex,
        int pageSize)
    {
        return ExecuteDbOperationAsync(() =>
            QueryMetricPageAsync(BuildPostCountRankingQuery(), pageIndex, pageSize));
    }

    /// <inheritdoc />
    public Task<int> GetUserPostCountRankAsync(long userId)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var target = await BuildPostCountRankingQuery()
                .Where(item => item.UserId == userId)
                .FirstAsync();
            return target == null
                ? 0
                : await CountOrdinalRankAsync(BuildPostCountRankingQuery(), target);
        });
    }

    /// <inheritdoc />
    public Task<(List<UserLeaderboardMetric> Items, int TotalCount)> GetCommentCountRankingAsync(
        int pageIndex,
        int pageSize)
    {
        return ExecuteDbOperationAsync(() =>
            QueryMetricPageAsync(BuildCommentCountRankingQuery(), pageIndex, pageSize));
    }

    /// <inheritdoc />
    public Task<int> GetUserCommentCountRankAsync(long userId)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var target = await BuildCommentCountRankingQuery()
                .Where(item => item.UserId == userId)
                .FirstAsync();
            return target == null
                ? 0
                : await CountOrdinalRankAsync(BuildCommentCountRankingQuery(), target);
        });
    }

    /// <inheritdoc />
    public Task<(List<UserLeaderboardMetric> Items, int TotalCount)> GetPopularityRankingAsync(
        int pageIndex,
        int pageSize)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var ranking = await LoadPopularityRankingAsync();
            var safePageIndex = Math.Max(1, pageIndex);
            var safePageSize = Math.Max(1, pageSize);
            var items = ranking
                .Skip((safePageIndex - 1) * safePageSize)
                .Take(safePageSize)
                .ToList();
            return (items, ranking.Count);
        });
    }

    /// <inheritdoc />
    public Task<int> GetUserPopularityRankAsync(long userId)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var ranking = await LoadPopularityRankingAsync();
            var index = ranking.FindIndex(item => item.UserId == userId);
            return index < 0 ? 0 : index + 1;
        });
    }

    /// <inheritdoc />
    public Task<(List<Product> Items, int TotalCount)> GetHotProductRankingAsync(
        int pageIndex,
        int pageSize)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var safePageIndex = Math.Max(1, pageIndex);
            var safePageSize = Math.Max(1, pageSize);
            RefAsync<int> totalCount = 0;
            var items = await CreateTenantQueryableFor<Product>()
                .Where(product =>
                    product.IsEnabled &&
                    product.IsOnSale &&
                    !product.IsDeleted &&
                    product.SoldCount > 0)
                .OrderBy(product => product.SoldCount, OrderByType.Desc)
                .OrderBy(product => product.Id, OrderByType.Asc)
                .ToPageListAsync(safePageIndex, safePageSize, totalCount);
            return (items, totalCount.Value);
        });
    }

    /// <inheritdoc />
    public Task<List<LeaderboardUserProjection>> GetEligibleUsersAsync(
        IReadOnlyCollection<long> userIds)
    {
        if (userIds.Count == 0)
        {
            return Task.FromResult(new List<LeaderboardUserProjection>());
        }

        var resolvedUserIds = userIds.Distinct().ToList();
        return ExecuteDbOperationAsync(() => CreateTenantQueryableFor<User>()
            .Where(user =>
                resolvedUserIds.Contains(user.Id) &&
                user.Id > 0 &&
                user.IsEnable &&
                !user.IsDeleted)
            .Select(user => new LeaderboardUserProjection
            {
                UserId = user.Id,
                UserName = user.UserName,
                PublicId = user.PublicId,
                PublicIndex = user.PublicIndex
            })
            .ToListAsync());
    }

    private ISugarQueryable<LeaderboardMetricRow> BuildExperienceRankingQuery(DateTime now)
    {
        return CreateTenantQueryableFor<UserExperience>()
            .InnerJoin<User>((experience, user) =>
                experience.UserId == user.Id &&
                experience.TenantId == user.TenantId)
            .Where((experience, user) =>
                experience.UserId > 0 &&
                !experience.IsDeleted &&
                (!experience.ExpFrozen ||
                 (experience.FrozenUntil != null && experience.FrozenUntil <= now)) &&
                user.Id > 0 &&
                user.IsEnable &&
                !user.IsDeleted)
            .Select((experience, user) => new LeaderboardMetricRow
            {
                UserId = experience.UserId,
                Value = experience.TotalExp
            })
            .MergeTable();
    }

    private ISugarQueryable<LeaderboardMetricRow> BuildPostCountRankingQuery()
    {
        return CreateTenantQueryableFor<Post>()
            .InnerJoin<User>((post, user) =>
                post.AuthorId == user.Id &&
                post.TenantId == user.TenantId)
            .Where((post, user) =>
                post.IsPublished &&
                post.IsEnabled &&
                !post.IsDeleted &&
                user.Id > 0 &&
                user.IsEnable &&
                !user.IsDeleted)
            .GroupBy((post, user) => post.AuthorId)
            .Select((post, user) => new LeaderboardMetricRow
            {
                UserId = post.AuthorId,
                Value = SqlFunc.AggregateCount(post.Id)
            })
            .MergeTable();
    }

    private ISugarQueryable<LeaderboardMetricRow> BuildCommentCountRankingQuery()
    {
        return CreateTenantQueryableFor<Comment>()
            .InnerJoin<User>((comment, user) =>
                comment.AuthorId == user.Id &&
                comment.TenantId == user.TenantId)
            .Where((comment, user) =>
                comment.IsEnabled &&
                !comment.IsDeleted &&
                user.Id > 0 &&
                user.IsEnable &&
                !user.IsDeleted)
            .GroupBy((comment, user) => comment.AuthorId)
            .Select((comment, user) => new LeaderboardMetricRow
            {
                UserId = comment.AuthorId,
                Value = SqlFunc.AggregateCount(comment.Id)
            })
            .MergeTable();
    }

    private ISugarQueryable<LeaderboardMetricRow> BuildPostPopularityQuery()
    {
        return CreateTenantQueryableFor<Post>()
            .InnerJoin<User>((post, user) =>
                post.AuthorId == user.Id &&
                post.TenantId == user.TenantId)
            .Where((post, user) =>
                post.IsPublished &&
                post.IsEnabled &&
                !post.IsDeleted &&
                post.LikeCount > 0 &&
                user.Id > 0 &&
                user.IsEnable &&
                !user.IsDeleted)
            .GroupBy((post, user) => post.AuthorId)
            .Select((post, user) => new LeaderboardMetricRow
            {
                UserId = post.AuthorId,
                Value = SqlFunc.AggregateSum(post.LikeCount)
            })
            .MergeTable();
    }

    private ISugarQueryable<LeaderboardMetricRow> BuildCommentPopularityQuery()
    {
        return CreateTenantQueryableFor<Comment>()
            .InnerJoin<User>((comment, user) =>
                comment.AuthorId == user.Id &&
                comment.TenantId == user.TenantId)
            .Where((comment, user) =>
                comment.IsEnabled &&
                !comment.IsDeleted &&
                comment.LikeCount > 0 &&
                user.Id > 0 &&
                user.IsEnable &&
                !user.IsDeleted)
            .GroupBy((comment, user) => comment.AuthorId)
            .Select((comment, user) => new LeaderboardMetricRow
            {
                UserId = comment.AuthorId,
                Value = SqlFunc.AggregateSum(comment.LikeCount)
            })
            .MergeTable();
    }

    private async Task<List<UserLeaderboardMetric>> LoadPopularityRankingAsync()
    {
        var postMetrics = await BuildPostPopularityQuery().ToListAsync();
        var commentMetrics = await BuildCommentPopularityQuery().ToListAsync();

        return postMetrics
            .Concat(commentMetrics)
            .GroupBy(item => item.UserId)
            .Select(group => new UserLeaderboardMetric(
                group.Key,
                group.Sum(item => item.Value)))
            .Where(item => item.Value > 0)
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.UserId)
            .ToList();
    }

    private static async Task<(List<UserLeaderboardMetric> Items, int TotalCount)> QueryMetricPageAsync(
        ISugarQueryable<LeaderboardMetricRow> query,
        int pageIndex,
        int pageSize)
    {
        var safePageIndex = Math.Max(1, pageIndex);
        var safePageSize = Math.Max(1, pageSize);
        RefAsync<int> totalCount = 0;
        var rows = await query
            .OrderBy(item => item.Value, OrderByType.Desc)
            .OrderBy(item => item.UserId, OrderByType.Asc)
            .ToPageListAsync(safePageIndex, safePageSize, totalCount);
        var items = rows
            .Select(item => new UserLeaderboardMetric(item.UserId, item.Value))
            .ToList();
        return (items, totalCount.Value);
    }

    private static async Task<int> CountOrdinalRankAsync(
        ISugarQueryable<LeaderboardMetricRow> query,
        LeaderboardMetricRow target)
    {
        var precedingCount = await query
            .Where(item =>
                item.Value > target.Value ||
                (item.Value == target.Value && item.UserId < target.UserId))
            .CountAsync();
        return precedingCount + 1;
    }

    private sealed class LeaderboardMetricRow
    {
        public long UserId { get; set; }

        public long Value { get; set; }
    }
}
