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

    public async Task<(List<Post> data, int totalCount)> QueryQuestionPostPageAsync(
        long? categoryId,
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

        RefAsync<int> totalCount = 0;

        var query = CreateTenantQueryableFor<PostQuestion>()
            .InnerJoin<Post>((question, post) =>
                question.PostId == post.Id &&
                question.TenantId == post.TenantId &&
                post.IsPublished &&
                !post.IsDeleted)
            .Where((question, post) =>
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
