using Radish.Common;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using SqlSugar;

namespace Radish.Repository;

/// <summary>帖子收藏关系与 CollectCount 投影的 Main 原子事务边界。</summary>
public sealed class UserPostBookmarkRepository
    : BaseRepository<UserPostBookmark>, IUserPostBookmarkRepository
{
    public UserPostBookmarkRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public async Task<Post?> QueryPostByPublicIdAsync(long tenantId, string postPublicId)
    {
        var normalizedPublicId = postPublicId.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedPublicId))
        {
            return null;
        }

        return await ExecuteDbOperationAsync(() => DbProtectedClient.Queryable<Post>()
            .Where(item =>
                item.TenantId == Math.Max(0, tenantId) &&
                item.PublicId == normalizedPublicId)
            .FirstAsync());
    }

    public async Task<Post?> QueryPostByIdAsync(long tenantId, long postId)
    {
        if (postId <= 0)
        {
            return null;
        }

        return await ExecuteDbOperationAsync(() =>
            QueryPostAsync(Math.Max(0, tenantId), postId));
    }

    public async Task<PostBookmarkWriteResult> SetStateAsync(PostBookmarkStateCommand command)
    {
        ValidateStateCommand(command);
        try
        {
            return await ExecuteDbOperationAsync(() => SetStateCoreAsync(command));
        }
        catch (Exception exception) when (RepositorySqlHelper.IsUniqueConstraintException(exception))
        {
            var current = await ReadCurrentStateAsync(command);
            if (current.IsBookmarked == command.IsBookmarked)
            {
                return current;
            }

            throw new PostBookmarkStateConflictException();
        }
    }

    public async Task<PostBookmarkRemoveResult> RemoveAsync(
        long tenantId,
        long userId,
        string bookmarkPublicId,
        string operatorName,
        DateTime nowUtc)
    {
        var normalizedPublicId = bookmarkPublicId.Trim().ToLowerInvariant();
        if (userId <= 0 || !UserPostBookmark.HasPublicIdFormat(normalizedPublicId))
        {
            return new PostBookmarkRemoveResult(null, false);
        }

        var existing = await ExecuteDbOperationAsync(() => DbProtectedClient.Queryable<UserPostBookmark>()
            .Where(item =>
                item.TenantId == Math.Max(0, tenantId) &&
                item.UserId == userId &&
                item.PublicId == normalizedPublicId)
            .FirstAsync());
        if (existing == null || existing.IsDeleted)
        {
            return new PostBookmarkRemoveResult(existing, false);
        }

        return await ExecuteDbOperationAsync(() => RemoveCoreAsync(
            Math.Max(0, tenantId),
            userId,
            normalizedPublicId,
            existing.PostId,
            NormalizeOperatorName(operatorName, userId),
            NormalizeUtc(nowUtc)));
    }

    public async Task<(IReadOnlyList<UserPostBookmark> Items, int Total)> QueryMineAsync(
        long tenantId,
        long userId,
        int pageIndex,
        int pageSize)
    {
        var safePageIndex = Math.Max(1, pageIndex);
        var safePageSize = Math.Clamp(pageSize, 1, 50);
        return await ExecuteDbOperationAsync(async () =>
        {
            var query = DbProtectedClient.Queryable<UserPostBookmark>()
                .Where(item =>
                    item.TenantId == Math.Max(0, tenantId) &&
                    item.UserId == userId &&
                    !item.IsDeleted);
            var total = await query.CountAsync();
            var items = await query
                .OrderBy(item => item.BookmarkedAt, OrderByType.Desc)
                .OrderBy(item => item.Id, OrderByType.Desc)
                .Skip((safePageIndex - 1) * safePageSize)
                .Take(safePageSize)
                .ToListAsync();
            return ((IReadOnlyList<UserPostBookmark>)items, total);
        });
    }

    public async Task<UserPostBookmark?> QueryActiveAsync(long tenantId, long userId, long postId)
    {
        if (userId <= 0 || postId <= 0)
        {
            return null;
        }

        return await ExecuteDbOperationAsync(() => DbProtectedClient.Queryable<UserPostBookmark>()
            .Where(item =>
                item.TenantId == Math.Max(0, tenantId) &&
                item.UserId == userId &&
                item.PostId == postId &&
                !item.IsDeleted)
            .FirstAsync());
    }

    private async Task<PostBookmarkWriteResult> SetStateCoreAsync(PostBookmarkStateCommand command)
    {
        DbProtectedClient.Ado.BeginTran();
        try
        {
            await AcquirePostRowLockAsync(command.TenantId, command.PostId);
            var post = await QueryPostAsync(command.TenantId, command.PostId);
            if (post == null)
            {
                throw new PostBookmarkPostNotFoundException();
            }
            if (post.IsDeleted || !post.IsEnabled || !post.IsPublished)
            {
                throw new PostBookmarkPostUnavailableException();
            }

            var user = await DbProtectedClient.Queryable<User>()
                .Where(item => item.TenantId == command.TenantId && item.Id == command.UserId)
                .FirstAsync();
            if (user is not { IsEnable: true, IsDeleted: false })
            {
                throw new PostBookmarkUserUnavailableException();
            }

            var bookmark = await QueryRelationAsync(command.TenantId, command.UserId, command.PostId);
            var currentlyBookmarked = bookmark is { IsDeleted: false };
            if (currentlyBookmarked == command.IsBookmarked)
            {
                post.CollectCount = await QueryCollectCountAsync(command.TenantId, command.PostId);
                DbProtectedClient.Ado.CommitTran();
                return new PostBookmarkWriteResult(
                    bookmark,
                    post,
                    currentlyBookmarked,
                    post.CollectCount,
                    false);
            }

            if (command.IsBookmarked)
            {
                bookmark = await CreateOrRestoreAsync(command, bookmark);
                await ApplyCollectCountDeltaAsync(command.TenantId, command.PostId, 1);
            }
            else
            {
                if (bookmark == null)
                {
                    throw new PostBookmarkStateConflictException();
                }

                await SoftDeleteAsync(bookmark, command);
                await ApplyCollectCountDeltaAsync(command.TenantId, command.PostId, -1);
            }

            post.CollectCount = await QueryCollectCountAsync(command.TenantId, command.PostId);
            DbProtectedClient.Ado.CommitTran();
            return new PostBookmarkWriteResult(
                bookmark,
                post,
                command.IsBookmarked,
                post.CollectCount,
                true);
        }
        catch
        {
            DbProtectedClient.Ado.RollbackTran();
            throw;
        }
    }

    private async Task<PostBookmarkRemoveResult> RemoveCoreAsync(
        long tenantId,
        long userId,
        string bookmarkPublicId,
        long postId,
        string operatorName,
        DateTime nowUtc)
    {
        DbProtectedClient.Ado.BeginTran();
        try
        {
            await AcquirePostRowLockAsync(tenantId, postId);
            var bookmark = await DbProtectedClient.Queryable<UserPostBookmark>()
                .Where(item =>
                    item.TenantId == tenantId &&
                    item.UserId == userId &&
                    item.PublicId == bookmarkPublicId)
                .FirstAsync();
            if (bookmark == null || bookmark.IsDeleted)
            {
                DbProtectedClient.Ado.CommitTran();
                return new PostBookmarkRemoveResult(bookmark, false);
            }

            var affectedRows = await DbProtectedClient.Updateable<UserPostBookmark>()
                .SetColumns(item => new UserPostBookmark
                {
                    IsDeleted = true,
                    DeletedAt = nowUtc,
                    DeletedBy = operatorName,
                    ModifyTime = nowUtc,
                    ModifyBy = operatorName,
                    ModifyId = userId
                })
                .Where(item => item.Id == bookmark.Id && !item.IsDeleted)
                .ExecuteCommandAsync();
            if (affectedRows <= 0)
            {
                DbProtectedClient.Ado.CommitTran();
                return new PostBookmarkRemoveResult(bookmark, false);
            }

            bookmark.IsDeleted = true;
            bookmark.DeletedAt = nowUtc;
            bookmark.DeletedBy = operatorName;
            bookmark.ModifyTime = nowUtc;
            bookmark.ModifyBy = operatorName;
            bookmark.ModifyId = userId;
            if (await QueryPostAsync(tenantId, postId) != null)
            {
                await ApplyCollectCountDeltaAsync(tenantId, postId, -1);
            }

            DbProtectedClient.Ado.CommitTran();
            return new PostBookmarkRemoveResult(bookmark, true);
        }
        catch
        {
            DbProtectedClient.Ado.RollbackTran();
            throw;
        }
    }

    private async Task<PostBookmarkWriteResult> ReadCurrentStateAsync(PostBookmarkStateCommand command)
    {
        return await ExecuteDbOperationAsync(async () =>
        {
            var post = await QueryPostAsync(command.TenantId, command.PostId);
            if (post == null)
            {
                throw new PostBookmarkPostNotFoundException();
            }

            var bookmark = await QueryRelationAsync(command.TenantId, command.UserId, command.PostId);
            post.CollectCount = await QueryCollectCountAsync(command.TenantId, command.PostId);
            return new PostBookmarkWriteResult(
                bookmark,
                post,
                bookmark is { IsDeleted: false },
                post.CollectCount,
                false);
        });
    }

    private async Task<UserPostBookmark> CreateOrRestoreAsync(
        PostBookmarkStateCommand command,
        UserPostBookmark? bookmark)
    {
        if (bookmark == null)
        {
            bookmark = new UserPostBookmark
            {
                Id = SnowFlakeSingle.Instance.NextId(),
                PublicId = UserPostBookmark.GeneratePublicId(),
                TenantId = command.TenantId,
                UserId = command.UserId,
                PostId = command.PostId,
                BookmarkedAt = command.NowUtc,
                IsDeleted = false,
                CreateTime = command.NowUtc,
                CreateBy = command.OperatorName,
                CreateId = command.UserId
            };
            await DbProtectedClient.Insertable(bookmark).ExecuteCommandAsync();
            return bookmark;
        }

        var affectedRows = await DbProtectedClient.Updateable<UserPostBookmark>()
            .SetColumns(item => new UserPostBookmark
            {
                BookmarkedAt = command.NowUtc,
                IsDeleted = false,
                DeletedAt = null,
                DeletedBy = null,
                ModifyTime = command.NowUtc,
                ModifyBy = command.OperatorName,
                ModifyId = command.UserId
            })
            .Where(item => item.Id == bookmark.Id && item.IsDeleted)
            .ExecuteCommandAsync();
        if (affectedRows <= 0)
        {
            throw new PostBookmarkStateConflictException();
        }

        bookmark.BookmarkedAt = command.NowUtc;
        bookmark.IsDeleted = false;
        bookmark.DeletedAt = null;
        bookmark.DeletedBy = null;
        bookmark.ModifyTime = command.NowUtc;
        bookmark.ModifyBy = command.OperatorName;
        bookmark.ModifyId = command.UserId;
        return bookmark;
    }

    private async Task SoftDeleteAsync(
        UserPostBookmark bookmark,
        PostBookmarkStateCommand command)
    {
        var affectedRows = await DbProtectedClient.Updateable<UserPostBookmark>()
            .SetColumns(item => new UserPostBookmark
            {
                IsDeleted = true,
                DeletedAt = command.NowUtc,
                DeletedBy = command.OperatorName,
                ModifyTime = command.NowUtc,
                ModifyBy = command.OperatorName,
                ModifyId = command.UserId
            })
            .Where(item => item.Id == bookmark.Id && !item.IsDeleted)
            .ExecuteCommandAsync();
        if (affectedRows <= 0)
        {
            throw new PostBookmarkStateConflictException();
        }

        bookmark.IsDeleted = true;
        bookmark.DeletedAt = command.NowUtc;
        bookmark.DeletedBy = command.OperatorName;
        bookmark.ModifyTime = command.NowUtc;
        bookmark.ModifyBy = command.OperatorName;
        bookmark.ModifyId = command.UserId;
    }

    private async Task AcquirePostRowLockAsync(long tenantId, long postId)
    {
        if (DbProtectedClient.CurrentConnectionConfig.DbType != DbType.PostgreSQL)
        {
            return;
        }

        var tableName = RepositorySqlHelper.QuoteIdentifier(
            DbProtectedClient.EntityMaintenance.GetEntityInfo<Post>().DbTableName);
        var tenantColumn = RepositorySqlHelper.QuoteIdentifier(nameof(Post.TenantId));
        var idColumn = RepositorySqlHelper.QuoteIdentifier(nameof(Post.Id));
        await DbProtectedClient.Ado.GetScalarAsync(
            $"SELECT 1 FROM {tableName} WHERE {tenantColumn} = @tenantId AND {idColumn} = @postId FOR UPDATE",
            new SugarParameter("@tenantId", tenantId),
            new SugarParameter("@postId", postId));
    }

    private async Task ApplyCollectCountDeltaAsync(long tenantId, long postId, int delta)
    {
        var tableName = RepositorySqlHelper.QuoteIdentifier(
            DbProtectedClient.EntityMaintenance.GetEntityInfo<Post>().DbTableName);
        var tenantColumn = RepositorySqlHelper.QuoteIdentifier(nameof(Post.TenantId));
        var idColumn = RepositorySqlHelper.QuoteIdentifier(nameof(Post.Id));
        var collectCountColumn = RepositorySqlHelper.QuoteIdentifier(nameof(Post.CollectCount));
        if (delta > 0)
        {
            await DbProtectedClient.Ado.ExecuteCommandAsync(
                $"UPDATE {tableName} SET {collectCountColumn} = {collectCountColumn} + 1 " +
                $"WHERE {tenantColumn} = @tenantId AND {idColumn} = @postId",
                new SugarParameter("@tenantId", tenantId),
                new SugarParameter("@postId", postId));
            return;
        }

        await DbProtectedClient.Ado.ExecuteCommandAsync(
            $"""
            UPDATE {tableName}
            SET {collectCountColumn} = CASE
                WHEN {collectCountColumn} > 0 THEN {collectCountColumn} - 1
                ELSE 0
            END
            WHERE {tenantColumn} = @tenantId AND {idColumn} = @postId
            """,
            new SugarParameter("@tenantId", tenantId),
            new SugarParameter("@postId", postId));
    }

    private async Task<Post?> QueryPostAsync(long tenantId, long postId)
    {
        return await DbProtectedClient.Queryable<Post>()
            .Where(item => item.TenantId == tenantId && item.Id == postId)
            .FirstAsync();
    }

    private async Task<UserPostBookmark?> QueryRelationAsync(long tenantId, long userId, long postId)
    {
        return await DbProtectedClient.Queryable<UserPostBookmark>()
            .Where(item =>
                item.TenantId == tenantId &&
                item.UserId == userId &&
                item.PostId == postId)
            .FirstAsync();
    }

    private async Task<int> QueryCollectCountAsync(long tenantId, long postId)
    {
        return await DbProtectedClient.Queryable<Post>()
            .Where(item => item.TenantId == tenantId && item.Id == postId)
            .Select(item => item.CollectCount)
            .FirstAsync();
    }

    private static void ValidateStateCommand(PostBookmarkStateCommand command)
    {
        if (command.UserId <= 0 || command.PostId <= 0)
        {
            throw new ArgumentException("收藏写入需要有效的用户和帖子。", nameof(command));
        }
        if (command.TenantId < 0)
        {
            throw new ArgumentException("收藏写入的租户无效。", nameof(command));
        }
        if (command.NowUtc == default)
        {
            throw new ArgumentException("收藏写入时间不能为空。", nameof(command));
        }
    }

    private static string NormalizeOperatorName(string operatorName, long userId)
    {
        return string.IsNullOrWhiteSpace(operatorName)
            ? $"User-{userId}"
            : operatorName.Trim();
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
