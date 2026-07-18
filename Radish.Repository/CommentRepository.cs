using System.Text.Json;
using Radish.Common.CoreTool;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using SqlSugar;

namespace Radish.Repository;

/// <summary>评论仓储</summary>
public class CommentRepository : BaseRepository<Comment>, ICommentRepository
{
    private readonly IReliableOutboxRepository? _reliableOutboxRepository;
    private readonly TimeProvider _timeProvider;

    public CommentRepository(
        IUnitOfWorkManage unitOfWorkManage,
        IReliableOutboxRepository? reliableOutboxRepository = null,
        TimeProvider? timeProvider = null) : base(unitOfWorkManage)
    {
        _reliableOutboxRepository = reliableOutboxRepository;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<CommentLikePersistenceResult> ToggleCommentLikeAsync(long userId, string userName, long commentId)
    {
        try
        {
            return await ExecuteDbOperationAsync(async () =>
            {
                DbProtectedClient.Ado.BeginTran();
                try
                {
                    var result = await ToggleCommentLikeCoreAsync(userId, commentId);
                    if (result.Delta > 0)
                    {
            await AddLikeOutboxAsync(result, userId, userName);
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
            return await ReadCurrentCommentLikeResultAsync(userId, commentId);
        }
    }

    private async Task<CommentLikePersistenceResult> ToggleCommentLikeCoreAsync(long userId, long commentId)
    {
        var comment = await QueryCommentForLikeAsync(commentId);
        var existingLike = await QueryCommentLikeRelationAsync(comment.TenantId, userId, commentId);

        if (existingLike is { IsDeleted: false })
        {
            var affectedRows = await DbProtectedClient.Updateable<UserCommentLike>()
                .SetColumns(like => new UserCommentLike { IsDeleted = true })
                .Where(like => like.Id == existingLike.Id && !like.IsDeleted)
                .ExecuteCommandAsync();

            if (affectedRows <= 0)
            {
                return await ReadCurrentCommentLikeResultCoreAsync(userId, commentId, 0);
            }

            await ApplyCommentLikeCountDeltaAsync(commentId, -1);
            var likeCount = await QueryCommentLikeCountAsync(commentId);
            return BuildCommentLikeResult(comment, false, likeCount, -1);
        }

        if (existingLike is { IsDeleted: true })
        {
            var now = DateTime.UtcNow;
            var affectedRows = await DbProtectedClient.Updateable<UserCommentLike>()
                .SetColumns(like => new UserCommentLike
                {
                    IsDeleted = false,
                    LikedAt = now
                })
                .Where(like => like.Id == existingLike.Id && like.IsDeleted)
                .ExecuteCommandAsync();

            if (affectedRows <= 0)
            {
                return await ReadCurrentCommentLikeResultCoreAsync(userId, commentId, 0);
            }

            await ApplyCommentLikeCountDeltaAsync(commentId, 1);
            var likeCount = await QueryCommentLikeCountAsync(commentId);
            return BuildCommentLikeResult(comment, true, likeCount, 1);
        }

        await DbProtectedClient.Insertable(new UserCommentLike
        {
            TenantId = comment.TenantId,
            UserId = userId,
            CommentId = commentId,
            PostId = comment.PostId,
            LikedAt = DateTime.UtcNow,
            IsDeleted = false
        }).ExecuteReturnSnowflakeIdAsync();

        await ApplyCommentLikeCountDeltaAsync(commentId, 1);
        var finalLikeCount = await QueryCommentLikeCountAsync(commentId);
        return BuildCommentLikeResult(comment, true, finalLikeCount, 1);
    }

    private async Task<CommentLikePersistenceResult> ReadCurrentCommentLikeResultAsync(long userId, long commentId)
    {
        return await ExecuteDbOperationAsync(() => ReadCurrentCommentLikeResultCoreAsync(userId, commentId, 0));
    }

    private async Task<CommentLikePersistenceResult> ReadCurrentCommentLikeResultCoreAsync(long userId, long commentId, int delta)
    {
        var comment = await QueryCommentForLikeAsync(commentId);
        var relation = await QueryCommentLikeRelationAsync(comment.TenantId, userId, commentId);
        var likeCount = await QueryCommentLikeCountAsync(commentId);
        return BuildCommentLikeResult(comment, relation?.IsDeleted == false, likeCount, delta);
    }

    private async Task<Comment> QueryCommentForLikeAsync(long commentId)
    {
        var comment = await CreateTenantQueryableFor<Comment>()
            .Where(comment => comment.Id == commentId && !comment.IsDeleted)
            .FirstAsync();

        return comment ?? throw new InvalidOperationException("评论不存在或已被删除");
    }

    private async Task<UserCommentLike?> QueryCommentLikeRelationAsync(long tenantId, long userId, long commentId)
    {
        return await CreateTenantQueryableFor<UserCommentLike>()
            .Where(like => like.TenantId == tenantId && like.UserId == userId && like.CommentId == commentId)
            .OrderBy(like => like.IsDeleted, OrderByType.Asc)
            .OrderBy(like => like.LikedAt, OrderByType.Desc)
            .OrderBy(like => like.Id, OrderByType.Desc)
            .FirstAsync();
    }

    private async Task ApplyCommentLikeCountDeltaAsync(long commentId, int delta)
    {
        var tableName = RepositorySqlHelper.QuoteIdentifier(DbProtectedClient.EntityMaintenance.GetEntityInfo<Comment>().DbTableName);
        var idColumn = RepositorySqlHelper.QuoteIdentifier(nameof(Comment.Id));
        var likeCountColumn = RepositorySqlHelper.QuoteIdentifier(nameof(Comment.LikeCount));

        if (delta > 0)
        {
            await DbProtectedClient.Ado.ExecuteCommandAsync(
                $"UPDATE {tableName} SET {likeCountColumn} = {likeCountColumn} + @delta WHERE {idColumn} = @commentId",
                new SugarParameter("@delta", delta),
                new SugarParameter("@commentId", commentId));
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
            WHERE {idColumn} = @commentId
            """,
            new SugarParameter("@decrement", decrement),
            new SugarParameter("@commentId", commentId));
    }

    private async Task<int> QueryCommentLikeCountAsync(long commentId)
    {
        return await CreateTenantQueryableFor<Comment>()
            .Where(comment => comment.Id == commentId)
            .Select(comment => comment.LikeCount)
            .FirstAsync();
    }

    private static CommentLikePersistenceResult BuildCommentLikeResult(Comment comment, bool isLiked, int likeCount, int delta)
    {
        return new CommentLikePersistenceResult(
            comment.Id,
            comment.TenantId,
            comment.PostId,
            comment.ParentId,
            comment.AuthorId,
            comment.Content,
            isLiked,
            likeCount,
            delta);
    }

    private async Task AddLikeOutboxAsync(CommentLikePersistenceResult result, long likerId, string likerName)
    {
        var repository = _reliableOutboxRepository
            ?? throw new InvalidOperationException("可靠 Outbox 仓储未注册");
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var notificationId = SnowFlakeSingle.Instance.NextId();
        var notificationBusinessKey = $"notification:comment-like:{result.CommentId}:liker:{likerId}:event:{notificationId}";
        var payload = new LikeEffectsTaskPayload(
            notificationId,
            result.CommentId,
            result.PostId,
            result.AuthorId,
            likerId,
            NormalizeActorName(likerName, likerId),
            result.Content,
            null,
            now.ToString("yyyyMMdd"),
            notificationBusinessKey);
        await repository.AddAsync(new ReliableOutboxDraft(
            ReliableOutboxSources.Main,
            result.TenantId,
            ReliableTaskTypes.CommentLiked,
            1,
            $"task:comment-like:{result.CommentId}:liker:{likerId}:at:{now.Ticks}",
            "Comment",
            result.CommentId.ToString(),
            JsonSerializer.Serialize(payload),
            now));
    }

    private static string NormalizeActorName(string actorName, long actorId)
    {
        return string.IsNullOrWhiteSpace(actorName) ? $"User-{actorId}" : actorName.Trim();
    }

    public async Task<List<Comment>> QueryLatestInteractorCommentsByPostIdsAsync(
        IReadOnlyDictionary<long, long> postAuthorMap,
        int takePerPost)
    {
        return await ExecuteDbOperationAsync(async () =>
        {
            if (postAuthorMap.Count == 0 || takePerPost <= 0)
            {
                return [];
            }

            var tableName = QuoteIdentifier(DbProtectedClient.EntityMaintenance.GetEntityInfo<Comment>().DbTableName);
            var postIdColumn = QuoteIdentifier(nameof(Comment.PostId));
            var authorIdColumn = QuoteIdentifier(nameof(Comment.AuthorId));
            var createTimeColumn = QuoteIdentifier(nameof(Comment.CreateTime));
            var idColumn = QuoteIdentifier(nameof(Comment.Id));
            var isEnabledColumn = QuoteIdentifier(nameof(Comment.IsEnabled));
            var isDeletedColumn = QuoteIdentifier(nameof(Comment.IsDeleted));
            var tenantIdColumn = QuoteIdentifier(nameof(Comment.TenantId));
            const string excludedAuthorIdColumn = "\"ExcludedAuthorId\"";

            var parameters = new List<SugarParameter>
            {
                new("@takePerPost", takePerPost),
                new("@isEnabled", true),
                new("@isDeleted", false)
            };

            var requestedRows = new List<string>();
            var rowIndex = 0;
            foreach (var (postId, authorId) in postAuthorMap)
            {
                requestedRows.Add($"(@postId{rowIndex}, @excludedAuthorId{rowIndex})");
                parameters.Add(new SugarParameter($"@postId{rowIndex}", postId));
                parameters.Add(new SugarParameter($"@excludedAuthorId{rowIndex}", authorId));
                rowIndex++;
            }

            var currentTenantId = App.CurrentUser.TenantId > 0 ? App.CurrentUser.TenantId : 0;
            string tenantCondition;
            if (currentTenantId > 0)
            {
                parameters.Add(new SugarParameter("@tenantId", currentTenantId));
                tenantCondition = $"(c.{tenantIdColumn} = @tenantId OR c.{tenantIdColumn} = 0)";
            }
            else
            {
                tenantCondition = $"c.{tenantIdColumn} = 0";
            }

            var sql = $"""
WITH requested({postIdColumn}, {excludedAuthorIdColumn}) AS (
    VALUES {string.Join(", ", requestedRows)}
),
deduped AS (
    SELECT
        c.*,
        ROW_NUMBER() OVER (
            PARTITION BY c.{postIdColumn}, c.{authorIdColumn}
            ORDER BY c.{createTimeColumn} DESC, c.{idColumn} DESC
        ) AS "AuthorRank"
    FROM {tableName} AS c
    INNER JOIN requested AS r ON r.{postIdColumn} = c.{postIdColumn}
    WHERE c.{authorIdColumn} > 0
      AND c.{authorIdColumn} <> r.{excludedAuthorIdColumn}
      AND c.{isEnabledColumn} = @isEnabled
      AND c.{isDeletedColumn} = @isDeleted
      AND {tenantCondition}
),
ranked AS (
    SELECT
        deduped.*,
        ROW_NUMBER() OVER (
            PARTITION BY deduped.{postIdColumn}
            ORDER BY deduped.{createTimeColumn} DESC, deduped.{idColumn} DESC
        ) AS "PostRank"
    FROM deduped
    WHERE deduped."AuthorRank" = 1
)
SELECT *
FROM ranked
WHERE "PostRank" <= @takePerPost
ORDER BY {postIdColumn}, {createTimeColumn} DESC, {idColumn} DESC;
""";

            return await DbProtectedClient.Ado.SqlQueryAsync<Comment>(sql, parameters.ToArray());
        });
    }

    private static string QuoteIdentifier(string identifier)
    {
        return string.Join(".", identifier
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => $"\"{part.Replace("\"", "\"\"")}\""));
    }
}
