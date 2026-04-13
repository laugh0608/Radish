using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using SqlSugar;

namespace Radish.Repository;

/// <summary>帖子仓储</summary>
public class PostRepository : BaseRepository<Post>, IPostRepository
{
    public PostRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public async Task<(List<Post> data, int totalCount)> QueryForumPostPageAsync(
        long? categoryId,
        long? tagId,
        string? keyword,
        DateTime? startTime,
        DateTime? endTime,
        int pageIndex,
        int pageSize,
        string sortBy)
    {
        var normalizedKeyword = keyword?.Trim() ?? string.Empty;
        var hasKeyword = !string.IsNullOrWhiteSpace(normalizedKeyword);
        var hasCategory = categoryId.HasValue && categoryId.Value > 0;
        var hasStartTime = startTime.HasValue;
        var hasEndTime = endTime.HasValue;
        var hasTag = tagId.HasValue && tagId.Value > 0;
        var categoryValue = categoryId ?? 0;
        var tagValue = tagId ?? 0;
        var startTimeValue = startTime ?? DateTime.MinValue;
        var endTimeValue = endTime ?? DateTime.MaxValue;

        RefAsync<int> totalCount = 0;

        HashSet<long> taggedPostIds = [];
        if (hasTag)
        {
            taggedPostIds = (await CreateTenantQueryableFor<PostTag>()
                .Where(postTag => postTag.TagId == tagValue)
                .Select(postTag => postTag.PostId)
                .ToListAsync())
                .ToHashSet();

            if (taggedPostIds.Count == 0)
            {
                return (new List<Post>(), 0);
            }
        }

        var query = CreateTenantQueryableFor<Post>()
            .Where(post => post.IsPublished && !post.IsDeleted && (!hasTag || taggedPostIds.Contains(post.Id)));

        if (hasCategory)
        {
            query = query.Where(post => post.CategoryId == categoryValue);
        }

        if (hasStartTime)
        {
            query = query.Where(post => post.CreateTime >= startTimeValue);
        }

        if (hasEndTime)
        {
            query = query.Where(post => post.CreateTime <= endTimeValue);
        }

        if (hasKeyword)
        {
            query = query.Where(post =>
                post.Title.Contains(normalizedKeyword) ||
                post.Summary.Contains(normalizedKeyword) ||
                post.Content.Contains(normalizedKeyword));
        }

        query = sortBy.ToLowerInvariant() switch
        {
            "hottest" => query
                .OrderBy(post => post.IsTop, OrderByType.Desc)
                .OrderBy(post => post.ViewCount + post.LikeCount * 2 + post.CommentCount * 3, OrderByType.Desc)
                .OrderBy(post => post.CreateTime, OrderByType.Desc),
            "essence" => query
                .OrderBy(post => post.IsTop, OrderByType.Desc)
                .OrderBy(post => post.IsEssence, OrderByType.Desc)
                .OrderBy(post => post.CreateTime, OrderByType.Desc),
            _ => query
                .OrderBy(post => post.IsTop, OrderByType.Desc)
                .OrderBy(post => post.CreateTime, OrderByType.Desc)
        };

        var data = await query
            .Select(post => new Post
            {
                Id = post.Id,
                Title = post.Title,
                Slug = post.Slug,
                Summary = post.Summary,
                ContentType = post.ContentType,
                CoverAttachmentId = post.CoverAttachmentId,
                AuthorId = post.AuthorId,
                AuthorName = post.AuthorName,
                CategoryId = post.CategoryId,
                IsTop = post.IsTop,
                IsEssence = post.IsEssence,
                IsLocked = post.IsLocked,
                IsPublished = post.IsPublished,
                PublishTime = post.PublishTime,
                IsEnabled = post.IsEnabled,
                IsDeleted = post.IsDeleted,
                ViewCount = post.ViewCount,
                LikeCount = post.LikeCount,
                CommentCount = post.CommentCount,
                CollectCount = post.CollectCount,
                ShareCount = post.ShareCount,
                TenantId = post.TenantId,
                CreateTime = post.CreateTime,
                CreateBy = post.CreateBy,
                CreateId = post.CreateId,
                ModifyTime = post.ModifyTime,
                ModifyBy = post.ModifyBy,
                ModifyId = post.ModifyId
            })
            .ToPageListAsync(pageIndex, pageSize, totalCount);

        return (data, totalCount);
    }

    public async Task<(List<Post> data, int totalCount)> QueryQuestionPostPageAsync(
        long? categoryId,
        long? tagId,
        string? keyword,
        DateTime? startTime,
        DateTime? endTime,
        bool? isSolved,
        int pageIndex,
        int pageSize,
        string sortBy)
    {
        var normalizedKeyword = keyword?.Trim() ?? string.Empty;
        var hasKeyword = !string.IsNullOrWhiteSpace(normalizedKeyword);
        var hasCategory = categoryId.HasValue;
        var categoryValue = categoryId ?? 0;
        var hasStartTime = startTime.HasValue;
        var hasEndTime = endTime.HasValue;
        var startTimeValue = startTime ?? DateTime.MinValue;
        var endTimeValue = endTime ?? DateTime.MaxValue;
        var hasSolvedFilter = isSolved.HasValue;
        var solvedValue = isSolved ?? false;
        var hasTag = tagId.HasValue && tagId.Value > 0;
        var tagValue = tagId ?? 0;

        RefAsync<int> totalCount = 0;

        HashSet<long> taggedPostIds = [];
        if (hasTag)
        {
            taggedPostIds = (await CreateTenantQueryableFor<PostTag>()
                .Where(postTag => postTag.TagId == tagValue)
                .Select(postTag => postTag.PostId)
                .ToListAsync())
                .ToHashSet();

            if (taggedPostIds.Count == 0)
            {
                return (new List<Post>(), 0);
            }
        }

        var query = CreateTenantQueryableFor<PostQuestion>()
            .InnerJoin<Post>((question, post) =>
                question.PostId == post.Id &&
                question.TenantId == post.TenantId &&
                post.IsPublished &&
                !post.IsDeleted)
            .Where((question, post) =>
                (!hasTag || taggedPostIds.Contains(post.Id)) &&
                (!hasCategory || post.CategoryId == categoryValue) &&
                (!hasKeyword || post.Title.Contains(normalizedKeyword) || post.Content.Contains(normalizedKeyword)) &&
                (!hasStartTime || post.CreateTime >= startTimeValue) &&
                (!hasEndTime || post.CreateTime <= endTimeValue) &&
                (!hasSolvedFilter || question.IsSolved == solvedValue));

        query = sortBy.ToLowerInvariant() switch
        {
            "pending" => query
                .OrderBy((question, post) => post.IsTop, OrderByType.Desc)
                .OrderBy((question, post) => question.IsSolved, OrderByType.Asc)
                .OrderBy((question, post) => post.CreateTime, OrderByType.Desc),
            "answers" => query
                .OrderBy((question, post) => post.IsTop, OrderByType.Desc)
                .OrderBy((question, post) => question.AnswerCount, OrderByType.Desc)
                .OrderBy((question, post) => post.CreateTime, OrderByType.Desc),
            _ => query
                .OrderBy((question, post) => post.IsTop, OrderByType.Desc)
                .OrderBy((question, post) => post.CreateTime, OrderByType.Desc)
        };

        var data = await query
            .Select((question, post) => post)
            .ToPageListAsync(pageIndex, pageSize, totalCount);

        return (data, totalCount);
    }
}
