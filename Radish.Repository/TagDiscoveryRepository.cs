using Radish.Common.HttpContextTool;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using SqlSugar;

namespace Radish.Repository;

/// <summary>公开标签发现聚合仓储。</summary>
public sealed class TagDiscoveryRepository : BaseRepository<Tag>, ITagDiscoveryRepository
{
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public TagDiscoveryRepository(
        IUnitOfWorkManage unitOfWorkManage,
        ICurrentUserAccessor currentUserAccessor)
        : base(unitOfWorkManage)
    {
        _currentUserAccessor = currentUserAccessor;
    }

    public Task<int> QueryPublicPostCountAsync(long tagId)
    {
        if (tagId <= 0)
        {
            return Task.FromResult(0);
        }

        var tenantId = ResolveCurrentTenantId();
        var hasTenant = tenantId > 0;
        return ExecuteDbOperationAsync(() => DbProtectedClient
            .Queryable<PostTag, Post>((relation, post) => new JoinQueryInfos(
                JoinType.Inner,
                relation.PostId == post.Id))
            .Where((relation, post) =>
                relation.TagId == tagId &&
                post.IsPublished &&
                post.IsEnabled &&
                !post.IsDeleted &&
                (post.TenantId == 0 || (hasTenant && post.TenantId == tenantId)))
            .Select((relation, post) => post.Id)
            .Distinct()
            .CountAsync());
    }

    public Task<List<Tag>> QueryHotPublicTagsAsync(int topCount)
    {
        var safeTopCount = Math.Clamp(topCount, 1, 20);
        return ExecuteDbOperationAsync(async () =>
        {
            var rows = await BuildPublicTagAggregateQuery()
                .OrderByDescending(row => row.PublicPostCount)
                .OrderBy(row => row.SortOrder)
                .OrderBy(row => row.TagId)
                .Take(safeTopCount)
                .ToListAsync();

            return await HydrateTagsAsync(rows);
        });
    }

    public Task<List<Tag>> QueryRelatedPublicTagsAsync(long sourceTagId, int topCount)
    {
        if (sourceTagId <= 0)
        {
            return Task.FromResult(new List<Tag>());
        }

        var safeTopCount = Math.Clamp(topCount, 1, 20);
        var tenantId = ResolveCurrentTenantId();
        var hasTenant = tenantId > 0;

        return ExecuteDbOperationAsync(async () =>
        {
            var sharedRows = await DbProtectedClient
                .Queryable<Tag, PostTag, Post>((tag, relation, post) => new JoinQueryInfos(
                    JoinType.Inner,
                    tag.Id == relation.TagId,
                    JoinType.Inner,
                    relation.PostId == post.Id))
                .Where((tag, relation, post) =>
                    tag.Id != sourceTagId &&
                    tag.IsEnabled &&
                    !tag.IsDeleted &&
                    post.IsPublished &&
                    post.IsEnabled &&
                    !post.IsDeleted &&
                    (post.TenantId == 0 || (hasTenant && post.TenantId == tenantId)) &&
                    SqlFunc.Subqueryable<PostTag>()
                        .Where(sourceRelation =>
                            sourceRelation.TagId == sourceTagId &&
                            sourceRelation.PostId == post.Id)
                        .Any())
                .GroupBy((tag, relation, post) => new { tag.Id, tag.SortOrder })
                .Select((tag, relation, post) => new PublicTagAggregate
                {
                    TagId = tag.Id,
                    SortOrder = tag.SortOrder,
                    PublicPostCount = SqlFunc.AggregateDistinctCount(post.Id),
                    LatestPostTime = SqlFunc.AggregateMax(post.ModifyTime ?? post.PublishTime ?? post.CreateTime),
                    LatestRelationTime = SqlFunc.AggregateMax(relation.CreateTime)
                })
                .ToListAsync();

            if (sharedRows.Count == 0)
            {
                return [];
            }

            var candidateIds = sharedRows.Select(row => row.TagId).ToList();
            var publicCountMap = (await BuildPublicTagAggregateQuery()
                    .Where(row => candidateIds.Contains(row.TagId))
                    .ToListAsync())
                .ToDictionary(row => row.TagId, row => row.PublicPostCount);

            var selectedRows = sharedRows
                .OrderByDescending(row => row.PublicPostCount)
                .ThenByDescending(row => publicCountMap.GetValueOrDefault(row.TagId))
                .ThenBy(row => row.SortOrder)
                .ThenBy(row => row.TagId)
                .Take(safeTopCount)
                .ToList();

            foreach (var row in selectedRows)
            {
                row.PublicPostCount = publicCountMap.GetValueOrDefault(row.TagId);
            }

            return await HydrateTagsAsync(selectedRows);
        });
    }

