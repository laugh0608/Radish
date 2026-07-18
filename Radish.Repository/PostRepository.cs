using System.Text.Json;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using SqlSugar;

namespace Radish.Repository;

/// <summary>帖子仓储</summary>
public class PostRepository : BaseRepository<Post>, IPostRepository
{
    private readonly IReliableOutboxRepository? _reliableOutboxRepository;
    private readonly TimeProvider _timeProvider;

    public PostRepository(
        IUnitOfWorkManage unitOfWorkManage,
        IReliableOutboxRepository? reliableOutboxRepository = null,
        TimeProvider? timeProvider = null) : base(unitOfWorkManage)
    {
        _reliableOutboxRepository = reliableOutboxRepository;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<PostLikePersistenceResult> TogglePostLikeAsync(long userId, long postId)
    {
        try
        {
            return await ExecuteDbOperationAsync(async () =>
            {
                DbProtectedClient.Ado.BeginTran();
                try
                {
                    var result = await TogglePostLikeCoreAsync(userId, postId);
                    if (result.Delta > 0)
                    {
                        await AddLikeOutboxAsync(result, userId);
                    }
                    DbProtectedClient.Ado.CommitTran();
                    return result;
                }
                catch
                {
                    DbProtectedClient.Ado.RollbackTran();
                    throw;
                }
            });
        }
        catch (Exception ex) when (RepositorySqlHelper.IsUniqueConstraintException(ex))
        {
            return await ReadCurrentPostLikeResultAsync(userId, postId);
        }
    }

    private async Task<PostLikePersistenceResult> TogglePostLikeCoreAsync(long userId, long postId)
    {
        var post = await QueryPostForLikeAsync(postId);
        var existingLike = await QueryPostLikeRelationAsync(post.TenantId, userId, postId);

        if (existingLike is { IsDeleted: false })
        {
            var affectedRows = await DbProtectedClient.Updateable<UserPostLike>()
                .SetColumns(like => new UserPostLike { IsDeleted = true })
                .Where(like => like.Id == existingLike.Id && !like.IsDeleted)
                .ExecuteCommandAsync();

            if (affectedRows <= 0)
            {
                return await ReadCurrentPostLikeResultCoreAsync(userId, postId, 0);
            }

            await ApplyPostLikeCountDeltaAsync(postId, -1);
            var likeCount = await QueryPostLikeCountAsync(postId);
            return BuildPostLikeResult(post, false, likeCount, -1);
        }

        if (existingLike is { IsDeleted: true })
        {
            var now = DateTime.UtcNow;
            var affectedRows = await DbProtectedClient.Updateable<UserPostLike>()
                .SetColumns(like => new UserPostLike
                {
                    IsDeleted = false,
                    LikedAt = now
                })
                .Where(like => like.Id == existingLike.Id && like.IsDeleted)
                .ExecuteCommandAsync();

            if (affectedRows <= 0)
            {
                return await ReadCurrentPostLikeResultCoreAsync(userId, postId, 0);
            }

            await ApplyPostLikeCountDeltaAsync(postId, 1);
            var likeCount = await QueryPostLikeCountAsync(postId);
            return BuildPostLikeResult(post, true, likeCount, 1);
        }

        await DbProtectedClient.Insertable(new UserPostLike
        {
            TenantId = post.TenantId,
            UserId = userId,
            PostId = postId,
            LikedAt = DateTime.UtcNow,
            IsDeleted = false
        }).ExecuteReturnSnowflakeIdAsync();

        await ApplyPostLikeCountDeltaAsync(postId, 1);
        var finalLikeCount = await QueryPostLikeCountAsync(postId);
        return BuildPostLikeResult(post, true, finalLikeCount, 1);
    }

    private async Task<PostLikePersistenceResult> ReadCurrentPostLikeResultAsync(long userId, long postId)
    {
        return await ExecuteDbOperationAsync(() => ReadCurrentPostLikeResultCoreAsync(userId, postId, 0));
    }

    private async Task<PostLikePersistenceResult> ReadCurrentPostLikeResultCoreAsync(long userId, long postId, int delta)
    {
        var post = await QueryPostForLikeAsync(postId);
        var relation = await QueryPostLikeRelationAsync(post.TenantId, userId, postId);
        var likeCount = await QueryPostLikeCountAsync(postId);
        return BuildPostLikeResult(post, relation?.IsDeleted == false, likeCount, delta);
    }

    private async Task<Post> QueryPostForLikeAsync(long postId)
    {
        var post = await CreateTenantQueryableFor<Post>()
            .Where(post => post.Id == postId && !post.IsDeleted)
            .FirstAsync();

        return post ?? throw new InvalidOperationException("帖子不存在或已被删除");
    }

    private async Task<UserPostLike?> QueryPostLikeRelationAsync(long tenantId, long userId, long postId)
    {
        return await CreateTenantQueryableFor<UserPostLike>()
            .Where(like => like.TenantId == tenantId && like.UserId == userId && like.PostId == postId)
            .OrderBy(like => like.IsDeleted, OrderByType.Asc)
            .OrderBy(like => like.LikedAt, OrderByType.Desc)
            .OrderBy(like => like.Id, OrderByType.Desc)
            .FirstAsync();
    }

    private async Task ApplyPostLikeCountDeltaAsync(long postId, int delta)
    {
        var tableName = RepositorySqlHelper.QuoteIdentifier(DbProtectedClient.EntityMaintenance.GetEntityInfo<Post>().DbTableName);
        var idColumn = RepositorySqlHelper.QuoteIdentifier(nameof(Post.Id));
        var likeCountColumn = RepositorySqlHelper.QuoteIdentifier(nameof(Post.LikeCount));

        if (delta > 0)
        {
            await DbProtectedClient.Ado.ExecuteCommandAsync(
                $"UPDATE {tableName} SET {likeCountColumn} = {likeCountColumn} + @delta WHERE {idColumn} = @postId",
                new SugarParameter("@delta", delta),
                new SugarParameter("@postId", postId));
            return;
        }

        var decrement = Math.Abs(delta);
        await DbProtectedClient.Ado.ExecuteCommandAsync(
            $"""
            UPDATE {tableName}
            SET {likeCountColumn} = CASE
                WHEN {likeCountColumn} >= @decrement THEN {likeCountColumn} - @decrement
                ELSE 0
            END
            WHERE {idColumn} = @postId
            """,
            new SugarParameter("@decrement", decrement),
            new SugarParameter("@postId", postId));
    }

    private async Task<int> QueryPostLikeCountAsync(long postId)
    {
        return await CreateTenantQueryableFor<Post>()
            .Where(post => post.Id == postId)
            .Select(post => post.LikeCount)
            .FirstAsync();
    }

    private static PostLikePersistenceResult BuildPostLikeResult(Post post, bool isLiked, int likeCount, int delta)
    {
        return new PostLikePersistenceResult(
            post.Id,
            post.TenantId,
            post.AuthorId,
            post.Title,
            post.PublicId,
            isLiked,
            likeCount,
            delta);
    }

    private async Task AddLikeOutboxAsync(PostLikePersistenceResult result, long likerId)
    {
        var repository = _reliableOutboxRepository
            ?? throw new InvalidOperationException("可靠 Outbox 仓储未注册");
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var notificationId = SnowFlakeSingle.Instance.NextId();
        var notificationBusinessKey = $"notification:post-like:{result.PostId}:liker:{likerId}:event:{notificationId}";
        var payload = new LikeEffectsTaskPayload(
            notificationId,
            result.PostId,
            result.PostId,
            result.AuthorId,
            likerId,
            result.Title,
            result.PublicId,
            now.ToString("yyyyMMdd"),
            notificationBusinessKey);
        await repository.AddAsync(new ReliableOutboxDraft(
            ReliableOutboxSources.Main,
            result.TenantId,
            ReliableTaskTypes.PostLiked,
            1,
            $"task:post-like:{result.PostId}:liker:{likerId}:at:{now.Ticks}",
            "Post",
            result.PostId.ToString(),
            JsonSerializer.Serialize(payload),
            now));
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

        HashSet<long> taggedPostIds = [];
        if (hasTag)
        {
            taggedPostIds = (await ExecuteDbOperationAsync(() => CreateTenantQueryableFor<PostTag>()
                    .Where(postTag => postTag.TagId == tagValue)
                    .Select(postTag => postTag.PostId)
                    .ToListAsync()))
                .ToHashSet();

            if (taggedPostIds.Count == 0)
            {
                return (new List<Post>(), 0);
            }
        }

        return await ExecuteDbOperationAsync(async () =>
        {
            RefAsync<int> totalCount = 0;
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
                    PublicId = post.PublicId,
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

            return (data, totalCount.Value);
        });
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

        HashSet<long> taggedPostIds = [];
        if (hasTag)
        {
            taggedPostIds = (await ExecuteDbOperationAsync(() => CreateTenantQueryableFor<PostTag>()
                    .Where(postTag => postTag.TagId == tagValue)
                    .Select(postTag => postTag.PostId)
                    .ToListAsync()))
                .ToHashSet();

            if (taggedPostIds.Count == 0)
            {
                return (new List<Post>(), 0);
            }
        }

        return await ExecuteDbOperationAsync(async () =>
        {
            RefAsync<int> totalCount = 0;
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

            return (data, totalCount.Value);
        });
    }
}