    public Task<int> QueryIndexableTagCountAsync()
    {
        return ExecuteDbOperationAsync(() => BuildPublicTagAggregateQuery().CountAsync());
    }

    public Task<List<Tag>> QueryIndexableTagPageAsync(int pageIndex, int pageSize)
    {
        var safePageIndex = Math.Max(1, pageIndex);
        var safePageSize = Math.Max(1, pageSize);
        return ExecuteDbOperationAsync(async () =>
        {
            var rows = await BuildPublicTagAggregateQuery()
                .OrderByDescending(row => row.LatestPostTime)
                .OrderByDescending(row => row.LatestRelationTime)
                .OrderByDescending(row => row.TagId)
                .Skip((safePageIndex - 1) * safePageSize)
                .Take(safePageSize)
                .ToListAsync();

            return await HydrateTagsAsync(rows);
        });
    }

    private ISugarQueryable<PublicTagAggregate> BuildPublicTagAggregateQuery()
    {
        var tenantId = ResolveCurrentTenantId();
        var hasTenant = tenantId > 0;
        return DbProtectedClient
            .Queryable<Tag, PostTag, Post>((tag, relation, post) => new JoinQueryInfos(
                JoinType.Inner,
                tag.Id == relation.TagId,
                JoinType.Inner,
                relation.PostId == post.Id))
            .Where((tag, relation, post) =>
                tag.IsEnabled &&
                !tag.IsDeleted &&
                post.IsPublished &&
                post.IsEnabled &&
                !post.IsDeleted &&
                (post.TenantId == 0 || (hasTenant && post.TenantId == tenantId)))
            .GroupBy((tag, relation, post) => new { tag.Id, tag.SortOrder })
            .Select((tag, relation, post) => new PublicTagAggregate
            {
                TagId = tag.Id,
                SortOrder = tag.SortOrder,
                PublicPostCount = SqlFunc.AggregateDistinctCount(post.Id),
                LatestPostTime = SqlFunc.AggregateMax(post.ModifyTime ?? post.PublishTime ?? post.CreateTime),
                LatestRelationTime = SqlFunc.AggregateMax(relation.CreateTime)
            })
            .MergeTable();
    }

    private async Task<List<Tag>> HydrateTagsAsync(IReadOnlyCollection<PublicTagAggregate> rows)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var tagIds = rows.Select(row => row.TagId).ToList();
        var tags = await CreateTenantQueryableFor<Tag>()
            .Where(tag => tagIds.Contains(tag.Id) && tag.IsEnabled)
            .ToListAsync();
        var tagMap = tags.ToDictionary(tag => tag.Id);
        var result = new List<Tag>(rows.Count);

        foreach (var row in rows)
        {
            if (!tagMap.TryGetValue(row.TagId, out var tag))
            {
                continue;
            }

            tag.PostCount = row.PublicPostCount;
            tag.ModifyTime = ResolveLatestTime(tag.ModifyTime, row.LatestPostTime, row.LatestRelationTime);
            result.Add(tag);
        }

        return result;
    }

    private long ResolveCurrentTenantId()
    {
        return _currentUserAccessor.Current.TenantId > 0
            ? _currentUserAccessor.Current.TenantId
            : 0;
    }

    private static DateTime? ResolveLatestTime(params DateTime?[] values)
    {
        return values
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .DefaultIfEmpty()
            .Max();
    }

    private sealed class PublicTagAggregate
    {
        public long TagId { get; set; }

        public int SortOrder { get; set; }

        public int PublicPostCount { get; set; }

        public DateTime LatestPostTime { get; set; }

        public DateTime LatestRelationTime { get; set; }
    }
}
